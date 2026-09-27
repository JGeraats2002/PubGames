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
	public async Task<string> GetGoogleIdTokenAsync(string webClientId)
	{
		var activity = Platform.CurrentActivity
			?? throw new InvalidOperationException("No foreground activity to show the account picker on.");

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

		throw new AuthException("Google didn't return an ID token.");
	}

	public async Task SignOutAsync()
	{
		var activity = Platform.CurrentActivity;
		if (activity is null) return;

		var callback = new ClearCallback();
		CredentialManager.Companion.Create(activity).ClearCredentialStateAsync(
			new ClearCredentialStateRequest(), new CancellationSignal(), ContextCompat.GetMainExecutor(activity)!, callback);
		await callback.Task;
	}

	private sealed class CredentialCallback : Java.Lang.Object, ICredentialManagerCallback
	{
		private readonly TaskCompletionSource<GetCredentialResponse> _tcs = new();
		public Task<GetCredentialResponse> Task => _tcs.Task;

		public void OnResult(Java.Lang.Object? result) => _tcs.TrySetResult((GetCredentialResponse)result!);

		public void OnError(Java.Lang.Object e)
		{
			// The binding erases the Kotlin error type to Object; re-wrap as the managed exception peer.
			var error = Java.Lang.Object.GetObject<Java.Lang.Throwable>(e.Handle, Android.Runtime.JniHandleOwnership.DoNotTransfer);

			if (error is GetCredentialCancellationException)
				_tcs.TrySetCanceled();
			else if (error is NoCredentialException)
				_tcs.TrySetException(new AuthException("No Google account found on this device. Add one in Android Settings → Accounts."));
			else
				_tcs.TrySetException(new AuthException($"Google sign-in failed: {error?.Message}"));
		}
	}

	private sealed class ClearCallback : Java.Lang.Object, ICredentialManagerCallback
	{
		private readonly TaskCompletionSource _tcs = new();
		public Task Task => _tcs.Task;

		public void OnResult(Java.Lang.Object? result) => _tcs.TrySetResult();
		public void OnError(Java.Lang.Object e) => _tcs.TrySetResult();
	}
}
