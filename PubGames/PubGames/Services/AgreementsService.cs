using System.Text.Json;
using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// The admin agreements as the owner changed them in the app: one cloud
/// document (agreements/current) that every team member can read and only
/// the owner can write. The cloud function translateAgreements translates it
/// into every app language. This phone keeps a copy for offline use.
/// </summary>
public sealed class AgreementsService(FirestoreClient firestore)
{
	private const string Collection = "agreements";
	private const string DocumentId = "current";

	/// <summary>Where this phone keeps its copy of the owner's text.</summary>
	private const string CacheKey = "agreementsText";

	/// <summary>
	/// Uses the newest agreements: this phone's copy straight away, then the
	/// cloud's (at most a few seconds, so starting the app isn't held up).
	/// Offline or nothing changed by the owner: the copy or the built-in text stays.
	/// </summary>
	public async Task RefreshAsync()
	{
		AdminAgreements.Use(ReadCache());
		try
		{
			using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			var doc = await firestore.GetAsync(Collection, DocumentId, timeout.Token);
			if (doc is null || FromDoc(doc) is not { } text) return;
			Use(text);
		}
		catch (Exception)
		{
			// Offline (or not allowed to read): the copy on this phone is used.
		}
	}

	/// <summary>
	/// The owner saves changed agreements, written in the app's language.
	/// `changed`: they changed in a way that matters, so everyone agrees again
	/// (the owner has agreed by writing them); otherwise a small correction.
	/// The other languages follow once the cloud has translated them.
	/// </summary>
	public async Task SaveAsync(IReadOnlyList<HelpSection> sections, bool changed, string ownerEmail)
	{
		var version = changed ? AdminAgreements.Version + 1 : AdminAgreements.Version;
		var language = AppLanguage.Current.Code;
		var json = AgreementsText.Write(sections);
		await firestore.SetAsync(Collection, DocumentId, new Dictionary<string, object?>
		{
			["version"] = version,
			["sourceLanguage"] = language,
			["sectionsJson"] = json,
			["updatedAt"] = DateTime.UtcNow
		});
		Use(new AgreementsText(version, language, json, []));
		Preferences.Default.Set(AdminAgreements.LocalKey(ownerEmail), version);
	}

	/// <summary>Uses this text and keeps a copy on the phone.</summary>
	private static void Use(AgreementsText text)
	{
		AdminAgreements.Use(text);
		Preferences.Default.Set(CacheKey, JsonSerializer.Serialize(text));
	}

	/// <summary>This phone's copy of the owner's text, or null.</summary>
	private static AgreementsText? ReadCache()
	{
		var json = Preferences.Default.Get(CacheKey, string.Empty);
		if (json.Length == 0) return null;
		try
		{
			return JsonSerializer.Deserialize<AgreementsText>(json);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>The owner's text from the cloud document, or null when it can't be read.</summary>
	private static AgreementsText? FromDoc(Dictionary<string, object?> doc)
	{
		if (doc.GetValueOrDefault("sectionsJson") is not string json || AgreementsText.Read(json) is null) return null;
		var version = doc.GetValueOrDefault("version") is { } v ? Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture) : AdminAgreements.BuiltInVersion;
		var translations = new Dictionary<string, string>();
		// Only translations of this very text: an older translation would show what was there before.
		if (doc.GetValueOrDefault("translatedFrom") as string == json && doc.GetValueOrDefault("translations") is Dictionary<string, object?> map)
			foreach (var (code, value) in map)
				if (value is string translated)
					translations[code] = translated;
		return new AgreementsText(version, doc.GetValueOrDefault("sourceLanguage") as string ?? "en", json, translations);
	}
}
