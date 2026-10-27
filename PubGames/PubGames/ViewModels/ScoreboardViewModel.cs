using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>UI-facing wrapper pairing a participant row with the player's display info.</summary>
public partial class ParticipantRow : ObservableObject
{
	/// <summary>The player's seat and score at the game night.</summary>
	public required SessionParticipant Participant { get; init; }
	/// <summary>The player.</summary>
	public required Player Player { get; init; }

	/// <summary>The player's score in this subgame.</summary>
	public int Score => Participant.Score;
	/// <summary>Still playing (not sitting out).</summary>
	public bool IsActive => Participant.IsActive;

	/// <summary>1-based seat number as shown on screen.</summary>
	public int Seat => Participant.Position + 1;

	/// <summary>Has the best score: the card stands out.</summary>
	[ObservableProperty]
	private bool isLeader;

	/// <summary>Reached the game's warning score (and not a limit yet).</summary>
	[ObservableProperty]
	private bool isWarning;

	/// <summary>Reached the game's maximum or minimum score.</summary>
	[ObservableProperty]
	private bool isAtLimit;

	/// <summary>"Warning", "Max reached", ... shown under the name; empty otherwise.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasStatus))]
	private string status = string.Empty;

	/// <summary>Whether the card shows a badge (warning, max, min).</summary>
	public bool HasStatus => !string.IsNullOrEmpty(Status);

	/// <summary>Fixed input order: it's this player's turn to enter a score.</summary>
	[ObservableProperty]
	private bool isTurn;

	/// <summary>The player's + / − and amount box work: always, or with a fixed input order only on their turn.</summary>
	[ObservableProperty]
	private bool canEnter = true;

	/// <summary>The same in a few letters ("▲ MAX"), for cards too small for the full badge.</summary>
	[ObservableProperty]
	private string shortStatus = string.Empty;

	/// <summary>Shows whether the player is at a limit or the warning score.</summary>
	public void UpdateStatus(ScoringRules rules)
	{
		IsAtLimit = rules.IsAtLimit(Score);
		IsWarning = !IsAtLimit && rules.IsWarning(Score);
		Status = rules.StatusFor(Score);
		ShortStatus = rules.ShortStatusFor(Score);
	}

	/// <summary>Call after mutating Participant.Score or Position so bound UI refreshes.</summary>
	public void NotifyChanged()
	{
		OnPropertyChanged(nameof(Score));
		OnPropertyChanged(nameof(Seat));
	}
}

/// <summary>
/// Step 4 of a game night: the live scoreboard. Every active player is on
/// screen at once, in seat order; players can be changed at any time via
/// "Change players" (SessionPlayersPage) without touching anyone's score.
/// </summary>
public partial class ScoreboardViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;

	/// <summary>Every player at the game night, in seat order.</summary>
	public ObservableCollection<ParticipantRow> Rows { get; } = new();

	/// <summary>The game night.</summary>
	[ObservableProperty]
	private GameSession? session;

	/// <summary>The game's name, as the page's title.</summary>
	[ObservableProperty]
	private string gameName = "Score";

	/// <summary>The game being played, for its rules behind the ⓘ.</summary>
	public PubGame? Game { get; private set; }

	/// <summary>How scores are kept in this game.</summary>
	[ObservableProperty]
	private ScoringRules rules = ScoringRules.For(ScoringType.PointTally);

	/// <summary>The game's settings, e.g. how each subgame's result is noted.</summary>
	[ObservableProperty]
	private ScoringSettings settings = ScoringSettings.Default;

	/// <summary>The subgame that's being played (1 or more).</summary>
	public int CurrentRound => Math.Max(1, Session?.CurrentRound ?? 1);

	/// <summary>"Subgame 2 of 5", or "Subgame 2" when the players decide when to finish.</summary>
	public string SubgameLabel => IsFinished ? "Game finished"
		: Session?.SubgameCount is 1 ? "1 game"
		: Session?.SubgameCount is { } count ? $"Subgame {CurrentRound} of {count}"
		: $"Subgame {CurrentRound}";

	/// <summary>Whether the game is finished.</summary>
	public bool IsFinished => Session?.EndedAt is not null;

	/// <summary>Scores are entered seat by seat (see ScoringSettings.FixedTurnOrder).</summary>
	public bool FixedTurnOrder => Settings.FixedTurnOrder && !IsFinished;

	/// <summary>The player whose turn it is, with a fixed input order.</summary>
	public ParticipantRow? TurnRow => FixedTurnOrder && Rows.Count > 0 ? Rows[Math.Clamp(Session?.TurnIndex ?? 0, 0, Rows.Count - 1)] : null;

	/// <summary>What happens with the end score: "Jan reached 100 ...". Empty when nobody reached it.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasEndMessage))]
	private string endMessage = string.Empty;

	/// <summary>Whether someone reached the end score: the message shows.</summary>
	public bool HasEndMessage => EndMessage.Length > 0;

	/// <summary>
	/// The end score ended this subgame by itself: the page opens its result.
	/// Raised once per subgame; coming back without confirming leaves it to the players.
	/// </summary>
	public event EventHandler? SubgameEnded;

	/// <summary>The subgame SubgameEnded was raised for, so it's raised only once.</summary>
	private (string SessionId, int Round)? _endedRound;

	private bool IsLastSubgame => Session?.SubgameCount is { } count && CurrentRound >= count;

	/// <summary>Next subgame: until the last one of a fixed number, or always when there's no fixed number.</summary>
	public bool CanGoNext => !IsFinished && !IsLastSubgame;

	/// <summary>Finish game: on the last subgame, or any time when there's no fixed number.</summary>
	public bool CanFinish => !IsFinished && (IsLastSubgame || Session?.SubgameCount is null);

	/// <summary>The − next to the subgame count: never below the subgame being played.</summary>
	public bool CanHaveFewerSubgames => !IsFinished && Session?.SubgameCount is { } count && count > CurrentRound;

	/// <summary>
	/// Add or drop a subgame during the game, e.g. when the winner or loser
	/// isn't clear yet. Never fewer than the subgame being played.
	/// </summary>
	public async Task ChangeSubgameCountAsync(int delta)
	{
		if (Session is null || IsFinished) return;
		var count = Math.Clamp((Session.SubgameCount ?? CurrentRound) + delta, CurrentRound, 99);
		if (count == Session.SubgameCount) return;
		Session.SubgameCount = count;
		await _local.SaveSessionAsync(Session);
		NotifySubgameChanged();
	}

	/// <summary>The subgame changed: everything that depends on it updates.</summary>
	private void NotifySubgameChanged()
	{
		OnPropertyChanged(nameof(FixedTurnOrder));
		OnPropertyChanged(nameof(CurrentRound));
		OnPropertyChanged(nameof(SubgameLabel));
		OnPropertyChanged(nameof(IsFinished));
		OnPropertyChanged(nameof(CanGoNext));
		OnPropertyChanged(nameof(CanFinish));
		OnPropertyChanged(nameof(CanHaveFewerSubgames));
		SubgameChanged?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Raised when the subgame or the number of subgames changed, so the page can rearrange its buttons.</summary>
	public event EventHandler? SubgameChanged;

	/// <summary>Raised after the players were (re)loaded, so the page can rebuild its layout.</summary>
	public event EventHandler? RowsReloaded;

	/// <summary>
	/// Gets the services it works with - the phone's database - from the app (dependency injection).
	/// </summary>
	public ScoreboardViewModel(ILocalDatabaseService local)
	{
		_local = local;
	}

	/// <summary>Opened from the rules page with the "sessionId" of the game that just started.</summary>
	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("sessionId", out var id) && id is string sessionId)
			await LoadAsync(sessionId);
	}

	/// <summary>
	/// Loads the game night: the game, its players in seat order with their scores, and whose turn it is.
	/// </summary>
	public async Task LoadAsync(string sessionId)
	{
		Session = await _local.GetSessionAsync(sessionId);
		if (Session is null) return;

		var game = Game = await _local.GetGameAsync(Session.GameId);
		GameName = game?.Name ?? "Score";
		Rules = ScoringRules.For(game);
		Settings = game?.ScoringSettings ?? ScoringSettings.Default;
		NotifySubgameChanged();

		await ReloadPlayersAsync();
	}

	/// <summary>Re-reads who's playing, e.g. after coming back from "Change players".</summary>
	public async Task ReloadPlayersAsync()
	{
		if (Session is null) return;

		var participants = (await _local.GetParticipantsAsync(Session.Id)).Where(p => p.IsActive).ToList();
		var players = (await _local.GetPlayersByIdsAsync(participants.Select(p => p.PlayerId)))
			.ToDictionary(p => p.Id);

		Rows.Clear();
		foreach (var participant in participants)
		{
			var player = players.GetValueOrDefault(participant.PlayerId)
				?? new Player { Id = participant.PlayerId, Name = "Unknown player" };
			Rows.Add(new ParticipantRow { Participant = participant, Player = player });
		}
		UpdateLeaders();
		foreach (var row in Rows)
			row.UpdateStatus(Rules);
		UpdateTurn();
		UpdateEndMessage();
		RowsReloaded?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Marks whose turn it is; with a fixed input order only they can enter a score.</summary>
	private void UpdateTurn()
	{
		var turn = TurnRow;
		foreach (var row in Rows)
		{
			row.IsTurn = turn == row;
			row.CanEnter = !IsFinished && (turn is null || turn == row);
		}
		OnPropertyChanged(nameof(TurnRow));
	}

	/// <summary>
	/// Fixed input order: the player whose turn it was is done; the next seat
	/// is up. After the last seat it's seat 1 again, and a new round - which is
	/// when an equalizing turn ends the subgame.
	/// </summary>
	public async Task EndTurnAsync()
	{
		if (Session is null || !FixedTurnOrder || Rows.Count == 0) return;
		Session.TurnIndex = (Math.Clamp(Session.TurnIndex, 0, Rows.Count - 1) + 1) % Rows.Count;
		await _local.SaveSessionAsync(Session);
		UpdateTurn();
		await CheckEndAsync(turnEnded: true);
	}

	/// <summary>
	/// Whether the end score ends the subgame now. Without a fixed input order
	/// that's checked after every score change; with one, after every turn.
	/// Automatic: the subgame ends - straight away, or with an equalizing turn
	/// once the others in that round had theirs. Manual: the players see who
	/// reached it and end the subgame themselves.
	/// </summary>
	private async Task CheckEndAsync(bool turnEnded)
	{
		if (Session is null || IsFinished || Settings.EndScore is null) return;
		if (FixedTurnOrder && !turnEnded) return;

		var reached = Rows.Where(r => Settings.ReachesEnd(r.Score)).ToList();
		var key = (Session.Id, CurrentRound);

		if (Settings.EndMode == EndMode.Automatic && Settings.EqualizingTurn && FixedTurnOrder)
		{
			if (Session.FinalRoundFrom is null && reached.Count > 0)
			{
				// The turn just ended was the one that reached it.
				Session.FinalRoundFrom = (Session.TurnIndex - 1 + Rows.Count) % Rows.Count;
				await _local.SaveSessionAsync(Session);
			}
			if (Session.FinalRoundFrom is not null && Session.TurnIndex == 0 && _endedRound != key)
			{
				_endedRound = key;
				UpdateEndMessage();
				SubgameEnded?.Invoke(this, EventArgs.Empty);
				return;
			}
			UpdateEndMessage();
			return;
		}

		if (Settings.EndMode == EndMode.Automatic && reached.Count > 0 && _endedRound != key)
		{
			_endedRound = key;
			UpdateEndMessage();
			SubgameEnded?.Invoke(this, EventArgs.Empty);
			return;
		}
		UpdateEndMessage();
	}

	/// <summary>The line above the cards about the end score.</summary>
	private void UpdateEndMessage()
	{
		if (Settings.EndScore is not { } end || IsFinished)
		{
			EndMessage = string.Empty;
			return;
		}
		var reached = Rows.Where(r => Settings.ReachesEnd(r.Score)).Select(r => r.Player.Name).ToList();
		var who = TieBreak.Names(reached);
		var verb = reached.Count == 1 ? "has" : "have";

		if (Session?.FinalRoundFrom is { } from && from < Rows.Count)
		{
			var reacher = Rows[from].Player.Name;
			EndMessage = Session.TurnIndex == 0
				? $"🏁 {reacher} reached {end} and everyone had their last turn: this subgame is over."
				: $"🏁 {reacher} reached {end}. Everyone after {reacher} gets one last turn.";
			return;
		}
		if (reached.Count == 0)
			EndMessage = string.Empty;
		else if (Settings.EndMode == EndMode.Manual || _endedRound == (Session?.Id, CurrentRound))
			EndMessage = $"🏁 {who} {verb} reached {end}. End the subgame with {(CanGoNext ? "Next subgame" : "Finish game")}.";
		else
			EndMessage = string.Empty;
	}

	/// <summary>
	/// Adjusting one player's score never touches anyone else's row - each
	/// SessionParticipant is stored and updated independently. The score stays
	/// within the game's min and max: a step past a limit stops at the limit.
	/// </summary>
	public async Task AdjustScoreAsync(ParticipantRow row, int delta)
	{
		if (IsFinished) return;
		var score = Rules.Clamp(row.Participant.Score + delta);
		if (score == row.Participant.Score) return;
		row.Participant.Score = score;
		await _local.SaveParticipantAsync(row.Participant);
		if (Session is not null)
		{
			// Saved games are kept counting from the last score (see SavedGames).
			Session.LastPlayedAt = DateTime.UtcNow;
			await _local.SaveSessionAsync(Session);
		}
		row.NotifyChanged();
		row.UpdateStatus(Rules);
		UpdateLeaders();
		await CheckEndAsync(turnEnded: false);
	}

	/// <summary>Highlights whoever is winning right now (nobody while all scores are equal).</summary>
	private void UpdateLeaders()
	{
		if (Rules.HighestWins is not { } highestWins || Rows.Count < 2)
		{
			foreach (var row in Rows) row.IsLeader = false;
			return;
		}

		var best = highestWins ? Rows.Max(r => r.Score) : Rows.Min(r => r.Score);
		var allEqual = Rows.All(r => r.Score == best);
		foreach (var row in Rows)
			row.IsLeader = !allEqual && row.Score == best;
	}
}
