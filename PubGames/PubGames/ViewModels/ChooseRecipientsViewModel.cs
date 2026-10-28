using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>Someone on the list of people a message can go to, ticked or not.</summary>
public partial class RecipientChoice : ObservableObject
{
	/// <summary>The person.</summary>
	public required MessageRecipient Recipient { get; init; }

	/// <summary>Gets the message.</summary>
	[ObservableProperty]
	private bool isSelected;
}

/// <summary>
/// Choosing who a message goes to: search by name or email, show everyone, only admins or only moderators
/// (like the team page), and tick as many people as you like. Everyone, All admins and All moderators tick
/// (or untick) a whole group at once.
/// </summary>
public partial class ChooseRecipientsViewModel : ObservableObject
{
	/// <summary>Everyone who can get the message, alphabetical.</summary>
	private readonly List<RecipientChoice> _all;

	/// <summary>The admins (and the owner) that match the search and the filter.</summary>
	public ObservableCollection<RecipientChoice> Admins { get; } = [];

	/// <summary>The moderators that match the search and the filter.</summary>
	public ObservableCollection<RecipientChoice> Moderators { get; } = [];

	/// <summary>What's typed in the search bar: part of a name or email.</summary>
	[ObservableProperty]
	private string searchText = string.Empty;

	partial void OnSearchTextChanged(string value) => ApplyFilter();

	/// <summary>Which roles the lists show (the All, Admins and Moderators buttons).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowingAll), nameof(ShowingAdmins), nameof(ShowingModerators))]
	private TeamFilter filter = TeamFilter.All;

	partial void OnFilterChanged(TeamFilter value) => ApplyFilter();

	/// <summary>The All button is selected.</summary>
	public bool ShowingAll => Filter == TeamFilter.All;

	/// <summary>The Admins button is selected.</summary>
	public bool ShowingAdmins => Filter == TeamFilter.Admins;

	/// <summary>The Moderators button is selected.</summary>
	public bool ShowingModerators => Filter == TeamFilter.Moderators;

	/// <summary>The admins list has someone to show.</summary>
	public bool HasAdmins => Admins.Count > 0;

	/// <summary>The moderators list has someone to show.</summary>
	public bool HasModerators => Moderators.Count > 0;

	/// <summary>The admins list's heading, with how many it shows.</summary>
	public string AdminsHeading => L.F("Admins ({0})", Admins.Count);

	/// <summary>The moderators list's heading, with how many it shows.</summary>
	public string ModeratorsHeading => L.F("Moderators ({0})", Moderators.Count);

	/// <summary>Nobody matches the search.</summary>
	public bool HasNoMatches => !HasAdmins && !HasModerators;

	/// <summary>Everyone on the list is ticked.</summary>
	public bool EveryoneSelected => _all.Count > 0 && _all.All(c => c.IsSelected);

	/// <summary>Every admin is ticked.</summary>
	public bool AllAdminsSelected => Group(admins: true) is { Count: > 0 } group && group.All(c => c.IsSelected);

	/// <summary>Every moderator is ticked.</summary>
	public bool AllModeratorsSelected => Group(admins: false) is { Count: > 0 } group && group.All(c => c.IsSelected);

	/// <summary>The team has moderators (otherwise All moderators isn't offered).</summary>
	public bool HasAnyModerators => Group(admins: false).Count > 0;

	/// <summary>How many people are ticked.</summary>
	public int SelectedCount => _all.Count(c => c.IsSelected);

	/// <summary>The button at the bottom, with how many people get the message.</summary>
	public string WriteButtonText => SelectedCount == 1 ? L.T("Write message (1 person)") : L.F("Write message ({0} people)", SelectedCount);

	/// <summary>Someone is ticked, so the message can be written.</summary>
	public bool CanWrite => SelectedCount > 0;

	/// <summary>The people who get the message.</summary>
	public List<MessageRecipient> Selected => _all.Where(c => c.IsSelected).Select(c => c.Recipient).ToList();

	/// <summary>Shows everyone who can get a message, with nobody ticked yet.</summary>
	public ChooseRecipientsViewModel(IEnumerable<MessageRecipient> recipients)
	{
		_all = recipients
			.OrderBy(r => r.Label, StringComparer.CurrentCultureIgnoreCase)
			.Select(r => new RecipientChoice { Recipient = r })
			.ToList();
		foreach (var choice in _all)
			choice.PropertyChanged += (_, _) => SelectionChanged();
		ApplyFilter();
	}

	/// <summary>The admins, or the moderators, of everyone on the list.</summary>
	private List<RecipientChoice> Group(bool admins) => _all.Where(c => c.Recipient.IsAdmin == admins).ToList();

	/// <summary>Shows only the given roles (the All, Admins and Moderators buttons).</summary>
	[RelayCommand]
	private void SetFilter(TeamFilter value) => Filter = value;

	/// <summary>Everyone: ticks everyone, or unticks everyone when they all are.</summary>
	[RelayCommand]
	private void ToggleEveryone() => Tick(_all, !EveryoneSelected);

	/// <summary>All admins: ticks every admin, or unticks them when they all are.</summary>
	[RelayCommand]
	private void ToggleAdmins() => Tick(Group(admins: true), !AllAdminsSelected);

	/// <summary>All moderators: ticks every moderator, or unticks them when they all are.</summary>
	[RelayCommand]
	private void ToggleModerators() => Tick(Group(admins: false), !AllModeratorsSelected);

	/// <summary>Tapping a person ticks or unticks them.</summary>
	[RelayCommand]
	private void Toggle(RecipientChoice choice) => choice.IsSelected = !choice.IsSelected;

	/// <summary>Ticks or unticks a group of people.</summary>
	private static void Tick(IEnumerable<RecipientChoice> choices, bool selected)
	{
		foreach (var choice in choices)
			choice.IsSelected = selected;
	}

	/// <summary>Someone was ticked or unticked: the group buttons and the count follow.</summary>
	private void SelectionChanged()
	{
		OnPropertyChanged(nameof(EveryoneSelected));
		OnPropertyChanged(nameof(AllAdminsSelected));
		OnPropertyChanged(nameof(AllModeratorsSelected));
		OnPropertyChanged(nameof(SelectedCount));
		OnPropertyChanged(nameof(WriteButtonText));
		OnPropertyChanged(nameof(CanWrite));
	}

	/// <summary>
	/// Fills the two lists with the people that match the search (name or email) and the filter. Who's ticked
	/// stays ticked, also when they're out of view.
	/// </summary>
	private void ApplyFilter()
	{
		var search = SearchText.Trim();
		Admins.Clear();
		Moderators.Clear();
		foreach (var choice in _all.Where(c => search.Length == 0
			|| c.Recipient.Label.Contains(search, StringComparison.CurrentCultureIgnoreCase)
			|| c.Recipient.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
			|| (c.Recipient.HasLanguage && c.Recipient.LanguageName.Contains(search, StringComparison.CurrentCultureIgnoreCase))))
		{
			if (choice.Recipient.IsAdmin)
			{
				if (Filter != TeamFilter.Moderators) Admins.Add(choice);
			}
			else if (Filter != TeamFilter.Admins)
			{
				Moderators.Add(choice);
			}
		}

		OnPropertyChanged(nameof(HasAdmins));
		OnPropertyChanged(nameof(HasModerators));
		OnPropertyChanged(nameof(AdminsHeading));
		OnPropertyChanged(nameof(ModeratorsHeading));
		OnPropertyChanged(nameof(HasNoMatches));
	}
}
