using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// "Change players" during a game: move players to another seat, replace
/// someone, remove someone, or add a saved or brand-new player. Nobody else's
/// score changes: every player's score lives on their own SessionParticipant.
/// Opened with a "sessionId" navigation parameter.
/// </summary>
public partial class SessionPlayersViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;
	private readonly IAuthService _auth;

	private string _sessionId = string.Empty;

	/// <summary>The game's starting score, for players who join during the game.</summary>
	private int _startScore;

	/// <summary>Everyone who was ever in this game, including removed players (their score is kept for if they rejoin).</summary>
	private List<SessionParticipant> _allParticipants = new();

	/// <summary>Who's playing now, in seat order.</summary>
	public ObservableCollection<ParticipantRow> Rows { get; } = new();

	/// <summary>Saved players who aren't playing right now.</summary>
	public ObservableCollection<Player> AvailablePlayers { get; } = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasAvailablePlayers))]
	private int availableCount;

	public bool HasAvailablePlayers => AvailableCount > 0;

	[ObservableProperty]
	private string newPlayerName = string.Empty;

	/// <summary>Add the new player to "Saved players" too, or only to this game.</summary>
	[ObservableProperty]
	private bool saveNewPlayer = true;

	[ObservableProperty]
	private string statusMessage = string.Empty;

	public SessionPlayersViewModel(ILocalDatabaseService local, IAuthService auth)
	{
		_local = local;
		_auth = auth;
	}

	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("sessionId", out var id) && id is string sessionId)
		{
			_sessionId = sessionId;
			var session = await _local.GetSessionAsync(sessionId);
			var game = session is null ? null : await _local.GetGameAsync(session.GameId);
			_startScore = ScoringRules.For(game).StartScore;
			await LoadAsync();
		}
	}

	private async Task LoadAsync()
	{
		_allParticipants = await _local.GetParticipantsAsync(_sessionId);
		var active = _allParticipants.Where(p => p.IsActive).OrderBy(p => p.Position).ToList();
		var players = (await _local.GetPlayersByIdsAsync(active.Select(p => p.PlayerId))).ToDictionary(p => p.Id);

		Rows.Clear();
		foreach (var participant in active)
			Rows.Add(new ParticipantRow
			{
				Participant = participant,
				Player = players.GetValueOrDefault(participant.PlayerId) ?? new Player { Id = participant.PlayerId, Name = "Unknown player" }
			});

		var playingIds = active.Select(p => p.PlayerId).ToHashSet();
		AvailablePlayers.Clear();
		foreach (var player in (await _local.GetPlayersAsync(_auth.AccountId)).Where(p => !playingIds.Contains(p.Id)))
			AvailablePlayers.Add(player);
		AvailableCount = AvailablePlayers.Count;
	}

	/// <summary>Moves a player one seat up (-1) or down (+1). Only seat positions change.</summary>
	public async Task MoveAsync(ParticipantRow row, int direction)
	{
		var from = Rows.IndexOf(row);
		var to = from + direction;
		if (from < 0 || to < 0 || to >= Rows.Count) return;

		Rows.Move(from, to);
		await SaveSeatsAsync();
	}

	/// <summary>The player leaves the game; their score stays on record and comes back if they rejoin.</summary>
	public async Task RemoveAsync(ParticipantRow row)
	{
		row.Participant.IsActive = false;
		await _local.SaveParticipantAsync(row.Participant);
		Rows.Remove(row);
		await SaveSeatsAsync();
		await LoadAsync();
	}

	/// <summary>Adds a saved player at the last seat. Someone who played earlier in this game gets their old score back.</summary>
	public async Task AddAsync(Player player)
	{
		await SeatAsync(player, seat: Rows.Count, startScore: null);
		await LoadAsync();
		StatusMessage = $"{player.Name} joined the game.";
	}

	/// <summary>Adds someone who isn't a saved player yet; saved to "Saved players" only when SaveNewPlayer is on.</summary>
	public async Task<Player?> CreatePlayerAsync(string name)
	{
		name = name.Trim();
		if (name.Length == 0)
		{
			StatusMessage = "Type the new player's name first.";
			return null;
		}
		if (Rows.Any(r => r.Player.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
		{
			StatusMessage = $"{name} is already playing.";
			return null;
		}

		var player = new Player
		{
			OwnerAccountId = _auth.AccountId,
			Name = name,
			AvatarInitials = Player.InitialsFrom(name),
			IsTemporary = !SaveNewPlayer
		};
		await _local.SavePlayerAsync(player);
		return player;
	}

	public async Task AddNewPlayerAsync()
	{
		if (await CreatePlayerAsync(NewPlayerName) is not { } player) return;
		NewPlayerName = string.Empty;
		await AddAsync(player);
	}

	/// <summary>
	/// Someone else takes this seat, and with it everything that belongs to it:
	/// the score of the subgame being played and the results of earlier
	/// subgames. The leaving player keeps nothing in this game's stats.
	/// </summary>
	public async Task ReplaceAsync(ParticipantRow row, Player replacement)
	{
		var seat = Rows.IndexOf(row);
		var score = row.Participant.Score;
		row.Participant.IsActive = false;
		row.Participant.Score = _startScore;
		await _local.SaveParticipantAsync(row.Participant);
		Rows.Remove(row);

		await _local.TransferRoundResultsAsync(_sessionId, row.Player.Id, replacement.Id);
		await SeatAsync(replacement, seat, startScore: score);
		await LoadAsync();
		StatusMessage = $"{replacement.Name} took {row.Player.Name}'s seat, score and results.";
	}

	/// <summary>
	/// Puts a player on a seat (0-based), shifting later seats down. A null
	/// startScore reuses the score from an earlier stint in this game, if any;
	/// someone new starts at the game's starting score.
	/// </summary>
	private async Task SeatAsync(Player player, int seat, int? startScore)
	{
		var participant = _allParticipants.FirstOrDefault(p => p.PlayerId == player.Id && !p.IsActive)
			?? new SessionParticipant { SessionId = _sessionId, PlayerId = player.Id, Score = _startScore };
		participant.IsActive = true;
		if (startScore is { } score)
			participant.Score = score;

		var row = new ParticipantRow { Participant = participant, Player = player };
		Rows.Insert(Math.Clamp(seat, 0, Rows.Count), row);
		await SaveSeatsAsync();
	}

	/// <summary>Writes each active player's seat from the current order (scores untouched).</summary>
	private async Task SaveSeatsAsync()
	{
		for (var i = 0; i < Rows.Count; i++)
		{
			Rows[i].Participant.Position = i;
			await _local.SaveParticipantAsync(Rows[i].Participant);
			Rows[i].NotifyChanged();
		}
	}
}
