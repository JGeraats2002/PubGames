using Microsoft.Maui.Graphics.Platform;
using PubGames.Models;

namespace PubGames.Views;

/// <summary>
/// Makes an explanation picture without leaving the app: arrows, text, cards,
/// dice, hands, drinks and more on a white sheet, all in one style. Opened
/// with OpenAsync, which gives back the picture and the drawing it was made
/// from; with that drawing the picture can be opened here again later.
/// </summary>
public partial class ImageEditorPage : ContentPage
{
	/// <summary>In alphabetical order, the two marks last.</summary>
	private static readonly (DrawKind Kind, string Text)[] AddChoices =
	[
		(DrawKind.Arrow, "Arrow"), (DrawKind.Card, "Card"), (DrawKind.Coaster, "Coaster"), (DrawKind.Coin, "Coin"),
		(DrawKind.Die, "Die"), (DrawKind.Drink, "Drink"), (DrawKind.Hand, "Hand"), (DrawKind.Number, "Number"),
		(DrawKind.Person, "Person"), (DrawKind.Table, "Table"), (DrawKind.Text, "Text"),
		(DrawKind.Check, "✓"), (DrawKind.Cross, "✕")
	];

	private const float MinScale = 0.3f;
	private const float MaxScale = 3f;

	/// <summary>The saved picture's width in pixels.</summary>
	private const int ExportWidth = 1200;

	private readonly DrawingBoard _board = new();
	private readonly TaskCompletionSource<EditedImage?> _result = new();

	/// <summary>Editing an existing picture: leaving without changes loses nothing.</summary>
	private string? _opened;

	/// <summary>The drawing as it was before each change, newest on top; what Undo goes back to.</summary>
	private readonly Stack<string> _undo = new();

	// Moving an item with one finger.
	private PointF _grabOffset;

	/// <summary>The item was dragged, resized or turned since the finger went down.</summary>
	private bool _moved;

	// Resizing and turning it with two.
	private bool _pinching;
	private float _pinchDistance, _pinchAngle, _pinchScale, _pinchRotation;

	/// <summary>Covers may have a coloured background; rules pictures are always on white.</summary>
	private readonly bool _cover;

	private ImageEditorPage(bool cover, string? drawing)
	{
		InitializeComponent();
		_cover = cover;
		// Covers are shown square; rules pictures are wide.
		_board.IsSquare = cover;
		if (drawing is not null && _board.Load(drawing))
		{
			_opened = _board.ToJson();
			Title = Heading.Text = "Edit image";
		}
		// A rules picture is always on cream, so it's clear in the text where it begins and ends.
		if (!cover)
		{
			_board.Background = DrawingBoard.CreamBackground;
			if (_opened is not null)
				_opened = _board.ToJson();
		}
		Canvas.Drawable = _board;

		foreach (var (kind, text) in AddChoices)
			AddButtons.Add(Chip(text, isOn: false, () => AddItem(kind)));
		ShowOptions();
	}

	/// <summary>Opens the editor. Returns the picture, or null if the user cancelled.</summary>
	/// <param name="cover">For a game's cover: square to start with, and with a background colour to choose.</param>
	/// <param name="drawing">The drawing of an earlier picture to carry on with; null for an empty sheet.</param>
	public static async Task<EditedImage?> OpenAsync(INavigation navigation, bool cover, string? drawing = null)
	{
		var page = new ImageEditorPage(cover, drawing);
		await navigation.PushModalAsync(page);
		return await page._result.Task;
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (width <= 0 || height <= 0) return;

		// As wide as the page, but never so high that the choices below don't fit.
		var ratio = _board.Height / DrawingBoard.Width;
		var canvasWidth = Math.Min(width - 26, height * 0.42 / ratio);
		Canvas.WidthRequest = canvasWidth;
		Canvas.HeightRequest = canvasWidth * ratio;
	}

	/// <summary>The back button: the same as Cancel.</summary>
	protected override bool OnBackButtonPressed()
	{
		OnCancelClicked(this, EventArgs.Empty);
		return true;
	}

	private async void OnCancelClicked(object? sender, EventArgs e)
	{
		var nothingToLose = _opened is not null ? _board.ToJson() == _opened : _board.Items.Count == 0;
		if (!nothingToLose && !await DisplayAlertAsync(
				_opened is null ? "Discard this image?" : "Discard your changes?",
				_opened is null ? "What you made here will be lost." : "The image stays as it was.",
				"Discard", "Keep editing"))
			return;

		_result.TrySetResult(null);
		await Navigation.PopModalAsync();
	}

	private async void OnUseClicked(object? sender, EventArgs e)
	{
		if (_board.Items.Count == 0)
		{
			await DisplayAlertAsync("Nothing to use yet", "Add something to the image first, with the buttons at the top.", "OK");
			return;
		}

		_result.TrySetResult(new EditedImage(Render(), _board.ToJson()));
		await Navigation.PopModalAsync();
	}

	/// <summary>The drawing as a PNG, without the selection frame.</summary>
	private byte[] Render()
	{
		_board.Selected = null;
		var height = (int)(ExportWidth * _board.Height / DrawingBoard.Width);
		using var context = new PlatformBitmapExportContext(ExportWidth, height, 1f);
		_board.Draw(context.Canvas, new RectF(0, 0, ExportWidth, height));
		using var stream = new MemoryStream();
		context.WriteToStream(stream);
		return stream.ToArray();
	}

	private void AddItem(DrawKind kind)
	{
		Checkpoint();
		DrawItem item;
		if (kind == DrawKind.Table)
		{
			// The table is where everything else lies on: in the middle, under what's there already.
			item = new DrawItem { Kind = kind, X = DrawingBoard.Width / 2, Y = _board.Height / 2 };
			_board.Items.Insert(0, item);
		}
		else
		{
			// Each new item a little further down, so they don't land exactly on top of each other.
			var step = _board.Items.Count % 5 * 40;
			item = new DrawItem { Kind = kind, X = DrawingBoard.Width / 2 - 80 + step, Y = _board.Height / 2 - 80 + step };
			_board.Items.Add(item);
		}
		Select(item);
	}

	/// <summary>Remembers the drawing as it is now, just before a change, so Undo can go back to it.</summary>
	private void Checkpoint()
	{
		var now = _board.ToJson();
		if (_undo.Count == 0 || _undo.Peek() != now)
			_undo.Push(now);
	}

	/// <summary>Takes back the last change (and the one before, each time it's tapped).</summary>
	private void OnUndoClicked(object? sender, EventArgs e)
	{
		var now = _board.ToJson();
		while (_undo.Count > 0)
		{
			var earlier = _undo.Pop();
			// Remembered before something that changed nothing after all, like a tap without dragging.
			if (earlier == now) continue;

			_board.Load(earlier);
			OnSizeAllocated(Width, Height);
			Select(null);
			return;
		}
	}

	private async void OnInfoClicked(object? sender, EventArgs e) => await InfoButton.ShowAsync(this, Help.ImageEditor(_cover));

	private void Select(DrawItem? item)
	{
		_board.Selected = item;
		ShowOptions();
		Canvas.Invalidate();
	}

	/// <summary>After a choice changed: shows it on the drawing and in the highlighted buttons.</summary>
	private void Changed()
	{
		ShowOptions();
		Canvas.Invalidate();
	}

	// ----- Touch -----

	private PointF ToBoard(PointF touch)
	{
		var scale = (float)(DrawingBoard.Width / Canvas.Width);
		return new PointF(touch.X * scale, touch.Y * scale);
	}

	private void OnCanvasTouchStarted(object? sender, TouchEventArgs e)
	{
		if (e.Touches.Length == 0) return;

		var point = ToBoard(e.Touches[0]);
		var item = _board.ItemAt(point);
		if (item != _board.Selected)
			Select(item);
		if (item is not null)
		{
			// About to be dragged, resized or turned.
			Checkpoint();
			_grabOffset = new PointF(item.X - point.X, item.Y - point.Y);
		}
		_pinching = false;
	}

	private void OnCanvasTouchMoved(object? sender, TouchEventArgs e)
	{
		if (_board.Selected is not { } item || e.Touches.Length == 0) return;

		if (e.Touches.Length >= 2)
		{
			var a = ToBoard(e.Touches[0]);
			var b = ToBoard(e.Touches[1]);
			var distance = Math.Max(1, a.Distance(b));
			var angle = MathF.Atan2(b.Y - a.Y, b.X - a.X) * 180 / MathF.PI;
			if (!_pinching)
			{
				_pinching = true;
				(_pinchDistance, _pinchAngle, _pinchScale, _pinchRotation) = (distance, angle, item.Scale, item.Rotation);
			}
			item.Scale = Math.Clamp(_pinchScale * distance / _pinchDistance, MinScale, MaxScale);
			item.Rotation = ((_pinchRotation + angle - _pinchAngle) % 360 + 360) % 360;
		}
		else
		{
			var point = ToBoard(e.Touches[0]);
			if (_pinching)
			{
				// Back to one finger: carry on from where the item is now, without a jump.
				_pinching = false;
				_grabOffset = new PointF(item.X - point.X, item.Y - point.Y);
			}
			item.X = Math.Clamp(point.X + _grabOffset.X, 0, DrawingBoard.Width);
			item.Y = Math.Clamp(point.Y + _grabOffset.Y, 0, _board.Height);
		}
		_moved = true;
		Canvas.Invalidate();
	}

	private void OnCanvasTouchEnded(object? sender, TouchEventArgs e)
	{
		// The choices show where the item is now, and its new size and angle.
		_pinching = false;
		if (!_moved) return;
		_moved = false;
		ShowOptions();
	}

	// ----- The choices below the drawing -----

	private void ShowOptions()
	{
		Options.Clear();

		if (_board.Selected is not { } item)
		{
			Options.Add(Hint(_board.Items.Count == 0
				? "Add something with the buttons at the top. Then drag it into place."
				: "Tap something on the image to change it, or add more with the buttons at the top."));
			Options.Add(Chips("Shape of the image", ["Square", "Wide"], i => _board.IsSquare == (i == 0), i =>
			{
				_board.IsSquare = i == 0;
				foreach (var placed in _board.Items)
					placed.Y = Math.Min(placed.Y, _board.Height);
				OnSizeAllocated(Width, Height);
			}));
			if (_cover)
				Options.Add(SortedChips("Background colour", DrawingBoard.BackgroundNames, i => _board.Background == i, i => _board.Background = i));
			return;
		}

		Options.Add(new Label { Text = item.Label, FontAttributes = FontAttributes.Bold, FontSize = 16 });

		switch (item.Kind)
		{
			case DrawKind.Text:
				var entry = new Entry { Text = item.Text, Placeholder = "Type the text", MaxLength = 40 };
				// One step back for Undo per time the text is typed in, not per letter.
				var typing = false;
				entry.TextChanged += (_, args) =>
				{
					if (!typing)
					{
						Checkpoint();
						typing = true;
					}
					item.Text = args.NewTextValue ?? string.Empty;
					Canvas.Invalidate();
				};
				entry.Unfocused += (_, _) => typing = false;
				Options.Add(entry);
				Options.Add(ColorChips(item));
				break;

			case DrawKind.Arrow:
				Options.Add(Chips("Shape", ["Straight", "Curved"], i => item.Curved == (i == 1), i => item.Curved = i == 1));
				Options.Add(Chips("Points", ["One way", "Both ways"], i => item.BothEnds == (i == 1), i => item.BothEnds = i == 1));
				Options.Add(ColorChips(item));
				break;

			case DrawKind.Card:
				Options.Add(Chips("What", ["One card", "Deck"], i => item.Deck == (i == 1), i => item.Deck = i == 1));
				Options.Add(Chips(item.Deck ? "Deck" : "Side", item.Deck ? ["Open", "Closed"] : ["Face up", "Face down"],
					i => item.FaceDown == (i == 1), i => item.FaceDown = i == 1));
				if (!item.FaceDown)
				{
					Options.Add(SortedChips(item.Deck ? "Suit of the card on top" : "Suit", DrawItem.Suits, i => item.Suit == i, i => item.Suit = i));
					Options.Add(Chips(item.Deck ? "Value of the card on top" : "Value", DrawItem.Ranks, i => item.Rank == i, i => item.Rank = i));
				}
				break;

			case DrawKind.Die:
				Options.Add(Chips("What", DrawItem.DiceShapes, i => item.Shape == i, i => item.Shape = i));
				if (item.Shape != 2)
					Options.Add(Chips(item.Shape == 0 ? "Number" : "Number on the left die", ["1", "2", "3", "4", "5", "6"],
						i => item.Value == i + 1, i => item.Value = i + 1));
				break;

			case DrawKind.Coin:
				Options.Add(Chips("Side", DrawItem.CoinSides, i => item.Side == i, i => item.Side = i));
				break;

			case DrawKind.Coaster:
				Options.Add(Chips("Shape", DrawItem.CoasterShapes, i => item.Shape == i, i => item.Shape = i));
				Options.Add(ColorChips(item));
				break;

			case DrawKind.Hand:
				Options.Add(Chips("Hand", ["Neutral (open hand)", "Choose fingers"], i => item.Neutral == (i == 0), i => item.Neutral = i == 0));
				if (!item.Neutral)
					// Starts as a fist; every finger tapped here sticks out.
					Options.Add(Chips("Fingers sticking out (none: a fist)", DrawItem.FingerNames,
						i => item.Fingers[i], i => item.Fingers[i] = !item.Fingers[i]));
				Options.Add(Chips("Which hand", ["Right", "Left"], i => item.Mirrored == (i == 1), i => item.Mirrored = i == 1));
				break;

			case DrawKind.Drink:
				Options.Add(SortedChips("Kind", DrawItem.DrinkShapes, i => item.Shape == i, i => item.Shape = i));
				Options.Add(Chips("How full", DrawItem.FillNames, i => item.Fill == i, i => item.Fill = i));
				break;

			case DrawKind.Person:
				Options.Add(ColorChips(item));
				break;

			case DrawKind.Table:
				Options.Add(Chips("Shape", DrawItem.TableShapes, i => item.Shape == i, i => item.Shape = i));
				Options.Add(SortedChips("Surface", DrawItem.TableSurfaces, i => item.Side == i, i => item.Side = i));
				break;

			case DrawKind.Number:
				Options.Add(Chips("Number", ["1", "2", "3", "4", "5", "6", "7", "8", "9"], i => item.Value == i + 1, i => item.Value = i + 1));
				Options.Add(ColorChips(item));
				break;
		}

		// Help with lining things up; dragging on the image stays free.
		Options.Add(Chips("Place across", ["Left", "¼", "Middle", "¾", "Right"],
			i => Math.Abs(item.X - AlignedPlace(i, DrawingBoard.Width, Extent(item).Width)) < 1,
			i => item.X = AlignedPlace(i, DrawingBoard.Width, Extent(item).Width)));
		Options.Add(Chips("Place up and down", ["Top", "¼", "Middle", "¾", "Bottom"],
			i => Math.Abs(item.Y - AlignedPlace(i, _board.Height, Extent(item).Height)) < 1,
			i => item.Y = AlignedPlace(i, _board.Height, Extent(item).Height)));

		// The exact place of the item's centre: 0 is the left or top edge, across goes to 1000.
		Options.Add(SliderRow("Across", string.Empty, 0, (int)DrawingBoard.Width, (int)MathF.Round(item.X), value => item.X = value));
		Options.Add(SliderRow("Down", string.Empty, 0, (int)_board.Height, (int)MathF.Round(item.Y), value => item.Y = value));

		// Size as a percentage of the normal size, turn in degrees: whole numbers, so two items can be made exactly alike.
		Options.Add(SliderRow("Size", "%", (int)(MinScale * 100), (int)(MaxScale * 100), (int)MathF.Round(item.Scale * 100), value => item.Scale = value / 100f));
		Options.Add(SliderRow("Turn", "°", 0, 360, (int)MathF.Round(item.Rotation), value => item.Rotation = value % 360));
		Options.Add(Chips(null, ["Turn ↺ 45°", "Turn ↻ 45°", "Upright"], _ => false, i =>
			item.Rotation = i == 2 ? 0 : (MathF.Round(item.Rotation / 45) * 45 + (i == 0 ? 315 : 45)) % 360));

		Options.Add(Chips(null, ["Copy", "To front", "To back"], _ => false, i =>
		{
			if (i == 0)
			{
				var copy = item.Clone();
				copy.X = Math.Min(copy.X + 50, DrawingBoard.Width);
				copy.Y = Math.Min(copy.Y + 50, _board.Height);
				_board.Items.Add(copy);
				_board.Selected = copy;
				return;
			}
			_board.Items.Remove(item);
			_board.Items.Insert(i == 1 ? _board.Items.Count : 0, item);
		}));

		var delete = new Button
		{
			Text = "Remove from image", HeightRequest = 40, FontSize = 13, HorizontalOptions = LayoutOptions.Start,
			BackgroundColor = Colors.Transparent, TextColor = Resource("Danger"), BorderColor = Resource("Danger"), BorderWidth = 1
		};
		delete.Clicked += (_, _) =>
		{
			Checkpoint();
			_board.Items.Remove(item);
			Select(null);
		};
		Options.Add(delete);
	}

	/// <summary>
	/// Where an item's centre goes for an alignment button: 0 and 4 put its
	/// edge against the edge of the image, 1-3 put its centre on the quarter lines.
	/// </summary>
	/// <param name="size">The image's width or height.</param>
	/// <param name="halfExtent">Half the room the item takes in that direction.</param>
	private static float AlignedPlace(int quarter, float size, float halfExtent) => quarter switch
	{
		0 => Math.Min(halfExtent, size / 2),
		4 => Math.Max(size - halfExtent, size / 2),
		_ => size * quarter / 4
	};

	/// <summary>Half the width and height an item takes on the image, as it's sized and turned now.</summary>
	private static SizeF Extent(DrawItem item)
	{
		var half = item.HalfSize;
		var angle = item.Rotation * MathF.PI / 180;
		var (cos, sin) = (MathF.Abs(MathF.Cos(angle)), MathF.Abs(MathF.Sin(angle)));
		return new SizeF((half.Width * cos + half.Height * sin) * item.Scale, (half.Width * sin + half.Height * cos) * item.Scale);
	}

	private View ColorChips(DrawItem item) =>
		SortedChips("Colour", DrawItem.ColorNames, i => item.ColorIndex == i, i => item.ColorIndex = i);

	/// <summary>
	/// Chips in alphabetical order, whatever order the choices are kept in:
	/// a saved drawing remembers a choice by its place in that list, so the
	/// list itself can't be reordered.
	/// </summary>
	private View SortedChips(string? title, string[] choices, Func<int, bool> isOn, Action<int> choose)
	{
		// "♠ Spades" sorts as Spades.
		var order = Enumerable.Range(0, choices.Length)
			.OrderBy(i => new string(choices[i].SkipWhile(c => !char.IsLetter(c)).ToArray()), StringComparer.CurrentCultureIgnoreCase)
			.ToArray();
		return Chips(title, order.Select(i => choices[i]).ToArray(), i => isOn(order[i]), i => choose(order[i]));
	}

	/// <summary>A row of buttons to choose from; the chosen ones are highlighted.</summary>
	private View Chips(string? title, string[] choices, Func<int, bool> isOn, Action<int> choose)
	{
		var row = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
		for (var i = 0; i < choices.Length; i++)
		{
			var index = i;
			var chip = Chip(choices[i], isOn(i), () =>
			{
				Checkpoint();
				choose(index);
				Changed();
			});
			chip.Margin = new Thickness(0, 0, 6, 6);
			row.Add(chip);
		}
		if (title is null) return row;

		return new VerticalStackLayout
		{
			Spacing = 4,
			Children = { new Label { Text = title, FontSize = 12, TextColor = Resource("TextSecondary") }, row }
		};
	}

	private static Button Chip(string text, bool isOn, Action tapped)
	{
		var chip = new Button
		{
			Text = text, HeightRequest = 38, Padding = new Thickness(14, 0), FontSize = 13, CornerRadius = 19,
			BackgroundColor = isOn ? Resource("Primary") : Colors.Transparent,
			TextColor = isOn ? Colors.White : Resource("TextPrimary"),
			BorderColor = isOn ? Resource("Primary") : Resource("Border"), BorderWidth = 1
		};
		chip.Clicked += (_, _) => tapped();
		return chip;
	}

	/// <summary>
	/// A slider with its value next to it, which can also be typed in exactly.
	/// The drawing follows while the slider moves or a number is typed; the
	/// buttons aren't rebuilt, so neither is interrupted.
	/// </summary>
	private View SliderRow(string title, string unit, int minimum, int maximum, int value, Action<int> set)
	{
		value = Math.Clamp(value, minimum, maximum);
		var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = value, VerticalOptions = LayoutOptions.Center };
		var entry = new Entry
		{
			Text = value.ToString(), Keyboard = Keyboard.Numeric, MaxLength = 4, FontSize = 14,
			HorizontalTextAlignment = TextAlignment.End, VerticalOptions = LayoutOptions.Center
		};

		// One of the two changes the other; this keeps that from going round in circles.
		var syncing = false;
		// One step back for Undo per drag of the slider or number typed, not per value it passes.
		var remembered = false;
		void Remember()
		{
			if (remembered) return;
			Checkpoint();
			remembered = true;
		}
		slider.DragCompleted += (_, _) => remembered = false;
		slider.ValueChanged += (_, args) =>
		{
			if (syncing) return;
			Remember();
			var chosen = (int)Math.Round(args.NewValue);
			syncing = true;
			entry.Text = chosen.ToString();
			syncing = false;
			set(chosen);
			Canvas.Invalidate();
		};
		entry.TextChanged += (_, args) =>
		{
			// While typing: only numbers that can be used as they are ("1" on the way to "120" is left alone).
			if (syncing || !int.TryParse(args.NewTextValue, out var typed) || typed < minimum || typed > maximum) return;
			Remember();
			syncing = true;
			slider.Value = typed;
			syncing = false;
			set(typed);
			Canvas.Invalidate();
		};
		// Done typing: a number outside the limits becomes the nearest one that's allowed.
		void Settle()
		{
			var typed = int.TryParse(entry.Text, out var number) ? Math.Clamp(number, minimum, maximum) : (int)Math.Round(slider.Value);
			syncing = true;
			entry.Text = typed.ToString();
			slider.Value = typed;
			syncing = false;
			set(typed);
			Canvas.Invalidate();
		}
		entry.Completed += (_, _) => Settle();
		entry.Unfocused += (_, _) =>
		{
			Settle();
			remembered = false;
		};

		var grid = new Grid
		{
			ColumnSpacing = 6,
			ColumnDefinitions =
			{
				new ColumnDefinition(48), new ColumnDefinition(GridLength.Star), new ColumnDefinition(64), new ColumnDefinition(14)
			}
		};
		grid.Add(new Label { Text = title, FontSize = 12, TextColor = Resource("TextSecondary"), VerticalOptions = LayoutOptions.Center });
		grid.Add(slider, 1);
		grid.Add(entry, 2);
		grid.Add(new Label { Text = unit, FontSize = 14, VerticalOptions = LayoutOptions.Center }, 3);
		return grid;
	}

	private static Label Hint(string text) => new() { Text = text, FontSize = 13, TextColor = Resource("TextSecondary") };

	private static Color Resource(string key) => (Color)Application.Current!.Resources[key];
}
