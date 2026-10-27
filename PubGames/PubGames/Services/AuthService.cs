using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace PubGames.Services;

/// <summary>The signed-in account. Uid is the Firebase user id and is the account id used everywhere else.</summary>
/// IsAdmin means a built-in admin from AuthConfig.AdminEmails; team roles come from ITeamService.
public record AppUser(string Uid, string Email, string DisplayName, bool IsAdmin);

/// <summary>Platform-specific "pick a Google account" step. Returns a Google ID token.</summary>
public interface IGoogleSignInProvider
{
	Task<string> GetGoogleIdTokenAsync(string webClientId);
	Task SignOutAsync();
}

/// <summary>
/// The signed-in account: signing in with Google, staying signed in (also offline), signing out, and the
/// short-lived token that proves to the cloud who's asking.
/// </summary>
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
	/// <summary>Value kept from when teams were called host orgs, so existing games still match.</summary>
	public const string DefaultTeamId = "current-org";

	private const string RefreshTokenKey = "auth_refresh_token";
	private const string UserKey = "auth_user";

	private readonly IGoogleSignInProvider _google;
	private readonly HttpClient _http = new();

	private string? _idToken;
	private DateTime _idTokenExpiresAt;

	/// <summary>
	/// Gets the services it works with - Google sign-in - from the app (dependency injection).
	/// </summary>
	public AuthService(IGoogleSignInProvider google)
	{
		_google = google;
	}

	/// <summary>The signed-in user, or null when nobody is signed in.</summary>
	public AppUser? CurrentUser { get; private set; }
	/// <summary>Whether someone is signed in.</summary>
	public bool IsSignedIn => CurrentUser is not null;
	/// <inheritdoc />
	public string AccountId => CurrentUser?.Uid ?? GuestAccountId;

	public event EventHandler? SignedInChanged;

	private Task<bool>? _restore;

	/// <inheritdoc />
	public Task<bool> InitializeAsync() => _restore ??= RestoreAsync();

	/// <summary>
	/// Starting the app: signs in again with the saved session, if there is one. Offline the saved identity
	/// is used; a revoked session means signing in again.
	/// </summary>
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

		SignedInChanged?.Invoke(this, EventArgs.Empty);
		return true;
	}

	/// <summary>
	/// Signs in with the Google account the user picks: its Google token is exchanged with Firebase for a
	/// session, which is saved securely on the phone.
	/// </summary>
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

		SignedInChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Signs out: the saved session is removed from the phone and the Google account is forgotten.
	/// </summary>
	public async Task SignOutAsync()
	{
		ClearSession();
		try { await _google.SignOutAsync(); } catch { /* best effort: clears the account picker's auto-select */ }
		SignedInChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <inheritdoc />
	public async Task<string?> GetIdTokenAsync()
	{
		if (CurrentUser is null) return null;
		if (_idToken is not null && DateTime.UtcNow < _idTokenExpiresAt) return _idToken;

		var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
		if (refreshToken is null) return null;

		await RefreshIdTokenAsync(refreshToken);
		return _idToken;
	}

	/// <summary>
	/// Gets a fresh ID token (valid for an hour) with the saved refresh token; throws AuthException when the
	/// session is no longer valid.
	/// </summary>
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

	/// <summary>
	/// Removes the saved session from the phone's secure storage and forgets the signed-in user.
	/// </summary>
	private void ClearSession()
	{
		SecureStorage.Default.Remove(RefreshTokenKey);
		SecureStorage.Default.Remove(UserKey);
		CurrentUser = null;
		_idToken = null;
	}

	/// <summary>
	/// Keeps the ID token and when it expires (a minute early, so it's never used just as it runs out).
	/// </summary>
	private void SetIdToken(string idToken, string expiresInSeconds)
	{
		_idToken = idToken;
		// Refresh a minute early so a token never expires mid-request.
		_idTokenExpiresAt = DateTime.UtcNow.AddSeconds(int.Parse(expiresInSeconds) - 60);
	}

	/// <summary>Whether this email is one of the app's built-in admins (AuthConfig.AdminEmails).</summary>
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

	/// <summary>Firebase's answer to signing in with a Google token.</summary>
	private record SignInWithIdpResponse(
		[property: JsonPropertyName("localId")] string LocalId,
		[property: JsonPropertyName("email")] string? Email,
		[property: JsonPropertyName("displayName")] string? DisplayName,
		[property: JsonPropertyName("idToken")] string IdToken,
		[property: JsonPropertyName("refreshToken")] string RefreshToken,
		[property: JsonPropertyName("expiresIn")] string ExpiresIn);

	/// <summary>Firebase's answer to refreshing the ID token.</summary>
	private record RefreshResponse(
		[property: JsonPropertyName("id_token")] string IdToken,
		[property: JsonPropertyName("refresh_token")] string RefreshToken,
		[property: JsonPropertyName("expires_in")] string ExpiresIn);
}

/// <summary>Signing in failed or the session is no longer valid, with a message for the user.</summary>
public class AuthException(string message) : Exception(message);
