using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>A game the user played, with its picture and categories and their stats in it.</summary>
public sealed class StatsGameRow
{
	public required string GameId { get; init; }
	public required string Name { get; init; }
	public string? CoverImageUrl { get; init; }
	public List<string> CategoryIds { get; init; } = new();

	/// <summary>The game's number of players, for the filter; null where there's no limit (or the game is gone).</summary>
	public int? MinPlayers { get; init; }
	public int? MaxPlayers { get; init; }
	public required GameStats Stats { get; init; }

	/// <summary>Every finished game of this kind, newest first.</summary>
	public required List<PlayedGame> Games { get; init; }
}

/// <summary>
/// Account > My stats: every game the user played, found the same way as in
/// the game library (search by name, category clouds). Tapping one opens its
/// stats.
/// </summary>
public partial class MyStatsViewModel : ObservableObject
{
	private readonly IStatsService _stats;
	private readonly ILocalDatabaseService _local;
	private readonly ICategoryService _categories;

	private List<StatsGameRow> _all = new();

	public ObservableCollection<StatsGameRow> VisibleGames { get; } = new();

	/// <summary>Category clouds: "All" plus every category of the games played; several can be selected.</summary>
	public CategoryFilterSet Filters { get; } = new();

	[ObservableProperty]
	private string searchText = string.Empty;

	[ObservableProperty]
	private string message = string.Empty;

	public MyStatsViewModel(IStatsService stats, ILocalDatabaseService local, ICategoryService categories)
	{
		_stats = stats;
		_local = local;
		_categories = categories;
		Filters.Changed += (_, _) => ApplyFilters();
	}

	public async Task LoadAsync()
	{
		var (played, offline) = await _stats.GetMyGamesAsync();
		var games = (await _local.GetManageableGamesAsync()).Concat(await _local.GetGamesAsync())
			.GroupBy(g => g.Id).ToDictionary(g => g.Key, g => g.First());
		var categories = await _categories.GetCachedAsync();

		_all = played
			.GroupBy(p => p.GameId)
			.Select(group =>
			{
				var game = games.GetValueOrDefault(group.Key);
				var name = game?.Name ?? group.First().GameName;
				return new StatsGameRow
				{
					GameId = group.Key,
					Name = name,
					CoverImageUrl = game?.CoverImageUrl,
					CategoryIds = game?.CategoryIds ?? new(),
					MinPlayers = game?.MinPlayers,
					MaxPlayers = game?.MaxPlayers,
					Stats = GameStats.From(name, group),
					Games = group.OrderByDescending(p => p.PlayedAt).ToList()
				};
			})
			.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
			.ToList();

		// Only categories of games played, so a single cloud never shows an empty list.
		var used = _all.SelectMany(r => r.CategoryIds).ToHashSet();
		Filters.Show(await _categories.GetCachedGroupsAsync(), categories.Where(c => used.Contains(c.Id)));

		Message = offline ? "Offline - your stats as last seen on this phone."
			: _all.Count == 0 ? "No finished games yet." : string.Empty;
		ApplyFilters();
	}

	partial void OnSearchTextChanged(string value) => ApplyFilters();

	private void ApplyFilters()
	{
		var text = SearchText.Trim();
		VisibleGames.Clear();
		foreach (var row in _all.Where(r =>
			(text.Length == 0 || r.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))
			&& Filters.Matches(r.CategoryIds, r.MinPlayers, r.MaxPlayers)))
			VisibleGames.Add(row);
	}
}

/// <summary>One finished game in a game's history: when, and how it ended.</summary>
public sealed record PlayedGameLine(string When, string Outcome);

/// <summary>My stats > one game: played, won, lost, tied with percentages, and each time it was played.</summary>
public partial class GameStatsViewModel : ObservableObject, IQueryAttributable
{
	[ObservableProperty]
	private string title = "Stats";

	[ObservableProperty]
	private GameStats stats = GameStats.From("Game", []);

	[ObservableProperty]
	private string? coverImageUrl;

	public ObservableCollection<PlayedGameLine> History { get; } = new();

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (!query.TryGetValue("row", out var r) || r is not StatsGameRow row) return;

		Title = row.Name;
		Stats = row.Stats;
		CoverImageUrl = row.CoverImageUrl;
		History.Clear();
		foreach (var game in row.Games)
			History.Add(new PlayedGameLine(
				game.PlayedAt.ToLocalTime().ToString("ddd d MMM yyyy, HH:mm", System.Globalization.CultureInfo.CurrentCulture),
				game.Outcome switch { GameOutcome.Won => "🏆 Won", GameOutcome.Lost => "✖ Lost", _ => "= Tied" }));
	}
}
