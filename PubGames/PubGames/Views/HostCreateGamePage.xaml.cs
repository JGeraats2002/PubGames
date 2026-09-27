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

	private async void OnUploadCoverClicked(object? sender, EventArgs e)
	{
		var result = await MediaPicker.Default.PickPhotoAsync();
		if (result is null) return;

		// TODO: upload result.FullPath (or OpenReadAsync stream) to your cloud
		// storage (e.g. Firebase Storage / S3) and set _vm.CoverImageUrl to the
		// resulting public URL.
	}

	private async void OnInsertRulesImageClicked(object? sender, EventArgs e)
	{
		var result = await MediaPicker.Default.PickPhotoAsync();
		if (result is null) return;

		// TODO: upload the image, then insert a markdown/HTML image reference
		// at the cursor position in _vm.RulesText, e.g.:
		// _vm.RulesText += $"\n![rules image]({uploadedUrl})\n";
	}
}
