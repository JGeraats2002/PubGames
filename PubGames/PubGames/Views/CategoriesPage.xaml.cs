using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// Admin tab &gt; Categories: the main categories with their categories - added, renamed, reordered or
/// deleted by admins, proposed by moderators.
/// </summary>
public partial class CategoriesPage : ContentPage
{
	private const string ChooseGames = "Choose its games";
	private const string AddCategory = "Add a category";
	private const string Rename = "Rename";
	private const string MoveCategory = "Move to another main category";
	private const string Delete = "Delete";

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
			_vm.IsAdmin ? "New category" : "Propose a new category",
			group is not null ? $"Name of the new category in \"{group.Name}\":"
				: _vm.IsAdmin ? "Name of the category:" : "Name of the category. An admin will review it first:",
			group is null ? "Next" : "Add", "Cancel", placeholder: "e.g. Party", maxLength: 30);
		if (string.IsNullOrWhiteSpace(name) || !_vm.CheckNewName(name, group)) return;

		// Every category is filed under a main category (unless none exist yet).
		if (group is null && _vm.Groups.Count > 0)
		{
			group = await ChooseGroupAsync($"Main category for \"{GameCategory.NormalizeName(name)}\"", _vm.Groups);
			if (group is null) return;
		}

		if (await _vm.AddAsync(name, group) is { } done)
			await DisplayAlertAsync("Done", done, "OK");
	}

	/// <summary>Adds a main category (admins) or proposes one (moderators).</summary>
	private async void OnAddGroupClicked(object? sender, EventArgs e)
	{
		var name = await DisplayPromptAsync(
			_vm.IsAdmin ? "New main category" : "Propose a new main category",
			_vm.IsAdmin
				? "Name of the main category. Every game will need at least one of its categories:"
				: "Name of the main category. An admin will review it first:",
			_vm.IsAdmin ? "Add" : "Submit for review", "Cancel", placeholder: "e.g. Location", maxLength: 30);
		if (string.IsNullOrWhiteSpace(name)) return;

		if (await _vm.AddGroupAsync(name) is { } done)
			await DisplayAlertAsync("Done", done, "OK");
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

		var title = _vm.IsAdmin ? row.Name : $"{row.Name} - propose a change";
		switch (await DisplayActionSheetAsync(title, "Cancel", Delete, actions.ToArray()))
		{
			case ChooseGames:
				await Shell.Current.GoToAsync(nameof(CategoryGamesPage), new Dictionary<string, object> { ["categoryId"] = category.Id });
				break;

			case Rename:
				if (await AskNewNameAsync("Rename category", row.Name) is { } name)
					await TellAsync(_vm.RenameAsync(category, name));
				break;

			case MoveCategory:
				if (await ChooseGroupAsync($"Move \"{row.Name}\" to", otherGroups) is { } group)
					await TellAsync(_vm.MoveAsync(category, group));
				break;

			case Delete:
				var games = row.GameCount == 0 ? "No games are in it." : $"It's taken off {row.CountLabel}.";
				if (await ConfirmDeleteAsync(row.Name, games))
					await TellAsync(_vm.DeleteAsync(category));
				break;
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

		var title = _vm.IsAdmin ? group.Name : $"{group.Name} - propose a change";
		switch (await DisplayActionSheetAsync(title, "Cancel", Delete, AddCategory, Rename))
		{
			case AddCategory:
				await AddCategoryAsync(group);
				break;

			case Rename:
				if (await AskNewNameAsync("Rename main category", group.Name) is { } name)
					await TellAsync(_vm.RenameGroupAsync(group, name));
				break;

			case Delete:
				if (!_vm.CanDeleteGroup)
				{
					await DisplayAlertAsync("Can't delete", "This is the last main category. Add another one first, or rename this one.", "OK");
					break;
				}

				var categories = section.Count == 0
					? "It has no categories."
					: $"Its categories ({string.Join(", ", section.Select(r => r.Name))}) are deleted too and taken off every game.";
				if (await ConfirmDeleteAsync(group.Name, categories))
					await TellAsync(_vm.DeleteGroupAsync(group));
				break;
		}
	}

	/// <summary>Admins delete; moderators ask an admin to.</summary>
	private Task<bool> ConfirmDeleteAsync(string name, string consequence) => _vm.IsAdmin
		? DisplayAlertAsync($"Delete \"{name}\"?", $"{consequence} This can't be undone.", "Delete", "Cancel")
		: DisplayAlertAsync($"Propose deleting \"{name}\"?", $"{consequence} An admin will review this first; nothing changes until then.", "Submit for review", "Cancel");

	/// <summary>Shows what a change gives back to tell the user (moderators: that it was submitted for review).</summary>
	private async Task TellAsync(Task<string?> change)
	{
		if (await change is { } message)
			await DisplayAlertAsync("Submitted for review", message, "OK");
	}

	/// <summary>Null when cancelled or unchanged.</summary>
	private async Task<string?> AskNewNameAsync(string title, string currentName)
	{
		var name = await DisplayPromptAsync(title, "New name:", "Rename", "Cancel", initialValue: currentName, maxLength: 30);
		return string.IsNullOrWhiteSpace(name) || GameCategory.NormalizeName(name) == currentName ? null : name;
	}

	/// <summary>Null when cancelled.</summary>
	private async Task<CategoryGroup?> ChooseGroupAsync(string title, List<CategoryGroup> groups)
	{
		var chosen = await DisplayActionSheetAsync(title, "Cancel", null, groups.Select(g => g.Name).ToArray());
		return groups.FirstOrDefault(g => g.Name == chosen);
	}
}
