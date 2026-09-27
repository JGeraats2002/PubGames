using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class HostCreateGamePage : ContentPage
{
	private readonly HostCreateGameViewModel _vm;

	public HostCreateGamePage(HostCreateGameViewModel vm)
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

	/// <summary>Lives here rather than in the ViewModel because it needs the editor's cursor position.</summary>
	private async void OnInsertRulesImageClicked(object? sender, EventArgs e)
	{
		// Read the cursor before the picker opens; the editor loses focus meanwhile.
		var text = _vm.RulesText ?? string.Empty;
		var cursor = Math.Clamp(RulesEditor.CursorPosition, 0, text.Length);

		var reference = await _vm.UploadImageAsync();
		if (reference is null) return;

		// Own line so the rules page shows the picture between paragraphs.
		var tag = "\n" + RulesBlock.RulesImageTag(reference) + "\n";
		_vm.RulesText = text.Insert(cursor, tag);
		RulesEditor.CursorPosition = cursor + tag.Length;
	}
}
