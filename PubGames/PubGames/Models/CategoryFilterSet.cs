using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubGames.Models;

/// <summary>
/// The filters for finding games, set on the filter page behind the Filter
/// button next to every search bar: the categories under their main
/// categories, and the number of players. Nothing is required. Within one
/// main category a game has to be in any of the chosen categories; a game has
/// to fit every main category something is chosen in, and the number of
/// players. Used by every page that lists games.
/// </summary>
public partial class CategoryFilterSet : ObservableObject
{
	/// <summary>The highest number of players that can be filtered on.</summary>
	public const int MaxPlayers = 20;

	private readonly HashSet<string> _selected = new();

	/// <summary>The categories to choose from, under their main categories.</summary>
	public ObservableCollection<CategoryChoiceGroup> Groups { get; } = new();

	/// <summary>Raised when a filter changes, so the page can filter its list again.</summary>
	public event EventHandler? Changed;

	/// <summary>How many players there are; 0 for any number. A game fits when this is within its minimum and maximum.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PlayersText))]
	private int players;

	partial void OnPlayersChanged(int value) => OnChanged();

	/// <summary>"Any number of players" or "4 players": the player filter as the slider shows it.</summary>
	public string PlayersText => Players switch
	{
		0 => L.T("Any number of players"),
		1 => L.T("1 player"),
		_ => L.F("{0} players", Players)
	};

	/// <summary>How many filters are set: each chosen category, and the number of players.</summary>
	public int ActiveCount => _selected.Count + (Players > 0 ? 1 : 0);

	/// <summary>Whether any filter is set.</summary>
	public bool IsFiltering => ActiveCount > 0;

	/// <summary>The Filter button's text, which says how many filters are set.</summary>
	public string ButtonText => ActiveCount == 0 ? L.T("Filter") : L.F("Filter ({0})", ActiveCount);

	/// <summary>Shows these categories under their main categories, keeping what was chosen if still there.</summary>
	public void Show(IEnumerable<CategoryGroup> groups, IEnumerable<GameCategory> categories)
	{
		var list = categories.ToList();
		_selected.IntersectWith(list.Select(c => c.Id));
		Groups.Clear();
		foreach (var (group, inGroup) in CategoryGroup.Arrange(groups, list))
		{
			if (inGroup.Count == 0) continue;
			Groups.Add(new CategoryChoiceGroup
			{
				Group = group,
				Choices = inGroup.Select(c => new CategoryChoice { Category = c, IsSelected = _selected.Contains(c.Id) }).ToList()
			});
		}
		OnPropertyChanged(nameof(ButtonText));
		OnPropertyChanged(nameof(IsFiltering));
	}

	/// <summary>Chooses a category, or takes it off again.</summary>
	public void Toggle(CategoryChoice choice)
	{
		var id = choice.Category.Id;
		if (!_selected.Remove(id))
			_selected.Add(id);
		choice.IsSelected = _selected.Contains(id);
		OnChanged();
	}

	/// <summary>Back to no filters at all.</summary>
	public void Clear()
	{
		_selected.Clear();
		foreach (var choice in Groups.SelectMany(g => g.Choices))
			choice.IsSelected = false;
		Players = 0;
		OnChanged();
	}

	/// <summary>Whether this game fits every filter that's set.</summary>
	public bool Matches(PubGame game) => Matches(game.CategoryIds, game.MinPlayers, game.MaxPlayers);

	/// <summary>Whether a game with these categories and this many players fits every filter that's set.</summary>
	public bool Matches(IEnumerable<string> categoryIds, int? minPlayers, int? maxPlayers)
	{
		if (Players > 0 && (Players < minPlayers || Players > maxPlayers)) return false;
		if (_selected.Count == 0) return true;

		var ids = categoryIds.ToHashSet();
		return Groups.All(group =>
		{
			var chosen = group.Choices.Where(c => c.IsSelected).ToList();
			return chosen.Count == 0 || chosen.Any(c => ids.Contains(c.Category.Id));
		});
	}

	/// <summary>A filter changed: the filter button's text updates and the list filters again.</summary>
	private void OnChanged()
	{
		OnPropertyChanged(nameof(ButtonText));
		OnPropertyChanged(nameof(IsFiltering));
		Changed?.Invoke(this, EventArgs.Empty);
	}
}
