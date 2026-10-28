using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// Everyone's totals over the subgames played so far. During the game ("Stats")
/// players are listed in seat order without places, so the winner stays a
/// surprise; after Finish game ("final") they're ranked and the winner or
/// loser is announced. Opened with "sessionId" and "final".
/// </summary>
public partial class StandingsViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;

	/// <summary>Every player with their place and total.</summary>
	[ObservableProperty]
	private List<PlayerStanding> rows = new();

	/// <summary>The final result (not the stats during the game).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PageTitle), nameof(DoneText))]
	private bool isFinal;

	/// <summary>Final only: "🏆 Winner: Jan" or "✖ Loser: Piet".</summary>
	[ObservableProperty]
	private string headline = string.Empty;

	/// <summary>The game's name and how many subgames were played.</summary>
	[ObservableProperty]
	private string subtitle = string.Empty;

	/// <summary>Who's playing at the end, in seat order: the line-up for playing again.</summary>
	public List<Player> Lineup { get; private set; } = new();

	/// <summary>The game, for Play again.</summary>
	public PubGame? Game { get; private set; }

	/// <summary>The number of subgames this game had, used again when playing again.</summary>
	public int? SubgameCount { get; private set; }

	/// <summary>The account that played, for the next session.</summary>
	public string AccountId { get; private set; } = string.Empty;

	/// <summary>"Final result" or "Stats".</summary>
	public string PageTitle => IsFinal ? L.T("Final result") : L.T("Stats");
	/// <summary>The button's text: "Done", or "Back to the game".</summary>
	public string DoneText => IsFinal ? L.T("Done") : L.T("Back to the game");

	/// <summary>
	/// Gets the services it works with - the phone's database - from the app (dependency injection).
	/// </summary>
	public StandingsViewModel(ILocalDatabaseService local)
	{
		_local = local;
	}

	/// <summary>Opened during a game or for the final result: loads the standings.</summary>
	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		IsFinal = query.TryGetValue("final", out var f) && f is true;
		if (query.TryGetValue("sessionId", out var id) && id is string sessionId)
			await LoadAsync(sessionId);
	}

	/// <summary>Works out and shows the standings of the game night from its subgame results.</summary>
	private async Task LoadAsync(string sessionId)
	{
		var session = await _local.GetSessionAsync(sessionId);
		if (session is null) return;

		var game = Game = (await _local.GetGameAsync(session.GameId))?.InAppLanguage();
		SubgameCount = session.SubgameCount;
		AccountId = session.AccountId;
		var settings = game?.ScoringSettings ?? ScoringSettings.Default;
		var results = await _local.GetRoundResultsAsync(sessionId);
		var participants = await _local.GetParticipantsAsync(sessionId);
		var names = (await _local.GetPlayersByIdsAsync(results.Select(r => r.PlayerId).Distinct()))
			.ToDictionary(p => p.Id, p => p.Name);

		var active = participants.Where(p => p.IsActive).OrderBy(p => p.Position).ToList();
		var lineupPlayers = (await _local.GetPlayersByIdsAsync(active.Select(p => p.PlayerId))).ToDictionary(p => p.Id);
		Lineup = active.Select(p => lineupPlayers.GetValueOrDefault(p.PlayerId)).OfType<Player>().ToList();

		var standings = Standings.Calculate(settings.ResultMode, results, names);
		var played = results.Where(r => r.IsSubgame).Select(r => r.Round).Distinct().Count();

		if (IsFinal)
		{
			Headline = Standings.Headline(settings.ResultMode, standings);
			Subtitle = L.F("{0} · {1} {2} played", game?.Name ?? L.T("Game"), played, (played == 1 ? L.T("subgame") : L.T("subgames")));
			foreach (var standing in standings)
				standing.ShowPlace = true;
			Rows = standings;
		}
		else
		{
			Subtitle = played == 0
				? L.T("No subgames finished yet.")
				: L.F("After {0} {1}.", played, (played == 1 ? L.T("subgame") : L.T("subgames")));
			// Seat order (players who left at the end), so the stats don't give the ranking away.
			var seat = participants.Where(p => p.IsActive).ToDictionary(p => p.PlayerId, p => p.Position);
			Rows = standings.OrderBy(s => seat.GetValueOrDefault(s.PlayerId, int.MaxValue)).ThenBy(s => s.Name).ToList();
		}
	}
}
