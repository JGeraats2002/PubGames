using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>A language on the language screen: its flag, its name, and whether it's chosen.</summary>
public partial class LanguageChoice : ObservableObject
{
	/// <summary>The language.</summary>
	public required AppLanguageInfo Language { get; init; }

	/// <summary>The language's name, in the language the screen is in now.</summary>
	public string Name => Language.Name;

	/// <summary>The language's flag.</summary>
	public string Flag => Language.Flag;

	/// <summary>Said by the screen reader: the name, and whether it's chosen.</summary>
	public string Description => IsSelected ? L.F("{0}, chosen", Name) : Name;

	/// <summary>This is the language that's chosen.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Description))]
	private bool isSelected;

	/// <summary>The screen's language changed: show the name in it.</summary>
	public void Refresh()
	{
		OnPropertyChanged(nameof(Name));
		OnPropertyChanged(nameof(Description));
	}
}

/// <summary>
/// The language screen: the first time someone signs in, and from Account &gt; Settings &gt; Language. Tapping a
/// flag shows the screen in that language straight away; the button confirms it.
/// </summary>
public partial class LanguageViewModel : ObservableObject
{
	/// <summary>The language the app was in when the screen opened, to go back to when nothing is confirmed.</summary>
	private readonly AppLanguageInfo _original = AppLanguage.Current;

	/// <summary>A language was confirmed with the button.</summary>
	private bool _confirmed;

	/// <summary>Every language, with its flag.</summary>
	public ObservableCollection<LanguageChoice> Choices { get; } =
		new(AppLanguage.All.Select(l => new LanguageChoice { Language = l, IsSelected = l == AppLanguage.Current }));

	/// <summary>The first time after signing in (otherwise opened from Settings).</summary>
	public bool IsFirstTime { get; set; }

	/// <summary>The screen's title.</summary>
	public string Heading => L.T("Choose your language");

	/// <summary>Under the title.</summary>
	public string Explanation => L.T("You can change it later under Account > Settings.");

	/// <summary>The button that confirms the language.</summary>
	public string ConfirmText => IsFirstTime ? L.T("Continue") : L.T("Use this language");

	/// <summary>Screens in the chosen language run right to left (Arabic).</summary>
	public FlowDirection FlowDirection => Selected.FlowDirection;

	/// <summary>The language that's chosen on the screen.</summary>
	private AppLanguageInfo Selected => Choices.First(c => c.IsSelected).Language;

	/// <summary>Raised when the language is confirmed.</summary>
	public event EventHandler? Confirmed;

	/// <summary>Chooses a language: the screen shows in it straight away.</summary>
	[RelayCommand]
	private void Choose(LanguageChoice choice)
	{
		foreach (var other in Choices)
			other.IsSelected = other == choice;
		AppLanguage.Preview(choice.Language);
		foreach (var other in Choices)
			other.Refresh();
		OnPropertyChanged(nameof(Heading));
		OnPropertyChanged(nameof(Explanation));
		OnPropertyChanged(nameof(ConfirmText));
		OnPropertyChanged(nameof(FlowDirection));
	}

	/// <summary>The language the screen opened in was changed (to rebuild the app in it).</summary>
	public bool ChangedLanguage => Selected != _original;

	/// <summary>Keeps the chosen language. The page closes; from Settings the app is then built again in it.</summary>
	[RelayCommand]
	private void Confirm()
	{
		_confirmed = true;
		AppLanguage.Set(Selected, notify: false);
		Confirmed?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Leaving without confirming: the app stays in the language it was in.</summary>
	public void Leave()
	{
		if (!_confirmed)
			AppLanguage.Preview(_original);
	}
}
