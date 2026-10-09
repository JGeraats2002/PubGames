using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using PubGames.Models;

namespace PubGames.Views;

/// <summary>
/// The category dropdown, used on every page that shows categories so they
/// look and behave the same everywhere. Closed, it takes one line that says
/// which categories are selected; tapped, it opens the category clouds. What
/// a tap on a cloud does is up to the page (see CategoryTapped): select
/// categories for a game, or filter a list of games.
/// </summary>
public partial class CategoryClouds : ContentView
{
	public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
		nameof(ItemsSource), typeof(IEnumerable), typeof(CategoryClouds),
		propertyChanged: (view, old, items) => ((CategoryClouds)view).OnItemsChanged((IEnumerable?)old, (IEnumerable?)items));

	public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
		nameof(Placeholder), typeof(string), typeof(CategoryClouds), "All categories",
		propertyChanged: (view, _, _) => ((CategoryClouds)view).ShowSummary());

	public static readonly BindableProperty AlwaysOpenProperty = BindableProperty.Create(
		nameof(AlwaysOpen), typeof(bool), typeof(CategoryClouds), false,
		propertyChanged: (view, _, open) =>
		{
			var clouds = (CategoryClouds)view;
			clouds.Header.IsVisible = !(bool)open;
			clouds.Clouds.IsVisible = (bool)open;
		});

	/// <summary>Just the clouds, without the line to open and close them (the filter page).</summary>
	public bool AlwaysOpen
	{
		get => (bool)GetValue(AlwaysOpenProperty);
		set => SetValue(AlwaysOpenProperty, value);
	}

	/// <summary>The clouds: CategoryChoice items, selected ones highlighted.</summary>
	public IEnumerable? ItemsSource
	{
		get => (IEnumerable?)GetValue(ItemsSourceProperty);
		set => SetValue(ItemsSourceProperty, value);
	}

	/// <summary>What the closed dropdown says while no category is selected.</summary>
	public string Placeholder
	{
		get => (string)GetValue(PlaceholderProperty);
		set => SetValue(PlaceholderProperty, value);
	}

	public event EventHandler<CategoryChoice>? CategoryTapped;

	/// <summary>The choices being watched, so the closed dropdown always says what's selected.</summary>
	private readonly List<CategoryChoice> _watched = new();

	public CategoryClouds()
	{
		InitializeComponent();
		ShowSummary();
	}

	private void OnItemsChanged(IEnumerable? old, IEnumerable? items)
	{
		if (old is INotifyCollectionChanged oldList)
			oldList.CollectionChanged -= OnListChanged;
		if (items is INotifyCollectionChanged newList)
			newList.CollectionChanged += OnListChanged;

		BindableLayout.SetItemsSource(Clouds, items);
		Watch();
	}

	private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e) => Watch();

	private void Watch()
	{
		foreach (var choice in _watched)
			choice.PropertyChanged -= OnChoiceChanged;
		_watched.Clear();
		_watched.AddRange(ItemsSource?.OfType<CategoryChoice>() ?? []);
		foreach (var choice in _watched)
			choice.PropertyChanged += OnChoiceChanged;
		ShowSummary();
	}

	private void OnChoiceChanged(object? sender, PropertyChangedEventArgs e) => ShowSummary();

	/// <summary>The selected categories on one line ("All" in a filter is not a category, so it isn't listed).</summary>
	private void ShowSummary()
	{
		var selected = _watched.Where(c => c.IsSelected && c.Category.Id.Length > 0).Select(c => c.Category.Name).ToList();
		Summary.Text = selected.Count == 0 ? Placeholder : string.Join(", ", selected);
		Summary.FontAttributes = selected.Count == 0 ? FontAttributes.None : FontAttributes.Bold;
	}

	private void OnHeaderTapped(object? sender, TappedEventArgs e)
	{
		Clouds.IsVisible = !Clouds.IsVisible;
		Arrow.Text = Clouds.IsVisible ? "▴" : "▾";
	}

	private void OnCloudTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is CategoryChoice choice)
			CategoryTapped?.Invoke(this, choice);
	}
}
