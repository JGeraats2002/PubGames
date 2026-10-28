using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// Admin tab &gt; Categories: the main categories with their categories - added, renamed, reordered or
/// deleted by admins, proposed by moderators.
/// </summary>
public partial class CategoriesPage : ContentPage
{
	private static string ChooseGames => L.T("Choose its games");
	private static string AddCategory => L.T("Add a category");
	private static string Rename => L.T("Rename");
	private static string MoveCategory => L.T("Move to another main category");
	private static string Delete => L.T("Delete");

	private readonly CategoriesViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public CategoriesPage(CategoriesViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Categories(_vm.IsAdmin));
	}

	/// <summary>Also runs when coming back from choosing a category's games, so the counts update.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Adds a category (admins) or proposes one (moderators).</summary>
	private async void OnAddClicked(object? sender, EventArgs e) => await AddCategoryAsync(group: null);

	/// <param name="group">The main category to add it to; null to ask.</param>
	private async Task AddCategoryAsync(CategoryGroup? group)
	{
		var name = await DisplayPromptAsync(
			_vm.IsAdmin ? L.T("New category") : L.T("Propose a new category"),
			group is not null ? L.F("Name of the new category in \"{0}\":", group.Name)
				: _vm.IsAdmin ? L.T("Name of the category:") : L.T("Name of the category. An admin will review it first:"),
			group is null ? L.T("Next") : L.T("Add"), L.T("Cancel"), placeholder: L.T("e.g. Party"), maxLength: 30);
		if (string.IsNullOrWhiteSpace(name) || !_vm.CheckNewName(name, group)) return;

		// Every category is filed under a main category (unless none exist yet).
		if (group is null && _vm.Groups.Count > 0)
		{
			group = await ChooseGroupAsync(L.F("Main category for \"{0}\"", GameCategory.NormalizeName(name)), _vm.Groups);
			if (group is null) return;
		}

		if (await _vm.AddAsync(name, group) is { } done)
			await DisplayAlertAsync(L.T("Done"), done, "OK");
	}

	/// <summary>Adds a main category (admins) or proposes one (moderators).</summary>
	private async void OnAddGroupClicked(object? sender, EventArgs e)
	{
		var name = await DisplayPromptAsync(
			_vm.IsAdmin ? L.T("New main category") : L.T("Propose a new main category"),
			_vm.IsAdmin
				? L.T("Name of the main category. Every game will need at least one of its categories:")
				: L.T("Name of the main category. An admin will review it first:"),
			_vm.IsAdmin ? L.T("Add") : L.T("Submit for review"), L.T("Cancel"), placeholder: L.T("e.g. Location"), maxLength: 30);
		if (string.IsNullOrWhiteSpace(name)) return;

		if (await _vm.AddGroupAsync(name) is { } done)
			await DisplayAlertAsync(L.T("Done"), done, "OK");
	}

	/// <summary>Everything that can be done with a category: admins do it, moderators propose it.</summary>
	private async void OnCategoryTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not CategoryRow row) return;

		var category = row.Category;
		var otherGroups = _vm.Groups.Where(g => g.Id != category.GroupId).ToList();
		var actions = new List<string>();
		if (_vm.IsAdmin) actions.Add(ChooseGames);
		actions.Add(Rename);
		if (otherGroups.Count > 0) actions.Add(MoveCategory);

		var title = _vm.IsAdmin ? row.Name : L.F("{0} - propose a change", row.Name);
		var choice = await DisplayActionSheetAsync(title, L.T("Cancel"), Delete, actions.ToArray());
		if (choice == ChooseGames)
			await Shell.Current.GoToAsync(nameof(CategoryGamesPage), new Dictionary<string, object> { ["categoryId"] = category.Id });
		else if (choice == Rename)
		{
			if (await AskNewNameAsync(L.T("Rename category"), row.Name) is { } name)
				await TellAsync(_vm.RenameAsync(category, name));
		}
		else if (choice == MoveCategory)
		{
			if (await ChooseGroupAsync(L.F("Move \"{0}\" to", row.Name), otherGroups) is { } group)
				await TellAsync(_vm.MoveAsync(category, group));
		}
		else if (choice == Delete)
		{
			var games = row.GameCount == 0 ? L.T("No games are in it.") : L.F("It's taken off {0}.", row.CountLabel);
			if (await ConfirmDeleteAsync(row.Name, games))
				await TellAsync(_vm.DeleteAsync(category));
		}
	}

	// The arrows on a category's row: one place up or down within its main category. At the top or bottom nothing happens.

	/// <summary>Moves a category one place up in its main category.</summary>
	private async void OnCategoryUpClicked(object? sender, EventArgs e) => await MoveCategoryAsync(sender, -1);

	/// <summary>Moves a category one place down in its main category.</summary>
	private async void OnCategoryDownClicked(object? sender, EventArgs e) => await MoveCategoryAsync(sender, +1);

	/// <summary>Moves the tapped category one place up (-1) or down (+1).</summary>
	private async Task MoveCategoryAsync(object? sender, int direction)
	{
		if (sender is BindableObject { BindingContext: CategoryRow row }
			&& _vm.Sections.FirstOrDefault(s => s.Contains(row)) is { } section)
			await _vm.ReorderAsync(section, row, direction);
	}

	/// <summary>Everything that can be done with a main category: admins do it, moderators propose it.</summary>
	private async void OnGroupTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not CategorySection { CanEdit: true, Group: { } group } section) return;

		var title = _vm.IsAdmin ? group.Name : L.F("{0} - propose a change", group.Name);
		var choice = await DisplayActionSheetAsync(title, L.T("Cancel"), Delete, AddCategory, Rename);
		if (choice == AddCategory)
			await AddCategoryAsync(group);
		else if (choice == Rename)
		{
			if (await AskNewNameAsync(L.T("Rename main category"), group.Name) is { } name)
				await TellAsync(_vm.RenameGroupAsync(group, name));
		}
		else if (choice == Delete)
		{
			if (!_vm.CanDeleteGroup)
			{
				await DisplayAlertAsync(L.T("Can't delete"), L.T("This is the last main category. Add another one first, or rename this one."), "OK");
				return;
			}

			var categories = section.Count == 0
				? L.T("It has no categories.")
				: L.F("Its categories ({0}) are deleted too and taken off every game.", string.Join(", ", section.Select(r => r.Name)));
			if (await ConfirmDeleteAsync(group.Name, categories))
				await TellAsync(_vm.DeleteGroupAsync(group));
		}
	}

	/// <summary>Admins delete; moderators ask an admin to.</summary>
	private Task<bool> ConfirmDeleteAsync(string name, string consequence) => _vm.IsAdmin
		? DisplayAlertAsync(L.F("Delete \"{0}\"?", name), L.F("{0} This can't be undone.", consequence), L.T("Delete"), L.T("Cancel"))
		: DisplayAlertAsync(L.F("Propose deleting \"{0}\"?", name), L.F("{0} An admin will review this first; nothing changes until then.", consequence), L.T("Submit for review"), L.T("Cancel"));

	/// <summary>Shows what a change gives back to tell the user (moderators: that it was submitted for review).</summary>
	private async Task TellAsync(Task<string?> change)
	{
		if (await change is { } message)
			await DisplayAlertAsync(L.T("Submitted for review"), message, "OK");
	}

	/// <summary>Null when cancelled or unchanged.</summary>
	private async Task<string?> AskNewNameAsync(string title, string currentName)
	{
		var name = await DisplayPromptAsync(title, L.T("New name:"), L.T("Rename"), L.T("Cancel"), initialValue: currentName, maxLength: 30);
		return string.IsNullOrWhiteSpace(name) || GameCategory.NormalizeName(name) == currentName ? null : name;
	}

	/// <summary>Null when cancelled.</summary>
	private async Task<CategoryGroup?> ChooseGroupAsync(string title, List<CategoryGroup> groups)
	{
		var chosen = await DisplayActionSheetAsync(title, L.T("Cancel"), null, groups.Select(g => g.Name).ToArray());
		return groups.FirstOrDefault(g => g.Name == chosen);
	}
}
