using System.Text.RegularExpressions;

namespace PubGames.Models;

/// <summary>
/// Rules in sentences, the same way the cloud cuts them (functions/shared.js, segmentize and joinSegments):
/// pictures stand alone, text is cut into paragraphs, lines and sentences, and each piece remembers what stood
/// in front of it so the rules can be put together again exactly.
/// </summary>
public static partial class Sentences
{
	/// <summary>The rules in segments (original only: Text is the same as Src).</summary>
	public static List<TranslationSegment> Of(string rulesText)
	{
		var segments = new List<TranslationSegment>();
		var text = rulesText ?? string.Empty;
		var position = 0;
		foreach (Match picture in PictureTag().Matches(text))
		{
			AddText(text[position..picture.Index]);
			segments.Add(new TranslationSegment { Src = picture.Value, Sep = "\n\n", Text = picture.Value });
			position = picture.Index + picture.Length;
		}
		AddText(text[position..]);
		if (segments.Count > 0)
			segments[0] = segments[0] with { Sep = string.Empty };
		return segments;

		void AddText(string piece)
		{
			foreach (var paragraph in ParagraphBreak().Split(piece).Where(p => p.Trim().Length > 0))
			{
				var lines = LineBreak().Split(paragraph.Trim());
				for (var line = 0; line < lines.Length; line++)
				{
					var sentences = SentenceEnd().Split(lines[line].Trim()).Where(s => s.Trim().Length > 0).ToList();
					for (var index = 0; index < sentences.Count; index++)
						segments.Add(new TranslationSegment
						{
							Src = sentences[index].Trim(),
							Text = sentences[index].Trim(),
							Sep = index > 0 ? " " : line > 0 ? "\n" : "\n\n"
						});
				}
			}
		}
	}

	/// <summary>
	/// Segments together again as rules text, with each segment's translation. Emptied segments (text removed
	/// by a person) are left out, with their separator.
	/// </summary>
	public static string Join(IEnumerable<TranslationSegment> segments)
	{
		var result = new System.Text.StringBuilder();
		foreach (var segment in segments.Where(s => s.Text.Trim().Length > 0))
		{
			if (result.Length > 0) result.Append(segment.Sep.Length == 0 ? " " : segment.Sep);
			result.Append(segment.Text);
		}
		return result.ToString();
	}

	/// <summary>
	/// Which item of the old list each item of the new list is (an exact match, in order), or -1 for one that
	/// is new or changed: the longest common subsequence, as diff tools (and the cloud's matchSegments) find it.
	/// </summary>
	public static int[] Match(IReadOnlyList<string> older, IReadOnlyList<string> newer)
	{
		var longest = new int[older.Count + 1, newer.Count + 1];
		for (var i = older.Count - 1; i >= 0; i--)
			for (var j = newer.Count - 1; j >= 0; j--)
				longest[i, j] = older[i] == newer[j] ? longest[i + 1, j + 1] + 1 : Math.Max(longest[i + 1, j], longest[i, j + 1]);
		var match = Enumerable.Repeat(-1, newer.Count).ToArray();
		int a = 0, b = 0;
		while (a < older.Count && b < newer.Count)
		{
			if (older[a] == newer[b]) { match[b] = a; a++; b++; }
			else if (longest[a + 1, b] >= longest[a, b + 1]) a++;
			else b++;
		}
		return match;
	}

	/// <summary>A picture in the rules: ![image](reference).</summary>
	[GeneratedRegex(@"!\[[^\]]*\]\([^)\s]+\)")]
	private static partial Regex PictureTag();

	/// <summary>An empty line between paragraphs.</summary>
	[GeneratedRegex(@"\r?\n[ \t]*\r?\n")]
	private static partial Regex ParagraphBreak();

	/// <summary>A line break inside a paragraph.</summary>
	[GeneratedRegex(@"\r?\n")]
	private static partial Regex LineBreak();

	/// <summary>Where a sentence ends: after . ! ? … (and their Chinese forms), followed by a space.</summary>
	[GeneratedRegex(@"(?<=[.!?…。！？])\s+(?=\S)")]
	private static partial Regex SentenceEnd();
}
