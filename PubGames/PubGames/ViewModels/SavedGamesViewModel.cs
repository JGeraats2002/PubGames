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
/// Every saved game on this phone: tap one to continue it (or to see how it
/// ended). Select chooses games to delete, with Select all / Deselect all.
/// At the top: how long games are kept.
/// </summary>
public partial class SavedGamesViewModel : ObservableObject
{
	private readonly SavedGames _savedGames;

	public SavedGamesViewModel(SavedGames savedGames)
	{
		_savedGames = savedGames;
	}

	public ObservableCollection<SavedGameRow> Rows { get; } = new();

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

	public List<SavedGamesRetentionOption> RetentionOptions { get; } = SavedGamesRetentionOption.All;

	public SavedGamesRetentionOption? SelectedRetention
	{
		get => RetentionOptions.FirstOrDefault(o => o.Retention == _savedGames.Retention);
		set
		{
			if (value is null || value.Retention == _savedGames.Retention) return;
			_savedGames.Retention = value.Retention;
			OnPropertyChanged();
			OnPropertyChanged(nameof(RetentionInfo));
		}
	}

	/// <summary>What the chosen time means.</summary>
	public string RetentionInfo => _savedGames.Retention switch
	{
		SavedGamesRetention.DontSave => "Games aren't kept: once you leave a game it's gone (a finished game's result still counts in the stats).",
		SavedGamesRetention.Forever => "Games are kept until you delete them.",
		_ => $"A game is deleted {SelectedRetention?.Label} after it was last played. Only games with at least one score are kept, on this phone only."
	};

	public async Task LoadAsync()
	{
		var games = await _savedGames.GetAsync();
		Rows.Clear();
		foreach (var game in games)
		{
			var row = new SavedGameRow { Game = game };
			row.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(SavedGameRow.IsSelected))
					SelectedCount = Rows.Count(r => r.IsSelected);
			};
			Rows.Add(row);
		}
		SelectedCount = 0;
		IsLoaded = true;
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
			Rows.Remove(row);
		SelectedCount = 0;
		IsSelecting = false;
		OnPropertyChanged(nameof(IsEmpty));
	}
}
