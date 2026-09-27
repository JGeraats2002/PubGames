using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

	/// <summary>Call after mutating Participant.Score so bound UI refreshes.</summary>
	public void NotifyScoreChanged() => OnPropertyChanged(nameof(Score));
}

public partial class ScoreboardViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;

	public ObservableCollection<ParticipantRow> Rows { get; } = new();

	[ObservableProperty]
	private GameSession? session;

	[ObservableProperty]
	private string gameName = "Score";

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

		var participants = (await _local.GetParticipantsAsync(sessionId)).Where(p => p.IsActive).ToList();
		var players = (await _local.GetPlayersByIdsAsync(participants.Select(p => p.PlayerId)))
			.ToDictionary(p => p.Id);

		Rows.Clear();
		foreach (var participant in participants)
		{
			var player = players.GetValueOrDefault(participant.PlayerId)
				?? new Player { Id = participant.PlayerId, Name = "Unknown player" };
			Rows.Add(new ParticipantRow { Participant = participant, Player = player });
		}
	}

	/// <summary>
	/// Adjusting one player's score never touches anyone else's row - each
	/// SessionParticipant is stored and updated independently. Bind +/- score
	/// buttons to this with the row as CommandParameter and a fixed delta
	/// (e.g. two buttons: one calling AdjustScore(row, 1), one AdjustScore(row, -1)).
	/// </summary>
	public async Task AdjustScoreAsync(ParticipantRow row, int delta)
	{
		row.Participant.Score += delta;
		await _local.SaveParticipantAsync(row.Participant);
		row.NotifyScoreChanged();
	}

	/// <summary>
	/// Removing a player mid-game: flip IsActive off, leave Score exactly as
	/// it is. Their row disappears from active play but their score stays on
	/// record for the session history.
	/// </summary>
	[RelayCommand]
	private async Task RemovePlayerAsync(ParticipantRow row)
	{
		row.Participant.IsActive = false;
		await _local.SaveParticipantAsync(row.Participant);
		Rows.Remove(row);
	}

	/// <summary>
	/// Adding a player mid-game: new SessionParticipant row starting at 0,
	/// placed at the end of the current seat order. Everyone else's Score
	/// and Position are untouched.
	/// </summary>
	[RelayCommand]
	private async Task AddPlayerAsync(Player player)
	{
		if (Session is null) return;

		var participant = new SessionParticipant
		{
			SessionId = Session.Id,
			PlayerId = player.Id,
			Score = 0,
			Position = Rows.Count,
			IsActive = true
		};

		await _local.SaveParticipantAsync(participant);
		Rows.Add(new ParticipantRow { Participant = participant, Player = player });
	}

	/// <summary>
	/// Drag-to-reorder: only rewrites the Position field for the rows that
	/// moved. Scores are never read or written by this method.
	/// </summary>
	public async Task ReorderAsync(int fromIndex, int toIndex)
	{
		if (fromIndex == toIndex) return;

		var moved = Rows[fromIndex];
		Rows.RemoveAt(fromIndex);
		Rows.Insert(toIndex, moved);

		for (var i = 0; i < Rows.Count; i++)
		{
			Rows[i].Participant.Position = i;
			await _local.SaveParticipantAsync(Rows[i].Participant);
		}
	}
}
