using PubGames.Services;

namespace PubGames.Models;

/// <summary>What a change to a game does to one of its languages.</summary>
public enum TranslationChangeKind
{
	/// <summary>The translation was corrected by hand (which also counts as checking it).</summary>
	Corrected,

	/// <summary>The translation was marked as checked (Translation is fine), without changing it.</summary>
	Approved,

	/// <summary>A correction by hand was taken away: the cloud translates it again.</summary>
	BackToAutomatic,

	/// <summary>This language becomes the original (Make this the original).</summary>
	NewOriginal,

	/// <summary>This was the original and is now kept as the translation into it.</summary>
	OldOriginal
}

/// <summary>One language that a change to a game touches, for review requests.</summary>
public sealed record TranslationChange(AppLanguageInfo Language, TranslationChangeKind Kind)
{
	/// <summary>The language's flag.</summary>
	public string Flag => Language.Flag;

	/// <summary>The language's name and what happens to it, e.g. "French: translation corrected".</summary>
	public string Label => L.F("{0}: {1}", Language.Name, Kind switch
	{
		TranslationChangeKind.Corrected => L.T("translation corrected"),
		TranslationChangeKind.Approved => L.T("translation marked as checked"),
		TranslationChangeKind.BackToAutomatic => L.T("back to the automatic translation"),
		TranslationChangeKind.NewOriginal => L.T("becomes the original"),
		_ => L.T("was the original, kept as the translation")
	});

	/// <summary>Who on the team uses the app in this language, to ask them to look; filled in by the review page.</summary>
	public string Speakers { get; init; } = string.Empty;

	/// <summary>The languages a change to a game touches: corrected, checked, back to automatic or a new original.</summary>
	/// <param name="before">The game as it is now; null for a new game.</param>
	/// <param name="after">The game with the proposed changes.</param>
	public static List<TranslationChange> Between(PubGame? before, PubGame after)
	{
		var changes = new List<TranslationChange>();
		var sourceAfter = after.SourceLanguageCode;
		var sourceBefore = before?.SourceLanguageCode;
		var newOriginal = before is not null && sourceBefore != sourceAfter;
		if (newOriginal)
		{
			if (AppLanguage.Find(sourceAfter) is { } becomes) changes.Add(new(becomes, TranslationChangeKind.NewOriginal));
			if (AppLanguage.Find(sourceBefore) is { } was) changes.Add(new(was, TranslationChangeKind.OldOriginal));
		}

		var beforeTranslations = before?.Translations ?? [];
		var afterTranslations = after.Translations;
		foreach (var language in AppLanguage.All)
		{
			if (language.Code == sourceAfter || (newOriginal && language.Code == sourceBefore)) continue;
			beforeTranslations.TryGetValue(language.Code, out var was);
			afterTranslations.TryGetValue(language.Code, out var now);

			if (now is null)
			{
				if (was is { Manual: true })
					changes.Add(new(language, TranslationChangeKind.BackToAutomatic));
				continue;
			}

			var contentChanged = was is null || was.Name != now.Name || was.RulesText != now.RulesText || !SameTexts(was.ImageTexts, now.ImageTexts);
			if (now.Manual && contentChanged)
				changes.Add(new(language, TranslationChangeKind.Corrected));
			else if (now.CheckedHash.Length > 0 && now.CheckedHash != (was?.CheckedHash ?? string.Empty))
				changes.Add(new(language, TranslationChangeKind.Approved));
		}
		return changes;
	}

	/// <summary>Whether two sets of picture texts are the same.</summary>
	private static bool SameTexts(Dictionary<string, List<string>> a, Dictionary<string, List<string>> b) =>
		a.Count == b.Count && a.All(entry => b.TryGetValue(entry.Key, out var other) && other.SequenceEqual(entry.Value));
}

/// <summary>
/// One row of the side-by-side check on a review request: a piece of the original and the same piece in the
/// translation, or a picture (shown with the translation's texts).
/// </summary>
public sealed record ComparisonRow(string? Original, string? Translation, string? ImageRef)
{
	/// <summary>A piece of text in both languages.</summary>
	public bool IsText => ImageRef is null;

	/// <summary>A picture.</summary>
	public bool IsImage => ImageRef is not null;

	/// <summary>The translation is missing for this piece.</summary>
	public bool HasNoTranslation => IsText && string.IsNullOrWhiteSpace(Translation);

	/// <summary>
	/// The original's rules next to a translation's, piece by piece: each text in the original is followed by
	/// the same text in the translation (in the same order; pieces left over in the translation come last).
	/// Pictures show once, with the translation's texts in them.
	/// </summary>
	public static List<ComparisonRow> Pair(string originalName, string originalRules, GameTranslation translation, string languageCode)
	{
		var rows = new List<ComparisonRow> { new(originalName, translation.Name, null) };
		var translatedTexts = RulesBlock.Parse(translation.RulesText).Where(b => b.IsText).Select(b => b.Text!).ToList();
		var next = 0;
		foreach (var block in RulesBlock.Parse(originalRules))
		{
			if (block.IsImage)
			{
				rows.Add(new(null, null, TranslatedPictures.Translate(block.ImageRef!, languageCode, translation.ImageTexts)));
				continue;
			}
			rows.Add(new(block.Text, next < translatedTexts.Count ? translatedTexts[next] : null, null));
			next++;
		}
		for (; next < translatedTexts.Count; next++)
			rows.Add(new(null, translatedTexts[next], null));
		return rows;
	}
}

/// <summary>One change in a review request, shown as "before" and then "after".</summary>
/// <param name="Flag">The flag of the language it's in.</param>
/// <param name="Heading">Which language and what kind of change, e.g. "French · Change".</param>
/// <param name="Before">The text before (empty for something added).</param>
/// <param name="After">The text after (empty for something removed).</param>
public sealed record SentenceChange(string Flag, string Heading, string Before, string After)
{
	/// <summary>The picture before (for a picture that changed or was removed), or null.</summary>
	public string? BeforeImage { get; init; }

	/// <summary>The picture after (for a picture that was added or changed), or null.</summary>
	public string? AfterImage { get; init; }

	/// <summary>There's a picture before.</summary>
	public bool HasBeforeImage => BeforeImage is not null;

	/// <summary>There's a picture after.</summary>
	public bool HasAfterImage => AfterImage is not null;

	/// <summary>The picture reference in a picture tag ("![image](fsimg:ID)" gives "fsimg:ID"), or null.</summary>
	public static string? ImageOf(string? tag) =>
		tag is not null && tag.StartsWith("![", StringComparison.Ordinal) && tag.Split('(', ')') is { Length: > 1 } parts ? parts[1] : null;

	/// <summary>There's a text before.</summary>
	public bool HasBefore => Before.Length > 0;

	/// <summary>There's a text after.</summary>
	public bool HasAfter => After.Length > 0;

	/// <summary>
	/// Every change sentence by sentence: in the original (compared as sentences), then per translation (added,
	/// changed or removed sentences, and how many were marked as checked).
	/// </summary>
	public static List<SentenceChange> Between(PubGame? before, PubGame after)
	{
		var changes = new List<SentenceChange>();
		if (AppLanguage.Find(after.SourceLanguageCode) is { } original)
			changes.AddRange(Compare(original, Sentences.Of(before?.RulesText ?? string.Empty), Sentences.Of(after.RulesText), L.T("original")));

		var beforeTranslations = before?.Translations ?? [];
		foreach (var language in AppLanguage.All.Where(l => l.Code != after.SourceLanguageCode))
		{
			if (!after.Translations.TryGetValue(language.Code, out var now) || now.Segments.Count == 0) continue;
			beforeTranslations.TryGetValue(language.Code, out var was);
			var old = was?.Segments ?? [];
			var oldBySource = old.Where(s => s.Src.Length > 0).GroupBy(s => s.Src).ToDictionary(g => g.Key, g => g.First());
			var checkedNow = 0;
			foreach (var segment in now.Segments.Where(s => s.IsPicture))
			{
				// Pictures are the same in every language: added, replaced or removed here, they change everywhere.
				if (segment.Status == TranslationSegment.Added)
					changes.Add(new(language.Flag, HeadingFor(language, L.T("Picture added")), string.Empty, string.Empty) { AfterImage = ImageOf(segment.Text) });
				else if (segment.Text.Trim().Length == 0)
					changes.Add(new(language.Flag, HeadingFor(language, L.T("Picture removed")), string.Empty, string.Empty) { BeforeImage = ImageOf(segment.Src) });
				else if (segment.Text != segment.Src)
					changes.Add(new(language.Flag, HeadingFor(language, L.T("Picture changed")), string.Empty, string.Empty)
						{ BeforeImage = ImageOf(segment.Src), AfterImage = ImageOf(segment.Text) });
			}
			foreach (var segment in now.Segments.Where(s => !s.IsPicture))
			{
				if (segment.Status == TranslationSegment.Added)
					changes.Add(new(language.Flag, HeadingFor(language, L.T("Addition")), string.Empty, segment.Text));
				else if (oldBySource.TryGetValue(segment.Src, out var previous))
				{
					if (previous.Text != segment.Text)
						changes.Add(new(language.Flag, HeadingFor(language, segment.Text.Trim().Length == 0 ? L.T("Removal") : L.T("Change")), previous.Text, segment.Text));
					else if (segment.IsChecked && !previous.IsChecked)
						checkedNow++;
				}
			}
			if (checkedNow > 0)
				changes.Add(new(language.Flag, HeadingFor(language, L.T("Checked")), string.Empty,
					checkedNow == 1 ? L.T("1 sentence marked as checked, unchanged") : L.F("{0} sentences marked as checked, unchanged", checkedNow)));
		}
		return changes;
	}

	/// <summary>"French · Change".</summary>
	private static string HeadingFor(AppLanguageInfo language, string kind) => $"{language.Name} · {kind}";

	/// <summary>The original's changes, sentence by sentence: added, removed, and changed (a new sentence in the place of an old one).</summary>
	private static IEnumerable<SentenceChange> Compare(AppLanguageInfo language, List<TranslationSegment> older, List<TranslationSegment> newer, string what)
	{
		var match = Sentences.Match(older.Select(s => s.Src).ToList(), newer.Select(s => s.Src).ToList());
		var matchedOld = match.Where(m => m >= 0).ToHashSet();
		var changes = new List<SentenceChange>();
		// Removed sentences and added ones, paired as changes in their order.
		var removed = older.Where((s, i) => !matchedOld.Contains(i)).ToList();
		var added = newer.Where((s, j) => match[j] < 0).ToList();
		for (var k = 0; k < Math.Max(removed.Count, added.Count); k++)
		{
			var was = k < removed.Count ? removed[k] : null;
			var now = k < added.Count ? added[k] : null;
			var kind = was is null ? L.T("Addition") : now is null ? L.T("Removal") : L.T("Change");
			changes.Add(new(language.Flag, $"{language.Name} ({what}) · {kind}",
				was is { IsPicture: true } ? string.Empty : Describe(was), now is { IsPicture: true } ? string.Empty : Describe(now))
			{
				BeforeImage = was is { IsPicture: true } ? ImageOf(was.Src) : null,
				AfterImage = now is { IsPicture: true } ? ImageOf(now.Src) : null
			});
		}
		return changes;
	}

	/// <summary>A sentence as text, or a picture as "(picture)".</summary>
	private static string Describe(TranslationSegment? segment) =>
		segment is null ? string.Empty : segment.IsPicture ? L.T("(picture)") : segment.Src;
}
