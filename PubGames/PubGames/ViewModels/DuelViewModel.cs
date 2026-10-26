using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;

namespace PubGames.ViewModels;

/// <summary>
/// A tie-break duel to play: why (who's tied for what), who plays it, and how
/// it's scored - like the game, or with the duel's own scoring.
/// </summary>
public sealed record DuelSetup(string Reason, IReadOnlyList<Player> Players, ScoringType Type, ScoringSettings Settings);

/// <summary>One player in a duel.</summary>
public partial class DuelRow : ObservableObject
{
	public required Player Player { get; init; }

	[ObservableProperty]
	private int score;

	/// <summary>Type in the score: what's typed. Manual plus / minus: the amount for the next + or −.</summary>
	[ObservableProperty]
	private string typed = string.Empty;
}

/// <summary>
/// The duel popup: the tied players keep score the way the duel is scored -
/// + and − by a step, + and − by a typed amount, or typing in everyone's final
/// score - and confirm it. The caller then works out the new result.
/// </summary>
public partial class DuelViewModel : ObservableObject
{
	public DuelViewModel(DuelSetup setup)
	{
		Setup = setup;
		Rules = ScoringRules.For(setup.Type, setup.Settings);
		foreach (var player in setup.Players)
			Rows.Add(new DuelRow { Player = player, Score = Rules.StartScore });
	}

	public DuelSetup Setup { get; }

	public ScoringRules Rules { get; }

	public ObservableCollection<DuelRow> Rows { get; } = new();

	public string Reason => Setup.Reason;

	/// <summary>Type in the score: one box per player, no + and −.</summary>
	public bool IsTyped => Setup.Type == ScoringType.TypedScore;

	/// <summary>Manual plus / minus: an amount box next to + and −.</summary>
	public bool IsManual => Rules.ManualAmounts is not null;

	public bool HasButtons => !IsTyped;

	/// <summary>How to keep score in this duel, and which score wins it.</summary>
	public string Explanation => $"{Rules.Explanation} The {(Setup.Settings.LowestWins ? "lowest" : "highest")} score wins the duel.";

	public string MinusLabel => Rules.MinusLabel;

	public string PlusLabel => Rules.PlusLabel;

	public string AmountPlaceholder => Rules.ManualAmounts?.ExampleAmount()?.ToString(CultureInfo.CurrentCulture) ?? "0";

	[ObservableProperty]
	private string message = string.Empty;

	/// <summary>+ (1) or − (-1): by the step, or by the typed amount within the duel's amount rules.</summary>
	public void Adjust(DuelRow row, int direction)
	{
		Message = string.Empty;
		var delta = direction > 0 ? Rules.PlusStep : Rules.MinusStep ?? -Rules.PlusStep;
		if (Rules.ManualAmounts is { } manual)
		{
			if (ReadNumber(row.Typed) is not { } amount)
			{
				Message = $"{row.Player.Name}: type an amount first. {manual.AllowedAmountsHint()}";
				return;
			}
			if (!manual.AllowsAmount(amount))
			{
				Message = $"{row.Player.Name}: {amount} isn't allowed. {manual.AllowedAmountsHint()}";
				return;
			}
			delta = direction > 0 ? amount : -amount;
			row.Typed = string.Empty;
		}
		row.Score = Rules.Clamp(row.Score + delta);
	}

	/// <summary>
	/// Everyone's duel score by player id, or null with Message saying what's
	/// missing (Type in the score: a box that isn't a whole number).
	/// </summary>
	public Dictionary<string, int>? Scores()
	{
		Message = string.Empty;
		if (IsTyped)
		{
			foreach (var row in Rows)
			{
				if (ReadNumber(row.Typed) is not { } typed)
				{
					Message = $"Type in {row.Player.Name}'s score as a whole number.";
					return null;
				}
				row.Score = typed;
			}
		}
		return Rows.ToDictionary(r => r.Player.Id, r => r.Score);
	}

	/// <summary>"Jan: 3\nPiet: 1", for asking whether the score is right.</summary>
	public string ScoreLines => string.Join("\n", Rows.Select(r => $"{r.Player.Name}: {r.Score}"));

	private static int? ReadNumber(string text) =>
		// Some keyboards type a real minus sign (−) instead of a hyphen.
		int.TryParse(text.Trim().Replace('−', '-'), NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out var value) ? value : null;
}
