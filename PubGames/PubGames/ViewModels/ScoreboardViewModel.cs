using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>UI-facing wrapper pairing a participant row with the player's display info.</summary>
public partial class ParticipantRow : ObservableObject
{
	public required SessionParticipant Participant { get; init; }
	public required Player Player { get; init; }

	public int Score => Participant.Score;
	public bool IsActive => Participant.IsActive;

	/// <summary>1-based seat number as shown on screen.</summary>
	public int Seat => Participant.Position + 1;

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

	public bool HasStatus => !string.IsNullOrEmpty(Status);

	public void UpdateStatus(ScoringRules rules)
	{
		IsAtLimit = rules.IsAtLimit(Score);
		IsWarning = !IsAtLimit && rules.IsWarning(Score);
		Status = rules.StatusFor(Score);
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

	public ObservableCollection<ParticipantRow> Rows { get; } = new();

	[ObservableProperty]
	private GameSession? session;

	[ObservableProperty]
	private string gameName = "Score";

	[ObservableProperty]
	private ScoringRules rules = ScoringRules.For(ScoringType.PointTally);

	/// <summary>The game's settings, e.g. how each subgame's result is noted.</summary>
	[ObservableProperty]
	private ScoringSettings settings = ScoringSettings.Default;

	public int CurrentRound => Math.Max(1, Session?.CurrentRound ?? 1);

	/// <summary>"Subgame 2 of 5", or "Subgame 2" when the players decide when to finish.</summary>
	public string SubgameLabel => IsFinished ? "Game finished"
		: Session?.SubgameCount is 1 ? "1 game"
		: Session?.SubgameCount is { } count ? $"Subgame {CurrentRound} of {count}"
		: $"Subgame {CurrentRound}";

	public bool IsFinished => Session?.EndedAt is not null;

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

	private void NotifySubgameChanged()
	{
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

	public async Task LoadAsync(string sessionId)
	{
		Session = await _local.GetSessionAsync(sessionId);
		if (Session is null) return;

		var game = await _local.GetGameAsync(Session.GameId);
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
		RowsReloaded?.Invoke(this, EventArgs.Empty);
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
		row.NotifyChanged();
		row.UpdateStatus(Rules);
		UpdateLeaders();
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
