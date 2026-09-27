using System.Text.RegularExpressions;
using Microsoft.Maui.Graphics.Platform;

namespace PubGames.Services;

/// <summary>
/// Uploads and loads game images (covers, rules pictures). Games store only an
/// image reference string, so the storage backend can change without touching
/// existing games:
///   "fsimg:{id}" - stored in Firestore (free plan; current backend)
///   "https://..." - any web URL, e.g. Firebase Storage after moving to the Blaze plan
/// </summary>
public interface IImageStore
{
	/// <summary>Lets the user pick a photo, shrinks and uploads it. Returns its reference, or null if they cancelled.</summary>
	Task<string?> PickAndUploadAsync();

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

	/// <summary>Leaves headroom under Firestore's 1 MiB document limit. Keep in sync with firestore.rules.</summary>
	private const int MaxBytes = 700 * 1024;
	private const int MaxSide = 1280;

	private readonly FirestoreClient _firestore;
	private readonly IAuthService _auth;
	private readonly HttpClient _http = new();

	public FirestoreImageStore(FirestoreClient firestore, IAuthService auth)
	{
		_firestore = firestore;
		_auth = auth;
	}

	public async Task<string?> PickAndUploadAsync()
	{
		var picked = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
		{
			SelectionLimit = 1,
			MaximumWidth = MaxSide,
			MaximumHeight = MaxSide,
			CompressionQuality = 80,
			// Apply the camera's EXIF orientation, or portrait photos show up sideways.
			RotateImage = true
		});
		var file = picked?.FirstOrDefault();
		if (file is null) return null;

		var bytes = await ShrinkAsync(file);
		var id = Guid.NewGuid().ToString("N");

		await _firestore.SetAsync(Collection, id, new Dictionary<string, object?>
		{
			["id"] = id,
			["data"] = bytes,
			["createdByUserId"] = _auth.AccountId,
			["createdAt"] = DateTime.UtcNow
		});

		await File.WriteAllBytesAsync(CachePath(id), bytes);
		return Scheme + id;
	}

	public async Task<Stream> OpenAsync(string reference, CancellationToken ct = default)
	{
		if (reference.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			return await _http.GetStreamAsync(reference, ct);

		if (!reference.StartsWith(Scheme) || !IdPattern().IsMatch(reference[Scheme.Length..]))
			throw new ArgumentException($"Not an image reference: {reference}");

		var id = reference[Scheme.Length..];
		var path = CachePath(id);
		if (!File.Exists(path))
		{
			var doc = await _firestore.GetAsync(Collection, id, ct)
				?? throw new FileNotFoundException($"Image {id} no longer exists.");
			await File.WriteAllBytesAsync(path, (byte[])doc["data"]!, ct);
		}
		return File.OpenRead(path);
	}

	/// <summary>
	/// The picker already resizes to MaxSide; if the result is still too big
	/// (e.g. a detailed PNG), re-encode as smaller, lower-quality JPEGs until it fits.
	/// </summary>
	private static async Task<byte[]> ShrinkAsync(FileResult file)
	{
		await using var source = await file.OpenReadAsync();
		using var original = new MemoryStream();
		await source.CopyToAsync(original);
		if (original.Length <= MaxBytes)
			return original.ToArray();

		original.Position = 0;
		using var image = PlatformImage.FromStream(original);
		float side = MaxSide, quality = 0.75f;
		for (var attempt = 0; attempt < 5; attempt++)
		{
			using var resized = image.Downsize(side, disposeOriginal: false);
			using var output = new MemoryStream();
			await resized.SaveAsync(output, ImageFormat.Jpeg, quality);
			if (output.Length <= MaxBytes)
				return output.ToArray();

			side *= 0.75f;
			quality = Math.Max(0.5f, quality - 0.1f);
		}
		throw new InvalidOperationException("This image is too large to upload - try a smaller one.");
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

	// Ids are generated as Guid "N" strings; anything else could escape the cache folder.
	[GeneratedRegex("^[A-Za-z0-9]{1,64}$")]
	private static partial Regex IdPattern();
}
