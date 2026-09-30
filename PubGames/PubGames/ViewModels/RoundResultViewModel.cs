using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>One player on the result screen: marked as winner/loser, or given a place.</summary>
public partial class ResultRow : ObservableObject
{
	public required Player Player { get; init; }
	public required int Score { get; init; }
	public required ResultMode Mode { get; init; }

	/// <summary>How many players are in this subgame: the lowest possible place.</summary>
	public required int PlayerCount { get; init; }

	public bool IsRanking => Mode == ResultMode.Ranking;
	public bool IsPicking => !IsRanking;

	/// <summary>Winner or loser mode: tapped as a winner/loser. Several players can be.</summary>
	[ObservableProperty]
	private bool isMarked;

	/// <summary>Ranking mode: 1 = first. Players can share a place.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PlaceLabel))]
	private int place = 1;

	public string PlaceLabel => Standings.Ordinal(Place);

	/// <summary>The badge on a marked card, in words so it doesn't rely on colour.</summary>
	public string MarkText => Mode == ResultMode.Loser ? "✖ LOSER" : "🏆 WINNER";

	public string ScoreText => $"Score: {Score}";
}

/// <summary>
/// After a subgame: the players note its result the way the game's maker chose
/// (winner(s), a ranking, or loser(s)). Confirming saves it, then either starts
/// the next subgame with every score back at the starting score, or - after
/// Finish game - ends the whole game. Opened with "sessionId" and "finish".
/// </summary>
public partial class RoundResultViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;

	private GameSession? _session;
	private ScoringRules _rules = ScoringRules.For(ScoringType.PlusMinus);
	private List<SessionParticipant> _participants = new();
	private bool _saving;
	private bool _lowestWins;

	/// <summary>
	/// Asked when finishing ends in a tie for the win (or for loser), with who's
	/// tied; true plays an extra subgame instead of ending. Set by the page.
	/// </summary>
	public Func<string, Task<bool>>? AskExtraSubgame { get; set; }

	public string? SessionId => _session?.Id;

	public ObservableCollection<ResultRow> Rows { get; } = new();

	[ObservableProperty]
	private ResultMode mode;

	/// <summary>True after "Finish game": this is the last subgame.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ConfirmText))]
	private bool isFinishing;

	[ObservableProperty]
	private string title = "Result";

	[ObservableProperty]
	private string instruction = string.Empty;

	[ObservableProperty]
	private string statusMessage = string.Empty;

	public string ConfirmText => IsFinishing ? "Finish game & show result" : "Start next subgame";

	/// <summary>Raised after saving; true when the whole game is finished.</summary>
	public event EventHandler<bool>? Saved;

	public RoundResultViewModel(ILocalDatabaseService local)
	{
		_local = local;
	}

	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		IsFinishing = query.TryGetValue("finish", out var f) && f is true;
		if (query.TryGetValue("sessionId", out var id) && id is string sessionId)
			await LoadAsync(sessionId);
	}

	private async Task LoadAsync(string sessionId)
	{
		_session = await _local.GetSessionAsync(sessionId);
		if (_session is null) return;

		var game = await _local.GetGameAsync(_session.GameId);
		var settings = game?.ScoringSettings ?? ScoringSettings.Default;
		_rules = ScoringRules.For(game);
		Mode = settings.ResultMode;

		var round = Math.Max(1, _session.CurrentRound);
		Title = _session.SubgameCount is { } count ? $"Result of subgame {round} of {count}" : $"Result of subgame {round}";
		_lowestWins = settings.LowestWins;
		var best = _lowestWins ? "lowest" : "highest";
		Instruction = Mode switch
		{
			ResultMode.Ranking => $"Places are filled in from the scores ({best} score first). Change them with − and + if needed; players can share a place.",
			ResultMode.Loser => $"The loser is picked from the scores (the {(_lowestWins ? "highest" : "lowest")} score). Tap to change: more players can lose together.",
			_ => $"The winner is picked from the scores (the {best} score). Tap to change: more players can share the win."
		};

		_participants = await _local.GetParticipantsAsync(sessionId);
		var active = _participants.Where(p => p.IsActive).OrderBy(p => p.Position).ToList();
		var players = (await _local.GetPlayersByIdsAsync(active.Select(p => p.PlayerId))).ToDictionary(p => p.Id);

		// Suggested from the scores, depending on whether the highest or lowest score wins.
		// Equal scores share a place (1, 1, 3). When everyone has the same score nobody is picked.
		int Better(int score) => _lowestWins
			? active.Count(other => other.Score < score)
			: active.Count(other => other.Score > score);
		var allEqual = active.Select(p => p.Score).Distinct().Count() <= 1;

		Rows.Clear();
		foreach (var participant in active)
		{
			var place = 1 + Better(participant.Score);
			var isLast = active.All(other => Better(other.Score) <= Better(participant.Score));
			Rows.Add(new ResultRow
			{
				Player = players.GetValueOrDefault(participant.PlayerId) ?? new Player { Id = participant.PlayerId, Name = "Unknown player" },
				Score = participant.Score,
				Mode = Mode,
				PlayerCount = active.Count,
				Place = place,
				IsMarked = !allEqual && Mode switch
				{
					ResultMode.Winner => place == 1,
					ResultMode.Loser => isLast,
					_ => false
				}
			});
		}
	}

	public void Toggle(ResultRow row) => row.IsMarked = !row.IsMarked;

	public void ChangePlace(ResultRow row, int direction) =>
		row.Place = Math.Clamp(row.Place + direction, 1, row.PlayerCount);

	/// <summary>"Jan and Sanne are tied for the win." when the totals don't decide the game; null otherwise.</summary>
	private async Task<string?> TieAsync()
	{
		var results = await _local.GetRoundResultsAsync(_session!.Id);
		var names = (await _local.GetPlayersByIdsAsync(results.Select(r => r.PlayerId).Distinct())).ToDictionary(p => p.Id, p => p.Name);
		var standings = Standings.Calculate(Mode, results, names);

		List<string> tied;
		if (Mode == ResultMode.Loser)
		{
			var most = standings.Count == 0 ? 0 : standings.Max(s => s.Losses);
			tied = most == 0 ? [] : standings.Where(s => s.Losses == most).Select(s => s.Name).ToList();
		}
		else
			tied = standings.Where(s => s.Place == 1).Select(s => s.Name).ToList();

		if (tied.Count < 2) return null;
		var who = $"{string.Join(", ", tied.SkipLast(1))} and {tied[^1]}";
		return Mode == ResultMode.Loser ? $"{who} are tied as loser." : $"{who} are tied for the win.";
	}

	public async Task ConfirmAsync()
	{
		if (_session is null || _saving) return;

		if (Rows.Count == 0)
		{
			StatusMessage = "Nobody is playing - add players first.";
			return;
		}
		if (Mode != ResultMode.Ranking && !Rows.Any(r => r.IsMarked))
		{
			StatusMessage = Mode == ResultMode.Loser ? "Tap at least one loser." : "Tap at least one winner.";
			return;
		}

		_saving = true;
		try
		{
			var round = Math.Max(1, _session.CurrentRound);
			await _local.SaveRoundResultsAsync(_session.Id, round, Rows.Select(r => new RoundResult
			{
				SessionId = _session.Id,
				Round = round,
				PlayerId = r.Player.Id,
				Score = r.Score,
				Place = Mode == ResultMode.Ranking ? r.Place : null,
				IsWinner = Mode == ResultMode.Winner && r.IsMarked,
				IsLoser = Mode == ResultMode.Loser && r.IsMarked
			}));

			var finish = IsFinishing;
			if (finish && await TieAsync() is { } tie && AskExtraSubgame is not null && await AskExtraSubgame(tie))
			{
				// Not decided yet: one more subgame, the game goes on.
				_session.SubgameCount = round + 1;
				finish = false;
			}

			if (finish)
				_session.EndedAt = DateTime.UtcNow;
			else
			{
				// Every subgame starts fresh, also for players sitting out who may rejoin.
				foreach (var participant in _participants.Where(p => p.Score != _rules.StartScore))
				{
					participant.Score = _rules.StartScore;
					await _local.SaveParticipantAsync(participant);
				}
				_session.CurrentRound = round + 1;
			}
			await _local.SaveSessionAsync(_session);
			Saved?.Invoke(this, finish);
		}
		finally
		{
			_saving = false;
		}
	}
}
