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

/// <summary>
/// One piece of a translated game's rules on the Languages page: text to correct, or a picture. A picture made
/// with Create image shows with its texts in this language, drawn again as they're corrected.
/// </summary>
public partial class TranslationBlock : ObservableObject
{
	/// <summary>The translated text of this piece.</summary>
	[ObservableProperty]
	private string text = string.Empty;

	/// <summary>The picture; null for a piece of text.</summary>
	public string? ImageRef { get; init; }

	/// <summary>A piece of text.</summary>
	public bool IsText => ImageRef is null;

	/// <summary>A picture.</summary>
	public bool IsImage => ImageRef is not null;

	/// <summary>The texts in this picture, when it was made with Create image and has any.</summary>
	public ObservableCollection<PictureText> Texts { get; } = [];

	/// <summary>The picture has texts to translate.</summary>
	[ObservableProperty]
	private bool hasTexts;

	/// <summary>The picture as shown: with the texts as they are on the page (see TranslatedPictures.Preview).</summary>
	[ObservableProperty]
	private string? previewRef;

	/// <summary>Shows the picture's texts to correct, and the picture with them.</summary>
	public void ShowTexts(IEnumerable<PictureText> texts) => PictureTexts.Show(Texts, texts, reference => PreviewRef = reference, () => HasTexts = Texts.Count > 0);

	/// <summary>The pieces of a rules text, in order.</summary>
	public static ObservableCollection<TranslationBlock> FromRulesText(string rules) =>
		new(RulesBlock.Parse(rules).Select(b => b.IsImage
			? new TranslationBlock { ImageRef = b.ImageRef, PreviewRef = b.ImageRef }
			: new TranslationBlock { Text = b.Text! }));

	/// <summary>Back to a rules text, the same way the rules editor saves it.</summary>
	public static string ToRulesText(IEnumerable<TranslationBlock> blocks) =>
		string.Join("\n\n", blocks
			.Select(b => b.IsImage ? RulesBlock.RulesImageTag(b.ImageRef!) : b.Text.Trim())
			.Where(s => s.Length > 0));
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
	[NotifyPropertyChangedFor(nameof(TranslationLanguages))]
	private AppLanguageInfo? sourceLanguage = AppLanguage.Current;

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

	/// <summary>A correction to the language that was shown is kept before the next one shows.</summary>
	partial void OnTranslationLanguageChanging(AppLanguageInfo? oldValue, AppLanguageInfo? newValue)
	{
		if (!_changingOriginal)
			_translations = EffectiveTranslations();
	}

	/// <summary>Make this the original is busy: the translations are already put in order.</summary>
	private bool _changingOriginal;

	/// <summary>The language shown has a translation, so it can become the original (see MakeOriginalAsync).</summary>
	[ObservableProperty]
	private bool canMakeOriginal;

	/// <summary>
	/// Make this the original: the translation shown (with the corrections on the page) becomes the game's
	/// name and rules, written in that language, so anyone can change the game in their own language. Pictures
	/// with text are drawn again with the texts in that language. The old original is kept as the translation
	/// into its language, as corrected by hand, so nothing written by a person is lost; the automatic
	/// translations are made again from the new original after saving. Returns false when it couldn't (a
	/// picture couldn't be uploaded; StatusMessage says why).
	/// </summary>
	public async Task<bool> MakeOriginalAsync()
	{
		if (TranslationLanguage is not { } language || SourceLanguage is not { } oldLanguage) return false;
		var translations = EffectiveTranslations();
		if (!translations.TryGetValue(language.Code, out var chosen)) return false;

		var oldName = Name.Trim();
		var oldRules = EditableRulesBlock.ToRulesText(RulesBlocks);
		var oldCover = CoverImageUrl;

		// Pictures with text get a new version with the texts in the new language.
		var replaced = new Dictionary<string, string>();
		var oldTexts = new Dictionary<string, List<string>>();
		foreach (var reference in PictureReferences($"{oldRules} {oldCover}"))
		{
			var id = reference[FirestoreImageStore.Scheme.Length..];
			if (!chosen.ImageTexts.TryGetValue(id, out var texts)
				|| await _images.GetDrawingAsync(reference) is not { } drawing) continue;
			var board = new DrawingBoard();
			if (!board.Load(drawing)) continue;
			var originals = board.Texts();
			if (texts.Count != originals.Count || texts.SequenceEqual(originals)) continue;

			var next = 0;
			foreach (var item in board.Items.Where(i => i.Kind == DrawKind.Text))
			{
				if (!string.IsNullOrWhiteSpace(texts[next])) item.Text = texts[next];
				next++;
			}
			var png = await MainThread.InvokeOnMainThreadAsync(board.ToPng);
			if (await UploadPictureAsync(new EditedImage(png, board.ToJson())) is not { } uploaded) return false;
			replaced[reference] = uploaded;
			oldTexts[uploaded[FirestoreImageStore.Scheme.Length..]] = originals;
		}

		string Swap(string text) => replaced.Aggregate(text, (current, pair) => current.Replace(pair.Key, pair.Value));
		Dictionary<string, List<string>> SwapIds(Dictionary<string, List<string>> texts) =>
			texts.ToDictionary(t => replaced.TryGetValue(FirestoreImageStore.Scheme + t.Key, out var to) ? to[FirestoreImageStore.Scheme.Length..] : t.Key, t => t.Value);

		var others = translations
			.Where(t => t.Key != language.Code)
			.ToDictionary(t => t.Key, t => t.Value with { RulesText = Swap(t.Value.RulesText), ImageTexts = SwapIds(t.Value.ImageTexts) });

		Name = chosen.Name;
		ShowRules(Swap(string.IsNullOrWhiteSpace(chosen.RulesText) ? oldRules : chosen.RulesText));
		CoverImageUrl = oldCover is null ? null : Swap(oldCover);
		others[oldLanguage.Code] = new GameTranslation
		{
			Name = oldName,
			RulesText = Swap(oldRules),
			ImageTexts = oldTexts,
			Manual = true,
			SourceHash = GameTranslation.HashOf(Name.Trim(), EditableRulesBlock.ToRulesText(RulesBlocks), CoverImageUrl)
		};

		_changingOriginal = true;
		try
		{
			_translations = others;
			SourceLanguage = language;
			TranslationLanguage = TranslationLanguages.FirstOrDefault(l => l.Code == oldLanguage.Code);
		}
		finally
		{
			_changingOriginal = false;
		}
		ShowTranslation();
		StatusMessage = L.F("{0} is now the original. Save to keep it; the other languages are translated again from it.", language.Name);
		return true;
	}

	/// <summary>The pictures made in the app (fsimg:...) in a text, each once.</summary>
	private static List<string> PictureReferences(string text) =>
		System.Text.RegularExpressions.Regex.Matches(text, @"fsimg:[A-Za-z0-9_]+").Select(m => m.Value).Distinct().ToList();

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

	/// <summary>The cover as shown on the Languages page: with the texts as they are there.</summary>
	[ObservableProperty]
	private string? coverPreviewRef;

	/// <summary>The translated rules in pieces, as shown and corrected.</summary>
	public ObservableCollection<TranslationBlock> TranslatedBlocks { get; private set; } = [];

	/// <summary>What the translation shown is: automatic, corrected, out of date, or still to be made.</summary>
	[ObservableProperty]
	private string translationStatus = string.Empty;

	/// <summary>Whether a person has checked the translation shown: not yet, done, or to look at again.</summary>
	[ObservableProperty]
	private string checkStatus = string.Empty;

	/// <summary>The translation shown is checked against the original as it is now.</summary>
	[ObservableProperty]
	private bool isChecked;

	/// <summary>The translation shown can be marked as checked: it exists and isn't checked for this original yet.</summary>
	[ObservableProperty]
	private bool canApproveTranslation;

	/// <summary>Who is signed in, as a checker is named: their name, or their email.</summary>
	private string MyCheckerName => _auth.CurrentUser is { } user
		? (string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName)
		: string.Empty;

	/// <summary>The fingerprint of the original as it is on the form now (see GameTranslation.HashOf).</summary>
	private string OriginalHash() => GameTranslation.HashOf(Name.Trim(), EditableRulesBlock.ToRulesText(RulesBlocks), CoverImageUrl);

	/// <summary>
	/// Translation is fine: the translation shown (with any corrections on the page) is marked as checked by
	/// the signed-in person, against the original as it is now. Saving keeps it; the cloud then takes the
	/// "translation to check" messages for this language away.
	/// </summary>
	[RelayCommand]
	private void ApproveTranslation()
	{
		if (TranslationLanguage is not { } language) return;
		var translations = EffectiveTranslations();
		if (!translations.TryGetValue(language.Code, out var translation)) return;

		translations[language.Code] = translation with { CheckedHash = OriginalHash(), CheckedBy = MyCheckerName };
		_translations = translations;
		ShowTranslation();
		StatusMessage = L.T("Marked as checked. Save to keep it.");
	}

	/// <summary>The translation shown was corrected by hand, so it can go back to the automatic one.</summary>
	[ObservableProperty]
	private bool canUseAutomaticTranslation;

	/// <summary>The name and rules the page showed for this language, to see whether they were corrected.</summary>
	private (string Name, string Rules) _shownTranslation;

	/// <summary>Shows the translation of the chosen language, or the original to translate when there's none yet.</summary>
	private void ShowTranslation()
	{
		var code = TranslationLanguage?.Code;
		var sourceName = Name.Trim();
		var sourceRules = EditableRulesBlock.ToRulesText(RulesBlocks);

		if (code is not null && _translations.TryGetValue(code, out var translation))
		{
			_shownTranslation = (translation.Name, translation.RulesText);
			var outOfDate = translation.SourceHash != GameTranslation.HashOf(sourceName, sourceRules, CoverImageUrl);
			TranslationStatus = (translation.Manual, outOfDate) switch
			{
				(true, true) => L.T("Corrected by hand, but the original has changed since. Check it, or use the automatic translation again."),
				(true, false) => L.T("Corrected by hand. It won't be translated automatically any more."),
				(false, true) => L.T("Translated automatically. The original has changed: it's translated again after saving."),
				_ => L.T("Translated automatically. Correct it here if something isn't right.")
			};
			CanUseAutomaticTranslation = translation.Manual;
			var check = translation.CheckFor(GameTranslation.HashOf(sourceName, sourceRules, CoverImageUrl));
			CheckStatus = check switch
			{
				TranslationCheck.Checked => L.F("✓ Checked by {0}.", translation.CheckedBy),
				TranslationCheck.CheckAgain => L.F("Checked by {0}, but the original has changed since: look at it again.", translation.CheckedBy),
				_ => L.T("Not checked by a person yet.")
			};
			IsChecked = check == TranslationCheck.Checked;
			CanApproveTranslation = !IsChecked;
		}
		else
		{
			// Nothing yet: the original, to translate by hand if wanted.
			_shownTranslation = (sourceName, sourceRules);
			TranslationStatus = L.T("Not translated yet: this happens automatically a minute after saving. You can also translate it here yourself.");
			CanUseAutomaticTranslation = false;
			CheckStatus = string.Empty;
			IsChecked = false;
			CanApproveTranslation = false;
		}

		TranslatedName = _shownTranslation.Name;
		TranslatedBlocks = TranslationBlock.FromRulesText(_shownTranslation.Rules);
		OnPropertyChanged(nameof(TranslatedBlocks));
		CoverTexts.Clear();
		HasCoverTexts = false;
		CoverPreviewRef = CoverImageUrl;
		CanMakeOriginal = code is not null && _translations.ContainsKey(code);
		_ = ShowPictureTextsAsync(TranslatedBlocks, code is not null && _translations.TryGetValue(code, out var shown) ? shown.ImageTexts : []);
	}

	/// <summary>
	/// Fills in the texts of the pictures made with Create image - the translation, or the original when
	/// there's none yet. Loading a drawing can take a moment; when another language shows by then, it stops.
	/// </summary>
	private async Task ShowPictureTextsAsync(ObservableCollection<TranslationBlock> blocks, Dictionary<string, List<string>> translated)
	{
		foreach (var block in blocks.Where(b => b.IsImage))
		{
			var texts = await PictureTextsAsync(block.ImageRef, translated);
			if (blocks != TranslatedBlocks) return;
			block.ShowTexts(texts);
		}

		var cover = await PictureTextsAsync(CoverImageUrl, translated);
		if (blocks != TranslatedBlocks) return;
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
	private IEnumerable<PictureText> AllPictureTexts() => TranslatedBlocks.SelectMany(b => b.Texts).Concat(CoverTexts);

	/// <summary>
	/// The translations with what's typed on the Languages page: a changed name or rules count as corrected by
	/// hand (for this version of the original). Changes nothing itself.
	/// </summary>
	private Dictionary<string, GameTranslation> EffectiveTranslations()
	{
		var result = new Dictionary<string, GameTranslation>(_translations);
		// The original's own language has no translation (one may be left from when Written in was wrong).
		if (SourceLanguage is { } source)
			result.Remove(source.Code);
		if (TranslationLanguage is not { } language) return result;

		var name = TranslatedName.Trim();
		var rules = TranslationBlock.ToRulesText(TranslatedBlocks);
		var pictureTexts = AllPictureTexts().ToList();
		if (name == _shownTranslation.Name.Trim()
			&& rules == TranslationBlock.ToRulesText(TranslationBlock.FromRulesText(_shownTranslation.Rules))
			&& !pictureTexts.Any(t => t.IsChanged))
			return result;

		var imageTexts = new Dictionary<string, List<string>>(
			result.TryGetValue(language.Code, out var before) ? before.ImageTexts : []);
		foreach (var picture in pictureTexts.GroupBy(t => t.PictureId))
			imageTexts[picture.Key] = picture.Select(t => t.Text.Trim()).ToList();

		result[language.Code] = new GameTranslation
		{
			Name = name,
			RulesText = rules,
			ImageTexts = imageTexts,
			Manual = true,
			SourceHash = OriginalHash(),
			// Whoever corrects it has read it against the original: that counts as checked.
			CheckedHash = OriginalHash(),
			CheckedBy = MyCheckerName
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
