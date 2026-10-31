using System.Collections.ObjectModel;
using PubGames.Models;
using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// The rules and agreements for admins and moderators (AdminAgreements),
/// the last page of the Admin tab. It opens first the first time someone
/// signs in as an admin or moderator, and again when the agreements change;
/// until they tap "I've read this and agree" the other admin pages stay
/// closed. The owner can change the agreements here with ✏ Edit.
/// </summary>
public partial class AgreementsPage : ContentPage
{
	private readonly ITeamService _team;
	private readonly AgreementsService _agreements;

	/// <summary>The sections while the owner edits them.</summary>
	private readonly ObservableCollection<EditableAgreement> _editing = new();

	/// <summary>The owner is editing: the sections are boxes, and Cancel / Save show.</summary>
	private bool _isEditing;

	/// <summary>Sets up the page with the agreements in the app's language; the owner gets ✏ Edit.</summary>
	public AgreementsPage(ITeamService team, AgreementsService agreements)
	{
		InitializeComponent();
		_team = team;
		_agreements = agreements;
		BindableLayout.SetItemsSource(EditList, _editing);
		if (_team.Me?.IsBuiltInAdmin == true)
		{
			var edit = new ToolbarItem { Text = L.T("✏ Edit"), Order = ToolbarItemOrder.Primary, Command = new Command(StartEditing) };
			SemanticProperties.SetDescription(edit, L.T("Change the agreements"));
			ToolbarItems.Add(edit);
		}
	}

	/// <summary>Every time the page shows: the newest agreements, agreed or not, and why it opened.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		ShowState();
		if (_isEditing) return;
		await _agreements.RefreshAsync();
		if (!_isEditing) ShowState();
	}

	/// <summary>The agreements as they are; the button while not agreed yet, afterwards a line that says so.</summary>
	private void ShowState()
	{
		ReadPanel.IsVisible = !_isEditing;
		EditPanel.IsVisible = _isEditing;
		EditButtons.IsVisible = _isEditing;
		if (_isEditing)
		{
			AgreeButton.IsVisible = AgreedLabel.IsVisible = NoticeBox.IsVisible = false;
			return;
		}

		BindableLayout.SetItemsSource(SectionsList, AdminAgreements.Sections);
		var agreed = AdminAgreements.IsAgreed(_team.Me);
		AgreeButton.IsVisible = !agreed;
		AgreedLabel.IsVisible = agreed;
		NoticeBox.IsVisible = !agreed;
		var before = _team.Me is { } me && (me.AgreedRulesVersion > 0 || Preferences.Default.Get(AdminAgreements.LocalKey(me.Email), 0) > 0);
		NoticeLabel.Text = before
			? L.T("The agreements have changed. Read them again and agree to them to use the admin pages.")
			: L.T("Welcome to the team. Read these agreements and agree to them to use the admin pages.");
	}

	/// <summary>Agreed: remembered, and the admin pages open up, starting with the library.</summary>
	private async void OnAgreeClicked(object? sender, EventArgs e)
	{
		AgreeButton.IsEnabled = false;
		try
		{
			await _team.AgreeToRulesAsync();
			ShowState();
			await Shell.Current.GoToAsync(AppShell.LibraryRoute);
		}
		finally
		{
			AgreeButton.IsEnabled = true;
		}
	}

	/// <summary>✏ Edit (owner only): the sections as they show now become boxes to change.</summary>
	private async void StartEditing()
	{
		if (_isEditing) return;
		FillEditor(AdminAgreements.Sections);
		_isEditing = true;
		EditMessage.IsVisible = false;
		ShowState();
		await PageScroll.ScrollToAsync(0, 0, false);
	}

	/// <summary>Puts these sections in the editor.</summary>
	private void FillEditor(IEnumerable<HelpSection> sections)
	{
		while (_editing.Count > 0)
			_editing.RemoveAt(0);
		foreach (var section in sections)
			_editing.Add(EditableAgreement.From(section));
	}

	/// <summary>A new, empty section at the bottom.</summary>
	private void OnAddSectionClicked(object? sender, EventArgs e) => _editing.Add(new EditableAgreement());

	/// <summary>✕: takes a section out.</summary>
	private void OnRemoveSectionClicked(object? sender, EventArgs e)
	{
		if ((sender as BindableObject)?.BindingContext is EditableAgreement section)
			_editing.Remove(section);
	}

	/// <summary>The agreements as they come with the app, to start from again (only saved with Save).</summary>
	private async void OnBuiltInClicked(object? sender, EventArgs e)
	{
		if (await DisplayAlertAsync(L.T("Back to the standard agreements?"), L.T("The boxes are filled with the agreements as they come with the app. Nothing changes until you save."),
				L.T("Fill in"), L.T("Cancel")))
			FillEditor(AdminAgreements.BuiltInSections);
	}

	/// <summary>Cancel: nothing changes.</summary>
	private void OnCancelEditClicked(object? sender, EventArgs e)
	{
		_isEditing = false;
		ShowState();
	}

	/// <summary>
	/// Save: every section needs a heading and a text. Then the owner chooses:
	/// a small correction (nobody has to agree again) or changed agreements
	/// (every admin and moderator reads them again and agrees).
	/// </summary>
	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		var sections = _editing.Select(s => s.ToSection()).ToList();
		var problem = sections.Count == 0 ? L.T("Add at least one section.")
			: sections.Any(s => s.Heading.Length == 0 || s.Text.Length == 0) ? L.T("Every section needs a heading and a text.")
			: null;
		EditMessage.Text = problem ?? string.Empty;
		EditMessage.IsVisible = problem is not null;
		if (problem is not null || _team.Me is not { IsBuiltInAdmin: true } me) return;

		var small = L.T("Small correction (nobody agrees again)");
		var changed = L.T("The agreements changed (everyone agrees again)");
		var choice = await DisplayActionSheetAsync(L.T("Save the agreements"), L.T("Cancel"), null, small, changed);
		if (choice != small && choice != changed) return;

		SaveButton.IsEnabled = false;
		try
		{
			await _agreements.SaveAsync(sections, choice == changed, me.Email);
			_isEditing = false;
			ShowState();
			await DisplayAlertAsync(L.T("Saved"), choice == changed
				? L.T("Every admin and moderator reads the new agreements and agrees to them the next time they open the app. The other languages follow in a minute.")
				: L.T("Everyone sees the corrected agreements; nobody has to agree again. The other languages follow in a minute."), "OK");
		}
		catch (Exception ex)
		{
			EditMessage.Text = L.F("Not saved: {0}", ex.Message);
			EditMessage.IsVisible = true;
		}
		finally
		{
			SaveButton.IsEnabled = true;
		}
	}
}
