using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;

namespace PubGames.ViewModels;

/// <summary>One word of a sentence on the Translations page, to tap when choosing what to change or remove.</summary>
public partial class WordChip : ObservableObject
{
	/// <summary>The word.</summary>
	public required string Text { get; init; }

	/// <summary>Which sentence on the page it's in.</summary>
	public required int Row { get; init; }

	/// <summary>Its place in that sentence.</summary>
	public required int Index { get; init; }

	/// <summary>Part of the selection (between the first and last word tapped).</summary>
	[ObservableProperty]
	private bool isSelected;
}

/// <summary>
/// One sentence (or picture) of a translation on the Translations page: the translation with its status, the
/// original it belongs to, and for a sentence to look at again the new automatic translation to compare with.
/// </summary>
public partial class SentenceRow : ObservableObject
{
	/// <summary>Its place in the translation's segments.</summary>
	public required int Index { get; init; }

	/// <summary>The segment as it is now on the page.</summary>
	public required TranslationSegment Segment { get; init; }

	/// <summary>A picture rather than a sentence.</summary>
	public bool IsPicture => Segment.IsPicture;

	/// <summary>A sentence.</summary>
	public bool IsSentence => !IsPicture;

	/// <summary>The words of the translation, to tap.</summary>
	public ObservableCollection<WordChip> Words { get; } = [];

	/// <summary>The original sentence, shown small above the translation.</summary>
	public string Original => Segment.Status == TranslationSegment.Added ? string.Empty : Segment.Src;

	/// <summary>The status, for the colour of the row: auto, approved, edited, review or added.</summary>
	public string Status => Segment.Status;

	/// <summary>Nobody checked it yet (translated by the cloud).</summary>
	public bool IsAuto => Segment.Status == TranslationSegment.Auto;

	/// <summary>The original changed after a person checked it.</summary>
	public bool IsReview => Segment.Status == TranslationSegment.Review;

	/// <summary>Added here; put into the original and every language after saving.</summary>
	public bool IsAdded => Segment.Status == TranslationSegment.Added;

	/// <summary>Checked by a person (approved or corrected).</summary>
	public bool IsChecked => Segment.IsChecked;

	/// <summary>The new automatic translation of a changed sentence, to compare with.</summary>
	public bool HasSuggestion => IsReview && !string.IsNullOrWhiteSpace(Segment.Suggestion);

	/// <summary>What the status means, short, under the sentence.</summary>
	public string StatusLabel => Segment.Status switch
	{
		TranslationSegment.Approved => L.T("✓ Checked"),
		TranslationSegment.Edited => Segment.Origin == "added" ? L.T("✓ Added here, now also in the original") : L.T("✓ Corrected"),
		TranslationSegment.Review => L.T("The original changed: look at it again"),
		TranslationSegment.Added => L.T("Added: after saving it goes into the original and every language"),
		_ => L.T("Not checked yet")
	};

	/// <summary>
	/// The picture itself (pictures only): its reference as stored, not the preview with translated texts
	/// (PreviewRef), so the image editor can open its drawing.
	/// </summary>
	public string? ImageRef => IsPicture ? SentenceChange.ImageOf(Segment.Text.Length > 0 ? Segment.Text : Segment.Src) : null;

	/// <summary>The picture with its texts in this language (pictures only).</summary>
	[ObservableProperty]
	private string? previewRef;

	/// <summary>The texts in the picture, to correct (pictures only).</summary>
	public ObservableCollection<PictureText> Texts { get; } = [];

	/// <summary>The picture has texts.</summary>
	[ObservableProperty]
	private bool hasTexts;

	/// <summary>Shows the picture's texts and the picture with them.</summary>
	public void ShowTexts(IEnumerable<PictureText> texts) =>
		PictureTexts.Show(Texts, texts, reference => PreviewRef = reference, () => HasTexts = Texts.Count > 0);

	/// <summary>A row for a segment, with its words.</summary>
	public static SentenceRow For(int index, TranslationSegment segment)
	{
		var tag = segment.Src.Length > 0 ? segment.Src : segment.Text;
		var row = new SentenceRow { Index = index, Segment = segment, PreviewRef = segment.IsPicture ? tag.Split('(', ')')[1] : null };
		if (!segment.IsPicture)
		{
			var words = segment.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			for (var word = 0; word < words.Length; word++)
				row.Words.Add(new WordChip { Text = words[word], Row = index, Index = word });
		}
		return row;
	}
}
