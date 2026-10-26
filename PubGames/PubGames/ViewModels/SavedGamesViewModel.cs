using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>A saved game in the list, with a tick while choosing games to delete.</summary>
public partial class SavedGameRow : ObservableObject
{
	public required SavedGame Game { get; init; }

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

	public SavedGamesViewModel(SavedGames savedGames)
	{
		_savedGames = savedGames;
	}

	/// <summary>The games on the open page.</summary>
	public ObservableCollection<SavedGameRow> Rows { get; } = new();

	/// <summary>False: unfinished games. True: finished games.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsUnfinished), nameof(SelectedRetention), nameof(RetentionInfo), nameof(RetentionTitle), nameof(EmptyText))]
	private bool showsFinished;

	public bool ShowsUnfinished => !ShowsFinished;

	partial void OnShowsFinishedChanged(bool value)
	{
		IsSelecting = false;
		SelectAll(false);
		ShowRows();
	}

	public string UnfinishedTabText => $"Unfinished ({_all.Count(r => !r.Game.IsFinished)})";
	public string FinishedTabText => $"Finished ({_all.Count(r => r.Game.IsFinished)})";

	/// <summary>Choosing games to delete: rows show a tick box and tapping one ticks it.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SelectButtonText), nameof(IsBrowsing))]
	private bool isSelecting;

	public bool IsBrowsing => !IsSelecting;

	public string SelectButtonText => IsSelecting ? "Cancel" : "Select";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(DeleteButtonText), nameof(HasSelection))]
	private int selectedCount;

	public bool HasSelection => SelectedCount > 0;

	public string DeleteButtonText => SelectedCount == 1 ? "Delete 1 game" : $"Delete {SelectedCount} games";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsEmpty))]
	private bool isLoaded;

	public bool IsEmpty => IsLoaded && Rows.Count == 0;

	public string EmptyText => ShowsFinished
		? "No finished games saved."
		: "No unfinished games. A game is saved once someone has a score.";

	public List<SavedGamesRetentionOption> RetentionOptions { get; } = SavedGamesRetentionOption.All;

	public string RetentionTitle => ShowsFinished ? "Keep finished games" : "Keep unfinished games";

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
			OnPropertyChanged(nameof(RetentionInfo));
		}
	}

	/// <summary>What the chosen time means.</summary>
	public string RetentionInfo => (Retention, ShowsFinished) switch
	{
		(SavedGamesRetention.DontSave, true) => "Finished games aren't kept: once a game is finished and you leave it, it's gone. Its result still counts in the stats.",
		(SavedGamesRetention.DontSave, false) => "Unfinished games aren't kept: once you leave a game it's gone.",
		(SavedGamesRetention.Forever, _) => "Games are kept until you delete them.",
		_ => $"A game is deleted {SelectedRetention?.Label} after it was last played. Only games with at least one score are kept, on this phone only."
	};

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

	public void ToggleSelecting()
	{
		IsSelecting = !IsSelecting;
		if (!IsSelecting)
			SelectAll(false);
	}

	public void SelectAll(bool selected)
	{
		foreach (var row in Rows)
			row.IsSelected = selected;
	}

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
