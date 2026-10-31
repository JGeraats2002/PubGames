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

	/// <summary>Score sheet: adds an empty list.</summary>
	private void OnAddSectionClicked(object? sender, EventArgs e) => Editor?.AddSection();

	/// <summary>The item a button in a template belongs to.</summary>
	private static T? ItemOf<T>(object? sender) where T : class => (sender as BindableObject)?.BindingContext as T;

	/// <summary>Score sheet: removes a list.</summary>
	private void OnRemoveSectionClicked(object? sender, EventArgs e)
	{
		if (ItemOf<EditableSheetSection>(sender) is { } section) Editor?.RemoveSection(section);
	}

	/// <summary>Score sheet: adds a row to a list.</summary>
	private void OnAddRowClicked(object? sender, EventArgs e)
	{
		if (ItemOf<EditableSheetSection>(sender) is { } section) ScoringEditor.AddRow(section);
	}

	/// <summary>Score sheet: removes a row.</summary>
	private void OnRemoveRowClicked(object? sender, EventArgs e)
	{
		if (ItemOf<EditableSheetRow>(sender) is { } row) Editor?.RemoveRow(row);
	}

	/// <summary>Score sheet: adds a rule to a row.</summary>
	private void OnAddRowRuleClicked(object? sender, EventArgs e)
	{
		if (ItemOf<EditableSheetRow>(sender) is { } row) ScoringEditor.AddRowRule(row);
	}

	/// <summary>Score sheet: removes a rule from a row.</summary>
	private void OnRemoveRowRuleClicked(object? sender, EventArgs e)
	{
		if (ItemOf<EditableScoreRule>(sender) is { } rule) Editor?.RemoveRowRule(rule);
	}

	/// <summary>Score sheet: adds a bonus to a list.</summary>
	private void OnAddBonusClicked(object? sender, EventArgs e)
	{
		if (ItemOf<EditableSheetSection>(sender) is { } section) ScoringEditor.AddBonus(section);
	}

	/// <summary>Score sheet: removes a bonus.</summary>
	private void OnRemoveBonusClicked(object? sender, EventArgs e)
	{
		if (ItemOf<EditableSheetBonus>(sender) is { } bonus) Editor?.RemoveBonus(bonus);
	}

	/// <summary>Removes a rule for entered amounts.</summary>
	private void OnRemoveAmountRuleClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: EditableScoreRule rule })
			Editor?.RemoveAmountRule(rule);
	}
}
