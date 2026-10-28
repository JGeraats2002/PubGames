using Android.OS;
using AndroidX.Core.Content;
using AndroidX.Credentials;
using AndroidX.Credentials.Exceptions;
using Google.Android.Libraries.Identity.GoogleId;
using PubGames.Services;

namespace PubGames;

/// <summary>
/// Shows Android's native Google account picker via Credential Manager and
/// returns the Google ID token for Firebase. Needs an Android OAuth client
/// (package name + SHA-1) in the same Google Cloud project as the web client id.
/// </summary>
public class GoogleSignInProvider : IGoogleSignInProvider
{
	/// <summary>
	/// Shows Android's Google account picker (Credential Manager) and returns the chosen account's Google ID
	/// token, which Firebase then turns into a sign-in.
	/// </summary>
	public async Task<string> GetGoogleIdTokenAsync(string webClientId)
	{
		var activity = Platform.CurrentActivity
			?? throw new InvalidOperationException(L.T("No foreground activity to show the account picker on."));

		var option = new GetSignInWithGoogleOption.Builder(webClientId).Build();
		var request = new GetCredentialRequest.Builder().AddCredentialOption(option).Build();

		var callback = new CredentialCallback();
		CredentialManager.Companion.Create(activity).GetCredentialAsync(
			activity, request, new CancellationSignal(), ContextCompat.GetMainExecutor(activity)!, callback);

		var response = await callback.Task;
		if (response.Credential is CustomCredential credential &&
		    credential.Type == GoogleIdTokenCredential.TypeGoogleIdTokenCredential)
		{
			return GoogleIdTokenCredential.CreateFrom(credential.Data).IdToken;
		}

		throw new AuthException(L.T("Google didn't return an ID token."));
	}

	/// <summary>
	/// Forgets the chosen Google account on this phone, so the next sign-in shows the account picker again.
	/// </summary>
	public async Task SignOutAsync()
	{
		var activity = Platform.CurrentActivity;
		if (activity is null) return;

		var callback = new ClearCallback();
		CredentialManager.Companion.Create(activity).ClearCredentialStateAsync(
			new ClearCredentialStateRequest(), new CancellationSignal(), ContextCompat.GetMainExecutor(activity)!, callback);
		await callback.Task;
	}

	/// <summary>Turns Credential Manager's Java callback into a Task the sign-in can await.</summary>
	private sealed class CredentialCallback : Java.Lang.Object, ICredentialManagerCallback
	{
		private readonly TaskCompletionSource<GetCredentialResponse> _tcs = new();
		/// <summary>Completes when an account is picked, or fails when picking didn't work.</summary>
		public Task<GetCredentialResponse> Task => _tcs.Task;

		/// <summary>The user picked an account: hands the result to the waiting sign-in.</summary>
		public void OnResult(Java.Lang.Object? result) => _tcs.TrySetResult((GetCredentialResponse)result!);

		/// <summary>
		/// Picking an account failed or was cancelled: the sign-in gets the error with a readable message.
		/// </summary>
		public void OnError(Java.Lang.Object e)
		{
			// The binding erases the Kotlin error type to Object; re-wrap as the managed exception peer.
			var error = Java.Lang.Object.GetObject<Java.Lang.Throwable>(e.Handle, Android.Runtime.JniHandleOwnership.DoNotTransfer);

			if (error is GetCredentialCancellationException)
				_tcs.TrySetCanceled();
			else if (error is NoCredentialException)
				_tcs.TrySetException(new AuthException(L.T("No Google account found on this device. Add one in Android Settings → Accounts.")));
			else
				_tcs.TrySetException(new AuthException(L.F("Google sign-in failed: {0}", error?.Message)));
		}
	}

	/// <summary>
	/// Turns the Java callback of forgetting the account into a Task the sign-out can await.
	/// </summary>
	private sealed class ClearCallback : Java.Lang.Object, ICredentialManagerCallback
	{
		private readonly TaskCompletionSource _tcs = new();
		/// <summary>Completes when the account is forgotten.</summary>
		public Task Task => _tcs.Task;

		/// <summary>The account is forgotten.</summary>
		public void OnResult(Java.Lang.Object? result) => _tcs.TrySetResult();
		/// <summary>Forgetting failed (e.g. nothing to forget): signing out goes on anyway.</summary>
		public void OnError(Java.Lang.Object e) => _tcs.TrySetResult();
	}
}
