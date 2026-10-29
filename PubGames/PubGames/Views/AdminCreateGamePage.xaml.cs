using Microsoft.Maui.Platform;
using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// The game form for admins and moderators: a new game, changes to a game, or a moderator's review request -
/// on three pages: General, Scoring and Rules.
/// </summary>
public partial class AdminCreateGamePage : ContentPage
{
	private readonly AdminCreateGameViewModel _vm;

	/// <summary>The piece of the rules text that was last tapped into; a picture goes at its cursor.</summary>
	private Editor? _lastRulesEditor;

	/// <summary>The picture being moved, until Place here or Cancel.</summary>
	private EditableRulesBlock? _movingImage;

	/// <summary>
	/// Sets up the game form: its view model, the ⓘ help, what happens once it's saved or deleted, and asking
	/// before leaving with unsaved changes.
	/// </summary>
	public AdminCreateGamePage(AdminCreateGameViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.CreateGame(_vm.IsModerator, _vm.ScoringType));
		_vm.AddPlaceChosen += OnAddPlaceChosen;
		// Another page of the form (a tab, or the page a missing choice is on): the keyboard closes.
		_vm.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(AdminCreateGameViewModel.CurrentPage)) SoftKeyboard.Hide();
		};
		_vm.Finished += async (_, message) =>
		{
			_leaving = true;
			if (message is not null)
				await DisplayAlertAsync(L.T("Done"), message, "OK");
			await Shell.Current.GoToAsync("..");
		};
		// The back arrow at the top asks first too.
		Shell.SetBackButtonBehavior(this, new BackButtonBehavior { Command = new Command(async () => await LeaveAsync()) });
	}

	/// <summary>Saved, deleted, withdrawn or chosen not to save: going back doesn't ask (again).</summary>
	private bool _leaving;

	/// <summary>The phone's back button: ask first.</summary>
	protected override bool OnBackButtonPressed()
	{
		_ = LeaveAsync();
		return true;
	}

	/// <summary>
	/// Going back with unsaved changes asks whether to save them. Saving goes
	/// back once it worked; if something is still missing, the form stays open
	/// and says what.
	/// </summary>
	private async Task LeaveAsync()
	{
		if (_leaving) return;

		if (_vm.HasUnsavedChanges)
		{
			var discard = L.T("Don't save");
			var save = _vm.SaveButtonText;
			var choice = await ChoicePopup.AskAsync(this, L.T("Save your changes?"), null, L.T("Keep editing"), discard, save);
			if (choice == save)
			{
				await _vm.SaveCommand.ExecuteAsync(null);
				if (!_leaving && !string.IsNullOrEmpty(_vm.StatusMessage))
					await DisplayAlertAsync(L.T("Not saved yet"), _vm.StatusMessage, "OK");
				return;
			}
			if (choice != discard) return;
		}

		_leaving = true;
		await Shell.Current.GoToAsync("..");
	}

	/// <summary>Every time the page shows: the categories to choose from are loaded fresh.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadCategoriesAsync();
	}

	/// <summary>Selects a category for the game, or unselects it.</summary>
	private void OnCategoryTapped(object? sender, CategoryChoice choice) => choice.IsSelected = !choice.IsSelected;

	/// <summary>Shows the General page of the form.</summary>
	private void OnGeneralTabClicked(object? sender, EventArgs e) => ShowPage(AdminCreateGameViewModel.GeneralPage);
	/// <summary>Shows the Scoring page of the form.</summary>
	private void OnScoringTabClicked(object? sender, EventArgs e) => ShowPage(AdminCreateGameViewModel.ScoringPage);
	/// <summary>Shows the Rules page of the form.</summary>
	private void OnRulesTabClicked(object? sender, EventArgs e) => ShowPage(AdminCreateGameViewModel.RulesPage);
	/// <summary>Shows the Languages page of the form.</summary>
	private void OnLanguagesTabClicked(object? sender, EventArgs e) => ShowPage(AdminCreateGameViewModel.LanguagesPage);

	/// <summary>
	/// Switches to a page of the form, scrolled to the top (a picture being moved is put down first).
	/// </summary>
	private async void ShowPage(int page)
	{
		if (_vm.CurrentPage == page) return;
		StopMoving();
		_vm.CurrentPage = page;
		await FormScroll.ScrollToAsync(0, 0, false);
	}

	/// <summary>"+ Add your rules": choose a part that isn't on this page yet. It shows in its own place in the form.</summary>
	private async void OnAddSectionClicked(object? sender, EventArgs e)
	{
		var options = _vm.AddableSections();
		if (options.Count == 0) return;

		var choice = await DisplayActionSheetAsync(L.T("Add your rules"), L.T("Cancel"), null, options.Select(o => o.Title).ToArray());
		if (options.FirstOrDefault(o => o.Title == choice) is { } picked)
			_vm.AddSection(picked.Section);
	}

	/// <summary>
	/// Fills in the duel's rules with the game's (after asking, when the duel already has rules of its own).
	/// </summary>
	private async void OnCopyGameRulesClicked(object? sender, EventArgs e)
	{
		if (_vm.DuelScoring.IsChosen
			&& !await DisplayAlertAsync(L.T("Copy the game's rules?"), L.T("What you filled in for the duel is replaced by the game's rules."), L.T("Copy"), L.T("Cancel")))
			return;
		if (!_vm.CopyGameRulesToDuel())
			await DisplayAlertAsync(L.T("No rules to copy yet"), L.T("Choose the game's scoring type first, at the top of this page."), "OK");
	}

	/// <summary>The ✕ next to a part: take it off the form, clearing what was filled in.</summary>
	private async void OnRemoveSectionClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { CommandParameter: GameFormSection section }) return;

		var title = AdminCreateGameViewModel.TitleOf(section);
		if (await DisplayAlertAsync(L.F("Remove {0}?", title), L.T("What you filled in here is cleared. You can add it again with + Add your rules."), L.T("Remove"), L.T("Keep")))
			_vm.RemoveSection(section);
	}

	/// <summary>
	/// Opens the image editor for a picture: game pictures are always made there (own photos are only for
	/// profile pictures). With a picture that was made there before, editing it or starting a new one is asked.
	/// Null when cancelled.
	/// </summary>
	/// <param name="editable">The drawing of the picture that's there now, if it was made in the image editor.</param>
	private async Task<EditedImage?> MakePictureAsync(bool cover, string? editable = null)
	{
		if (editable is not null)
		{
			var edit = L.T("Edit this image");
			var create = L.T("Create a new image");
			var choice = await DisplayActionSheetAsync(L.T("Picture"), L.T("Cancel"), null, edit, create);
			if (choice == edit)
				return await ImageEditorPage.OpenAsync(Navigation, cover, editable);
			if (choice != create)
				return null;
		}
		return await ImageEditorPage.OpenAsync(Navigation, cover);
	}

	/// <summary>
	/// Chooses the cover: create an image, or change the one created before.
	/// </summary>
	private async void OnCoverClicked(object? sender, EventArgs e)
	{
		var drawing = await _vm.GetDrawingAsync(_vm.CoverImageUrl);
		if (await MakePictureAsync(cover: true, drawing) is { } picture)
			await _vm.SetCoverAsync(picture);
	}

	/// <summary>
	/// A sentence or picture block was tapped: adding goes after it, changing opens it in a pop-up (or the
	/// image editor), removing asks first.
	/// </summary>
	private async void OnSentenceTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not SentenceRow row || _askingWhatToAdd) return;
		_askingWhatToAdd = true;
		try
		{
			switch (_vm.Mode)
			{
				case TranslationMode.Adding:
					_askingWhatToAdd = false;
					await AddAtAsync(row);
					break;
				case TranslationMode.Changing when row.IsPicture:
					var drawing = await _vm.GetDrawingAsync(row.ImageRef);
					if (await ImageEditorPage.OpenAsync(Navigation, cover: false, drawing) is { } edited
						&& await _vm.UploadPictureAsync(edited) is { } reference)
						_vm.ReplacePicture(row, reference);
					break;
				case TranslationMode.Changing:
					var text = await DisplayPromptAsync(L.T("Change"), L.F("Original: {0}", row.Original),
						L.T("Change"), L.T("Cancel"), initialValue: row.Segment.Text, maxLength: 2000, keyboard: Keyboard.Chat);
					if (text is not null) _vm.ReplaceSentence(row, text);
					break;
				case TranslationMode.Removing:
					var what = row.IsPicture
						? L.T("The picture leaves the original and every language after saving.")
						: row.Segment.Text;
					if (await DisplayAlertAsync(L.T("Remove this?"), what, L.T("Remove"), L.T("Keep")))
						_vm.RemoveRow(row);
					break;
			}
		}
		finally
		{
			_askingWhatToAdd = false;
		}
	}

	/// <summary>Discard: after asking, everything changed on the Translations page is undone.</summary>
	private async void OnDiscardTranslationsClicked(object? sender, EventArgs e)
	{
		if (await DisplayAlertAsync(L.T("Discard your changes?"), L.T("Everything you changed on the Translations page since you opened or saved the game is undone."),
				L.T("Discard"), L.T("Keep editing")))
			_vm.DiscardTranslations();
	}

	/// <summary>Adding: a word in a sentence was tapped (the sentence is the place).</summary>
	private async void OnAddPlaceChosen(object? sender, SentenceRow row) => await AddAtAsync(row);

	/// <summary>Adding at the start of the rules, as a paragraph of its own.</summary>
	private async void OnAddAtStartClicked(object? sender, EventArgs e) => await AddAtAsync(null);

	/// <summary>A tap on a word also reaches the sentence: only one question at a time.</summary>
	private bool _askingWhatToAdd;

	/// <summary>
	/// Asks what to add after the sentence (null: at the start): text, typed in a pop-up, or a picture made in
	/// the image editor.
	/// </summary>
	private async Task AddAtAsync(SentenceRow? after)
	{
		if (_askingWhatToAdd) return;
		_askingWhatToAdd = true;
		try
		{
			var text = L.T("Text");
			var picture = L.T("Picture (image editor)");
			var choice = await DisplayActionSheetAsync(L.T("What do you want to add?"), L.T("Cancel"), null, text, picture);
			if (choice == text)
			{
				if (await AskNewSentenceAsync() is { } typed)
					_vm.AddSentence(after, typed, newParagraph: after is null);
			}
			else if (choice == picture
				&& await ImageEditorPage.OpenAsync(Navigation, cover: false) is { } made
				&& await _vm.UploadPictureAsync(made) is { } reference)
			{
				_vm.AddPicture(after, reference);
			}
		}
		finally
		{
			_askingWhatToAdd = false;
		}
	}

	/// <summary>The pop-up for something to add, typed freely; null when cancelled.</summary>
	private async Task<string?> AskNewSentenceAsync()
	{
		var text = await DisplayPromptAsync(L.T("Add"),
			L.F("Type what to add, in {0}. After saving it also goes into the original and every other language.", _vm.TranslationLanguage?.Name ?? string.Empty),
			L.T("Add"), L.T("Cancel"), maxLength: 2000, keyboard: Keyboard.Chat);
		return string.IsNullOrWhiteSpace(text) ? null : text;
	}

	/// <summary>Changing: the pop-up shows the selected words in their sentence, with a bar to type the better version.</summary>
	private async void OnChangeSelectionClicked(object? sender, EventArgs e)
	{
		var selected = _vm.SelectedText();
		if (selected.Length == 0)
		{
			await DisplayAlertAsync(L.T("Nothing selected"), L.T("Tap the first and the last word you want to change."), "OK");
			return;
		}
		var text = await DisplayPromptAsync(L.T("Change"), L.F("In: {0}\n\nSelected: {1}", _vm.SelectedSentences(), selected),
			L.T("Change"), L.T("Cancel"), initialValue: selected, maxLength: 2000, keyboard: Keyboard.Chat);
		if (text is not null && text.Trim() != selected)
			_vm.ReplaceSelection(text);
	}

	/// <summary>Removing: after asking, the selected words leave this translation.</summary>
	private async void OnRemoveSelectionClicked(object? sender, EventArgs e)
	{
		var selected = _vm.SelectedText();
		if (selected.Length == 0)
		{
			await DisplayAlertAsync(L.T("Nothing selected"), L.T("Tap the first and the last word you want to remove."), "OK");
			return;
		}
		if (await DisplayAlertAsync(L.T("Remove this?"), selected, L.T("Remove"), L.T("Keep")))
			_vm.ReplaceSelection(string.Empty);
	}

	/// <summary>Deletes the game (admins) or asks an admin to (moderators), after asking.</summary>
	private async void OnDeleteClicked(object? sender, EventArgs e)
	{
		var confirmed = _vm.IsModerator
			? await DisplayAlertAsync(L.T("Request deletion?"),
				L.F("An admin will review your request. \"{0}\" will stay in every player's library until an admin approves it.", _vm.Name), L.T("Request deletion"), L.T("Cancel"))
			: await DisplayAlertAsync(L.T("Delete game?"),
				L.F("\"{0}\" will disappear from every player's library.", _vm.Name), L.T("Delete"), L.T("Cancel"));
		if (confirmed)
			await _vm.DeleteAsync();
	}

	/// <summary>Takes back the moderator's request that's still waiting for review, after asking.</summary>
	private async void OnWithdrawClicked(object? sender, EventArgs e)
	{
		var confirmed = await DisplayAlertAsync(L.T("Withdraw request?"),
			L.T("Your request will be taken back before an admin reviews it. Nothing will change for players."), L.T("Withdraw"), L.T("Cancel"));
		if (confirmed)
			await _vm.WithdrawAsync();
	}

	/// <summary>
	/// Remembers the piece of rules text the cursor is in, so a picture can be added there.
	/// </summary>
	private async void OnRulesEditorFocused(object? sender, FocusEventArgs e)
	{
		_lastRulesEditor = sender as Editor;
		if (_movingImage is null || _lastRulesEditor is not { } editor) return;

		// Moving a picture: the tap is the place. Remember it once the cursor has moved there, then close the
		// keyboard so Place here is in view (it would otherwise be behind the keyboard).
		await Task.Delay(300);
		if (_lastRulesEditor != editor) return;
		_moveCursor = CursorOf(editor);
		editor.Unfocus();
		if (editor.IsSoftInputShowing())
			await editor.HideSoftInputAsync(CancellationToken.None);
	}

	/// <summary>
	/// Where the cursor is in a text box. On Android straight from the text field: the editor's own value can
	/// lag behind after a tap, especially at the start or end of a word or line.
	/// </summary>
	private static int CursorOf(Editor editor)
	{
#if ANDROID
		if (editor.Handler?.PlatformView is Android.Widget.EditText field && field.SelectionStart >= 0)
			return field.SelectionStart;
#endif
		return editor.CursorPosition;
	}

	/// <summary>Where the cursor was when the text was tapped while moving a picture (the keyboard is closed by then).</summary>
	private int _moveCursor;

	/// <summary>
	/// The piece of text that was last tapped into and where its cursor is.
	/// Null when none was tapped (or it's gone, merged after a picture left).
	/// </summary>
	private (EditableRulesBlock Block, int Cursor)? RulesCursor() =>
		_lastRulesEditor?.BindingContext is EditableRulesBlock { IsText: true } block && _vm.RulesBlocks.Contains(block)
			? (block, _movingImage is null ? CursorOf(_lastRulesEditor) : _moveCursor)
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
			if (await MakePictureAsync(cover: false) is { } picture)
				reference = await _vm.UploadPictureAsync(picture);
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

		var edit = L.T("Edit image");
		var move = L.T("Move to another place");
		var remove = L.T("Remove");

		StopMoving();
		var drawing = await _vm.GetDrawingAsync(imageRef);
		string[] choices = drawing is null ? [move] : [edit, move];
		var choice = await DisplayActionSheetAsync(L.T("Picture"), L.T("Cancel"), remove, choices);
		if (choice == edit)
		{
			// The picture's own shape comes with its drawing.
			if (await ImageEditorPage.OpenAsync(Navigation, cover: false, drawing) is { } edited
				&& await _vm.UploadPictureAsync(edited) is { } reference)
				image.ImageRef = reference;
		}
		else if (choice == move)
		{
			// The place it goes is tapped from now on, not where the cursor happened to be. A text box that still
			// has the focus would not report a new tap, so it lets go first.
			if (_lastRulesEditor is { IsFocused: true } focused)
			{
				focused.Unfocus();
				if (focused.IsSoftInputShowing())
					await focused.HideSoftInputAsync(CancellationToken.None);
			}
			_lastRulesEditor = null;
			_movingImage = image;
			MoveBar.IsVisible = true;
			SaveBar.IsVisible = false;
		}
		else if (choice == remove)
		{
			if (await DisplayAlertAsync(L.T("Remove this picture?"), L.T("The picture is taken out of the rules."), L.T("Remove"), L.T("Keep")))
				EditableRulesBlock.RemoveImage(_vm.RulesBlocks, image);
		}
	}

	/// <summary>Puts the picture that's being moved at the cursor in the rules text.</summary>
	private async void OnPlaceHereClicked(object? sender, EventArgs e)
	{
		if (_movingImage is not { } image) return;

		if (RulesCursor() is not { } cursor)
		{
			await DisplayAlertAsync(L.T("Tap in the text first"), L.T("Tap in the rules where the picture should go, then tap Place here."), "OK");
			return;
		}

		StopMoving();
		EditableRulesBlock.MoveImage(_vm.RulesBlocks, image, cursor.Block, cursor.Cursor);
		_lastRulesEditor = null;
	}

	/// <summary>Stops moving a picture; it stays where it was.</summary>
	private void OnCancelMoveClicked(object? sender, EventArgs e) => StopMoving();

	/// <summary>Hides the bar for moving a picture and forgets which picture it was.</summary>
	private void StopMoving()
	{
		_movingImage = null;
		MoveBar.IsVisible = false;
		SaveBar.IsVisible = true;
	}
}
