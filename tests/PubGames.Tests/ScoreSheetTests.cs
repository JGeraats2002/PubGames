using PubGames.Models;

namespace PubGames.Tests;

/// <summary>Score sheets: what may be filled in and how the totals and bonuses add up.</summary>
public class ScoreSheetTests
{
	/// <summary>A standard Yatzy sheet, with its lists by name: the fixed rows are ticked off, and the Yatzy bonus needs the Yatzy row.</summary>
	private static (List<ScoreSheetSection> Sheet, ScoreSheetSection Upper, ScoreSheetSection Lower) Yatzy()
	{
		static ScoreSheetRow Upper(string name, int die) => new() { Name = name, Rules = [new(ScoreRuleKind.DivisibleBy, die), new(ScoreRuleKind.Between, die, die * 5)] };
		static ScoreSheetRow Range(string name) => new() { Name = name, Rules = [new(ScoreRuleKind.Between, 5, 30)] };
		static ScoreSheetRow Fixed(string name, int points) => new() { Name = name, FixedScore = points };
		var yatzy = Fixed("Yatzy", 50);
		List<ScoreSheetSection> sheet =
		[
			new()
			{
				Name = "Upper section",
				Rows = [Upper("Ones", 1), Upper("Twos", 2), Upper("Threes", 3), Upper("Fours", 4), Upper("Fives", 5), Upper("Sixes", 6)],
				Bonuses = [new() { Name = "Bonus", Kind = ScoreSheetBonusKind.TotalAtLeast, Threshold = 63, Points = 35 }]
			},
			new()
			{
				Name = "Lower section",
				Rows = [Range("Three of a kind"), Range("Four of a kind"), Fixed("Full house", 25), Fixed("Small straight", 30), Fixed("Large straight", 40), yatzy, Range("Chance")],
				Bonuses = [new() { Name = "Yatzy bonus", Kind = ScoreSheetBonusKind.PerTime, Points = 100, MaxTimes = 2, RequiresRowId = yatzy.Id }]
			}
		];
		return (sheet, sheet[0], sheet[1]);
	}

	[Fact]
	public void Yatzy_example_has_no_problems()
	{
		Assert.Null(ScoreSheet.Problem(Yatzy().Sheet));
	}

	[Fact]
	public void Upper_bonus_comes_at_63_and_not_before()
	{
		var (sheet, upper, _) = Yatzy();
		var cells = new Dictionary<string, int>();
		// 3 of each die: 3 + 6 + 9 + 12 + 15 + 18 = 63.
		for (var die = 1; die <= 6; die++)
			cells[upper.Rows[die - 1].Id] = die * 3;
		Assert.Equal(63, ScoreSheet.RowsTotal(upper, cells));
		Assert.Equal(98, ScoreSheet.SectionTotal(upper, cells));
		Assert.Equal(98, ScoreSheet.Total(sheet, cells));

		cells[upper.Rows[0].Id] = 2; // one point short
		Assert.Equal(62, ScoreSheet.SectionTotal(upper, cells));
	}

	[Fact]
	public void Yatzy_bonus_needs_the_Yatzy_row_and_counts_up_to_twice()
	{
		var (sheet, _, lower) = Yatzy();
		var bonus = lower.Bonuses[0];
		var yatzy = lower.Rows[5];
		var cells = new Dictionary<string, int>();
		Assert.False(ScoreSheet.IsUnlocked(bonus, cells));
		Assert.False(ScoreSheet.CanAddTime(bonus, cells));

		cells[yatzy.Id] = 0; // crossed out: still no bonus
		Assert.False(ScoreSheet.CanAddTime(bonus, cells));

		cells[yatzy.Id] = 50;
		Assert.True(ScoreSheet.CanAddTime(bonus, cells));
		cells[bonus.Id] = 2;
		Assert.False(ScoreSheet.CanAddTime(bonus, cells));
		Assert.Equal(250, ScoreSheet.Total(sheet, cells));

		// Yatzy taken out again: the bonus doesn't count any more.
		cells.Remove(yatzy.Id);
		Assert.Equal(0, ScoreSheet.Total(sheet, cells));
	}

	[Fact]
	public void Fixed_rows_take_their_score_or_0()
	{
		var fullHouse = Yatzy().Lower.Rows[2];
		Assert.Equal("Full house (25)", fullHouse.Title);
		Assert.True(ScoreSheet.Allows(fullHouse, 25));
		Assert.True(ScoreSheet.Allows(fullHouse, 0));
		Assert.False(ScoreSheet.Allows(fullHouse, 24));
		Assert.False(ScoreSheet.Allows(fullHouse with { AllowsZero = false }, 0));
	}

	[Fact]
	public void Zero_can_be_switched_off()
	{
		var throws = new ScoreSheetRow { Name = "Throws", AllowsZero = false };
		Assert.False(ScoreSheet.Allows(throws, 0));
		Assert.True(ScoreSheet.Allows(throws, 3));
		var limited = throws with { Rules = [new(ScoreRuleKind.Between, 0, 6)] };
		Assert.False(ScoreSheet.Allows(limited, 0)); // even when the rules would allow it
		Assert.True(ScoreSheet.Allows(limited, 6));
	}

	[Fact]
	public void Rows_saved_before_the_new_options_allow_0()
	{
		var row = System.Text.Json.JsonSerializer.Deserialize<ScoreSheetRow>("""{"Id":"a1b2c3d4","Name":"Threes","Rules":[]}""")!;
		Assert.True(row.AllowsZero);
		Assert.Null(row.FixedScore);
	}

	[Fact]
	public void Rows_follow_their_rules_and_always_allow_0()
	{
		var (_, upper, lower) = Yatzy();
		var threes = upper.Rows[2];
		Assert.True(ScoreSheet.Allows(threes, 9));
		Assert.True(ScoreSheet.Allows(threes, 0));
		Assert.False(ScoreSheet.Allows(threes, 10));
		Assert.False(ScoreSheet.Allows(threes, 18));

		Assert.True(ScoreSheet.Allows(lower.Rows[0], 17));
		Assert.False(ScoreSheet.Allows(lower.Rows[0], 31));
		Assert.True(ScoreSheet.Allows(new ScoreSheetRow { Name = "Free" }, 1234));
	}

	[Fact]
	public void Sheet_is_complete_when_every_row_is_filled_in()
	{
		var (sheet, _, lower) = Yatzy();
		var cells = sheet.SelectMany(s => s.Rows).ToDictionary(r => r.Id, _ => 0);
		Assert.True(ScoreSheet.IsComplete(sheet, cells));
		cells.Remove(lower.Rows[^1].Id);
		Assert.False(ScoreSheet.IsComplete(sheet, cells));
	}

	[Fact]
	public void Problems_name_what_is_missing()
	{
		Assert.NotNull(ScoreSheet.Problem([]));
		Assert.NotNull(ScoreSheet.Problem([new ScoreSheetSection { Name = "", Rows = [new() { Name = "A" }] }]));
		Assert.NotNull(ScoreSheet.Problem([new ScoreSheetSection { Name = "Top" }]));
		Assert.NotNull(ScoreSheet.Problem([new ScoreSheetSection { Name = "Top", Rows = [new() { Name = " " }] }]));
		Assert.NotNull(ScoreSheet.Problem([new ScoreSheetSection
		{
			Name = "Top", Rows = [new() { Name = "A" }],
			Bonuses = [new() { Name = "B", Kind = ScoreSheetBonusKind.TotalAtLeast, Points = 35 }]
		}]));
		Assert.NotNull(ScoreSheet.Problem([new ScoreSheetSection { Name = "Top", Rows = [new() { Name = "A", FixedScore = 0 }] }]));
		Assert.NotNull(ScoreSheet.Problem([new ScoreSheetSection
		{
			Name = "Top", Rows = [new() { Name = "A" }],
			Bonuses = [new() { Name = "B", Kind = ScoreSheetBonusKind.PerTime, Points = 100, RequiresRowId = "gone0000" }]
		}]));
		Assert.Null(ScoreSheet.Problem([new ScoreSheetSection { Name = "Top", Rows = [new() { Name = "A" }] }]));
	}

	[Fact]
	public void Cells_survive_saving_and_empty_is_null()
	{
		var cells = new Dictionary<string, int> { ["a1b2c3d4"] = 12, ["e5f6a7b8"] = -3 };
		Assert.Equal(cells, ScoreSheet.ReadCells(ScoreSheet.WriteCells(cells)));
		Assert.Null(ScoreSheet.WriteCells(new Dictionary<string, int>()));
		Assert.Empty(ScoreSheet.ReadCells(null));
		Assert.Empty(ScoreSheet.ReadCells("not json"));
	}

	[Fact]
	public void Sheet_survives_its_JSON()
	{
		var sheet = Yatzy().Sheet;
		var json = System.Text.Json.JsonSerializer.Serialize(sheet);
		var back = System.Text.Json.JsonSerializer.Deserialize<List<ScoreSheetSection>>(json)!;
		Assert.Equal(json, System.Text.Json.JsonSerializer.Serialize(back));
		Assert.Equal(sheet[0].Rows[2].Id, back[0].Rows[2].Id);
		Assert.Equal(sheet[0].Rows[2].Rules, back[0].Rows[2].Rules);
	}
}
