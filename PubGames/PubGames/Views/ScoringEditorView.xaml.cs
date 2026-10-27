using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// A scoring type with its numbers and amount rules (see ScoringEditor). The
/// game form uses it for the game's scoring and for a duel's own scoring.
/// </summary>
public partial class ScoringEditorView : ContentView
{
	/// <summary>Builds the scoring editor from its XAML.</summary>
	public ScoringEditorView()
	{
		InitializeComponent();
	}

	private ScoringEditor? Editor => BindingContext as ScoringEditor;

	/// <summary>Adds an empty rule for entered amounts.</summary>
	private void OnAddAmountRuleClicked(object? sender, EventArgs e) => Editor?.AddAmountRule();

	/// <summary>Switches how a rule links to the one above it: AND or OR.</summary>
	private void OnJoinerClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: EditableScoreRule rule })
			Editor?.ToggleJoiner(rule);
	}

	/// <summary>Removes a rule for entered amounts.</summary>
	private void OnRemoveAmountRuleClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: EditableScoreRule rule })
			Editor?.RemoveAmountRule(rule);
	}
}
