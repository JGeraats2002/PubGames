using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;

namespace PubGames.ViewModels;

/// <summary>One section of the admin agreements while the owner edits them: a heading and its text, as typed.</summary>
public partial class EditableAgreement : ObservableObject
{
	/// <summary>The section's heading, as typed.</summary>
	[ObservableProperty]
	private string heading = string.Empty;

	/// <summary>The section's text, as typed.</summary>
	[ObservableProperty]
	private string text = string.Empty;

	/// <summary>A section to edit.</summary>
	public static EditableAgreement From(HelpSection section) => new() { Heading = section.Heading, Text = section.Text };

	/// <summary>The section as it's saved.</summary>
	public HelpSection ToSection() => new(Heading.Trim(), Text.Trim());
}
