using Microsoft.Maui.Platform;
using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class AdminCreateGamePage : ContentPage
{
	private readonly AdminCreateGameViewModel _vm;

	/// <summary>The piece of the rules text that was last tapped into; a picture goes at its cursor.</summary>
	private Editor? _lastRulesEditor;

	/// <summary>The picture being moved, until Place here or Cancel.</summary>
	private EditableRulesBlock? _movingImage;

	public AdminCreateGamePage(AdminCreateGameViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.CreateGame(_vm.IsModerator, _vm.ScoringType));
		_vm.Finished += async (_, message) =>
		{
			if (message is not null)
				await DisplayAlertAsync("Done", message, "OK");
			await Shell.Current.GoToAsync("..");
		};
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadCategoriesAsync();
	}

	private void OnCategoryTapped(object? sender, CategoryChoice choice) => choice.IsSelected = !choice.IsSelected;

	/// <summary>
	/// Asks where the picture comes from: the phone's photos, the camera, or
	/// the image editor (which then opens). Null when cancelled.
	/// </summary>
	/// <param name="editable">The drawing of the picture that's there now, if it was made in the image editor: then editing it is offered too.</param>
	private async Task<(PictureSource Source, EditedImage? Drawing)?> ChoosePictureAsync(bool cover, string? editable = null)
	{
		const string edit = "Edit this image";
		const string upload = "Upload an image";
		const string photo = "Take a photo";
		const string create = "Create an image";

		string[] choices = editable is null ? [upload, photo, create] : [edit, upload, photo, create];
		switch (await DisplayActionSheetAsync("Where does the picture come from?", "Cancel", null, choices))
		{
			case upload:
				return (PictureSource.Library, null);
			case photo:
				return (PictureSource.Camera, null);
			case create:
				return await ImageEditorPage.OpenAsync(Navigation, cover) is { } created ? (PictureSource.Drawing, created) : null;
			case edit:
				return await ImageEditorPage.OpenAsync(Navigation, cover, editable) is { } edited ? (PictureSource.Drawing, edited) : null;
			default:
				return null;
		}
	}

	private async void OnCoverClicked(object? sender, EventArgs e)
	{
		var drawing = await _vm.GetDrawingAsync(_vm.CoverImageUrl);
		if (await ChoosePictureAsync(cover: true, drawing) is { } picture)
			await _vm.SetCoverAsync(picture.Source, picture.Drawing);
	}

	private async void OnDeleteClicked(object? sender, EventArgs e)
	{
		var confirmed = _vm.IsModerator
			? await DisplayAlertAsync("Request deletion?",
				$"An admin will review your request. \"{_vm.Name}\" will stay in every player's library until an admin approves it.", "Request deletion", "Cancel")
			: await DisplayAlertAsync("Delete game?",
				$"\"{_vm.Name}\" will disappear from every player's library.", "Delete", "Cancel");
		if (confirmed)
			await _vm.DeleteAsync();
	}

	private async void OnWithdrawClicked(object? sender, EventArgs e)
	{
		var confirmed = await DisplayAlertAsync("Withdraw request?",
			"Your request will be taken back before an admin reviews it. Nothing will change for players.", "Withdraw", "Cancel");
		if (confirmed)
			await _vm.WithdrawAsync();
	}

	private void OnRulesEditorFocused(object? sender, FocusEventArgs e) => _lastRulesEditor = sender as Editor;

	/// <summary>
	/// The piece of text that was last tapped into and where its cursor is.
	/// Null when none was tapped (or it's gone, merged after a picture left).
	/// </summary>
	private (EditableRulesBlock Block, int Cursor)? RulesCursor() =>
		_lastRulesEditor?.BindingContext is EditableRulesBlock { IsText: true } block && _vm.RulesBlocks.Contains(block)
			? (block, _lastRulesEditor.CursorPosition)
			: null;

	/// <summary>
	/// The picture goes where the cursor is in the rules (at the end if the
	/// rules weren't tapped into yet). First the keyboard closes and the text
	/// is split in two at the cursor, with an empty place in between; only
	/// then is the picture chosen or made, and put in that place.
	/// </summary>
	private async void OnInsertRulesImageClicked(object? sender, EventArgs e)
	{
		var cursor = RulesCursor();
		var at = cursor?.Block ?? _vm.RulesBlocks.Last(b => b.IsText);
		var textBefore = at.Text;

		// The keyboard first: it still holds the word the cursor is in, and would write it back over the split text.
		if (_lastRulesEditor is { } editor)
		{
			editor.Unfocus();
			if (editor.IsSoftInputShowing())
				await editor.HideSoftInputAsync(CancellationToken.None);
		}
		_lastRulesEditor = null;

		var room = EditableRulesBlock.MakeRoom(_vm.RulesBlocks, at, cursor?.Cursor ?? textBefore.Length);
		string? reference = null;
		try
		{
			if (await ChoosePictureAsync(cover: false) is { } picture)
				reference = await _vm.UploadPictureAsync(picture.Source, picture.Drawing);
		}
		finally
		{
			// Cancelled or failed: the text goes back to exactly how it was.
			if (reference is null)
				EditableRulesBlock.GiveUpRoom(_vm.RulesBlocks, room, textBefore);
			else
				room.ImageRef = reference;
		}
	}

	/// <summary>Holding a picture in the rules: edit it (if it was made in the image editor), move it or remove it.</summary>
	private async void OnRulesImageHeld(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: EditableRulesBlock { ImageRef: { } imageRef } image }) return;

		const string edit = "Edit image";
		const string move = "Move to another place";
		const string remove = "Remove";

		StopMoving();
		var drawing = await _vm.GetDrawingAsync(imageRef);
		string[] choices = drawing is null ? [move] : [edit, move];
		switch (await DisplayActionSheetAsync("Picture", "Cancel", remove, choices))
		{
			case edit:
				// The picture's own shape comes with its drawing.
				if (await ImageEditorPage.OpenAsync(Navigation, cover: false, drawing) is { } edited
					&& await _vm.UploadPictureAsync(PictureSource.Drawing, edited) is { } reference)
					image.ImageRef = reference;
				break;

			case move:
				// The place it goes is tapped from now on, not where the cursor happened to be.
				_lastRulesEditor = null;
				_movingImage = image;
				MoveBar.IsVisible = true;
				break;

			case remove:
				if (await DisplayAlertAsync("Remove this picture?", "The picture is taken out of the rules.", "Remove", "Keep"))
					EditableRulesBlock.RemoveImage(_vm.RulesBlocks, image);
				break;
		}
	}

	private async void OnPlaceHereClicked(object? sender, EventArgs e)
	{
		if (_movingImage is not { } image) return;

		if (RulesCursor() is not { } cursor)
		{
			await DisplayAlertAsync("Tap in the text first", "Tap in the rules where the picture should go, then tap Place here.", "OK");
			return;
		}

		StopMoving();
		EditableRulesBlock.MoveImage(_vm.RulesBlocks, image, cursor.Block, cursor.Cursor);
		_lastRulesEditor = null;
	}

	private void OnCancelMoveClicked(object? sender, EventArgs e) => StopMoving();

	private void StopMoving()
	{
		_movingImage = null;
		MoveBar.IsVisible = false;
	}
}
