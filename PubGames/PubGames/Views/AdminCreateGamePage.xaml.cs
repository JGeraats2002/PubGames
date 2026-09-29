using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class AdminCreateGamePage : ContentPage
{
	private readonly AdminCreateGameViewModel _vm;

	/// <summary>The rules text box the user last tapped into; the picture goes at its cursor.</summary>
	private Editor? _lastRulesEditor;

	public AdminCreateGamePage(AdminCreateGameViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_vm.Finished += async (_, _) => await Shell.Current.GoToAsync("..");
	}

	private async void OnDeleteClicked(object? sender, EventArgs e)
	{
		var confirmed = await DisplayAlertAsync("Delete game?",
			$"\"{_vm.Name}\" will disappear from every player's library.", "Delete", "Cancel");
		if (confirmed)
			await _vm.DeleteAsync();
	}

	private void OnRulesEditorFocused(object? sender, FocusEventArgs e) =>
		_lastRulesEditor = sender as Editor;

	/// <summary>Lives here rather than in the ViewModel because it needs the editor's cursor position.</summary>
	private async void OnInsertRulesImageClicked(object? sender, EventArgs e)
	{
		// Read the cursor before the picker opens; the editor loses focus meanwhile.
		// An editor whose block was removed (merged after deleting a picture) no longer counts.
		var block = _lastRulesEditor?.BindingContext as EditableRulesBlock;
		if (block is not null && !_vm.RulesBlocks.Contains(block))
			block = null;
		var cursor = block is null ? 0 : _lastRulesEditor!.CursorPosition;

		await _vm.InsertRulesImageAsync(block, block is null ? int.MaxValue : cursor);
		_lastRulesEditor = null;
	}

	private void OnRemoveRulesImageClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: EditableRulesBlock image })
			_vm.RemoveRulesImageCommand.Execute(image);
	}
}
