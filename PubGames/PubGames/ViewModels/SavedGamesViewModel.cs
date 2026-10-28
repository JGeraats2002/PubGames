using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>A saved game in the list, with a tick while choosing games to delete.</summary>
public partial class SavedGameRow : ObservableObject
{
	/// <summary>The saved game.</summary>
	public required SavedGame Game { get; init; }

	/// <summary>Whether it's selected to delete.</summary>
	[ObservableProperty]
	private bool isSelected;
}

/// <summary>
/// The saved games on this phone, on two pages: unfinished games (tap one to
/// continue it) and finished games (tap one to see how it ended). Each page
/// has its own time for how long games are kept. Select chooses games to
/// delete on the open page, with Select all / Deselect all.
/// </summary>
public partial class SavedGamesViewModel : ObservableObject
{
	private readonly SavedGames _savedGames;

	/// <summary>Every saved game, finished or not; Rows shows the ones of the open page.</summary>
	private List<SavedGameRow> _all = new();

	/// <summary>
	/// Gets the services it works with - saved games - from the app (dependency injection).
	/// </summary>
	public SavedGamesViewModel(SavedGames savedGames)
	{
		_savedGames = savedGames;
	}

	/// <summary>The games on the open page.</summary>
	public ObservableCollection<SavedGameRow> Rows { get; } = new();

	/// <summary>False: unfinished games. True: finished games.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsUnfinished), nameof(SelectedRetention), nameof(RetentionTitle), nameof(EmptyText))]
	private bool showsFinished;

	/// <summary>The Unfinished page is open.</summary>
	public bool ShowsUnfinished => !ShowsFinished;

	partial void OnShowsFinishedChanged(bool value)
	{
		IsSelecting = false;
		SelectAll(false);
		ShowRows();
	}

	/// <summary>"Unfinished (3)".</summary>
	public string UnfinishedTabText => L.F("Unfinished ({0})", _all.Count(r => !r.Game.IsFinished));
	/// <summary>"Finished (5)".</summary>
	public string FinishedTabText => L.F("Finished ({0})", _all.Count(r => r.Game.IsFinished));

	/// <summary>Choosing games to delete: rows show a tick box and tapping one ticks it.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SelectButtonText), nameof(IsBrowsing))]
	private bool isSelecting;

	/// <summary>Not choosing games to delete: tapping a game opens it.</summary>
	public bool IsBrowsing => !IsSelecting;

	/// <summary>"Select", or "Cancel" while choosing.</summary>
	public string SelectButtonText => IsSelecting ? L.T("Cancel") : L.T("Select");

	/// <summary>How many games are selected.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(DeleteButtonText), nameof(HasSelection))]
	private int selectedCount;

	/// <summary>Whether any game is selected: Delete can be tapped.</summary>
	public bool HasSelection => SelectedCount > 0;

	/// <summary>"Delete 1 game" or "Delete 3 games".</summary>
	public string DeleteButtonText => SelectedCount == 1 ? L.T("Delete 1 game") : L.F("Delete {0} games", SelectedCount);

	/// <summary>The games are loaded (so an empty list really is empty).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsEmpty))]
	private bool isLoaded;

	/// <summary>Loaded and no games on this page: the empty text shows.</summary>
	public bool IsEmpty => IsLoaded && Rows.Count == 0;

	/// <summary>What the empty page says.</summary>
	public string EmptyText => ShowsFinished
		? L.T("No finished games saved.")
		: L.T("No unfinished games. A game is saved once someone has a score.");

	/// <summary>Every choice of how long games are kept, for the picker.</summary>
	public List<SavedGamesRetentionOption> RetentionOptions { get; } = SavedGamesRetentionOption.All;

	/// <summary>"Keep unfinished games" or "Keep finished games".</summary>
	public string RetentionTitle => ShowsFinished ? L.T("Keep finished games") : L.T("Keep unfinished games");

	private SavedGamesRetention Retention
	{
		get => ShowsFinished ? _savedGames.FinishedRetention : _savedGames.Retention;
		set
		{
			if (ShowsFinished)
				_savedGames.FinishedRetention = value;
			else
				_savedGames.Retention = value;
		}
	}

	/// <summary>The time for the open page's games.</summary>
	public SavedGamesRetentionOption? SelectedRetention
	{
		get => RetentionOptions.FirstOrDefault(o => o.Retention == Retention);
		set
		{
			if (value is null || value.Retention == Retention) return;
			Retention = value.Retention;
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Shows the saved games of the page that's open (unfinished or finished), newest first.
	/// </summary>
	public async Task LoadAsync()
	{
		var games = await _savedGames.GetAsync();
		_all = games.Select(game =>
		{
			var row = new SavedGameRow { Game = game };
			row.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(SavedGameRow.IsSelected))
					SelectedCount = Rows.Count(r => r.IsSelected);
			};
			return row;
		}).ToList();
		OnPropertyChanged(nameof(UnfinishedTabText));
		OnPropertyChanged(nameof(FinishedTabText));
		ShowRows();
		IsLoaded = true;
	}

	/// <summary>Fills Rows with the open page's games, newest first.</summary>
	private void ShowRows()
	{
		Rows.Clear();
		foreach (var row in _all.Where(r => r.Game.IsFinished == ShowsFinished))
			Rows.Add(row);
		SelectedCount = 0;
		OnPropertyChanged(nameof(IsEmpty));
	}

	/// <summary>Starts or stops choosing games to delete.</summary>
	public void ToggleSelecting()
	{
		IsSelecting = !IsSelecting;
		if (!IsSelecting)
			SelectAll(false);
	}

	/// <summary>Selects every game on the page, or none.</summary>
	public void SelectAll(bool selected)
	{
		foreach (var row in Rows)
			row.IsSelected = selected;
	}

	/// <summary>Deletes the selected games from the phone.</summary>
	public async Task DeleteSelectedAsync()
	{
		var selected = Rows.Where(r => r.IsSelected).ToList();
		await _savedGames.DeleteAsync(selected.Select(r => r.Game.Session.Id));
		foreach (var row in selected)
		{
			Rows.Remove(row);
			_all.Remove(row);
		}
		SelectedCount = 0;
		IsSelecting = false;
		OnPropertyChanged(nameof(IsEmpty));
		OnPropertyChanged(nameof(UnfinishedTabText));
		OnPropertyChanged(nameof(FinishedTabText));
	}
}
