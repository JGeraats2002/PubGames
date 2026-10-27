using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubGames.Models;

/// <summary>
/// One piece of a game's rules for display: either a run of text or an image.
/// Rules are stored as plain text with markdown-style image tags,
/// e.g. "Deal 4 cards.\n\n![image](fsimg:abc123)\n\nThen..." - see RulesImageTag.
/// </summary>
public partial record RulesBlock(string? Text, string? ImageRef)
{
	/// <summary>A piece of text.</summary>
	public bool IsText => Text is not null;
	/// <summary>A picture.</summary>
	public bool IsImage => ImageRef is not null;

	/// <summary>The tag inserted into the rules text for an uploaded image.</summary>
	public static string RulesImageTag(string imageRef) => $"![image]({imageRef})";

	/// <summary>Splits a game's rules into text and pictures, in their order, for showing them.</summary>
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

	/// <summary>Finds a picture in the rules text: ![](reference).</summary>
	[GeneratedRegex(@"!\[[^\]]*\]\(([^)\s]+)\)")]
	private static partial Regex ImageTag();
}

/// <summary>
/// One piece of the rules on the create/edit page: a run of text to type in,
/// or a picture. On the page they follow each other without anything in
/// between, so it reads and types as one text with pictures in it, while the
/// text itself stays in ordinary text boxes - a picture can never cover it.
/// The list always alternates text, picture, text, ... and starts and ends
/// with text, so there's always somewhere to type around every picture.
/// </summary>
public partial class EditableRulesBlock : ObservableObject
{
	/// <summary>The text of this piece, as typed in the editor.</summary>
	[ObservableProperty]
	private string text = string.Empty;

	/// <summary>The picture; null for a piece of text. Changes when the picture is edited.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsText), nameof(IsImage))]
	private string? imageRef;

	/// <summary>Shown while this piece of text is empty.</summary>
	[ObservableProperty]
	private string placeholder = string.Empty;

	/// <summary>A piece of text.</summary>
	public bool IsText => ImageRef is null;
	/// <summary>A picture.</summary>
	public bool IsImage => ImageRef is not null;

	/// <summary>A place kept free for a picture that's still being chosen or made (see MakeRoom).</summary>
	public bool IsWaiting => ImageRef is { Length: 0 };

	/// <summary>A new piece of text for the rules editor.</summary>
	private static EditableRulesBlock ForText(string text) => new() { Text = text };
	/// <summary>A new picture for the rules editor.</summary>
	private static EditableRulesBlock ForImage(string imageRef) => new() { ImageRef = imageRef };

	/// <summary>
	/// Splits a game's rules into pieces of text and pictures for the editor (always starting and ending with
	/// text, so there's somewhere to type).
	/// </summary>
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
		ShowPlaceholders(blocks);
		return blocks;
	}

	/// <summary>Back to the stored format: every picture as an image tag on its own line, with an empty line above and below.</summary>
	public static string ToRulesText(IEnumerable<EditableRulesBlock> blocks) =>
		string.Join("\n\n", blocks
			.Select(b => b.IsWaiting ? string.Empty : b.IsImage ? RulesBlock.RulesImageTag(b.ImageRef!) : b.Text.Trim())
			.Where(s => s.Length > 0));

	/// <summary>
	/// Splits a piece of text at the cursor - also in the middle of a word -
	/// and puts the picture between the two halves. Without a piece of text
	/// (none was tapped yet) the picture goes at the end.
	/// </summary>
	public static void InsertImage(IList<EditableRulesBlock> blocks, EditableRulesBlock? at, int cursor, string imageRef)
	{
		if (at is null || at.IsImage || !blocks.Contains(at))
		{
			at = blocks.Last(b => b.IsText);
			cursor = at.Text.Length;
		}

		var index = blocks.IndexOf(at);
		cursor = Math.Clamp(cursor, 0, at.Text.Length);
		var after = at.Text[cursor..].TrimStart();
		at.Text = at.Text[..cursor].TrimEnd();
		blocks.Insert(index + 1, ForImage(imageRef));
		blocks.Insert(index + 2, ForText(after));
		ShowPlaceholders(blocks);
	}

	/// <summary>
	/// Makes room for a picture before it's there: splits the text at the
	/// cursor into two pieces right away, with an empty place between them.
	/// Returns that place, to give the picture to (ImageRef) or to GiveUpRoom.
	/// </summary>
	public static EditableRulesBlock MakeRoom(IList<EditableRulesBlock> blocks, EditableRulesBlock? at, int cursor)
	{
		InsertImage(blocks, at, cursor, string.Empty);
		return blocks.Last(b => b.IsWaiting);
	}

	/// <summary>No picture came after all: the text is put back exactly as it was before MakeRoom.</summary>
	/// <param name="textBefore">What the piece of text said before it was split.</param>
	public static void GiveUpRoom(IList<EditableRulesBlock> blocks, EditableRulesBlock room, string textBefore)
	{
		var index = blocks.IndexOf(room);
		if (index <= 0 || index >= blocks.Count - 1) return;

		blocks[index - 1].Text = textBefore;
		blocks.RemoveAt(index + 1);
		blocks.RemoveAt(index);
		ShowPlaceholders(blocks);
	}

	/// <summary>Removes a picture and joins the text that was above and below it.</summary>
	public static void RemoveImage(IList<EditableRulesBlock> blocks, EditableRulesBlock image)
	{
		var index = blocks.IndexOf(image);
		if (index <= 0 || index >= blocks.Count - 1 || image.IsText) return;

		var before = blocks[index - 1];
		var after = blocks[index + 1];
		before.Text = string.Join("\n", new[] { before.Text, after.Text }.Where(t => !string.IsNullOrWhiteSpace(t)));
		blocks.RemoveAt(index + 1);
		blocks.RemoveAt(index);
		ShowPlaceholders(blocks);
	}

	/// <summary>Moves a picture to the cursor's place in a piece of text.</summary>
	public static void MoveImage(IList<EditableRulesBlock> blocks, EditableRulesBlock image, EditableRulesBlock at, int cursor)
	{
		if (image.ImageRef is not { } imageRef || at.IsImage || !blocks.Contains(image) || !blocks.Contains(at)) return;

		// At its new place first, then away from the old one: that also works when both are in the same piece of text.
		InsertImage(blocks, at, cursor, imageRef);
		RemoveImage(blocks, image);
	}

	/// <summary>Only rules without anything in them say what to type.</summary>
	private static void ShowPlaceholders(IList<EditableRulesBlock> blocks)
	{
		foreach (var block in blocks)
			block.Placeholder = blocks.Count == 1 ? "Explain the rules..." : string.Empty;
	}
}

/// <summary>The stored rules text, as far as its pictures go.</summary>
public static partial class RulesImages
{
	/// <summary>
	/// Every picture on its own line with an empty line above and below, so
	/// rules saved before that was the rule compare equal to the same rules now.
	/// </summary>
	public static string Normalize(string text)
	{
		text = LeadingSpace().Replace(text, "\n\n$1");
		text = TrailingSpace().Replace(text, "$1\n\n");
		return text.Trim();
	}

	/// <summary>Every image a game shows: its cover and the pictures in its rules.</summary>
	public static IEnumerable<string> Refs(string? coverImageRef, string? rulesText)
	{
		if (!string.IsNullOrEmpty(coverImageRef))
			yield return coverImageRef;
		foreach (Match tag in ImageTag().Matches(rulesText ?? string.Empty))
			yield return tag.Groups[1].Value;
	}

	/// <summary>Finds a picture in the rules text: ![](reference).</summary>
	[GeneratedRegex(@"!\[[^\]]*\]\(([^)\s]+)\)")]
	private static partial Regex ImageTag();

	/// <summary>Finds a picture with the spaces and empty lines in front of it.</summary>
	[GeneratedRegex(@"[ \t\r\n]*(!\[[^\]]*\]\([^)\s]+\))")]
	private static partial Regex LeadingSpace();

	/// <summary>Finds a picture with the spaces and empty lines after it.</summary>
	[GeneratedRegex(@"(!\[[^\]]*\]\([^)\s]+\))[ \t\r\n]*")]
	private static partial Regex TrailingSpace();
}
