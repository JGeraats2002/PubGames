using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;

namespace PubGames.ViewModels;

/// <summary>
/// A tie-break duel to play: why (who's tied for what), who plays it, how
/// it's scored and how it ends (Settings.EffectiveDuelEnd), and what it has
/// to decide: only who's best, only who's worst (WorstCounts), or the whole
/// order of the players (WholeOrder).
/// </summary>
public sealed record DuelSetup(string Reason, IReadOnlyList<Player> Players, ScoringType Type, ScoringSettings Settings,
	bool WholeOrder = false, bool WorstCounts = false);

/// <summary>One player in a duel.</summary>
public partial class DuelRow : ObservableObject
{
	public required Player Player { get; init; }

	[ObservableProperty]
	private int score;

	/// <summary>Type in the score: the points of this turn. Manual plus / minus: the amount for + or −.</summary>
	[ObservableProperty]
	private string typed = string.Empty;

	/// <summary>It's this player's turn: only they can enter a score.</summary>
	[ObservableProperty]
	private bool isTurn;

	/// <summary>Best of: the rounds this player has won.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(RoundsWonText))]
	private int roundsWon;

	public string RoundsWonText => RoundsWon == 1 ? "1 round won" : $"{RoundsWon} rounds won";
}

/// <summary>
/// The duel popup. The players take turns in seat order, keeping score the
/// way the duel is scored - + and − by a step (then Turn done), + or − by a
/// typed amount (that ends the turn), or typing in the points of the turn.
/// The duel ends by itself, the way it's set up:
/// - a number of turns each: then the best score wins; if that's still tied
///   (or the lead is too small) everyone plays another turn;
/// - first to a score: at the end of a round in which someone reached it,
///   the best score wins - everyone has had as many turns; tied or too small
///   a lead: another round;
/// - best of: each round's best player wins the round; the first to win more
///   than half of the rounds wins.
/// Every turn can be taken back with Undo.
/// </summary>
public partial class DuelViewModel : ObservableObject
{
	/// <summary>Everything that changes during the duel, to go back to with Undo.</summary>
	private sealed record State(int[] Scores, int[] RoundsWon, int Turn, int Round)
	{
		public bool SameAs(State other) => Turn == other.Turn && Round == other.Round
			&& Scores.SequenceEqual(other.Scores) && RoundsWon.SequenceEqual(other.RoundsWon);
	}

	/// <summary>The duel as it was when each earlier turn began, newest on top.</summary>
	private readonly Stack<State> _history = new();

	/// <summary>The duel as it was when the current turn began.</summary>
	private State _turnStart;

	/// <summary>Which row's turn it is.</summary>
	private int _turn;

	public DuelViewModel(DuelSetup setup)
	{
		Setup = setup;
		Rules = ScoringRules.For(setup.Type, setup.Settings);
		foreach (var player in setup.Players)
			Rows.Add(new DuelRow { Player = player, Score = Rules.StartScore });
		ShowTurn();
		_turnStart = Now();
	}

	public DuelSetup Setup { get; }

	public ScoringRules Rules { get; }

	private ScoringSettings Settings => Setup.Settings;

	public ObservableCollection<DuelRow> Rows { get; } = new();

	public string Reason => Setup.Reason;

	/// <summary>Type in the score: a box for the points of the turn, no + and −.</summary>
	public bool IsTyped => Setup.Type == ScoringType.TypedScore;

	/// <summary>Manual plus / minus: an amount box next to + and −.</summary>
	public bool IsManual => Rules.ManualAmounts is not null;

	public bool HasButtons => !IsTyped;

	/// <summary>A box to type in: the turn's points, or the amount for + or −.</summary>
	public bool HasBox => IsTyped || IsManual;

	/// <summary>Plus / minus and Type in the score end a turn with Turn done; a typed amount ends it by itself.</summary>
	public bool HasTurnDone => !IsManual;

	public bool IsBestOf => Settings.EffectiveDuelEnd == DuelEnd.BestOf;

	private int Margin => Math.Max(1, Settings.DuelMargin ?? 1);

	/// <summary>How to keep score in this duel, which score wins it, and how it ends.</summary>
	public string Explanation =>
		$"{Rules.Explanation} The {(Settings.LowestWins ? "lowest" : "highest")} score wins. Take turns in the order below. "
		+ char.ToUpperInvariant(Settings.DuelEndSummary[0]) + Settings.DuelEndSummary[1..] + ".";

	public string MinusLabel => Rules.MinusLabel;

	public string PlusLabel => Rules.PlusLabel;

	public string AmountPlaceholder => "0";

	[ObservableProperty]
	private string message = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _messageTimer = new();

	partial void OnMessageChanged(string value) => _messageTimer.Shown(value, () => Message, v => Message = v);

	/// <summary>"Round 2 of 3 · Jan's turn".</summary>
	[ObservableProperty]
	private string status = string.Empty;

	[ObservableProperty]
	private int round = 1;

	[ObservableProperty]
	private bool canUndo;

	/// <summary>
	/// + (1) or − (-1) for the player whose turn it is: by the step, or by the
	/// typed amount within the duel's amount rules (which ends the turn). True
	/// when that ended the duel.
	/// </summary>
	public bool Adjust(DuelRow row, int direction)
	{
		Message = string.Empty;
		if (!row.IsTurn)
		{
			Message = $"It's {Rows[_turn].Player.Name}'s turn.";
			return false;
		}

		var delta = direction > 0 ? Rules.PlusStep : Rules.MinusStep ?? -Rules.PlusStep;
		if (Rules.ManualAmounts is { } manual)
		{
			if (ReadNumber(row.Typed) is not { } amount)
			{
				Message = $"{row.Player.Name}: type an amount first. {manual.AllowedAmountsHint()}";
				return false;
			}
			if (!manual.AllowsAmount(amount) && amount != 0)
			{
				Message = $"{row.Player.Name}: {amount} isn't allowed. {manual.AllowedAmountsHint()}";
				return false;
			}
			row.Typed = string.Empty;
			row.Score = Rules.Clamp(row.Score + (direction > 0 ? amount : -amount));
			return EndTurn();
		}

		row.Score = Rules.Clamp(row.Score + delta);
		CanUndo = true;
		return false;
	}

	/// <summary>Turn done: Type in the score adds the typed points (empty is 0). True when that ended the duel.</summary>
	public bool TurnDone()
	{
		Message = string.Empty;
		var row = Rows[_turn];
		if (IsTyped)
		{
			var text = row.Typed.Trim();
			var points = text.Length == 0 ? 0 : ReadNumber(text);
			if (points is null)
			{
				Message = $"Type {row.Player.Name}'s points as a whole number.";
				return false;
			}
			row.Score += points.Value;
			row.Typed = string.Empty;
		}
		return EndTurn();
	}

	/// <summary>The turn passes on; after the last player the round is checked. True when the duel is over.</summary>
	private bool EndTurn()
	{
		_history.Push(_turnStart);
		var over = false;
		_turn++;
		if (_turn == Rows.Count)
		{
			over = EndRound();
			if (!over)
			{
				_turn = 0;
				Round++;
			}
		}
		if (!over)
			ShowTurn();
		else
			foreach (var row in Rows)
				row.IsTurn = false;
		_turnStart = Now();
		CanUndo = true;
		return over;
	}

	/// <summary>Everyone has played this round: whether the duel is decided now.</summary>
	private bool EndRound()
	{
		var scores = Rows.Select(r => r.Score).ToList();
		switch (Settings.EffectiveDuelEnd)
		{
			case DuelEnd.BestOf:
			{
				var best = Settings.LowestWins ? scores.Min() : scores.Max();
				var winners = Rows.Where(r => r.Score == best).ToList();
				if (winners.Count == 1)
				{
					winners[0].RoundsWon++;
					Message = $"{winners[0].Player.Name} wins round {Round}.";
				}
				else
					Message = $"Round {Round} is tied: nobody gets the round.";
				// Every round starts again from the starting score.
				foreach (var row in Rows)
					row.Score = Rules.StartScore;
				return Rows.Any(r => r.RoundsWon >= Settings.DuelRoundsToWin);
			}

			case DuelEnd.Score:
				if (!scores.Any(Settings.ReachesEnd)) return false;
				if (IsDecided(scores)) return true;
				Message = "Not decided yet: everyone plays another round.";
				return false;

			default:
				if (Round < Settings.DuelTurns) return false;
				if (IsDecided(scores)) return true;
				Message = Margin > 1 ? $"Not decided yet (a lead of {Margin} is needed): everyone plays another turn." : "Still tied: everyone plays another turn.";
				return false;
		}
	}

	/// <summary>
	/// Whether the scores decide what the duel is for, by at least the lead
	/// needed: who's best, who's worst, or every place.
	/// </summary>
	private bool IsDecided(List<int> scores)
	{
		var order = scores.OrderBy(s => Settings.LowestWins ? s : -s).ToList();
		bool Apart(int better, int worse) => Math.Abs(order[better] - order[worse]) >= Margin;

		if (order.Count < 2) return true;
		if (Setup.WholeOrder)
			return Enumerable.Range(0, order.Count - 1).All(i => Apart(i, i + 1));
		return Setup.WorstCounts ? Apart(order.Count - 2, order.Count - 1) : Apart(0, 1);
	}

	/// <summary>Takes back what was entered in this turn, or else the turn before it.</summary>
	public void Undo()
	{
		Message = string.Empty;
		if (Now().SameAs(_turnStart) && _history.Count > 0)
			_turnStart = _history.Pop();
		Restore(_turnStart);
		CanUndo = _history.Count > 0;
	}

	private State Now() => new(Rows.Select(r => r.Score).ToArray(), Rows.Select(r => r.RoundsWon).ToArray(), _turn, Round);

	private void Restore(State state)
	{
		for (var i = 0; i < Rows.Count; i++)
		{
			Rows[i].Score = state.Scores[i];
			Rows[i].RoundsWon = state.RoundsWon[i];
			Rows[i].Typed = string.Empty;
		}
		_turn = state.Turn;
		Round = state.Round;
		ShowTurn();
	}

	private void ShowTurn()
	{
		for (var i = 0; i < Rows.Count; i++)
			Rows[i].IsTurn = i == _turn;
		var round = Settings.EffectiveDuelEnd switch
		{
			DuelEnd.Turns when Round <= Settings.DuelTurns => $"Turn {Round} of {Settings.DuelTurns}",
			DuelEnd.Turns => $"Extra turn {Round - Settings.DuelTurns}",
			DuelEnd.BestOf => $"Round {Round} · first to {Settings.DuelRoundsToWin} {(Settings.DuelRoundsToWin == 1 ? "round" : "rounds")} wins",
			_ => $"Round {Round}"
		};
		Status = $"{round} · {Rows[_turn].Player.Name}'s turn";
	}

	/// <summary>
	/// Everyone's duel result by player id, as the game works out the order:
	/// the duel score, or with best of the rounds won (turned around when the
	/// lowest score wins, so the most rounds still comes first).
	/// </summary>
	public Dictionary<string, int> Scores() => Rows.ToDictionary(r => r.Player.Id, r =>
		IsBestOf ? (Settings.LowestWins ? -r.RoundsWon : r.RoundsWon) : r.Score);

	/// <summary>"Jan: 3\nPiet: 1" (or rounds won), for checking the result.</summary>
	public string ScoreLines => string.Join("\n", Rows.Select(r => $"{r.Player.Name}: {(IsBestOf ? r.RoundsWonText : r.Score.ToString(CultureInfo.CurrentCulture))}"));

	private static int? ReadNumber(string text) =>
		// Some keyboards type a real minus sign (−) instead of a hyphen.
		int.TryParse(text.Trim().Replace('−', '-'), NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out var value) ? value : null;
}
