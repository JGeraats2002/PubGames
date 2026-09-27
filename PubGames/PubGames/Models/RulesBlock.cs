using System.Text.RegularExpressions;

namespace PubGames.Models;

/// <summary>
/// One piece of a game's rules for display: either a run of text or an image.
/// Rules are stored as plain text with markdown-style image tags,
/// e.g. "Deal 4 cards.\n![image](fsimg:abc123)\nThen..." - see RulesImageTag.
/// </summary>
public partial record RulesBlock(string? Text, string? ImageRef)
{
	public bool IsText => Text is not null;
	public bool IsImage => ImageRef is not null;

	/// <summary>The tag inserted into the rules text for an uploaded image.</summary>
	public static string RulesImageTag(string imageRef) => $"![image]({imageRef})";

	public static List<RulesBlock> Parse(string rules)
	{
		var blocks = new List<RulesBlock>();
		var position = 0;
		foreach (Match match in ImageTag().Matches(rules))
		{
			AddText(rules[position..match.Index]);
			blocks.Add(new RulesBlock(null, match.Groups[1].Value));
			position = match.Index + match.Length;
		}
		AddText(rules[position..]);
		return blocks;

		void AddText(string text)
		{
			text = text.Trim('\r', '\n');
			if (!string.IsNullOrWhiteSpace(text))
				blocks.Add(new RulesBlock(text, null));
		}
	}

	[GeneratedRegex(@"!\[[^\]]*\]\(([^)\s]+)\)")]
	private static partial Regex ImageTag();
}
