using System.Text.Json;
using System.Text.Json.Serialization;

namespace PubGames.Models;

/// <summary>
/// One list on a score sheet (Yatzy-style scoring), e.g. "Upper section".
/// Its rows are filled in per player; its bonuses are added on top.
/// </summary>
public sealed record ScoreSheetSection
{
	/// <summary>Stays the same when the list is renamed, so filled-in scores keep their place.</summary>
	public string Id { get; init; } = ScoreSheet.NewId();

	/// <summary>The list's name, as the admin typed it.</summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>The rows players fill in, top to bottom.</summary>
	public List<ScoreSheetRow> Rows { get; init; } = [];

	/// <summary>The bonuses of this list, under its rows.</summary>
	public List<ScoreSheetBonus> Bonuses { get; init; } = [];
}

/// <summary>One row on a score sheet, e.g. "Threes": a name and the rules a score in it must meet.</summary>
public sealed record ScoreSheetRow
{
	/// <summary>Stays the same when the row is renamed, so filled-in scores keep their place.</summary>
	public string Id { get; init; } = ScoreSheet.NewId();

	/// <summary>The row's name, as the admin typed it.</summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>Every score in this row must meet these (AND / OR, like the amount rules). No rules: any score.</summary>
	public List<ScoreRule> Rules { get; init; } = [];

	/// <summary>
	/// A fixed score (Yatzy's Full house: 25): players tick the row instead of
	/// typing, and the rules don't apply. Null: the score is typed in.
	/// </summary>
	public int? FixedScore { get; init; }

	/// <summary>
	/// Whether 0 may be filled in, to cross the row out when a throw failed.
	/// Off for games where 0 means nothing, like counting the throws needed.
	/// </summary>
	public bool AllowsZero { get; init; } = true;

	/// <summary>"Full house (25)" for a fixed score, otherwise just the name.</summary>
	[JsonIgnore]
	public string Title => FixedScore is { } score ? $"{Name} ({score})" : Name;
}

/// <summary>Stored as a number, so new kinds go at the end.</summary>
public enum ScoreSheetBonusKind
{
	/// <summary>Added by itself once the list's rows add up to at least Threshold (Yatzy: 63 or more gives 35).</summary>
	TotalAtLeast,

	/// <summary>Filled in by the players, Points per time, at most MaxTimes times (Yatzy: 100 per extra Yatzy).</summary>
	PerTime
}

/// <summary>A bonus in a list of a score sheet.</summary>
public sealed record ScoreSheetBonus
{
	/// <summary>Stays the same when the bonus is renamed, so filled-in bonuses keep their place.</summary>
	public string Id { get; init; } = ScoreSheet.NewId();

	/// <summary>The bonus's name, as the admin typed it.</summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>Automatic at a total, or filled in per time.</summary>
	public ScoreSheetBonusKind Kind { get; init; }

	/// <summary>TotalAtLeast: the total of the list's rows that gives the bonus.</summary>
	public int? Threshold { get; init; }

	/// <summary>The bonus points: once (TotalAtLeast) or per time (PerTime).</summary>
	public int Points { get; init; }

	/// <summary>PerTime: how many times it can be scored at most; null is no limit.</summary>
	public int? MaxTimes { get; init; }

	/// <summary>
	/// The bonus can only be scored once this row has a score other than 0
	/// (Yatzy: the Yatzy bonus needs the Yatzy row). Null: always possible.
	/// </summary>
	public string? RequiresRowId { get; init; }
}

/// <summary>
/// How a score sheet adds up and what may be filled in. A player's filled-in
/// cells are kept as row (or bonus) id → value; for a per-time bonus the value
/// is the number of times. Free of MAUI, so it can be tested.
/// </summary>
public static class ScoreSheet
{
	/// <summary>A short id for a new list, row or bonus.</summary>
	public static string NewId() => Guid.NewGuid().ToString("N")[..8];

	/// <summary>A player's filled-in cells from their saved text; empty when there's nothing (yet).</summary>
	public static Dictionary<string, int> ReadCells(string? text)
	{
		if (string.IsNullOrWhiteSpace(text)) return [];
		try
		{
			return JsonSerializer.Deserialize<Dictionary<string, int>>(text) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	/// <summary>A player's cells as text for saving; null when nothing is filled in.</summary>
	public static string? WriteCells(IReadOnlyDictionary<string, int> cells) =>
		cells.Count == 0 ? null : JsonSerializer.Serialize(cells);

	/// <summary>The rows of a list added up (empty cells count as 0).</summary>
	public static int RowsTotal(ScoreSheetSection section, IReadOnlyDictionary<string, int> cells) =>
		section.Rows.Sum(row => cells.GetValueOrDefault(row.Id));

	/// <summary>Whether a bonus can be scored now: always, or once the row it needs has a score other than 0.</summary>
	public static bool IsUnlocked(ScoreSheetBonus bonus, IReadOnlyDictionary<string, int> cells) =>
		bonus.RequiresRowId is not { } rowId || cells.GetValueOrDefault(rowId) != 0;

	/// <summary>
	/// What a bonus is worth now: automatic once the rows reach the threshold, or points × times.
	/// Nothing while the row it needs isn't scored.
	/// </summary>
	public static int BonusPoints(ScoreSheetBonus bonus, ScoreSheetSection section, IReadOnlyDictionary<string, int> cells) =>
		!IsUnlocked(bonus, cells) ? 0 : bonus.Kind switch
		{
			ScoreSheetBonusKind.TotalAtLeast => bonus.Threshold is { } threshold && RowsTotal(section, cells) >= threshold ? bonus.Points : 0,
			_ => cells.GetValueOrDefault(bonus.Id) * bonus.Points
		};

	/// <summary>Per-time bonus: whether + may add another time (unlocked and below the most times).</summary>
	public static bool CanAddTime(ScoreSheetBonus bonus, IReadOnlyDictionary<string, int> cells) =>
		IsUnlocked(bonus, cells) && (bonus.MaxTimes is not { } max || cells.GetValueOrDefault(bonus.Id) < max);

	/// <summary>Every row of the sheet, in every list, top to bottom.</summary>
	public static IEnumerable<ScoreSheetRow> AllRows(IReadOnlyList<ScoreSheetSection> sections) =>
		sections.SelectMany(section => section.Rows);

	/// <summary>A list's total: its rows and its bonuses.</summary>
	public static int SectionTotal(ScoreSheetSection section, IReadOnlyDictionary<string, int> cells) =>
		RowsTotal(section, cells) + section.Bonuses.Sum(bonus => BonusPoints(bonus, section, cells));

	/// <summary>The whole sheet's total: every list added up.</summary>
	public static int Total(IReadOnlyList<ScoreSheetSection> sections, IReadOnlyDictionary<string, int> cells) =>
		sections.Sum(section => SectionTotal(section, cells));

	/// <summary>
	/// Whether a score may go in this row: the fixed score, or one the rules
	/// allow. 0 (crossing the row out) only when the row allows it.
	/// </summary>
	public static bool Allows(ScoreSheetRow row, int score) =>
		score == 0 ? row.AllowsZero
		: row.FixedScore is { } fixedScore ? score == fixedScore
		: AmountRuleSet.Allows(row.Rules, score);

	/// <summary>What may be filled in, for the message when a score isn't allowed.</summary>
	public static string Hint(ScoreSheetRow row)
	{
		var allowed = row.Rules.Count == 0 ? null : AmountRuleSet.Describe(row.Rules);
		return (allowed, row.AllowsZero) switch
		{
			(null, true) => L.T("Any score."),
			(null, false) => L.T("Any score except 0."),
			(_, true) => L.F("The score must be {0}, or 0 to cross it out.", allowed),
			_ => L.F("The score must be {0}.", allowed)
		};
	}

	/// <summary>Whether every row and every per-time bonus has something filled in.</summary>
	public static bool IsComplete(IReadOnlyList<ScoreSheetSection> sections, IReadOnlyDictionary<string, int> cells) =>
		sections.All(section => section.Rows.All(row => cells.ContainsKey(row.Id)));

	/// <summary>"2 lists, 15 rows, 2 bonuses", for admins reviewing a game.</summary>
	public static string Summary(IReadOnlyList<ScoreSheetSection> sections)
	{
		var rows = sections.Sum(section => section.Rows.Count);
		var bonuses = sections.Sum(section => section.Bonuses.Count);
		return L.F("{0} list(s), {1} row(s), {2} bonus(es)", sections.Count, rows, bonuses);
	}

	/// <summary>"at least 63 gives 35" or "100 per time, at most 2 times".</summary>
	public static string Describe(ScoreSheetBonus bonus) => bonus.Kind switch
	{
		ScoreSheetBonusKind.TotalAtLeast => L.F("{0} or more gives {1}", bonus.Threshold, bonus.Points),
		_ => bonus.MaxTimes is { } max
			? L.F("{0} per time, at most {1} times", bonus.Points, max)
			: L.F("{0} per time", bonus.Points)
	};

	/// <summary>What has to be fixed before the sheet can be used, or null when it's fine.</summary>
	public static string? Problem(IReadOnlyList<ScoreSheetSection> sections)
	{
		if (sections.Count == 0)
			return L.T("Add at least one list to the score sheet.");
		for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
		{
			var section = sections[sectionIndex];
			var list = section.Name.Trim().Length > 0 ? section.Name.Trim() : L.F("List {0}", sectionIndex + 1);
			if (section.Name.Trim().Length == 0)
				return L.F("List {0}: give it a name.", sectionIndex + 1);
			if (section.Rows.Count == 0)
				return L.F("{0}: add at least one row.", list);
			for (var rowIndex = 0; rowIndex < section.Rows.Count; rowIndex++)
			{
				var row = section.Rows[rowIndex];
				if (row.Name.Trim().Length == 0)
					return L.F("{0}, row {1}: give it a name.", list, rowIndex + 1);
				if (row.FixedScore is 0)
					return L.F("{0}, {1}: the fixed score can't be 0.", list, row.Name.Trim());
				if (row.FixedScore is not null)
					continue; // a ticked row has no rules
				for (var ruleIndex = 0; ruleIndex < row.Rules.Count; ruleIndex++)
					if (row.Rules[ruleIndex].Problem(ruleIndex + 1) is { } ruleProblem)
						return $"{list}, {row.Name.Trim()}: {ruleProblem}";
			}
			for (var bonusIndex = 0; bonusIndex < section.Bonuses.Count; bonusIndex++)
			{
				var bonus = section.Bonuses[bonusIndex];
				var name = bonus.Name.Trim().Length > 0 ? bonus.Name.Trim() : L.F("bonus {0}", bonusIndex + 1);
				if (bonus.Name.Trim().Length == 0)
					return L.F("{0}, bonus {1}: give it a name.", list, bonusIndex + 1);
				if (bonus.Points == 0)
					return L.F("{0}, {1}: the bonus needs points.", list, name);
				if (bonus.Kind == ScoreSheetBonusKind.TotalAtLeast && bonus.Threshold is null)
					return L.F("{0}, {1}: fill in the total that gives the bonus.", list, name);
				if (bonus.Kind == ScoreSheetBonusKind.PerTime && bonus.MaxTimes is < 1)
					return L.F("{0}, {1}: the most times must be 1 or more.", list, name);
				if (bonus.RequiresRowId is { } rowId && !AllRows(sections).Any(r => r.Id == rowId))
					return L.F("{0}, {1}: choose the row the bonus needs again.", list, name);
			}
		}
		return null;
	}
}
