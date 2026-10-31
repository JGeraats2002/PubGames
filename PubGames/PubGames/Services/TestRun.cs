using PubGames.Models;
using PubGames.Views;

namespace PubGames.Services;

/// <summary>
/// "Test as player" from the game form: the game as it's filled in now is
/// played the way players will play it, with made-up players ("Player 1",
/// "Player 2", ...). Nothing of it is kept: the copy of the game, the
/// players and the scores belong to an account of their own (AccountId), so
/// they never show up in saved games, stats or the players' lists, and are
/// deleted when the test stops. Each test starts from the form again, so a
/// changed rule counts in the next test straight away.
/// </summary>
public sealed class TestRun(ILocalDatabaseService local)
{
	/// <summary>The account the test's players and sessions belong to; no one signs in with it.</summary>
	public const string AccountId = "test-run";

	/// <summary>The id of the test's copy of the game.</summary>
	public const string GameId = "test-run-game";

	/// <summary>Whether a test is going on: the game-night pages show Stop test.</summary>
	public bool IsActive { get; private set; }

	/// <summary>Play again in a test: going back past the form to start over, which mustn't stop the test.</summary>
	public bool IsRestarting { get; private set; }

	/// <summary>The copy of the game being tested.</summary>
	public PubGame? Game { get; private set; }

	/// <summary>The made-up players, in seat order.</summary>
	public List<Player> Players { get; private set; } = [];

	/// <summary>
	/// Starts a test of `game` (the form's content) with this many players:
	/// earlier test data is cleared first. The copy is kept as deleted, so no
	/// list of games shows it.
	/// </summary>
	public async Task StartAsync(PubGame game, int playerCount)
	{
		await ClearAsync();
		game.Id = GameId;
		game.Status = GameStatus.Deleted;
		await local.SaveGameAsync(game);

		var players = new List<Player>();
		for (var number = 1; number <= playerCount; number++)
		{
			var player = new Player
			{
				Id = $"{AccountId}-player-{number}",
				OwnerAccountId = AccountId,
				Name = L.F("Player {0}", number),
				IsTemporary = true
			};
			await local.SavePlayerAsync(player);
			players.Add(player);
		}

		Game = game;
		Players = players;
		IsActive = true;
	}

	/// <summary>Play again during a test: the scores are cleared and the test starts over at the rules.</summary>
	public async Task RestartAsync()
	{
		await ClearSessionsAsync();
		IsRestarting = true;
		try
		{
			await BackToFormAsync();
			if (Game is { } game)
				await Shell.Current.GoToAsync(nameof(GameRulesPage), new ShellNavigationQueryParameters
				{
					["game"] = game,
					["players"] = Players
				});
		}
		finally
		{
			IsRestarting = false;
		}
	}

	/// <summary>Stops the test: everything it saved is deleted and the game form shows again, as it was.</summary>
	public async Task StopAndReturnAsync()
	{
		await StopAsync();
		await BackToFormAsync();
	}

	/// <summary>Stops the test and deletes what it saved, without going anywhere (the form showed again, or another tab opened).</summary>
	public async Task StopAsync()
	{
		if (!IsActive) return;
		IsActive = false;
		await ClearAsync();
	}

	/// <summary>Deletes the test's games; the copy of the game and the players are overwritten by the next test.</summary>
	private async Task ClearAsync() => await ClearSessionsAsync();

	/// <summary>Deletes every game played in a test, with its scores and results.</summary>
	private async Task ClearSessionsAsync()
	{
		var sessions = await local.GetSessionsAsync(AccountId);
		await local.DeleteSessionsAsync(sessions.Select(s => s.Id));
	}

	/// <summary>Goes back to the game form the test was started from, closing every game-night page on top of it.</summary>
	private static async Task BackToFormAsync()
	{
		var stack = Shell.Current.Navigation.NavigationStack;
		var form = stack.ToList().FindLastIndex(p => p is AdminCreateGamePage);
		if (form < 0)
		{
			await Shell.Current.GoToAsync(AppShell.LibraryRoute);
			return;
		}
		var above = stack.Count - 1 - form;
		if (above > 0)
			await Shell.Current.GoToAsync(string.Join("/", Enumerable.Repeat("..", above)));
	}

	/// <summary>
	/// Adds Stop test to the title bar of a game-night page, shown while a
	/// test is going on, so the test can be stopped from anywhere.
	/// </summary>
	public void AttachStopButton(ContentPage page)
	{
		var stop = new ToolbarItem
		{
			Text = L.T("⏹ Stop test"),
			Order = ToolbarItemOrder.Primary,
			Command = new Command(async () => await StopAndReturnAsync())
		};
		SemanticProperties.SetDescription(stop, L.T("Stop the test and go back to the game form"));
		page.Appearing += (_, _) =>
		{
			var shown = page.ToolbarItems.Contains(stop);
			if (IsActive && !shown)
				page.ToolbarItems.Insert(0, stop);
			else if (!IsActive && shown)
				page.ToolbarItems.Remove(stop);
		};
	}
}
