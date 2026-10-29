using SQLite;

namespace PubGames.Models;

/// <summary>
/// A game in the library. Content (name/rules/images) is authored by an admin or moderator;
/// categories and scoring type are set at creation; pricing is admin-controlled
/// per the "only some games are paid" decision.
/// </summary>
public class PubGame
{
	/// <summary>The game's unique id.</summary>
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>The game's name.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Rich-text/markdown explanation of how to play, may reference inline image URLs.</summary>
	public string RulesText { get; set; } = string.Empty;

	/// <summary>
	/// The language Name and RulesText are written in (en, nl, ...; see AppLanguage). Empty for games from
	/// before translations: those are English.
	/// </summary>
	public string SourceLanguage { get; set; } = string.Empty;

	/// <summary>The language the game was written in; English for older games.</summary>
	[Ignore]
	public string SourceLanguageCode => string.IsNullOrEmpty(SourceLanguage) ? "en" : SourceLanguage;

	/// <summary>The translations into the other languages, as JSON; use Translations.</summary>
	public string? TranslationsText { get; set; }

	/// <summary>The name and rules in the other languages, by language code (see GameTranslation).</summary>
	[Ignore]
	public Dictionary<string, GameTranslation> Translations
	{
		get => GameTranslation.FromJson(TranslationsText);
		set => TranslationsText = GameTranslation.ToJson(value);
	}

	/// <summary>
	/// The game as players see it in the app's language: a copy with the translated name and rules, or this
	/// game itself when it's written in that language or not translated yet. Never save the copy as the game.
	/// </summary>
	public PubGame InAppLanguage()
	{
		var code = Services.AppLanguage.Current.Code;
		if (code == SourceLanguageCode
			|| !Translations.TryGetValue(code, out var translation)
			|| string.IsNullOrWhiteSpace(translation.Name))
			return this;

		var copy = (PubGame)MemberwiseClone();
		copy.Name = translation.Name;
		copy.RulesText = Services.TranslatedPictures.Translate(
			string.IsNullOrWhiteSpace(translation.RulesText) ? RulesText : translation.RulesText, code, translation.ImageTexts);
		if (CoverImageUrl is { } cover)
			copy.CoverImageUrl = Services.TranslatedPictures.Translate(cover, code, translation.ImageTexts);
		return copy;
	}

	/// <summary>
	/// The name in the app's language (the translation's name, also a corrected one), for lists that show the
	/// game itself, like the admins' library. The game keeps its own Name.
	/// </summary>
	[Ignore]
	public string ShownName => InAppLanguage().Name;

	/// <summary>The cover in the app's language (its text translated), for the same lists as ShownName.</summary>
	[Ignore]
	public string? ShownCoverImageUrl => InAppLanguage().CoverImageUrl;

	/// <summary>The cover picture's reference (see ImageRef), or null without one.</summary>
	public string? CoverImageUrl { get; set; }

	/// <summary>How scores are kept during the game.</summary>
	public ScoringType ScoringType { get; set; } = ScoringType.PointTally;

	/// <summary>The scoring type's settings (step, min, max, ...) as JSON; use ScoringSettings.</summary>
	public string? ScoringSettingsText { get; set; }

	[Ignore]
	public ScoringSettings ScoringSettings
	{
		get => ScoringSettings.FromJson(ScoringSettingsText);
		set => ScoringSettingsText = value.ToJson();
	}

	/// <summary>The scoring type as the admin library shows it.</summary>
	[Ignore]
	public string ScoringLabel => ScoringType is ScoringType.PlusMinus or ScoringType.ManualPlusMinus
		? $"{ScoringTypeOption.LabelFor(ScoringType)} ({ScoringSettings.SummaryFor(ScoringType)} · {ScoringSettings.TieSummaryFor(ScoringType)})"
		: $"{ScoringTypeOption.LabelFor(ScoringType)} ({ScoringSettings.ResultSummary} · {ScoringSettings.TieSummaryFor(ScoringType)})";

	/// <summary>Whether the game has to be unlocked (bought, a code, or the pass).</summary>
	public bool IsPaid { get; set; }

	/// <summary>Price in euros, e.g. 0.50m. Ignored when IsPaid is false. Admin-set, not fixed by the platform.</summary>
	public decimal Price { get; set; }

	/// <summary>GameCategory ids, comma-separated for SQLite; use CategoryIds. Every game needs at least one.</summary>
	public string CategoryIdsText { get; set; } = string.Empty;

	[Ignore]
	public List<string> CategoryIds
	{
		get => CategoryIdsText.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
		set => CategoryIdsText = string.Join(',', value.Distinct());
	}

	/// <summary>The fewest players the game needs; null for no minimum.</summary>
	public int? MinPlayers { get; set; }

	/// <summary>The most players the game can take; null for no maximum.</summary>
	public int? MaxPlayers { get; set; }

	/// <summary>"Any", "2+" (at least 2), "1-3" (at most 3) or "2-6".</summary>
	[Ignore]
	public string PlayersLabel => PlayersLabelFor(MinPlayers, MaxPlayers);

	/// <summary>
	/// Why this game can't be played with this many players - how many to add
	/// or take out - or null when the number fits.
	/// </summary>
	public string? PlayerCountProblem(int players)
	{
		static string Players(int n) => n == 1 ? L.T("1 player") : L.F("{0} players", n);

		if (MinPlayers is { } least && players < least)
			return L.F("{0} is for {1} players and you have {2}. Add at least {3} more to play this game.", Name, PlayersLabel, Players(players), Players(least - players));
		if (MaxPlayers is { } most && players > most)
			return L.F("{0} is for {1} players and you have {2}. Take out at least {3} to play this game.", Name, PlayersLabel, Players(players), Players(players - most));
		return null;
	}

	/// <summary>"2-6", "2+", "1-3" or "Any": the number of players a game is for.</summary>
	public static string PlayersLabelFor(int? min, int? max) => (min, max) switch
	{
		(null, null) => L.T("Any"),
		({ } least, null) => $"{least}+",
		(null, { } most) => most <= 1 ? "1" : $"1-{most}",
		({ } least, { } most) => least == most ? $"{least}" : $"{least}-{most}"
	};

	/// <summary>Category names for display, filled in by whoever shows the game (names live on GameCategory).</summary>
	[Ignore]
	public string CategoryLabel { get; set; } = string.Empty;

	/// <summary>Whether the game has no category yet.</summary>
	[Ignore]
	public bool HasNoCategory => string.IsNullOrEmpty(CategoryIdsText);

	/// <summary>
	/// Right of a game in the players' list: its price with a lock when the
	/// account doesn't own it yet; empty for free and owned games. Filled in
	/// by the list.
	/// </summary>
	[Ignore]
	public string AccessLabel { get; set; } = string.Empty;

	/// <summary>One line under the game's name in a list: "2-6 players · Cards, 15-30 min" (the categories when CategoryLabel is filled in).</summary>
	[Ignore]
	public string InfoLine => string.Join(" · ", new[]
	{
		(MinPlayers, MaxPlayers) switch
		{
			(null, null) => L.T("Any number of players"),
			_ when PlayersLabel == "1" => L.T("1 player"),
			_ => L.F("{0} players", PlayersLabel)
		},
		CategoryLabel
	}.Where(part => part.Length > 0));

	/// <summary>
	/// The admin library: which main categories the game has no category of
	/// yet, e.g. "Still needs: Duration, Play style", or "Waiting for review"
	/// once changes that fill them in were submitted. Empty when it's complete.
	/// </summary>
	[Ignore]
	public string MissingLabel { get; set; } = string.Empty;

	/// <summary>Whether something required is still missing (see MissingLabel).</summary>
	[Ignore]
	public bool HasMissing => MissingLabel.Length > 0;

	/// <summary>
	/// The moderators' library: another moderator's request (changes or
	/// deletion) that's waiting for this game. Until an admin decides or it's
	/// withdrawn, only that moderator can change the game.
	/// </summary>
	[Ignore]
	public ReviewRequest? TakenBy { get; set; }

	/// <summary>Whether another moderator has changes to it waiting for review.</summary>
	[Ignore]
	public bool IsTaken => TakenBy is not null;

	/// <summary>Who has changes to it waiting for review, as the admin library shows it.</summary>
	[Ignore]
	public string TakenLabel => TakenBy switch
	{
		null => string.Empty,
		{ Type: ReviewRequestType.Deletion } request => L.F("🔒 {0} asked to delete this game - waiting for review", request.SubmitterLabel),
		var request => L.F("🔒 {0} submitted changes - waiting for review", request.SubmitterLabel)
	};

	/// <summary>Which team owns/manages this game.</summary>
	public string TeamId { get; set; } = string.Empty;

	/// <summary>Which admin/moderator account authored it.</summary>
	public string CreatedByUserId { get; set; } = string.Empty;

	/// <summary>Draft, published or deleted: players only see published games.</summary>
	public GameStatus Status { get; set; } = GameStatus.Draft;

	/// <summary>When the game was made (UTC).</summary>
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	/// <summary>When it was published (UTC), or null while it's a draft.</summary>
	public DateTime? PublishedAt { get; set; }

	/// <summary>The status as the admin library shows it.</summary>
	[Ignore]
	public string StatusLabel => Status switch
	{
		GameStatus.Published => L.T("Published"),
		GameStatus.Deleted => L.T("Deleted"),
		GameStatus.Draft => L.T("Draft"),
		// Games from before review requests existed.
		_ => L.T("Waiting for review")
	};

	/// <summary>"€ 0,99" or "Free".</summary>
	[Ignore]
	public string PriceLabel => IsPaid ? Price.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("nl-NL")) : L.T("Free");
}

/// <summary>
/// Stored as a number on the phone, so the order must not change. The two
/// Pending* values are no longer used: moderators' proposals are review
/// requests now and don't change the game itself.
/// </summary>
public enum GameStatus
{
	Draft,
	PendingPublishApproval,
	Published,
	PendingDeletionApproval,
	Deleted
}
