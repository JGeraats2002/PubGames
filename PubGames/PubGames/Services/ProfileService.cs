using System.Text.Json;
using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// The user's public profile (name, nickname, picture, player code) and looking up other players by their
/// code.
/// </summary>
public interface IProfileService
{
	/// <summary>The signed-in user's profile as last seen on this phone; null before the first load.</summary>
	UserProfile? Mine { get; }

	/// <summary>
	/// The signed-in user's profile from the cloud; made (with a new personal
	/// code) the first time. Offline: the copy on this phone.
	/// </summary>
	Task<UserProfile?> LoadMineAsync();

	Task SaveMineAsync(UserProfile profile);

	/// <summary>Picks and uploads a new profile picture; returns its reference, or null if cancelled.</summary>
	Task<string?> PickPhotoAsync();

	/// <summary>The player with this personal code, or null if there's none.</summary>
	Task<UserProfile?> FindByCodeAsync(string code);

	Task<UserProfile?> GetAsync(string uid);
}

/// <summary>
/// Profiles live in the cloud (profiles/{uid}), readable by every signed-in
/// user so players can add each other by code. Codes are claimed in
/// codes/{code}, which the rules only let be created once - that's what keeps
/// every code unique.
/// </summary>
public class ProfileService(FirestoreClient firestore, IAuthService auth, IImageStore images) : IProfileService
{
	private const string Profiles = "profiles";
	private const string Codes = "codes";

	private string CacheKey => $"profile:{auth.AccountId}";

	/// <inheritdoc />
	public UserProfile? Mine { get; private set; }

	/// <inheritdoc />
	public async Task<UserProfile?> LoadMineAsync()
	{
		var user = auth.CurrentUser;
		if (user is null) return Mine = null;
		Mine ??= ReadCache();

		try
		{
			var profile = await GetAsync(user.Uid);
			if (profile is null)
			{
				var firstName = user.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
				profile = new UserProfile(user.Uid, user.DisplayName, firstName, await ClaimNewCodeAsync(user.Uid), null);
				await WriteAsync(profile);
			}
			else if (UserProfile.NormalizeCode(profile.Code) is null)
			{
				// A code from before codes became 8 characters can't be looked up any more: claim a new one.
				profile = profile with { Code = await ClaimNewCodeAsync(user.Uid) };
				await WriteAsync(profile);
			}
			Mine = profile;
			WriteCache(profile);
		}
		catch (Exception ex) when (ex is HttpRequestException or CloudException)
		{
			// Offline: the copy on this phone (null if there's never been one).
		}
		return Mine;
	}

	/// <inheritdoc />
	public async Task SaveMineAsync(UserProfile profile)
	{
		await WriteAsync(profile);
		Mine = profile;
		WriteCache(profile);
	}

	/// <inheritdoc />
	public Task<string?> PickPhotoAsync() => images.PickAndUploadProfilePhotoAsync();

	/// <inheritdoc />
	public async Task<UserProfile?> FindByCodeAsync(string code)
	{
		var claim = await firestore.GetAsync(Codes, code);
		return claim?.GetValueOrDefault("uid") is string uid ? await GetAsync(uid) : null;
	}

	/// <inheritdoc />
	public async Task<UserProfile?> GetAsync(string uid)
	{
		var f = await firestore.GetAsync(Profiles, uid);
		if (f is null) return null;
		return new UserProfile(uid,
			f.GetValueOrDefault("name") as string ?? string.Empty,
			f.GetValueOrDefault("nickname") as string ?? string.Empty,
			f.GetValueOrDefault("code") as string ?? string.Empty,
			f.GetValueOrDefault("photoRef") as string);
	}

	/// <summary>Saves a profile in the cloud (only the user's own: the security rules check that).</summary>
	private Task WriteAsync(UserProfile p) =>
		firestore.SetAsync(Profiles, p.Uid, new Dictionary<string, object?>
		{
			["uid"] = p.Uid,
			["name"] = p.Name,
			["nickname"] = p.Nickname,
			["code"] = p.Code,
			["photoRef"] = p.PhotoRef,
			["updatedAt"] = DateTime.UtcNow
		});

	/// <summary>
	/// Tries random codes until one is free. A taken code is refused by the
	/// rules (claims can't be overwritten), so the next one is tried.
	/// </summary>
	private async Task<string> ClaimNewCodeAsync(string uid)
	{
		for (var attempt = 0; attempt < 8; attempt++)
		{
			var code = UserProfile.NewCode();
			try
			{
				await firestore.SetAsync(Codes, code, new Dictionary<string, object?> { ["code"] = code, ["uid"] = uid });
				return code;
			}
			catch (CloudException ex) when (ex.StatusCode == 403)
			{
				// Taken by someone else: try another.
			}
		}
		throw new InvalidOperationException("Couldn't make your personal code. Check that the latest firestore.rules are published.");
	}

	/// <summary>The profile as saved on this phone, for when there's no connection.</summary>
	private UserProfile? ReadCache()
	{
		try
		{
			var json = Preferences.Default.Get(CacheKey, string.Empty);
			return json.Length == 0 ? null : JsonSerializer.Deserialize<UserProfile>(json);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>Saves the profile on this phone, for when there's no connection.</summary>
	private void WriteCache(UserProfile profile) =>
		Preferences.Default.Set(CacheKey, JsonSerializer.Serialize(profile));
}
