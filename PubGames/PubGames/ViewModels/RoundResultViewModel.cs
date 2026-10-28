using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>One player on the result screen: marked as winner/loser, or given a place.</summary>
public partial class ResultRow : ObservableObject
{
	/// <summary>The player.</summary>
	public required Player Player { get; init; }
	/// <summary>The player's score in the subgame.</summary>
	public required int Score { get; init; }
	/// <summary>How the result is noted: winners, places or losers.</summary>
	public required ResultMode Mode { get; init; }

	/// <summary>How many players are in this subgame: the lowest possible place.</summary>
	public required int PlayerCount { get; init; }

	/// <summary>Places are noted: the place and − / + show.</summary>
	public bool IsRanking => Mode == ResultMode.Ranking;
	/// <summary>Winners or losers are noted: tapping the row marks it.</summary>
	public bool IsPicking => !IsRanking;

	/// <summary>Winner or loser mode: tapped as a winner/loser. Several players can be.</summary>
	[ObservableProperty]
	private bool isMarked;

	/// <summary>Ranking mode: 1 = first. Players can share a place.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PlaceLabel))]
	private int place = 1;

	/// <summary>The place as text: 1st, 2nd ...</summary>
	public string PlaceLabel => Standings.Ordinal(Place);

	/// <summary>The badge on a marked card, in words so it doesn't rely on colour.</summary>
	public string MarkText => Mode == ResultMode.Loser ? L.T("✖ LOSER") : L.T("🏆 WINNER");

	/// <summary>"Score: 42".</summary>
	public string ScoreText => L.F("Score: {0}", Score);
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
	private readonly IStatsService _stats;

	private GameSession? _session;
	private ScoringRules _rules = ScoringRules.For(ScoringType.PlusMinus);
	private List<SessionParticipant> _participants = new();
	private bool _saving;
	private bool _lowestWins;
	private ScoringType _gameType = ScoringType.PlusMinus;
	private ScoringSettings _settings = ScoringSettings.Default;

	/// <summary>Whether the lowest score wins this game (for the ⓘ explanation).</summary>
	public bool LowestWins => _lowestWins;

	/// <summary>
	/// Asked when finishing ends in a tie for the win (or for loser), with who's
	/// tied; true plays an extra subgame instead of ending. Set by the page.
	/// </summary>
	public Func<string, Task<bool>>? AskExtraSubgame { get; set; }

	/// <summary>Plays a tie-break duel: everyone's confirmed duel score by player id, or null when stopped. Set by the page.</summary>
	public Func<DuelSetup, Task<Dictionary<string, int>?>>? PlayDuel { get; set; }

	/// <summary>Shows a title and a message, e.g. what a duel decided. Set by the page.</summary>
	public Func<string, string, Task>? Inform { get; set; }

	/// <summary>The game's scoring settings, e.g. whether ties are allowed (for the ⓘ explanation).</summary>
	public ScoringSettings Settings => _settings;

	/// <summary>The game night, for opening the final result.</summary>
	public string? SessionId => _session?.Id;

	/// <summary>Every player with their score and result.</summary>
	public ObservableCollection<ResultRow> Rows { get; } = new();

	/// <summary>How this game notes results: winners, places or losers.</summary>
	[ObservableProperty]
	private ResultMode mode;

	/// <summary>True after "Finish game": this is the last subgame.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ConfirmText))]
	private bool isFinishing;

	/// <summary>The page's title: which subgame.</summary>
	[ObservableProperty]
	private string title = L.T("Result");

	/// <summary>What's still missing, what a duel decided, or what went wrong.</summary>
	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>The confirm button's text: next subgame, or finish the game.</summary>
	public string ConfirmText => IsFinishing ? L.T("Finish game & show result") : L.T("Start next subgame");

	/// <summary>Raised after saving; true when the whole game is finished.</summary>
	public event EventHandler<bool>? Saved;

	/// <summary>
	/// Gets the services it works with - the phone's database and stats - from the app (dependency
	/// injection).
	/// </summary>
	public RoundResultViewModel(ILocalDatabaseService local, IStatsService stats)
	{
		_local = local;
		_stats = stats;
	}

	/// <summary>
	/// Opened after a subgame (or to finish the game): loads it and fills in the result from the scores.
	/// </summary>
	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		IsFinishing = query.TryGetValue("finish", out var f) && f is true;
		if (query.TryGetValue("sessionId", out var id) && id is string sessionId)
			await LoadAsync(sessionId);
	}

	/// <summary>
	/// Loads the game night, its players and scores, and fills in the result: winners, losers or places, by
	/// the game's rules.
	/// </summary>
	private async Task LoadAsync(string sessionId)
	{
		_session = await _local.GetSessionAsync(sessionId);
		if (_session is null) return;

		var game = await _local.GetGameAsync(_session.GameId);
		var settings = _settings = game?.ScoringSettings ?? ScoringSettings.Default;
		_gameType = game?.ScoringType ?? ScoringType.PlusMinus;
		_rules = ScoringRules.For(game);
		Mode = settings.ResultMode;

		var round = Math.Max(1, _session.CurrentRound);
		Title = _session.SubgameCount is { } count ? L.F("Result of subgame {0} of {1}", round, count) : L.F("Result of subgame {0}", round);
		_lowestWins = settings.LowestWins;

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
				Player = players.GetValueOrDefault(participant.PlayerId) ?? new Player { Id = participant.PlayerId, Name = L.T("Unknown player") },
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

	/// <summary>Marks a player as winner or loser, or takes the mark off.</summary>
	public void Toggle(ResultRow row) => row.IsMarked = !row.IsMarked;

	/// <summary>Moves a player one place better (-1) or worse (+1).</summary>
	public void ChangePlace(ResultRow row, int direction) =>
		row.Place = Math.Clamp(row.Place + direction, 1, row.PlayerCount);

	/// <summary>"Jan and Sanne are tied for the win." when the totals don't decide the game; null otherwise.</summary>
	private async Task<string?> TieAsync()
	{
		var standings = await SubgameStandingsAsync();

		List<string> tied;
		if (Mode == ResultMode.Loser)
		{
			var most = standings.Count == 0 ? 0 : standings.Max(s => s.Losses);
			tied = most == 0 ? [] : standings.Where(s => s.Losses == most).Select(s => s.Name).ToList();
		}
		else
			tied = standings.Where(s => s.Place == 1).Select(s => s.Name).ToList();

		if (tied.Count < 2) return null;
		var who = L.F("{0} and {1}", string.Join(", ", tied.SkipLast(1)), tied[^1]);
		return Mode == ResultMode.Loser ? L.F("{0} are tied as loser.", who) : L.F("{0} are tied for the win.", who);
	}

	/// <summary>Everyone's totals over the subgames, without any earlier duel after the final total.</summary>
	private async Task<List<PlayerStanding>> SubgameStandingsAsync()
	{
		var results = (await _local.GetRoundResultsAsync(_session!.Id)).Where(r => r.IsSubgame).ToList();
		var names = (await _local.GetPlayersByIdsAsync(results.Select(r => r.PlayerId).Distinct())).ToDictionary(p => p.Id, p => p.Name);
		return Standings.Calculate(Mode, results, names);
	}

	/// <summary>
	/// Plays duels between `tied` until what counts is decided: the whole order,
	/// or only who's best (worstCounts: who's worst). Players still tied after
	/// a duel play it again among themselves. Returns the players in groups from
	/// best to worst (a group shares a result that doesn't count), or null when
	/// the players stopped the duel.
	/// </summary>
	private async Task<List<List<Player>>?> DuelAsync(List<Player> tied, string reason, bool wholeOrder, bool worstCounts)
	{
		var (type, duelSettings) = _settings.DuelScoring(_gameType);
		if (PlayDuel is null || await PlayDuel(new DuelSetup(reason, tied, type, duelSettings, wholeOrder, worstCounts)) is not { } scores)
			return null;

		var byId = tied.ToDictionary(p => p.Id);
		var order = TieBreak.Order(scores, duelSettings.LowestWins);
		var again = TieBreak.StillTied(order, wholeOrder, worstCounts).ToHashSet();
		var result = new List<List<Player>>();
		for (var groupIndex = 0; groupIndex < order.Count; groupIndex++)
		{
			var group = order[groupIndex].Select(id => byId[id]).ToList();
			if (!again.Contains(groupIndex))
			{
				result.Add(group);
				continue;
			}
			var replay = await DuelAsync(group, L.F("Still tied after the duel: {0}. Play the duel again.", TieBreak.Names(group.Select(p => p.Name).ToList())), wholeOrder, worstCounts);
			if (replay is null) return null;
			result.AddRange(replay);
		}
		return result;
	}

	/// <summary>
	/// No ties after a subgame: players marked together as winner or loser, or
	/// sharing a place, duel for it. The duel's order decides the result - one
	/// winner or loser, or places one after another, so the place points
	/// differ: filled in on the result right away, and still to change by
	/// hand before confirming. Null when a duel was stopped; otherwise whether
	/// a duel was played.
	/// </summary>
	private async Task<bool?> PlayOffSubgameTiesAsync()
	{
		if (!_settings.DuelsAfterSubgame) return false;
		string NamesOf(IEnumerable<ResultRow> rows) => TieBreak.Names(rows.Select(r => r.Player.Name).ToList());

		if (Mode != ResultMode.Ranking)
		{
			var marked = Rows.Where(r => r.IsMarked).ToList();
			if (marked.Count < 2) return false;
			var loser = Mode == ResultMode.Loser;
			var reason = loser ? L.F("{0} are tied as loser of this subgame.", NamesOf(marked)) : L.F("{0} are tied for the win of this subgame.", NamesOf(marked));
			var order = await DuelAsync(marked.Select(r => r.Player).ToList(), reason, wholeOrder: false, worstCounts: loser);
			if (order is null) return null;

			var decided = (loser ? order[^1] : order[0]).Select(p => p.Id).ToHashSet();
			foreach (var row in marked)
				row.IsMarked = decided.Contains(row.Player.Id);
			var name = NamesOf(marked.Where(r => r.IsMarked));
			StatusMessage = (loser ? L.F("{0} lost the duel and is marked as loser.", name) : L.F("{0} won the duel and is marked as winner.", name))
				+ L.T(" Change it if needed, then confirm.");
			return true;
		}

		var every = _settings.TieBreakScope == TieBreakScope.Every;
		var shared = Rows.GroupBy(r => r.Place).Where(g => g.Count() > 1 && (every || g.Key == 1)).OrderBy(g => g.Key).ToList();
		if (shared.Count == 0) return false;
		foreach (var group in shared)
		{
			var rows = group.ToList();
			var order = await DuelAsync(rows.Select(r => r.Player).ToList(), L.F("{0} share {1} place.", NamesOf(rows), Standings.Ordinal(group.Key)), wholeOrder: every, worstCounts: false);
			if (order is null) return null;

			// The duel's order gives places one after another from the shared place: 1st, 2nd, ...
			var next = group.Key;
			foreach (var ids in order.Select(g => g.Select(p => p.Id).ToHashSet()))
			{
				foreach (var row in rows.Where(r => ids.Contains(r.Player.Id)))
					row.Place = next;
				next += ids.Count;
			}
		}
		StatusMessage = L.T("The duel decided the places and points below. Change them if needed, then confirm.");
		return true;
	}

	/// <summary>
	/// No ties in the final total: players with equal totals duel - for 1st
	/// place (or for loser), or for every shared place. The duel's order is
	/// saved as the final duel and decides their places in the final result.
	/// False when a duel was stopped.
	/// </summary>
	private async Task<bool> PlayOffFinalTiesAsync()
	{
		var standings = await SubgameStandingsAsync();
		var every = _settings.TieBreakScope == TieBreakScope.Every;
		var loserGame = Mode == ResultMode.Loser;
		var losers = loserGame ? Standings.Losers(standings) : [];
		var groups = standings.GroupBy(s => s.Place)
			.Select(g => g.ToList())
			.Where(g => g.Count > 1 && (every || (loserGame ? g.SequenceEqual(losers) && g[0].Losses > 0 : g[0].Place == 1)))
			.ToList();

		var players = (await _local.GetPlayersByIdsAsync(groups.SelectMany(g => g).Select(s => s.PlayerId))).ToDictionary(p => p.Id);
		var duelResults = new List<RoundResult>();
		var summary = new List<string>();
		foreach (var group in groups)
		{
			var names = TieBreak.Names(group.Select(s => s.Name).ToList());
			var isLoss = loserGame && group.SequenceEqual(losers);
			var reason = isLoss ? L.F("{0} are tied as loser of the game.", names)
				: group[0].Place == 1 ? L.F("{0} are tied for the win of the game.", names)
				: L.F("{0} share {1} place in the final total.", names, Standings.Ordinal(group[0].Place));
			var tied = group.Select(s => players.GetValueOrDefault(s.PlayerId) ?? new Player { Id = s.PlayerId, Name = s.Name }).ToList();
			var order = await DuelAsync(tied, reason, wholeOrder: every, worstCounts: isLoss);
			if (order is null) return false;

			var rank = 1;
			foreach (var tiedGroup in order)
			{
				duelResults.AddRange(tiedGroup.Select(p => new RoundResult
				{
					SessionId = _session!.Id,
					Round = RoundResult.FinalDuelRound,
					PlayerId = p.Id,
					Place = rank
				}));
				rank += tiedGroup.Count;
			}
			summary.Add(isLoss ? L.F("{0} lost the duel.", TieBreak.Names(order[^1].Select(p => p.Name).ToList()))
				: L.F("{0} won the duel.", TieBreak.Names(order[0].Select(p => p.Name).ToList())));
		}

		// Also clears a final duel from an earlier try.
		await _local.SaveRoundResultsAsync(_session!.Id, RoundResult.FinalDuelRound, duelResults);
		if (summary.Count > 0 && Inform is not null)
			await Inform(L.T("Duel decided"), string.Join("\n", summary));
		return true;
	}

	/// <summary>
	/// Who starts the next subgame (ScoringSettings.StartRotation): the seats
	/// turn so that player sits in seat 1, and everyone keeps their neighbours.
	/// The winner is the marked player in a winner game and 1st place
	/// otherwise; the loser is the marked player in a loser game and the last
	/// place otherwise. Players sitting out keep their seats after the others.
	/// </summary>
	private async Task RotateSeatsAsync()
	{
		if (_settings.StartRotation == StartRotation.None || Rows.Count < 2) return;

		// Rows are in seat order.
		var worst = Rows.Max(r => r.Place);
		var isWinner = Rows.Select(r => Mode == ResultMode.Winner ? r.IsMarked : r.Place == 1).ToList();
		var isLoser = Rows.Select(r => Mode == ResultMode.Loser ? r.IsMarked : r.Place == worst).ToList();
		if (SeatRotation.NextStarter(_settings.StartRotation, Rows.Count, isWinner, isLoser) is not { } starter || starter == 0)
			return;

		var active = Rows.Select(r => _participants.First(p => p.IsActive && p.PlayerId == r.Player.Id)).ToList();
		var seated = active.Skip(starter).Concat(active.Take(starter))
			.Concat(_participants.Where(p => !p.IsActive).OrderBy(p => p.Position))
			.ToList();
		for (var seat = 0; seat < seated.Count; seat++)
		{
			if (seated[seat].Position == seat) continue;
			seated[seat].Position = seat;
			await _local.SaveParticipantAsync(seated[seat]);
		}
	}

	/// <summary>The result goes to the linked players' stats; offline it goes out later (see StatsService).</summary>
	private async Task PublishStatsAsync()
	{
		try
		{
			await _stats.PublishFinishedGamesAsync();
		}
		catch (Exception)
		{
			// Offline: sent the next time the stats are published.
		}
	}

	/// <summary>
	/// Saves the subgame's result - after any duels - and goes on to the next subgame or the final result.
	/// </summary>
	public async Task ConfirmAsync()
	{
		if (_session is null || _saving) return;

		if (Rows.Count == 0)
		{
			StatusMessage = L.T("Nobody is playing - add players first.");
			return;
		}
		if (Mode != ResultMode.Ranking && !Rows.Any(r => r.IsMarked))
		{
			StatusMessage = Mode == ResultMode.Loser ? L.T("Tap at least one loser.") : L.T("Tap at least one winner.");
			return;
		}

		_saving = true;
		try
		{
			switch (await PlayOffSubgameTiesAsync())
			{
				case null:
					StatusMessage = L.T("The duel was stopped, so the tie isn't decided. Nothing is saved yet.");
					return;
				case true:
					// The duel's outcome is filled in: check it (or change it) and confirm again to save.
					return;
			}

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
			if (finish && _settings.DuelsAfterFinal)
			{
				if (!await PlayOffFinalTiesAsync())
				{
					StatusMessage = L.T("The duel was stopped, so the tie in the final total isn't decided. Confirm again to play it.");
					return;
				}
			}
			else if (finish && await TieAsync() is { } tie && AskExtraSubgame is not null && await AskExtraSubgame(tie))
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
				await RotateSeatsAsync();
				_session.CurrentRound = round + 1;
				_session.TurnIndex = 0;
				_session.FinalRoundFrom = null;
			}
			await _local.SaveSessionAsync(_session);
			if (finish)
				_ = PublishStatsAsync();
			Saved?.Invoke(this, finish);
		}
		finally
		{
			_saving = false;
		}
	}
}
