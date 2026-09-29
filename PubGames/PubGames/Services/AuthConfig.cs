namespace PubGames.Services;

/// <summary>
/// Values from the Firebase console. None of these are secrets - the Web API
/// key and OAuth client id ship inside every Firebase app.
/// </summary>
public static class AuthConfig
{
	/// <summary>Firebase console → Project settings → General → Web API key.</summary>
	public const string FirebaseApiKey = "AIzaSyBeN5_HszGzsjFzL40iwbdpR17KXCgnhZI";

	/// <summary>
	/// Firebase console → Authentication → Sign-in method → Google → Web SDK
	/// configuration → "Web client ID" (ends in .apps.googleusercontent.com).
	/// Must be the WEB client id, not the Android one.
	/// </summary>
	public const string GoogleWebClientId = "737835893040-vgbin8mt2kp74279etk3sqtdn5nskb92.apps.googleusercontent.com";

	/// <summary>Firebase console → Project settings → General → Project ID. Used for Firestore.</summary>
	public const string FirebaseProjectId = "pubgames-92835";

	/// <summary>
	/// Built-in admins: Google accounts that are always admin and can't be
	/// removed from inside the app. Keep in sync with isBuiltInAdmin() in
	/// firestore.rules - the rules are what actually protect the cloud data.
	/// Further admins and moderators are added by email on the Team page.
	/// </summary>
	public static readonly string[] AdminEmails =
	[
		"jjh.geraats@gmail.com",
	];

	public static bool IsConfigured =>
		!FirebaseApiKey.StartsWith("PASTE_") && !GoogleWebClientId.StartsWith("PASTE_");
}
