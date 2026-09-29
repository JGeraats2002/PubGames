using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

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

/// <summary>
/// One piece of the rules on the create/edit page: an editable run of text, or
/// a picture shown inline so the admin sees exactly what players will see.
/// The list always alternates text, picture, text, ... and starts and ends
/// with text, so there's always somewhere to type around every picture.
/// </summary>
public partial class EditableRulesBlock : ObservableObject
{
	[ObservableProperty]
	private string text = string.Empty;

	public string? ImageRef { get; }

	public bool IsText => ImageRef is null;
	public bool IsImage => ImageRef is not null;

	private EditableRulesBlock(string? imageRef) => ImageRef = imageRef;

	public static EditableRulesBlock ForText(string text) => new(null) { Text = text };
	public static EditableRulesBlock ForImage(string imageRef) => new(imageRef);

	public static ObservableCollection<EditableRulesBlock> FromRulesText(string rules)
	{
		var blocks = new ObservableCollection<EditableRulesBlock>();
		foreach (var block in RulesBlock.Parse(rules))
		{
			if (block.IsImage)
			{
				if (blocks.Count == 0 || blocks[^1].IsImage)
					blocks.Add(ForText(string.Empty));
				blocks.Add(ForImage(block.ImageRef!));
			}
			else
				blocks.Add(ForText(block.Text!));
		}
		if (blocks.Count == 0 || blocks[^1].IsImage)
			blocks.Add(ForText(string.Empty));
		return blocks;
	}

	/// <summary>Back to the stored format: pictures as ![image](...) tags on their own line.</summary>
	public static string ToRulesText(IEnumerable<EditableRulesBlock> blocks) =>
		string.Join("\n", blocks
			.Select(b => b.IsImage ? RulesBlock.RulesImageTag(b.ImageRef!) : b.Text.Trim('\r', '\n'))
			.Where(s => !string.IsNullOrWhiteSpace(s)));

	/// <summary>Splits the text block at the cursor and puts the picture between the two halves.</summary>
	public static void InsertImage(IList<EditableRulesBlock> blocks, EditableRulesBlock? at, int cursor, string imageRef)
	{
		// No text box was tapped yet: add the picture at the end.
		at ??= blocks.Last(b => b.IsText);
		var index = blocks.IndexOf(at);
		if (index < 0 || at.IsImage) return;

		cursor = Math.Clamp(cursor, 0, at.Text.Length);
		var after = at.Text[cursor..].TrimStart('\r', '\n');
		at.Text = at.Text[..cursor].TrimEnd('\r', '\n');
		blocks.Insert(index + 1, ForImage(imageRef));
		blocks.Insert(index + 2, ForText(after));
	}

	/// <summary>Removes a picture and joins the text that was above and below it.</summary>
	public static void RemoveImage(IList<EditableRulesBlock> blocks, EditableRulesBlock image)
	{
		var index = blocks.IndexOf(image);
		if (index <= 0 || index >= blocks.Count - 1 || image.IsText) return;

		var before = blocks[index - 1];
		var after = blocks[index + 1];
		before.Text = string.Join("\n", new[] { before.Text, after.Text }.Where(t => !string.IsNullOrEmpty(t)));
		blocks.RemoveAt(index + 1);
		blocks.RemoveAt(index);
	}
}
