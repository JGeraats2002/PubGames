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

	[ObservableProperty]
	private List<PlayerStanding> rows = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PageTitle), nameof(DoneText))]
	private bool isFinal;

	/// <summary>Final only: "🏆 Winner: Jan" or "✖ Loser: Piet".</summary>
	[ObservableProperty]
	private string headline = string.Empty;

	[ObservableProperty]
	private string subtitle = string.Empty;

	/// <summary>Who's playing at the end, in seat order: the line-up for playing again.</summary>
	public List<Player> Lineup { get; private set; } = new();

	public PubGame? Game { get; private set; }

	/// <summary>The number of subgames this game had, used again when playing again.</summary>
	public int? SubgameCount { get; private set; }

	/// <summary>The account that played, for the next session.</summary>
	public string AccountId { get; private set; } = string.Empty;

	public string PageTitle => IsFinal ? "Final result" : "Stats";
	public string DoneText => IsFinal ? "Done" : "Back to the game";

	public StandingsViewModel(ILocalDatabaseService local)
	{
		_local = local;
	}

	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		IsFinal = query.TryGetValue("final", out var f) && f is true;
		if (query.TryGetValue("sessionId", out var id) && id is string sessionId)
			await LoadAsync(sessionId);
	}

	private async Task LoadAsync(string sessionId)
	{
		var session = await _local.GetSessionAsync(sessionId);
		if (session is null) return;

		var game = Game = await _local.GetGameAsync(session.GameId);
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
		var played = results.Select(r => r.Round).Distinct().Count();

		if (IsFinal)
		{
			Headline = Standings.Headline(settings.ResultMode, standings);
			Subtitle = $"{game?.Name ?? "Game"} · {played} {(played == 1 ? "subgame" : "subgames")} played";
			foreach (var s in standings)
				s.ShowPlace = true;
			Rows = standings;
		}
		else
		{
			Subtitle = played == 0
				? "No subgames finished yet - totals appear after the first one."
				: $"After {played} {(played == 1 ? "subgame" : "subgames")}. The winner is revealed when you finish the game.";
			// Seat order (players who left at the end), so the stats don't give the ranking away.
			var seat = participants.Where(p => p.IsActive).ToDictionary(p => p.PlayerId, p => p.Position);
			Rows = standings.OrderBy(s => seat.GetValueOrDefault(s.PlayerId, int.MaxValue)).ThenBy(s => s.Name).ToList();
		}
	}
}
