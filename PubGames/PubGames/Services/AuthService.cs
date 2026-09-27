using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PubGames.Models;

namespace PubGames.Services;

/// <summary>The signed-in account. Uid is the Firebase user id and is the account id used everywhere else.</summary>
public record AppUser(string Uid, string Email, string DisplayName, bool IsAdmin);

/// <summary>Platform-specific "pick a Google account" step. Returns a Google ID token.</summary>
public interface IGoogleSignInProvider
{
	Task<string> GetGoogleIdTokenAsync(string webClientId);
	Task SignOutAsync();
}

public interface IAuthService
{
	AppUser? CurrentUser { get; }
	bool IsSignedIn { get; }

	/// <summary>Firebase uid when signed in, otherwise the shared offline guest account.</summary>
	string AccountId { get; }

	event EventHandler? SignedInChanged;

	/// <summary>
	/// Restores the last session from secure storage (once; later calls reuse the result).
	/// Await before reading AccountId on startup. Returns true if the user is signed in.
	/// </summary>
	Task<bool> InitializeAsync();
	Task SignInWithGoogleAsync();
	Task SignOutAsync();

	/// <summary>A fresh Firebase ID token for calling your backend / Firestore, refreshed if near expiry.</summary>
	Task<string?> GetIdTokenAsync();
}

/// <summary>
/// Google sign-in → Firebase Auth, over Firebase's REST API so no native
/// Firebase SDK bindings are needed. Only the refresh token is persisted
/// (in SecureStorage); ID tokens are short-lived and re-minted from it.
/// </summary>
public class AuthService : IAuthService
{
	public const string GuestAccountId = "local-account";
	public const string DefaultHostOrgId = "current-org";

	private const string RefreshTokenKey = "auth_refresh_token";
	private const string UserKey = "auth_user";

	private readonly IGoogleSignInProvider _google;
	private readonly ILocalDatabaseService _local;
	private readonly HttpClient _http = new();

	private string? _idToken;
	private DateTime _idTokenExpiresAt;

	public AuthService(IGoogleSignInProvider google, ILocalDatabaseService local)
	{
		_google = google;
		_local = local;
	}

	public AppUser? CurrentUser { get; private set; }
	public bool IsSignedIn => CurrentUser is not null;
	public string AccountId => CurrentUser?.Uid ?? GuestAccountId;

	public event EventHandler? SignedInChanged;

	private Task<bool>? _restore;

	public Task<bool> InitializeAsync() => _restore ??= RestoreAsync();

	private async Task<bool> RestoreAsync()
	{
		var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
		var userJson = await SecureStorage.Default.GetAsync(UserKey);
		if (refreshToken is null || userJson is null || !AuthConfig.IsConfigured)
			return false;

		var saved = JsonSerializer.Deserialize<AppUser>(userJson);
		if (saved is null)
			return false;

		// Admin status is re-derived rather than trusted from storage, so edits to AdminEmails apply on restart.
		CurrentUser = saved with { IsAdmin = IsAdminEmail(saved.Email) };

		try
		{
			await RefreshIdTokenAsync(refreshToken);
		}
		catch (HttpRequestException)
		{
			// Offline at the pub: keep the cached identity, token refreshes on next network call.
		}
		catch (AuthException)
		{
			// Refresh token revoked or account disabled - force a fresh sign-in.
			ClearSession();
			return false;
		}

		await EnsureAdminMembershipAsync();
		SignedInChanged?.Invoke(this, EventArgs.Empty);
		return true;
	}

	public async Task SignInWithGoogleAsync()
	{
		if (!AuthConfig.IsConfigured)
			throw new AuthException("Sign-in isn't configured yet: fill in Services/AuthConfig.cs.");

		var googleIdToken = await _google.GetGoogleIdTokenAsync(AuthConfig.GoogleWebClientId);

		var response = await _http.PostAsJsonAsync(
			$"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={AuthConfig.FirebaseApiKey}",
			new
			{
				postBody = $"id_token={googleIdToken}&providerId=google.com",
				requestUri = "http://localhost",
				returnSecureToken = true,
				returnIdpCredential = true
			});
		var body = await ReadOrThrowAsync<SignInWithIdpResponse>(response);

		var email = body.Email ?? string.Empty;
		CurrentUser = new AppUser(body.LocalId, email, body.DisplayName ?? email, IsAdminEmail(email));
		SetIdToken(body.IdToken, body.ExpiresIn);

		await SecureStorage.Default.SetAsync(RefreshTokenKey, body.RefreshToken);
		await SecureStorage.Default.SetAsync(UserKey, JsonSerializer.Serialize(CurrentUser));

		await EnsureAdminMembershipAsync();
		SignedInChanged?.Invoke(this, EventArgs.Empty);
	}

	public async Task SignOutAsync()
	{
		ClearSession();
		try { await _google.SignOutAsync(); } catch { /* best effort: clears the account picker's auto-select */ }
		SignedInChanged?.Invoke(this, EventArgs.Empty);
	}

	public async Task<string?> GetIdTokenAsync()
	{
		if (CurrentUser is null) return null;
		if (_idToken is not null && DateTime.UtcNow < _idTokenExpiresAt) return _idToken;

		var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
		if (refreshToken is null) return null;

		await RefreshIdTokenAsync(refreshToken);
		return _idToken;
	}

	private async Task RefreshIdTokenAsync(string refreshToken)
	{
		var response = await _http.PostAsync(
			$"https://securetoken.googleapis.com/v1/token?key={AuthConfig.FirebaseApiKey}",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "refresh_token",
				["refresh_token"] = refreshToken
			}));
		var body = await ReadOrThrowAsync<RefreshResponse>(response);

		SetIdToken(body.IdToken, body.ExpiresIn);
		if (body.RefreshToken != refreshToken)
			await SecureStorage.Default.SetAsync(RefreshTokenKey, body.RefreshToken);
	}

	/// <summary>Admins get a head-host row in the default org so they show up on the Team page.</summary>
	private async Task EnsureAdminMembershipAsync()
	{
		if (CurrentUser is not { IsAdmin: true } user) return;

		var members = await _local.GetHostMembersAsync(DefaultHostOrgId);
		var me = members.FirstOrDefault(m => m.UserId == user.Uid);
		if (me is { Role: HostRole.HeadHost } && me.DisplayName == user.DisplayName) return;

		me ??= new HostMember { HostOrgId = DefaultHostOrgId, UserId = user.Uid };
		me.Role = HostRole.HeadHost;
		me.CanAddGames = true;
		me.CanDeleteGames = true;
		me.DisplayName = user.DisplayName;
		await _local.SaveHostMemberAsync(me);
	}

	private void ClearSession()
	{
		SecureStorage.Default.Remove(RefreshTokenKey);
		SecureStorage.Default.Remove(UserKey);
		CurrentUser = null;
		_idToken = null;
	}

	private void SetIdToken(string idToken, string expiresInSeconds)
	{
		_idToken = idToken;
		// Refresh a minute early so a token never expires mid-request.
		_idTokenExpiresAt = DateTime.UtcNow.AddSeconds(int.Parse(expiresInSeconds) - 60);
	}

	private static bool IsAdminEmail(string email) =>
		AuthConfig.AdminEmails.Any(a => a.Equals(email, StringComparison.OrdinalIgnoreCase));

	private static async Task<T> ReadOrThrowAsync<T>(HttpResponseMessage response)
	{
		if (!response.IsSuccessStatusCode)
		{
			var error = await response.Content.ReadAsStringAsync();
			throw new AuthException($"Firebase sign-in failed ({(int)response.StatusCode}): {error}");
		}
		return (await response.Content.ReadFromJsonAsync<T>())!;
	}

	private record SignInWithIdpResponse(
		[property: JsonPropertyName("localId")] string LocalId,
		[property: JsonPropertyName("email")] string? Email,
		[property: JsonPropertyName("displayName")] string? DisplayName,
		[property: JsonPropertyName("idToken")] string IdToken,
		[property: JsonPropertyName("refreshToken")] string RefreshToken,
		[property: JsonPropertyName("expiresIn")] string ExpiresIn);

	private record RefreshResponse(
		[property: JsonPropertyName("id_token")] string IdToken,
		[property: JsonPropertyName("refresh_token")] string RefreshToken,
		[property: JsonPropertyName("expires_in")] string ExpiresIn);
}

public class AuthException(string message) : Exception(message);
