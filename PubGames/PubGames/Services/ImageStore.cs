using System.Text.RegularExpressions;
using Microsoft.Maui.Graphics.Platform;

namespace PubGames.Services;

/// <summary>
/// Uploads and loads images: game covers and rules pictures, and profile
/// pictures. Only an image reference string is stored, so the storage backend
/// can change without touching existing games:
///   "fsimg:{id}" - a game image stored in Firestore (free plan; current backend)
///   "fsphoto:{uid}_{id}" - a profile picture stored in Firestore
///   "https://..." - any web URL, e.g. Firebase Storage after moving to the Blaze plan
/// </summary>
public interface IImageStore
{
	/// <summary>Lets the user pick a photo, shrinks and uploads it. Returns its reference, or null if they cancelled.</summary>
	Task<string?> PickAndUploadAsync();

	/// <summary>The same with a photo taken with the camera now.</summary>
	Task<string?> TakePhotoAndUploadAsync();

	/// <summary>
	/// Uploads a picture made in the app (the image editor), with the drawing
	/// it was made from so it can be edited later. Returns its reference.
	/// </summary>
	Task<string> UploadAsync(byte[] image, string drawing);

	/// <summary>The drawing a picture was made from, or null if it wasn't made in the app (or can't be checked right now).</summary>
	Task<string?> GetDrawingAsync(string reference);

	/// <summary>Admins: deletes a game image that nothing shows any more, in the cloud and on this phone.</summary>
	Task DeleteAsync(string reference);

	/// <summary>The same for the signed-in user's profile picture: smaller, and only they can change it.</summary>
	Task<string?> PickAndUploadProfilePhotoAsync();

	/// <summary>Image bytes for a reference: from this phone's cache when possible, otherwise downloaded once and cached.</summary>
	Task<Stream> OpenAsync(string reference, CancellationToken ct = default);
}

/// <summary>
/// Free-plan image storage: each image is one Firestore document holding the
/// (shrunk) JPEG bytes. Firestore documents max out at 1 MiB, hence the size cap.
/// To upgrade, add a Firebase Storage implementation that returns https URLs
/// from PickAndUploadAsync; OpenAsync below already handles both kinds.
/// </summary>
public partial class FirestoreImageStore : IImageStore
{
	public const string Scheme = "fsimg:";
	private const string Collection = "images";

	/// <summary>Pictures made in the image editor: the drawing they were made from.</summary>
	private const string DrawingField = "drawing";

	public const string PhotoScheme = "fsphoto:";
	private const string PhotoCollection = "profilePhotos";
	private const int PhotoMaxBytes = 200 * 1024;
	private const int PhotoMaxSide = 512;

	/// <summary>Leaves headroom under Firestore's 1 MiB document limit. Keep in sync with firestore.rules.</summary>
	private const int MaxBytes = 700 * 1024;
	private const int MaxSide = 1280;

	private readonly FirestoreClient _firestore;
	private readonly IAuthService _auth;
	private readonly HttpClient _http = new();

	/// <summary>
	/// Gets the services it works with - the cloud and the sign-in - from the app (dependency injection).
	/// </summary>
	public FirestoreImageStore(FirestoreClient firestore, IAuthService auth)
	{
		_firestore = firestore;
		_auth = auth;
	}

	/// <inheritdoc />
	public async Task<string?> PickAndUploadAsync() =>
		await PickAsync(MaxSide, MaxBytes) is { } bytes ? await StoreAsync(bytes, drawing: null) : null;

	/// <inheritdoc />
	public async Task<string?> TakePhotoAndUploadAsync()
	{
		if (!MediaPicker.Default.IsCaptureSupported)
			throw new InvalidOperationException(L.T("This phone can't take photos from the app - upload a picture instead."));

		var photo = await MediaPicker.Default.CapturePhotoAsync(PickerOptions(MaxSide));
		return photo is null ? null : await StoreAsync(await ShrinkAsync(photo, MaxSide, MaxBytes), drawing: null);
	}

	/// <inheritdoc />
	public async Task<string> UploadAsync(byte[] image, string drawing)
	{
		using var stream = new MemoryStream(image);
		return await StoreAsync(await ShrinkAsync(stream, MaxSide, MaxBytes), drawing);
	}

	/// <summary>Saves a game image in the cloud and on this phone. Returns its reference.</summary>
	private async Task<string> StoreAsync(byte[] bytes, string? drawing)
	{
		var id = Guid.NewGuid().ToString("N");
		var fields = new Dictionary<string, object?>
		{
			["id"] = id,
			["data"] = bytes,
			["createdByUserId"] = _auth.AccountId,
			["createdAt"] = DateTime.UtcNow
		};
		if (drawing is not null)
			fields[DrawingField] = drawing;
		await _firestore.SetAsync(Collection, id, fields);

		await File.WriteAllBytesAsync(CachePath(id), bytes);
		await File.WriteAllTextAsync(DrawingPath(id), drawing ?? string.Empty);
		return Scheme + id;
	}

	/// <inheritdoc />
	public async Task<string?> GetDrawingAsync(string reference)
	{
		if (!reference.StartsWith(Scheme)) return null;
		var id = reference[Scheme.Length..];
		if (!IdPattern().IsMatch(id)) return null;

		// Remembered on this phone, also when there's none, so it's only looked up once.
		var path = DrawingPath(id);
		if (!File.Exists(path))
		{
			try
			{
				var doc = await _firestore.GetAsync(Collection, id);
				await File.WriteAllTextAsync(path, doc?.GetValueOrDefault(DrawingField) as string ?? string.Empty);
			}
			catch (Exception)
			{
				// Offline: can't tell right now.
				return null;
			}
		}

		var drawing = await File.ReadAllTextAsync(path);
		return drawing.Length == 0 ? null : drawing;
	}

	/// <inheritdoc />
	public async Task DeleteAsync(string reference)
	{
		// Only this app's own game images; a web URL isn't ours to delete.
		if (!reference.StartsWith(Scheme)) return;
		var id = reference[Scheme.Length..];
		if (!IdPattern().IsMatch(id)) return;

		await _firestore.DeleteAsync(Collection, id);
		File.Delete(CachePath(id));
		File.Delete(DrawingPath(id));
	}

	/// <summary>Where a created image's drawing is cached on the phone, next to the image itself.</summary>
	private static string DrawingPath(string id) => CachePath(id) + ".drawing";

	/// <inheritdoc />
	public async Task<string?> PickAndUploadProfilePhotoAsync()
	{
		var bytes = await PickAsync(PhotoMaxSide, PhotoMaxBytes);
		if (bytes is null) return null;

		// A new id for every new picture, so phones that cached the old one show the new one.
		var uid = _auth.AccountId;
		var id = $"{uid}_{Guid.NewGuid():N}";
		await _firestore.SetAsync(PhotoCollection, id, new Dictionary<string, object?>
		{
			["id"] = id,
			["uid"] = uid,
			["data"] = bytes,
			["createdAt"] = DateTime.UtcNow
		});

		await File.WriteAllBytesAsync(CachePath(id), bytes);
		return PhotoScheme + id;
	}

	/// <summary>The picked photo, shrunk to fit; null if the user cancelled.</summary>
	private static async Task<byte[]?> PickAsync(int maxSide, int maxBytes)
	{
		var picked = await MediaPicker.Default.PickPhotosAsync(PickerOptions(maxSide));
		var file = picked?.FirstOrDefault();
		return file is null ? null : await ShrinkAsync(file, maxSide, maxBytes);
	}

	/// <summary>
	/// How the photo picker or camera is opened: the title and the size photos are taken at.
	/// </summary>
	private static MediaPickerOptions PickerOptions(int maxSide) => new()
	{
		SelectionLimit = 1,
		MaximumWidth = maxSide,
		MaximumHeight = maxSide,
		CompressionQuality = 80,
		// Apply the camera's EXIF orientation, or portrait photos show up sideways.
		RotateImage = true
	};

	/// <inheritdoc />
	public async Task<Stream> OpenAsync(string reference, CancellationToken ct = default)
	{
		if (reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			return await _http.GetStreamAsync(reference, ct);

		var (collection, id) =
			reference.StartsWith(Scheme) ? (Collection, reference[Scheme.Length..])
			: reference.StartsWith(PhotoScheme) ? (PhotoCollection, reference[PhotoScheme.Length..])
			: (null, string.Empty);
		if (collection is null || !IdPattern().IsMatch(id))
			throw new ArgumentException(L.F("Not an image reference: {0}", reference));

		var path = CachePath(id);
		if (!File.Exists(path))
		{
			var doc = await _firestore.GetAsync(collection, id, ct)
				?? throw new FileNotFoundException(L.F("Image {0} no longer exists.", id));
			await File.WriteAllBytesAsync(path, (byte[])doc["data"]!, ct);
		}
		return File.OpenRead(path);
	}

	/// <summary>
	/// The picker already resizes to maxSide; if the result is still too big
	/// (e.g. a detailed PNG), re-encode as smaller, lower-quality JPEGs until it fits.
	/// </summary>
	private static async Task<byte[]> ShrinkAsync(FileResult file, int maxSide, int maxBytes)
	{
		await using var source = await file.OpenReadAsync();
		return await ShrinkAsync(source, maxSide, maxBytes);
	}

	/// <summary>
	/// Makes a picture small enough to store: at most `maxSide` pixels wide and high and `maxBytes` big, as a
	/// JPEG.
	/// </summary>
	private static async Task<byte[]> ShrinkAsync(Stream source, int maxSide, int maxBytes)
	{
		using var original = new MemoryStream();
		await source.CopyToAsync(original);
		if (original.Length <= maxBytes)
			return original.ToArray();

		original.Position = 0;
		using var image = PlatformImage.FromStream(original);
		float side = maxSide, quality = 0.75f;
		for (var attempt = 0; attempt < 5; attempt++)
		{
			using var resized = image.Downsize(side, disposeOriginal: false);
			using var output = new MemoryStream();
			await resized.SaveAsync(output, ImageFormat.Jpeg, quality);
			if (output.Length <= maxBytes)
				return output.ToArray();

			side *= 0.75f;
			quality = Math.Max(0.5f, quality - 0.1f);
		}
		throw new InvalidOperationException(L.T("This image is too large to upload - try a smaller one."));
	}

	/// <summary>
	/// AppDataDirectory rather than CacheDirectory: Android may wipe the cache,
	/// and images must keep working offline once seen.
	/// </summary>
	private static string CachePath(string id)
	{
		var dir = Path.Combine(FileSystem.AppDataDirectory, "images");
		Directory.CreateDirectory(dir);
		return Path.Combine(dir, id);
	}

	// Ids are Guid "N" strings (profile pictures: "{uid}_{guid}"); anything else could escape the cache folder.
	[GeneratedRegex("^[A-Za-z0-9_]{1,160}$")]
	private static partial Regex IdPattern();
}
