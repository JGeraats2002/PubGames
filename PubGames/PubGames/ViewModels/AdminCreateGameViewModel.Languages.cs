using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// One piece of a translated game's rules on the Languages page: text to correct, or a picture (shown, not
/// changed: pictures are the same in every language).
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

	/// <summary>The pieces of a rules text, in order.</summary>
	public static ObservableCollection<TranslationBlock> FromRulesText(string rules) =>
		new(RulesBlock.Parse(rules).Select(b => b.IsImage ? new TranslationBlock { ImageRef = b.ImageRef } : new TranslationBlock { Text = b.Text! }));

	/// <summary>Back to a rules text, the same way the rules editor saves it.</summary>
	public static string ToRulesText(IEnumerable<TranslationBlock> blocks) =>
		string.Join("\n\n", blocks
			.Select(b => b.IsImage ? RulesBlock.RulesImageTag(b.ImageRef!) : b.Text.Trim())
			.Where(s => s.Length > 0));
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
			TranslationLanguage = TranslationLanguages.FirstOrDefault();
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
		_translations = EffectiveTranslations();
	}

	/// <summary>Shows the chosen language's translation.</summary>
	partial void OnTranslationLanguageChanged(AppLanguageInfo? value) => ShowTranslation();

	/// <summary>The translated name, as shown and corrected.</summary>
	[ObservableProperty]
	private string translatedName = string.Empty;

	/// <summary>The translated rules in pieces, as shown and corrected.</summary>
	public ObservableCollection<TranslationBlock> TranslatedBlocks { get; private set; } = [];

	/// <summary>What the translation shown is: automatic, corrected, out of date, or still to be made.</summary>
	[ObservableProperty]
	private string translationStatus = string.Empty;

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
			var outOfDate = translation.SourceHash != GameTranslation.HashOf(sourceName, sourceRules);
			TranslationStatus = (translation.Manual, outOfDate) switch
			{
				(true, true) => L.T("Corrected by hand, but the original has changed since. Check it, or use the automatic translation again."),
				(true, false) => L.T("Corrected by hand. It won't be translated automatically any more."),
				(false, true) => L.T("Translated automatically. The original has changed: it's translated again after saving."),
				_ => L.T("Translated automatically. Correct it here if something isn't right.")
			};
			CanUseAutomaticTranslation = translation.Manual;
		}
		else
		{
			// Nothing yet: the original, to translate by hand if wanted.
			_shownTranslation = (sourceName, sourceRules);
			TranslationStatus = L.T("Not translated yet: this happens automatically a minute after saving. You can also translate it here yourself.");
			CanUseAutomaticTranslation = false;
		}

		TranslatedName = _shownTranslation.Name;
		TranslatedBlocks = TranslationBlock.FromRulesText(_shownTranslation.Rules);
		OnPropertyChanged(nameof(TranslatedBlocks));
	}

	/// <summary>
	/// The translations with what's typed on the Languages page: a changed name or rules count as corrected by
	/// hand (for this version of the original). Changes nothing itself.
	/// </summary>
	private Dictionary<string, GameTranslation> EffectiveTranslations()
	{
		var result = new Dictionary<string, GameTranslation>(_translations);
		if (TranslationLanguage is not { } language) return result;

		var name = TranslatedName.Trim();
		var rules = TranslationBlock.ToRulesText(TranslatedBlocks);
		if (name == _shownTranslation.Name.Trim() && rules == TranslationBlock.ToRulesText(TranslationBlock.FromRulesText(_shownTranslation.Rules)))
			return result;

		result[language.Code] = new GameTranslation
		{
			Name = name,
			RulesText = rules,
			Manual = true,
			SourceHash = GameTranslation.HashOf(Name.Trim(), EditableRulesBlock.ToRulesText(RulesBlocks))
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
		TranslationLanguage ??= TranslationLanguages.FirstOrDefault();
		ShowTranslation();
	}

	/// <summary>The translations as one text, the same whatever order they're in: part of the form's snapshot.</summary>
	private string TranslationsSnapshot() =>
		GameTranslation.ToJson(new SortedDictionary<string, GameTranslation>(EffectiveTranslations()).ToDictionary(t => t.Key, t => t.Value));

	/// <summary>Whether two games have the same original language and translations.</summary>
	private static bool SameTranslations(PubGame a, PubGame b) =>
		a.SourceLanguageCode == b.SourceLanguageCode
		&& GameTranslation.ToJson(new SortedDictionary<string, GameTranslation>(a.Translations).ToDictionary(t => t.Key, t => t.Value))
			== GameTranslation.ToJson(new SortedDictionary<string, GameTranslation>(b.Translations).ToDictionary(t => t.Key, t => t.Value));
}
