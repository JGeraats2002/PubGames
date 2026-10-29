using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>One text in a picture made with Create image, to check and correct in another language.</summary>
public partial class PictureText : ObservableObject
{
	/// <summary>The picture's id (after "fsimg:").</summary>
	public required string PictureId { get; init; }

	/// <summary>The text as it is in the original picture.</summary>
	public required string Original { get; init; }

	/// <summary>The translation the page showed, to see whether it was corrected.</summary>
	public required string Shown { get; init; }

	/// <summary>The translation, as shown and corrected.</summary>
	[ObservableProperty]
	private string text = string.Empty;

	/// <summary>The text was changed: the picture is drawn again with it.</summary>
	public event EventHandler? Changed;

	partial void OnTextChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);

	/// <summary>Corrected on the page.</summary>
	public bool IsChanged => Text.Trim() != Shown.Trim();
}

/// <summary>The texts of one picture on the Languages page, and its preview that follows them.</summary>
public static class PictureTexts
{
	/// <summary>
	/// Puts the texts on the page and shows the picture with them; when one is corrected, the picture is drawn
	/// again a moment after typing stops.
	/// </summary>
	public static void Show(ObservableCollection<PictureText> shown, IEnumerable<PictureText> texts, Action<string> setPreview, Action shownChanged)
	{
		CancellationTokenSource? pending = null;
		foreach (var text in texts)
		{
			shown.Add(text);
			text.Changed += async (_, _) =>
			{
				pending?.Cancel();
				var mine = pending = new CancellationTokenSource();
				try
				{
					await Task.Delay(500, mine.Token);
				}
				catch (TaskCanceledException)
				{
					return;
				}
				setPreview(PreviewOf(shown));
			};
		}
		shownChanged();
		if (shown.Count > 0)
			setPreview(PreviewOf(shown));
	}

	/// <summary>The picture's reference with these texts in it.</summary>
	private static string PreviewOf(IReadOnlyList<PictureText> texts) =>
		TranslatedPictures.Preview(texts[0].PictureId, texts.Select(t => string.IsNullOrWhiteSpace(t.Text) ? t.Original : t.Text).ToList());
}

/// <summary>
/// The game form's languages: which language the game is written in (General page), and the Languages page
/// where a team member checks and corrects the translations. Translations are made by the cloud after saving;
/// a correction by hand is kept and never translated over again.
/// </summary>
public partial class AdminCreateGameViewModel
{
	/// <summary>The Languages page of the form.</summary>
	public const int LanguagesPage = 3;

	/// <summary>The Languages page is open.</summary>
	public bool IsLanguagesPage => CurrentPage == LanguagesPage;

	/// <summary>Every language the app has, for "Written in".</summary>
	public IReadOnlyList<AppLanguageInfo> Languages => AppLanguage.All;

	/// <summary>The language the name and rules are written in. A new game starts in the app's language.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(TranslationLanguages), nameof(WrittenInMyLanguage), nameof(WrittenInMyLanguageHint))]
	private AppLanguageInfo? sourceLanguage = AppLanguage.Current;

	/// <summary>The game is written in the app's language: there's no translation into it to check.</summary>
	public bool WrittenInMyLanguage => SourceLanguage?.Code == AppLanguage.Current.Code;

	/// <summary>Why the Languages page doesn't open in the app's language for this game.</summary>
	public string WrittenInMyLanguageHint => SourceLanguage is { } source
		? L.F("This game is written in {0}, your own language, so {0} is the original and not a translation. Here you check its translations into the other languages.", source.Name)
		: string.Empty;

	/// <summary>Another language is chosen to check when the one shown becomes the original's language.</summary>
	partial void OnSourceLanguageChanged(AppLanguageInfo? value)
	{
		if (TranslationLanguage is null || TranslationLanguage == value)
			TranslationLanguage = PreferredTranslationLanguage();
	}

	/// <summary>The languages the game is translated into: all but the one it's written in.</summary>
	public List<AppLanguageInfo> TranslationLanguages => AppLanguage.All.Where(l => l != SourceLanguage).ToList();

	/// <summary>The translations as saved (or proposed), by language code; corrections are added when switching language or saving.</summary>
	private Dictionary<string, GameTranslation> _translations = [];

	/// <summary>The translation shown on the Languages page.</summary>
	[ObservableProperty]
	private AppLanguageInfo? translationLanguage;

	/// <summary>What's on the page for the language that was shown is kept before the next one shows.</summary>
	partial void OnTranslationLanguageChanging(AppLanguageInfo? oldValue, AppLanguageInfo? newValue)
	{
		_translations = EffectiveTranslations();
	}

	/// <summary>Shows the chosen language's translation.</summary>
	partial void OnTranslationLanguageChanged(AppLanguageInfo? value) => ShowTranslation();

	/// <summary>The translated name, as shown and corrected.</summary>
	[ObservableProperty]
	private string translatedName = string.Empty;

	/// <summary>The texts on the cover picture, when it was made with Create image.</summary>
	public ObservableCollection<PictureText> CoverTexts { get; } = [];

	/// <summary>The cover has texts to translate.</summary>
	[ObservableProperty]
	private bool hasCoverTexts;

	/// <summary>The cover as shown on the Translations page: with the texts as they are there.</summary>
	[ObservableProperty]
	private string? coverPreviewRef;

	/// <summary>The translation shown, sentence by sentence (and its pictures).</summary>
	public ObservableCollection<SentenceRow> SentenceRows { get; } = [];

	/// <summary>The sentences of the translation shown, as changed on the page (not saved yet).</summary>
	private List<TranslationSegment> _segments = [];

	/// <summary>The segments the page showed for this language, to see whether anything changed.</summary>
	private List<TranslationSegment> _shownSegments = [];

	/// <summary>The translation shown is made per sentence, so it can be checked and changed here.</summary>
	[ObservableProperty]
	private bool hasSentences;

	/// <summary>What the translation shown is, or why it can't be changed here yet.</summary>
	[ObservableProperty]
	private string translationStatus = string.Empty;

	/// <summary>Whether a person has checked the translation shown, and how many sentences still need it.</summary>
	[ObservableProperty]
	private string checkStatus = string.Empty;

	/// <summary>Every sentence of the translation shown is checked against the original as it is now.</summary>
	[ObservableProperty]
	private bool isChecked;

	/// <summary>The translation shown can be marked as checked.</summary>
	[ObservableProperty]
	private bool canApproveTranslation;

	/// <summary>Per language, how many sentences still have to be checked: the overview at the top.</summary>
	[ObservableProperty]
	private string toCheckOverview = string.Empty;

	/// <summary>Sentences that came into the original from a translation, for someone who speaks the original's language.</summary>
	public ObservableCollection<AddedToOriginal> AddedToOriginal { get; } = [];

	/// <summary>There are sentences in the original to approve.</summary>
	[ObservableProperty]
	private bool hasAddedToOriginal;

	/// <summary>What tapping does on the page: nothing, choose where to add, or choose words to change or remove.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsAdding), nameof(IsSelecting), nameof(IsChangingWords), nameof(IsRemovingWords), nameof(IsBrowsing), nameof(IsChoosingBlock))]
	private TranslationMode mode;

	/// <summary>Adding, changing or removing: a tap on a sentence or picture block chooses it.</summary>
	public bool IsChoosingBlock => Mode != TranslationMode.None;

	/// <summary>Tapping a sentence adds after it.</summary>
	public bool IsAdding => Mode == TranslationMode.Adding;

	/// <summary>Tapping words selects them.</summary>
	public bool IsSelecting => Mode is TranslationMode.Changing or TranslationMode.Removing;

	/// <summary>The selection will be changed.</summary>
	public bool IsChangingWords => Mode == TranslationMode.Changing;

	/// <summary>The selection will be removed.</summary>
	public bool IsRemovingWords => Mode == TranslationMode.Removing;

	/// <summary>No special mode: the buttons for checking and changing show.</summary>
	public bool IsBrowsing => Mode == TranslationMode.None;

	/// <summary>The words tapped as the ends of the selection.</summary>
	private readonly List<WordChip> _anchors = [];

	/// <summary>Who is signed in, as a checker is named: their name, or their email.</summary>
	private string MyCheckerName => _auth.CurrentUser is { } user
		? (string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName)
		: string.Empty;

	/// <summary>The fingerprint of the original as it is on the form now (see GameTranslation.HashOf).</summary>
	private string OriginalHash() => GameTranslation.HashOf(Name.Trim(), EditableRulesBlock.ToRulesText(RulesBlocks), CoverImageUrl);

	/// <summary>
	/// Translation is fine: every sentence of the language shown counts as checked by the signed-in person
	/// (sentences a person corrected stay corrected). Saving keeps it.
	/// </summary>
	[RelayCommand]
	private void ApproveTranslation()
	{
		if (TranslationLanguage is not { } language || !HasSentences) return;
		_segments = _segments.Select(s => s.IsPicture || s.Status is TranslationSegment.Edited or TranslationSegment.Added
			? s : s with { Status = TranslationSegment.Approved, Suggestion = null }).ToList();
		var translations = EffectiveTranslations();
		if (translations.TryGetValue(language.Code, out var translation))
			translations[language.Code] = translation with { CheckedHash = OriginalHash(), CheckedBy = MyCheckerName };
		_translations = translations;
		ShowTranslation();
		StatusMessage = L.T("Marked as checked. Save to keep it.");
	}

	/// <summary>Add: tap the sentence to add after (or Add at the start).</summary>
	[RelayCommand]
	private void StartAdding() => StartMode(TranslationMode.Adding);

	/// <summary>Change: tap the first and last word to change.</summary>
	[RelayCommand]
	private void StartChanging() => StartMode(TranslationMode.Changing);

	/// <summary>Remove: tap the first and last word to remove.</summary>
	[RelayCommand]
	private void StartRemoving() => StartMode(TranslationMode.Removing);

	/// <summary>Back to just reading: nothing selected.</summary>
	[RelayCommand]
	private void CancelMode() => StartMode(TranslationMode.None);

	/// <summary>Switches what tapping does, with nothing selected.</summary>
	private void StartMode(TranslationMode value)
	{
		_anchors.Clear();
		foreach (var word in SentenceRows.SelectMany(r => r.Words))
			word.IsSelected = false;
		Mode = value;
	}

	/// <summary>
	/// A tapped word while choosing: it becomes an end of the selection (everything between the first and last
	/// one tapped is selected); tapped again, it isn't any more.
	/// </summary>
	[RelayCommand]
	private void TapWord(WordChip word)
	{
		// Adding: a tap anywhere in a sentence chooses it as the place to add after.
		if (IsAdding)
		{
			if (word.Row < SentenceRows.Count) AddPlaceChosen?.Invoke(this, SentenceRows[word.Row]);
			return;
		}
		if (!IsSelecting) return;
		if (!_anchors.Remove(word))
			_anchors.Add(word);
		var (first, last) = SelectionEnds();
		foreach (var chip in SentenceRows.SelectMany(r => r.Words))
			chip.IsSelected = first is not null && Position(chip) >= Position(first) && Position(chip) <= Position(last!);
	}

	/// <summary>Where a word is on the page, as one number to compare (sentence first, then word).</summary>
	private static long Position(WordChip word) => word.Row * 100_000L + word.Index;

	/// <summary>The first and last word of the selection; nulls when nothing is selected.</summary>
	private (WordChip? First, WordChip? Last) SelectionEnds() => _anchors.Count == 0
		? (null, null)
		: (_anchors.MinBy(Position), _anchors.MaxBy(Position));

	/// <summary>The selected words as one text, for the pop-up; empty when nothing is selected.</summary>
	public string SelectedText() =>
		string.Join(" ", SentenceRows.SelectMany(r => r.Words).Where(w => w.IsSelected).Select(w => w.Text));

	/// <summary>The sentences the selection is in, as one text (for the pop-up, to see it in context).</summary>
	public string SelectedSentences() =>
		string.Join(" ", SentenceRows.Where(r => r.Words.Any(w => w.IsSelected)).Select(r => r.Segment.Text));

	/// <summary>
	/// Replaces the selected words with the given text (empty: removes them). The sentences it touches count
	/// as corrected by a person; a sentence emptied completely is left out of the translation.
	/// </summary>
	public void ReplaceSelection(string replacement)
	{
		var (first, last) = SelectionEnds();
		if (first is null || last is null) return;
		replacement = replacement.Trim();

		for (var row = first.Row; row <= last.Row; row++)
		{
			// Pictures in between stay: only text is changed or removed.
			if (SentenceRows[row].IsPicture) continue;
			var words = SentenceRows[row].Words.Select(w => w.Text).ToList();
			var from = row == first.Row ? first.Index : 0;
			var to = row == last.Row ? last.Index : words.Count - 1;
			var kept = words.Take(from).ToList();
			if (row == first.Row && replacement.Length > 0) kept.Add(replacement);
			kept.AddRange(words.Skip(to + 1));
			var index = SentenceRows[row].Index;
			_segments[index] = _segments[index] with
			{
				Text = string.Join(" ", kept),
				Status = _segments[index].Status == TranslationSegment.Added ? TranslationSegment.Added : TranslationSegment.Edited,
				Suggestion = null
			};
		}
		ShowSegments();
		StartMode(TranslationMode.None);
		StatusMessage = replacement.Length > 0 ? L.T("Changed. Save to keep it.") : L.T("Removed. Save to keep it.");
	}

	/// <summary>
	/// Adds a sentence after the given one (null: at the start). After saving, the cloud puts it into the
	/// original and from there into every language.
	/// </summary>
	public void AddSentence(SentenceRow? after, string text, bool newParagraph)
	{
		text = text.Trim();
		if (text.Length == 0) return;
		var at = after is null ? 0 : after.Index + 1;
		_segments.Insert(at, new TranslationSegment
		{
			Src = string.Empty, Text = text, Sep = newParagraph ? "\n\n" : " ", Status = TranslationSegment.Added
		});
		ShowSegments();
		StartMode(TranslationMode.None);
		StatusMessage = L.T("Added. Save to keep it: then it goes into the original and every language.");
	}

	/// <summary>Changing: the sentence gets the given text (typed in the pop-up). It counts as corrected by a person.</summary>
	public void ReplaceSentence(SentenceRow row, string text)
	{
		text = text.Trim();
		var segment = _segments[row.Index];
		if (text.Length == 0 || text == segment.Text) { StartMode(TranslationMode.None); return; }
		_segments[row.Index] = segment with
		{
			Text = text, Suggestion = null,
			Status = segment.Status == TranslationSegment.Added ? TranslationSegment.Added : TranslationSegment.Edited
		};
		ShowSegments();
		StartMode(TranslationMode.None);
		StatusMessage = L.T("Changed. Save to keep it.");
	}

	/// <summary>
	/// Changing a picture: it's replaced by the new version made in the image editor. After saving it's replaced
	/// in the original and every language (pictures are the same everywhere).
	/// </summary>
	public void ReplacePicture(SentenceRow row, string reference)
	{
		_segments[row.Index] = _segments[row.Index] with { Text = RulesBlock.RulesImageTag(reference) };
		ShowSegments();
		StartMode(TranslationMode.None);
		StatusMessage = L.T("Changed. Save to keep it: the picture is then replaced in the original and every language.");
	}

	/// <summary>
	/// Removing: a sentence leaves this translation; a picture leaves the original and every language after
	/// saving. Something added here and not saved yet just goes.
	/// </summary>
	public void RemoveRow(SentenceRow row)
	{
		var segment = _segments[row.Index];
		if (segment.Status == TranslationSegment.Added)
			_segments.RemoveAt(row.Index);
		else
			_segments[row.Index] = segment with
			{
				Text = string.Empty, Suggestion = null,
				Status = segment.IsPicture ? segment.Status : TranslationSegment.Edited
			};
		ShowSegments();
		StartMode(TranslationMode.None);
		StatusMessage = segment.IsPicture
			? L.T("Removed. Save to keep it: the picture then leaves the original and every language.")
			: L.T("Removed. Save to keep it.");
	}

	/// <summary>Verwerpen: everything changed on the Translations page since it was opened (or saved) is undone.</summary>
	public void DiscardTranslations()
	{
		_translations = new Dictionary<string, GameTranslation>(_opened?.Translations ?? []);
		ShowTranslation();
		StatusMessage = L.T("Changes to the translations discarded.");
	}

	/// <summary>Adding: a sentence was tapped as the place to add after (the page asks what to add).</summary>
	public event EventHandler<SentenceRow>? AddPlaceChosen;

	/// <summary>
	/// Adds a picture made in the image editor after the given sentence (null: at the start). After saving the
	/// cloud puts it in the same place in the original and every language.
	/// </summary>
	public void AddPicture(SentenceRow? after, string reference)
	{
		var at = after is null ? 0 : after.Index + 1;
		_segments.Insert(at, new TranslationSegment
		{
			Src = string.Empty, Text = RulesBlock.RulesImageTag(reference), Sep = "\n\n", Status = TranslationSegment.Added
		});
		ShowSegments();
		StartMode(TranslationMode.None);
		StatusMessage = L.T("Added. Save to keep it: then it goes into the original and every language.");
	}

	/// <summary>A sentence to look at again: take the new automatic translation of the changed original.</summary>
	[RelayCommand]
	private void TakeSuggestion(SentenceRow row)
	{
		if (row.Segment.Suggestion is not { } suggestion) return;
		_segments[row.Index] = row.Segment with { Text = suggestion, Status = TranslationSegment.Edited, Suggestion = null };
		ShowSegments();
	}

	/// <summary>A sentence to look at again: the translation that's there is still right.</summary>
	[RelayCommand]
	private void KeepTranslation(SentenceRow row)
	{
		_segments[row.Index] = row.Segment with { Status = TranslationSegment.Approved, Suggestion = null };
		ShowSegments();
	}

	/// <summary>A sentence that came into the original from a translation is right in the original's language.</summary>
	[RelayCommand]
	private void ApproveAddedToOriginal(AddedToOriginal added)
	{
		_translations = EffectiveTranslations();
		if (!_translations.TryGetValue(added.LanguageCode, out var translation)) return;
		_translations[added.LanguageCode] = translation with
		{
			Segments = translation.Segments.Select(s => s.Src == added.Sentence && s.Origin == "added" ? s with { Origin = null } : s).ToList()
		};
		ShowTranslation();
	}

	/// <summary>The translation shown was corrected by hand, so it can go back to the automatic one.</summary>
	[ObservableProperty]
	private bool canUseAutomaticTranslation;

	/// <summary>The name the page showed for this language, to see whether it was corrected.</summary>
	private string _shownName = string.Empty;

	/// <summary>Shows the translation of the chosen language sentence by sentence, with what still needs checking.</summary>
	private void ShowTranslation()
	{
		var code = TranslationLanguage?.Code;
		StartMode(TranslationMode.None);
		ShowOverview();

		if (code is not null && _translations.TryGetValue(code, out var translation))
		{
			_shownName = translation.Name;
			_segments = translation.Segments.ToList();
			_shownSegments = translation.Segments.ToList();
			HasSentences = _segments.Count > 0 || string.IsNullOrWhiteSpace(translation.RulesText);
			TranslationStatus = HasSentences
				? L.T("Translated automatically from the original, sentence by sentence. Check it, or change what isn't right.")
				: L.T("This translation is from before translations were made per sentence. Save the game once: then it's translated again, sentence by sentence.");
			CanUseAutomaticTranslation = translation.Segments.Any(s => s.Status is TranslationSegment.Edited or TranslationSegment.Approved);
		}
		else
		{
			_shownName = Name.Trim();
			_segments = [];
			_shownSegments = [];
			HasSentences = false;
			TranslationStatus = L.T("Not translated yet: this happens automatically a minute after saving.");
			CanUseAutomaticTranslation = false;
		}

		TranslatedName = _shownName;
		ShowSegments();
		CoverTexts.Clear();
		HasCoverTexts = false;
		CoverPreviewRef = CoverImageUrl;
		if (IsLanguagesPage)
			_ = ShowPictureTextsAsync(code is not null && _translations.TryGetValue(code, out var shown) ? shown.ImageTexts : []);
	}

	/// <summary>
	/// Puts the sentences on the page as they are now, with how many still need checking. The sentences
	/// themselves are only built while the Translations page is open: long rules make many of them.
	/// </summary>
	private void ShowSegments()
	{
		var pictureTexts = SentenceRows.Where(r => r.IsPicture).GroupBy(r => r.Segment.Src).ToDictionary(g => g.Key, g => g.First().Texts.ToList());
		SentenceRows.Clear();
		for (var index = 0; index < (IsLanguagesPage ? _segments.Count : 0); index++)
		{
			// Removed here (a sentence, or a picture that leaves every language): no longer shown.
			if (_segments[index].Text.Trim().Length == 0) continue;
			var row = SentenceRow.For(index, _segments[index]);
			if (row.IsPicture && pictureTexts.TryGetValue(row.Segment.Src, out var texts))
				row.ShowTexts(texts.Select(t => new PictureText { PictureId = t.PictureId, Original = t.Original, Shown = t.Shown, Text = t.Text }));
			SentenceRows.Add(row);
		}

		var sentences = _segments.Where(s => !s.IsPicture).ToList();
		var open = sentences.Count(s => !s.IsChecked);
		IsChecked = HasSentences && open == 0;
		CanApproveTranslation = HasSentences && open > 0;
		CheckStatus = !HasSentences ? string.Empty
			: open == 0 ? L.T("✓ Every sentence is checked.")
			: L.F("Still to check: {0} of {1} sentences.", open, sentences.Count);
	}

	/// <summary>The overview at the top: per language how many sentences still need checking, and what came into the original.</summary>
	private void ShowOverview()
	{
		var parts = new List<string>();
		AddedToOriginal.Clear();
		foreach (var language in TranslationLanguages)
		{
			if (!_translations.TryGetValue(language.Code, out var translation)) continue;
			var open = translation.Segments.Count(s => !s.IsPicture && !s.IsChecked);
			if (open > 0) parts.Add($"{language.Name} {open}");
			foreach (var added in translation.Segments.Where(s => s.Origin == "added"))
				AddedToOriginal.Add(new AddedToOriginal(language.Code, language.Name, added.Src, added.Text));
		}
		HasAddedToOriginal = AddedToOriginal.Count > 0;
		ToCheckOverview = parts.Count == 0 ? L.T("Every translation is checked.") : L.F("Still to check: {0}", string.Join(" · ", parts));
	}

	/// <summary>
	/// Fills in the texts of the pictures made with Create image - the translation, or the original when
	/// there's none yet. Loading a drawing can take a moment; when another language shows by then, it stops.
	/// </summary>
	private async Task ShowPictureTextsAsync(Dictionary<string, List<string>> translated)
	{
		var language = TranslationLanguage;
		foreach (var row in SentenceRows.Where(r => r.IsPicture).ToList())
		{
			var texts = await PictureTextsAsync(row.ImageRef, translated);
			if (language != TranslationLanguage) return;
			row.ShowTexts(texts);
		}

		var cover = await PictureTextsAsync(CoverImageUrl, translated);
		if (language != TranslationLanguage) return;
		PictureTexts.Show(CoverTexts, cover, reference => CoverPreviewRef = reference, () => HasCoverTexts = CoverTexts.Count > 0);
	}

	/// <summary>The texts of one picture to show: none for photos and pictures without text.</summary>
	private async Task<List<PictureText>> PictureTextsAsync(string? reference, Dictionary<string, List<string>> translated)
	{
		if (reference is null || !reference.StartsWith(FirestoreImageStore.Scheme)) return [];
		var id = reference[FirestoreImageStore.Scheme.Length..];
		var board = new DrawingBoard();
		if (await _images.GetDrawingAsync(reference) is not { } drawing || !board.Load(drawing)) return [];

		var originals = board.Texts();
		var shown = translated.TryGetValue(id, out var texts) && texts.Count == originals.Count ? texts : originals;
		return originals.Select((original, index) =>
			new PictureText { PictureId = id, Original = original, Shown = shown[index], Text = shown[index] }).ToList();
	}

	/// <summary>Every picture text on the page (rules pictures and the cover).</summary>
	private IEnumerable<PictureText> AllPictureTexts() => SentenceRows.SelectMany(r => r.Texts).Concat(CoverTexts);

	/// <summary>
	/// The translations with what's done on the Translations page: changed, added or removed sentences, a
	/// corrected name or picture texts. Changes nothing itself.
	/// </summary>
	private Dictionary<string, GameTranslation> EffectiveTranslations()
	{
		var result = new Dictionary<string, GameTranslation>(_translations);
		// The original's own language has no translation (one may be left from when Written in was wrong).
		if (SourceLanguage is { } source)
			result.Remove(source.Code);
		if (TranslationLanguage is not { } language || !result.TryGetValue(language.Code, out var before)) return result;

		var name = TranslatedName.Trim();
		var pictureTexts = AllPictureTexts().ToList();
		if (name == _shownName.Trim() && _segments.SequenceEqual(_shownSegments) && !pictureTexts.Any(t => t.IsChanged))
			return result;

		var imageTexts = new Dictionary<string, List<string>>(before.ImageTexts);
		foreach (var picture in pictureTexts.GroupBy(t => t.PictureId))
			imageTexts[picture.Key] = picture.Select(t => t.Text.Trim()).ToList();

		var allChecked = _segments.Where(s => !s.IsPicture).All(s => s.IsChecked);
		result[language.Code] = before with
		{
			Name = name,
			RulesText = Sentences.Join(_segments),
			ImageTexts = imageTexts,
			Segments = _segments.ToList(),
			Manual = _segments.Any(s => s.Status is TranslationSegment.Edited or TranslationSegment.Added),
			CheckedHash = allChecked ? OriginalHash() : string.Empty,
			CheckedBy = allChecked ? MyCheckerName : string.Empty
		};
		return result;
	}

	/// <summary>The language shown goes back to the automatic translation, made again after saving.</summary>
	[RelayCommand]
	private void UseAutomaticTranslation()
	{
		if (TranslationLanguage is not { } language) return;
		_translations.Remove(language.Code);
		ShowTranslation();
	}

	/// <summary>Fills the languages part of the form from a game or a proposal.</summary>
	private void ShowLanguages(string sourceLanguage, Dictionary<string, GameTranslation> translations)
	{
		_translations = translations;
		SourceLanguage = AppLanguage.Find(string.IsNullOrEmpty(sourceLanguage) ? "en" : sourceLanguage) ?? AppLanguage.All[0];
		TranslationLanguage = PreferredTranslationLanguage();
		ShowTranslation();
	}

	/// <summary>
	/// The translation the Languages page opens on: the app's language, so everyone corrects the game in their
	/// own language; the first other language when the game is written in it.
	/// </summary>
	private AppLanguageInfo? PreferredTranslationLanguage() =>
		TranslationLanguages.FirstOrDefault(l => l.Code == AppLanguage.Current.Code) ?? TranslationLanguages.FirstOrDefault();

	/// <summary>The translations as one text, the same whatever order they're in: part of the form's snapshot.</summary>
	private string TranslationsSnapshot() =>
		GameTranslation.ToJson(new SortedDictionary<string, GameTranslation>(EffectiveTranslations()).ToDictionary(t => t.Key, t => t.Value));

	/// <summary>Whether two games have the same original language and translations.</summary>
	private static bool SameTranslations(PubGame a, PubGame b) =>
		a.SourceLanguageCode == b.SourceLanguageCode
		&& GameTranslation.ToJson(new SortedDictionary<string, GameTranslation>(a.Translations).ToDictionary(t => t.Key, t => t.Value))
			== GameTranslation.ToJson(new SortedDictionary<string, GameTranslation>(b.Translations).ToDictionary(t => t.Key, t => t.Value));
}

/// <summary>What tapping does on the Translations page.</summary>
public enum TranslationMode
{
	/// <summary>Just reading and checking.</summary>
	None,

	/// <summary>Tapping a sentence adds a new one after it.</summary>
	Adding,

	/// <summary>Tapping words selects what to change.</summary>
	Changing,

	/// <summary>Tapping words selects what to remove.</summary>
	Removing
}

/// <summary>
/// A sentence that came into the original from a translation (added there by a person and translated back by
/// the cloud): someone who speaks the original's language approves it.
/// </summary>
/// <param name="LanguageCode">The translation it was added in.</param>
/// <param name="LanguageName">That language's name.</param>
/// <param name="Sentence">The sentence as it is now in the original.</param>
/// <param name="AddedAs">The sentence as it was added in that language.</param>
public sealed record AddedToOriginal(string LanguageCode, string LanguageName, string Sentence, string AddedAs);
