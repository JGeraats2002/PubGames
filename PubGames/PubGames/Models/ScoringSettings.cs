using System.Globalization;
using System.Text.Json;

namespace PubGames.Models;

/// <summary>
/// What an admin sets up for a game's scoring. ResultMode applies to every
/// scoring type; Step to WarningScore are Plus / minus only. (How many
/// subgames to play is up to the players: see GameSession.SubgameCount.) Stored on
/// the game as JSON text, so later scoring types can add their own settings
/// without new database columns.
/// </summary>
public sealed record ScoringSettings
{
	/// <summary>How much one tap on + or − adds or subtracts.</summary>
	public int Step { get; init; } = 1;

	/// <summary>Every player's score when the game starts (and when they join later).</summary>
	public int StartScore { get; init; }

	/// <summary>Scores never go above this; null means no maximum.</summary>
	public int? MaxScore { get; init; }

	/// <summary>Scores never go below this; null means no minimum.</summary>
	public int? MinScore { get; init; }

	/// <summary>A player's card is highlighted once their score reaches this; null means no warning.</summary>
	public int? WarningScore { get; init; }

	/// <summary>How the result of each subgame is noted: winner(s), a ranking or loser(s).</summary>
	public ResultMode ResultMode { get; init; } = ResultMode.Winner;

	/// <summary>
	/// False: the highest score is best in a subgame; true: the lowest. Used to
	/// pre-select the winner(s), loser(s) or ranking on the result screen.
	/// In loser mode "best" means "not the loser": true = the highest score loses.
	/// </summary>
	public bool LowestWins { get; init; }

	/// <summary>The two choices for LowestWins (false, true), worded for the result type.</summary>
	public static List<string> ScoreDirectionOptions(ResultMode mode) => mode switch
	{
		ResultMode.Loser => ["Lowest score loses", "Highest score loses"],
		ResultMode.Ranking => ["Highest score is 1st", "Lowest score is 1st"],
		_ => ["Highest score wins", "Lowest score wins"]
	};

	public string ScoreDirectionLabel => ScoreDirectionOptions(ResultMode)[LowestWins ? 1 : 0].ToLowerInvariant();

	public static ScoringSettings Default { get; } = new();

	/// <summary>"winner(s) only", "ranking (1st, 2nd, 3rd...)" or "loser(s) only".</summary>
	public string ResultSummary => $"{ResultModeOption.LabelFor(ResultMode).ToLowerInvariant()} · {ScoreDirectionLabel}";

	/// <summary>"steps of 1 · start at 0 · max 10 · warning at 9", for admins reviewing a game.</summary>
	public string Summary
	{
		get
		{
			var parts = new List<string> { $"steps of {Step}", $"start at {StartScore}" };
			if (MinScore is { } min) parts.Add($"min {min}");
			if (MaxScore is { } max) parts.Add($"max {max}");
			if (WarningScore is { } warning) parts.Add($"warning at {warning}");
			parts.Add(ResultSummary);
			return string.Join(" · ", parts);
		}
	}

	/// <summary>Why these settings can't be used, or null when they're fine.</summary>
	public string? Problem()
	{
		if (Step < 1)
			return "The step must be at least 1.";
		if (MinScore is { } min && MaxScore is { } max && min >= max)
			return "The minimum score must be lower than the maximum score.";
		if (MaxScore is { } maxScore && StartScore > maxScore)
			return "The starting score can't be higher than the maximum score.";
		if (MinScore is { } minScore && StartScore < minScore)
			return "The starting score can't be lower than the minimum score.";
		if (WarningScore is { } warning)
		{
			if (warning == StartScore)
				return "The warning can't be the same as the starting score - everyone would start with a warning.";
			if (MaxScore is { } m && warning > m || MinScore is { } n && warning < n)
				return "The warning must be between the minimum and maximum score.";
		}
		return null;
	}

	public string ToJson() => JsonSerializer.Serialize(this);

	/// <summary>Games saved before scoring settings existed have none: they get the defaults.</summary>
	public static ScoringSettings FromJson(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return Default;
		try
		{
			return JsonSerializer.Deserialize<ScoringSettings>(json) ?? Default;
		}
		catch (JsonException)
		{
			return Default;
		}
	}

	/// <summary>As a Firestore map field.</summary>
	public Dictionary<string, object?> ToMap() => new()
	{
		["step"] = Step,
		["startScore"] = StartScore,
		["maxScore"] = MaxScore,
		["minScore"] = MinScore,
		["warningScore"] = WarningScore,
		["resultMode"] = ResultMode.ToString(),
		["lowestWins"] = LowestWins
	};

	public static ScoringSettings FromMap(object? value)
	{
		if (value is not IDictionary<string, object?> map) return Default;

		int? Number(string key) => map.TryGetValue(key, out var v) && v is not null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : null;
		return new ScoringSettings
		{
			Step = Number("step") ?? 1,
			StartScore = Number("startScore") ?? 0,
			MaxScore = Number("maxScore"),
			MinScore = Number("minScore"),
			WarningScore = Number("warningScore"),
			ResultMode = map.TryGetValue("resultMode", out var mode) && Enum.TryParse<ResultMode>(mode as string, out var m) ? m : ResultMode.Winner,
			LowestWins = map.TryGetValue("lowestWins", out var lowest) && lowest is true
		};
	}
}
