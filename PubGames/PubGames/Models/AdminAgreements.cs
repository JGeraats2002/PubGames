using System.Text.Json;
using PubGames.Services;

namespace PubGames.Models;

/// <summary>
/// The rules and agreements for admins and moderators: what they publish,
/// at what price, and how. Every team member agrees to them once, before
/// they can use the admin pages. The owner can change them in the app
/// (AgreementsService); a change that matters raises Version, and everyone
/// agrees again. Until the owner changed them, the built-in text below is used.
/// </summary>
public static class AdminAgreements
{
	/// <summary>The version of the built-in text.</summary>
	public const int BuiltInVersion = 1;

	/// <summary>The owner's text from the cloud, or null while the built-in text is used.</summary>
	private static AgreementsText? _text;

	/// <summary>The version everyone has to agree to: raised when the owner changes the agreements in a way that matters.</summary>
	public static int Version => Math.Max(BuiltInVersion, _text?.Version ?? BuiltInVersion);

	/// <summary>Whether the owner's text is used rather than the built-in one.</summary>
	public static bool IsChangedByOwner => _text is not null;

	/// <summary>The agreements, section by section, in the app's language (the owner's own language until it's translated).</summary>
	public static IReadOnlyList<HelpSection> Sections => _text?.InLanguage(AppLanguage.Current.Code) ?? BuiltInSections;

	/// <summary>Uses the owner's text (null: the built-in text again).</summary>
	public static void Use(AgreementsText? text) => _text = text;

	/// <summary>The agreements as they come with the app, in the app's language.</summary>
	public static IReadOnlyList<HelpSection> BuiltInSections =>
	[
		new(L.T("Roles"), L.T("Admins publish and change games directly: players see the change straight away. Moderators submit their changes for review; an admin approves or rejects them, and the decision arrives in the Inbox. Requests, Feedback and Team are for admins only.")),
		new(L.T("Which price"), L.T("Free: well-known classics everyone already knows (like Mexen or Kingsen), with rules you can explain in a few minutes, few pictures and simple scoring. Free games make the app worth having without paying. €0.99: a fully worked-out game with pictures and set-up scoring, of about 15 to 45 minutes, or your own variant of a known game. €1.99: a big game, with extensive rules in several stages or subgames, a score sheet with many rows and bonuses, many pictures or 45 minutes or longer, or a unique game you won't find anywhere else.")),
		new(L.T("Price agreements"), L.T("When in doubt, choose the lower price. A free game never becomes paid later; from paid to free is fine. Similar games get the same price. Moderators propose a price, an admin decides.")),
		new(L.T("Content"), L.T("Nothing offensive, discriminating or sexual. No brand names, and no rules texts, pictures or logos copied from publishers: the rules of a game are free to use, their texts and names aren't (write Yatzy, not Yahtzee). Every picture is made in the image editor.")),
		new(L.T("Drinking games"), L.T("The app is for adults (18+). Nobody is forced to drink: every drinking task has an alternative, like another task or a sip of water. No games about drinking as fast or as much as possible: Google Play doesn't allow encouraging excessive drinking.")),
		new(L.T("Quality"), L.T("Before a game is published: the rules are complete and clear, the number of players and the categories are right, the scoring is tried out with Test as player, and the translations are checked.")),
		new(L.T("Misuse"), L.T("Breaking these agreements can cost you your admin or moderator rights. In doubt? Ask an admin first."))
	];

	/// <summary>Where this phone remembers that an account agreed (also for owners, who have no place on the team list).</summary>
	public static string LocalKey(string email) => $"agreedRules:{TeamMember.NormalizeEmail(email)}";

	/// <summary>
	/// Whether this team member agreed to the current agreements: on this
	/// phone, or on the team list in the cloud (another phone). Players
	/// (null) have nothing to agree to.
	/// </summary>
	public static bool IsAgreed(TeamMember? member) => member is null
		|| member.AgreedRulesVersion >= Version
		|| Preferences.Default.Get(LocalKey(member.Email), 0) >= Version;
}

/// <summary>
/// The agreements as the owner wrote them: their version, the language they
/// were written in, the sections as JSON, and the cloud's translations of
/// them (language code → sections as JSON).
/// </summary>
public sealed record AgreementsText(int Version, string SourceLanguage, string SectionsJson, Dictionary<string, string> Translations)
{
	/// <summary>The sections in this language; in the owner's language when there's no translation (yet).</summary>
	public IReadOnlyList<HelpSection> InLanguage(string code) =>
		(code != SourceLanguage && Translations.TryGetValue(code, out var translated) ? Read(translated) : null)
		?? Read(SectionsJson)
		?? AdminAgreements.BuiltInSections;

	/// <summary>Sections from their JSON; null when it can't be read or has none.</summary>
	public static List<HelpSection>? Read(string json)
	{
		try
		{
			var sections = JsonSerializer.Deserialize<List<HelpSection>>(json);
			return sections is { Count: > 0 } ? sections : null;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	/// <summary>Sections as JSON, for saving.</summary>
	public static string Write(IEnumerable<HelpSection> sections) => JsonSerializer.Serialize(sections.ToList());
}
