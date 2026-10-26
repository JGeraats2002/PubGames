using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// A scoring type with its numbers and amount rules (see ScoringEditor). The
/// game form uses it for the game's scoring and for a duel's own scoring.
/// </summary>
public partial class ScoringEditorView : ContentView
{
	public ScoringEditorView()
	{
		InitializeComponent();
	}

	private ScoringEditor? Editor => BindingContext as ScoringEditor;

	private void OnAddAmountRuleClicked(object? sender, EventArgs e) => Editor?.AddAmountRule();

	private void OnJoinerClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: EditableScoreRule rule })
			Editor?.ToggleJoiner(rule);
	}

	private void OnRemoveAmountRuleClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: EditableScoreRule rule })
			Editor?.RemoveAmountRule(rule);
	}
}
