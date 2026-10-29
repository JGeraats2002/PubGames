using System.Collections;
using System.ComponentModel;
using System.Windows.Input;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// A sentence as one label whose words can be tapped (one span per word), for choosing what to change or
/// remove on the Translations page. One label per sentence keeps the page light: a view per word made long
/// rules too slow to open.
/// </summary>
public class WordsLabel : Label
{
	/// <summary>The words of the sentence.</summary>
	public static readonly BindableProperty WordsProperty = BindableProperty.Create(
		nameof(Words), typeof(IList), typeof(WordsLabel), propertyChanged: (view, _, _) => ((WordsLabel)view).Build());

	/// <summary>What tapping a word does (gets the WordChip).</summary>
	public static readonly BindableProperty TapCommandProperty = BindableProperty.Create(
		nameof(TapCommand), typeof(ICommand), typeof(WordsLabel), propertyChanged: (view, _, _) => ((WordsLabel)view).Build());

	/// <summary>The words of the sentence.</summary>
	public IList? Words
	{
		get => (IList?)GetValue(WordsProperty);
		set => SetValue(WordsProperty, value);
	}

	/// <summary>What tapping a word does.</summary>
	public ICommand? TapCommand
	{
		get => (ICommand?)GetValue(TapCommandProperty);
		set => SetValue(TapCommandProperty, value);
	}

	/// <summary>The words now watched, to stop watching them when the sentence changes.</summary>
	private readonly List<WordChip> _watched = [];

	/// <summary>One span per word, tappable, highlighted while selected.</summary>
	private void Build()
	{
		foreach (var chip in _watched)
			chip.PropertyChanged -= OnChipChanged;
		_watched.Clear();

		var text = new FormattedString();
		foreach (var chip in Words?.OfType<WordChip>() ?? [])
		{
			var span = new Span { Text = chip.Text, BindingContext = chip };
			if (TapCommand is { } command)
				span.GestureRecognizers.Add(new TapGestureRecognizer { Command = command, CommandParameter = chip });
			Paint(span, chip.IsSelected);
			text.Spans.Add(span);
			text.Spans.Add(new Span { Text = " " });
			chip.PropertyChanged += OnChipChanged;
			_watched.Add(chip);
		}
		FormattedText = text;
	}

	/// <summary>A word was selected or unselected: its span follows.</summary>
	private void OnChipChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(WordChip.IsSelected) || sender is not WordChip chip) return;
		if (FormattedText?.Spans.FirstOrDefault(s => ReferenceEquals(s.BindingContext, chip)) is { } span)
			Paint(span, chip.IsSelected);
	}

	/// <summary>Selected words are white on the app's main colour.</summary>
	private static void Paint(Span span, bool selected)
	{
		var resources = Application.Current!.Resources;
		span.BackgroundColor = selected ? (Color)resources["Primary"] : Colors.Transparent;
		span.TextColor = selected ? Colors.White : (Color)resources["TextPrimary"];
	}
}
