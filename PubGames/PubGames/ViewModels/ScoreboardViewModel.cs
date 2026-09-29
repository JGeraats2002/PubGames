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
		Rules = ScoringRules.For(game?.ScoringType ?? ScoringType.Custom);

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
		RowsReloaded?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Adjusting one player's score never touches anyone else's row - each
	/// SessionParticipant is stored and updated independently.
	/// </summary>
	public async Task AdjustScoreAsync(ParticipantRow row, int delta)
	{
		if (delta == 0) return;
		row.Participant.Score += delta;
		await _local.SaveParticipantAsync(row.Participant);
		row.NotifyChanged();
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
