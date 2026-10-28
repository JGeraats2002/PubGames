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
	/// <summary>In alphabetical order in the app's language, the two marks last.</summary>
	private static (DrawKind Kind, string Text)[] AddChoices =>
	[
		.. new (DrawKind Kind, string Text)[]
		{
			(DrawKind.Arrow, L.T("Arrow")), (DrawKind.Card, L.T("Card")), (DrawKind.Coaster, L.T("Coaster")), (DrawKind.Coin, L.T("Coin")),
			(DrawKind.Die, L.T("Die")), (DrawKind.Drink, L.T("Drink")), (DrawKind.Hand, L.T("Hand")), (DrawKind.Number, L.T("Number")),
			(DrawKind.Person, L.T("Person")), (DrawKind.Table, L.T("Table")), (DrawKind.Text, L.T("Text"))
		}.OrderBy(choice => choice.Text, StringComparer.CurrentCultureIgnoreCase),
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

	/// <summary>
	/// Opens the editor for a cover (square, with a background colour) or a rules picture (wide), with an
	/// earlier drawing to change, if there is one.
	/// </summary>
	private ImageEditorPage(bool cover, string? drawing)
	{
		InitializeComponent();
		_cover = cover;
		// Covers are shown square; rules pictures are wide.
		_board.IsSquare = cover;
		if (drawing is not null && _board.Load(drawing))
		{
			_opened = _board.ToJson();
			Title = Heading.Text = L.T("Edit image");
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

	/// <summary>
	/// The page got its size (or the phone turned): the drawing area takes the width and the image's shape.
	/// </summary>
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

	/// <summary>Leaves the editor without using the image, after asking when something was drawn.</summary>
	private async void OnCancelClicked(object? sender, EventArgs e)
	{
		var nothingToLose = _opened is not null ? _board.ToJson() == _opened : _board.Items.Count == 0;
		if (!nothingToLose && !await DisplayAlertAsync(
				_opened is null ? L.T("Discard this image?") : L.T("Discard your changes?"),
				_opened is null ? L.T("What you made here will be lost.") : L.T("The image stays as it was."),
				L.T("Discard"), L.T("Keep editing")))
			return;

		_result.TrySetResult(null);
		await Navigation.PopModalAsync();
	}

	/// <summary>
	/// Uses the image: it's turned into a picture and handed back with its drawing, so it can be changed
	/// later.
	/// </summary>
	private async void OnUseClicked(object? sender, EventArgs e)
	{
		if (_board.Items.Count == 0)
		{
			await DisplayAlertAsync(L.T("Nothing to use yet"), L.T("Add something to the image first, with the buttons at the top."), "OK");
			return;
		}

		// Rude gestures are misuse of the app and of admin or moderator rights.
		if (_board.HasRudeHand)
		{
			await DisplayAlertAsync(L.T("This image can't be used"),
				L.T("A hand showing only the middle finger is offensive and not allowed in Pub Games. Change the fingers or remove the hand. Misuse of the app can cost you your admin or moderator rights."), "OK");
			return;
		}

		_result.TrySetResult(new EditedImage(Render(), _board.ToJson()));
		await Navigation.PopModalAsync();
	}

	/// <summary>The drawing as a PNG, without the selection frame.</summary>
	private byte[] Render() => _board.ToPng();

	/// <summary>
	/// Adds something to the drawing and selects it: a table goes under everything, the rest a little apart
	/// so they don't land on top of each other.
	/// </summary>
	private void AddItem(DrawKind kind)
	{
		Checkpoint();
		DrawItem item;
		if (kind == DrawKind.Table)
		{
			// The table is where everything else lies on: in the middle, under what's there already.
			item = new DrawItem { Kind = kind, X = DrawingBoard.Width / 2, Y = _board.Height / 2, AlignX = _alignAcross, AlignY = _alignDown };
			_board.Items.Insert(0, item);
		}
		else
		{
			// Each new item a little further down, so they don't land exactly on top of each other.
			var step = _board.Items.Count % 5 * 40;
			item = new DrawItem
			{
				Kind = kind, X = DrawingBoard.Width / 2 - 80 + step, Y = _board.Height / 2 - 80 + step,
				AlignX = _alignAcross, AlignY = _alignDown
			};
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

	/// <summary>Opens the editor's help.</summary>
	private async void OnInfoClicked(object? sender, EventArgs e) => await InfoButton.ShowAsync(this, Help.ImageEditor(_cover));

	/// <summary>
	/// Selects an item (or nothing): its frame shows and its choices appear below the drawing.
	/// </summary>
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

	/// <summary>
	/// A touch on the screen as a place on the drawing (whose size doesn't depend on the screen).
	/// </summary>
	private PointF ToBoard(PointF touch)
	{
		var scale = (float)(DrawingBoard.Width / Canvas.Width);
		return new PointF(touch.X * scale, touch.Y * scale);
	}

	/// <summary>A finger touches the drawing: the item under it is selected and can be dragged.</summary>
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

	/// <summary>
	/// Dragging moves the selected item; two fingers make it bigger or smaller and turn it.
	/// </summary>
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
			KeepAligned(item, () =>
			{
				item.Scale = Math.Clamp(_pinchScale * distance / _pinchDistance, MinScale, MaxScale);
				item.Rotation = ((_pinchRotation + angle - _pinchAngle) % 360 + 360) % 360;
			});
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

	/// <summary>The fingers are lifted: the drag or pinch is done.</summary>
	private void OnCanvasTouchEnded(object? sender, TouchEventArgs e)
	{
		// The choices show where the item is now, and its new size and angle.
		_pinching = false;
		if (!_moved) return;
		_moved = false;
		ShowOptions();
	}

	// ----- The choices below the drawing -----

	/// <summary>
	/// Shows the choices below the drawing: of the selected item, or of the image itself when nothing is
	/// selected.
	/// </summary>
	private void ShowOptions()
	{
		Options.Clear();

		if (_board.Selected is not { } item)
		{
			Options.Add(Hint(_board.Items.Count == 0
				? L.T("Add something with the buttons at the top. Then drag it into place.")
				: L.T("Tap something on the image to change it, or add more with the buttons at the top.")));
			Options.Add(SortedChips(L.T("Shape of the image"), [L.T("Square"), L.T("Wide")], i => _board.IsSquare == (i == 0), i =>
			{
				_board.IsSquare = i == 0;
				foreach (var placed in _board.Items)
					placed.Y = Math.Min(placed.Y, _board.Height);
				OnSizeAllocated(Width, Height);
			}));
			if (_cover)
				Options.Add(SortedChips(L.T("Background colour"), DrawingBoard.BackgroundNames, i => _board.Background == i, i => _board.Background = i));
			return;
		}

		Options.Add(new Label { Text = item.Label, FontAttributes = FontAttributes.Bold, FontSize = 16 });

		switch (item.Kind)
		{
			case DrawKind.Text:
				var entry = new Entry { Text = item.Text, Placeholder = L.T("Type the text"), MaxLength = 40 };
				// One step back for Undo per time the text is typed in, not per letter.
				var typing = false;
				entry.TextChanged += (_, args) =>
				{
					if (!typing)
					{
						Checkpoint();
						typing = true;
					}
					KeepAligned(item, () => item.Text = args.NewTextValue ?? string.Empty);
					Canvas.Invalidate();
				};
				entry.Unfocused += (_, _) => typing = false;
				Options.Add(entry);
				Options.Add(ColorChips(item));
				break;

			case DrawKind.Arrow:
				Options.Add(Chips(L.T("Shape"), [L.T("Straight"), L.T("Curved"), L.T("Bent")],
					i => i == (item.Bent ? 2 : item.Curved ? 1 : 0),
					i => (item.Curved, item.Bent) = (i == 1, i == 2)));
				Options.Add(Chips(L.T("Points"), [L.T("One way"), L.T("Both ways")], i => item.BothEnds == (i == 1), i => item.BothEnds = i == 1));
				if (item.Bent)
				{
					// The corner stays put; Turn below turns the whole arrow around it.
					Options.Add(Hint(L.T("Arm 1 points right from the corner, arm 2 is the angle further round (clockwise). One way: the head is on arm 2. Both ways: two hands, like a clock.")));
					Options.Add(SliderRow(L.T("Angle"), "°", 0, 359, (int)MathF.Round(item.BendAngle), value => item.BendAngle = value));
					Options.Add(SliderRow(L.T("Arm 1"), string.Empty, 30, 500, (int)MathF.Round(item.Arm1Length), value => item.Arm1Length = value));
					Options.Add(SliderRow(L.T("Arm 2"), string.Empty, 30, 500, (int)MathF.Round(item.Arm2Length), value => item.Arm2Length = value));
				}
				else
					Options.Add(SliderRow(L.T("Length"), string.Empty, 60, 1000, (int)MathF.Round(item.ArrowLength), value => item.ArrowLength = value));
				Options.Add(SliderRow(L.T("Thickness"), string.Empty, 4, 40, (int)MathF.Round(item.ArrowThickness), value => item.ArrowThickness = value));
				Options.Add(ColorChips(item));
				break;

			case DrawKind.Card:
				Options.Add(Chips(L.T("What"), [L.T("One card"), L.T("Deck")], i => item.Deck == (i == 1), i => item.Deck = i == 1));
				Options.Add(Chips(item.Deck ? L.T("Deck") : L.T("Side"), item.Deck ? [L.T("Open"), L.T("Closed")] : [L.T("Face up"), L.T("Face down")],
					i => item.FaceDown == (i == 1), i => item.FaceDown = i == 1));
				if (!item.FaceDown)
				{
					Options.Add(SortedChips(item.Deck ? L.T("Suit of the card on top") : L.T("Suit"), DrawItem.Suits, i => item.Suit == i, i => item.Suit = i));
					Options.Add(Chips(item.Deck ? L.T("Value of the card on top") : L.T("Value"), DrawItem.Ranks, i => item.Rank == i, i => item.Rank = i));
				}
				break;

			case DrawKind.Die:
				Options.Add(Chips(L.T("What"), DrawItem.DiceShapes, i => item.Shape == i, i => item.Shape = i));
				if (item.Shape != 2)
					Options.Add(Chips(item.Shape == 0 ? L.T("Number") : L.T("Number on the left die"), ["1", "2", "3", "4", "5", "6"],
						i => item.Value == i + 1, i => item.Value = i + 1));
				break;

			case DrawKind.Coin:
				Options.Add(SortedChips(L.T("Side"), DrawItem.CoinSides, i => item.Side == i, i => item.Side = i));
				break;

			case DrawKind.Coaster:
				Options.Add(SortedChips(L.T("Shape"), DrawItem.CoasterShapes, i => item.Shape == i, i => item.Shape = i));
				Options.Add(ColorChips(item));
				break;

			case DrawKind.Hand:
				Options.Add(Chips(L.T("Hand"), [L.T("Neutral (open hand)"), L.T("Choose fingers")], i => item.Neutral == (i == 0), i => item.Neutral = i == 0));
				if (!item.Neutral)
					// Starts as a fist; every finger tapped here sticks out.
					Options.Add(Chips(L.T("Fingers sticking out (none: a fist)"), DrawItem.FingerNames,
						i => item.Fingers[i], i => item.Fingers[i] = !item.Fingers[i]));
				Options.Add(Chips(L.T("Which hand"), [L.T("Right"), L.T("Left")], i => item.Mirrored == (i == 1), i => item.Mirrored = i == 1));
				break;

			case DrawKind.Drink:
				Options.Add(SortedChips(L.T("Kind"), DrawItem.DrinkShapes, i => item.Shape == i, i => item.Shape = i));
				Options.Add(Chips(L.T("How full"), DrawItem.FillNames, i => item.Fill == i, i => item.Fill = i));
				break;

			case DrawKind.Person:
				Options.Add(ColorChips(item));
				break;

			case DrawKind.Table:
				Options.Add(SortedChips(L.T("Shape"), DrawItem.TableShapes, i => item.Shape == i, i => item.Shape = i));
				Options.Add(SortedChips(L.T("Surface"), DrawItem.TableSurfaces, i => item.Side == i, i => item.Side = i));
				break;

			case DrawKind.Number:
				Options.Add(Chips(L.T("Number"), ["1", "2", "3", "4", "5", "6", "7", "8", "9"], i => item.Value == i + 1, i => item.Value = i + 1));
				Options.Add(ColorChips(item));
				break;
		}

		// Help with lining things up; dragging on the image stays free.
		Options.Add(Chips(L.T("Place across"), [L.T("Left"), "¼", L.T("Middle"), "¾", L.T("Right")],
			i => Math.Abs(item.FrameCentre.X - AlignedPlace(i, DrawingBoard.Width, Extent(item).Width)) < 1,
			i => MoveFrameCentre(item, true, AlignedPlace(i, DrawingBoard.Width, Extent(item).Width)), keepAligned: false));
		Options.Add(Chips(L.T("Place up and down"), [L.T("Top"), "¼", L.T("Middle"), "¾", L.T("Bottom")],
			i => Math.Abs(item.FrameCentre.Y - AlignedPlace(i, _board.Height, Extent(item).Height)) < 1,
			i => MoveFrameCentre(item, false, AlignedPlace(i, _board.Height, Extent(item).Height)), keepAligned: false));

		// The exact place, measured the way chosen with the alignment icons: from the image's left edge,
		// centre or right edge to the same point of the item (and top, middle or bottom down).
		Options.Add(PlaceRow(item, across: true));
		Options.Add(PlaceRow(item, across: false));

		// Size as a percentage of the normal size, turn in degrees: whole numbers, so two items can be made exactly alike.
		Options.Add(SliderRow(L.T("Size"), "%", (int)(MinScale * 100), (int)(MaxScale * 100), (int)MathF.Round(item.Scale * 100), value => item.Scale = value / 100f));
		Options.Add(SliderRow(L.T("Turn"), "°", 0, 360, (int)MathF.Round(item.Rotation), value => item.Rotation = value % 360));
		Options.Add(Chips(null, [L.T("Turn ↺ 45°"), L.T("Turn ↻ 45°"), L.T("Upright")], _ => false, i =>
			item.Rotation = i == 2 ? 0 : (MathF.Round(item.Rotation / 45) * 45 + (i == 0 ? 315 : 45)) % 360));

		Options.Add(Chips(null, [L.T("Copy"), L.T("To front"), L.T("To back")], keepAligned: false, isOn: _ => false, choose: i =>
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
			Text = L.T("Remove from image"), HeightRequest = 48, FontSize = 13, HorizontalOptions = LayoutOptions.Start,
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

	// ----- Alignment and exact place -----
	//
	// Each item has an alignment, chosen with the stripe icons: left, centre or
	// right (and top, middle or bottom). Across and Down give where that side
	// (or the middle) of its frame is - the frame fits tightly around the
	// drawing - always counted from the left and top edge of the drawing area.
	// Changing the alignment leaves the item where it is: only the number
	// changes, to the side it now gives. Making the item bigger or smaller
	// (size, pinching, length, text, ...) keeps that side where it is, so it
	// grows away from it; centred, it grows the same in every direction.

	private const string AlignAcrossKey = "imageEditor.alignAcross";
	private const string AlignDownKey = "imageEditor.alignDown";

	/// <summary>The alignment chosen last, for new items: 0 left / top, 1 centre / middle, 2 right / bottom.</summary>
	private int _alignAcross = Preferences.Default.Get(AlignAcrossKey, 1);
	private int _alignDown = Preferences.Default.Get(AlignDownKey, 1);

	/// <summary>The item's alignment; one from before alignment existed counts as centred.</summary>
	private static int AlignOf(DrawItem item, bool across) => (across ? item.AlignX : item.AlignY) ?? 1;

	/// <summary>The three alignment icons and the number (slider and box) itself.</summary>
	private View PlaceRow(DrawItem item, bool across)
	{
		var align = AlignOf(item, across);
		var chosen = across ? item.AlignX : item.AlignY;
		var icons = new HorizontalStackLayout { Spacing = 6 };
		for (var alignment = 0; alignment < 3; alignment++)
		{
			var index = alignment;
			icons.Add(AlignIcon(across, index, chosen == index, () =>
			{
				// The item stays where it is; the number changes to the side it now gives.
				Checkpoint();
				if (across)
				{
					item.AlignX = index;
					_alignAcross = index;
					Preferences.Default.Set(AlignAcrossKey, index);
				}
				else
				{
					item.AlignY = index;
					_alignDown = index;
					Preferences.Default.Set(AlignDownKey, index);
				}
				Changed();
			}));
		}
		var (minimum, maximum) = Range(item, across);
		var slider = SliderRow(across ? L.T("Across") : L.T("Down"), string.Empty, (int)MathF.Ceiling(minimum), (int)MathF.Floor(maximum),
			(int)MathF.Round(Measured(item, across)), value => PlaceAt(item, across, value), keepAligned: false);
		return new VerticalStackLayout
		{
			Spacing = 4,
			Children =
			{
				icons,
				slider
			}
		};
	}

	/// <summary>Half the item's frame on the drawing area, the size of the area, and the alignment - across or down.</summary>
	private (float Half, float Size, int Align) Axis(DrawItem item, bool across) => across
		? (Extent(item).Width, DrawingBoard.Width, AlignOf(item, true))
		: (Extent(item).Height, _board.Height, AlignOf(item, false));

	/// <summary>Where the measured side (or middle) of the frame is, from the left or top edge of the drawing area.</summary>
	private float Measured(DrawItem item, bool across)
	{
		var (half, _, align) = Axis(item, across);
		var centre = item.FrameCentre;
		return (across ? centre.X : centre.Y) + (align - 1) * half;
	}

	/// <summary>Moves the item so the middle of its frame is at this place across or down.</summary>
	private static void MoveFrameCentre(DrawItem item, bool across, float place)
	{
		var centre = item.FrameCentre;
		if (across) item.X += place - centre.X;
		else item.Y += place - centre.Y;
	}

	/// <summary>
	/// The numbers that keep the whole frame inside the drawing area (an item
	/// bigger than the area can only be in its middle).
	/// </summary>
	private (float Minimum, float Maximum) Range(DrawItem item, bool across)
	{
		var (half, size, align) = Axis(item, across);
		var lowestCentre = Math.Min(half, size / 2);
		var highestCentre = Math.Max(size - half, size / 2);
		return (lowestCentre + (align - 1) * half, highestCentre + (align - 1) * half);
	}

	/// <summary>Puts the measured side (or middle) at this number, kept inside the drawing area.</summary>
	private void PlaceAt(DrawItem item, bool across, float value)
	{
		var (half, _, align) = Axis(item, across);
		var (minimum, maximum) = Range(item, across);
		MoveFrameCentre(item, across, Math.Clamp(value, minimum, maximum) - (align - 1) * half);
	}

	/// <summary>A change of size or turn, after which the measured sides (or middles) are where they were.</summary>
	private void KeepAligned(DrawItem item, Action change)
	{
		var (across, down) = (Measured(item, true), Measured(item, false));
		change();
		PlaceAt(item, true, across);
		PlaceAt(item, false, down);
	}

	/// <summary>
	/// An alignment icon like in a word processor: stripes of different
	/// lengths lined up left, centred or right - or, for down, top, middle or bottom.
	/// </summary>
	private static View AlignIcon(bool across, int align, bool isOn, Action tapped)
	{
		var icon = new GraphicsView
		{
			Drawable = new AlignStripes(across, align, isOn ? Colors.White : Resource("TextPrimary")),
			WidthRequest = 30, HeightRequest = 26, InputTransparent = true
		};
		var chip = new Border
		{
			Content = icon, Padding = new Thickness(10, 11),
			BackgroundColor = isOn ? Resource("Primary") : Colors.Transparent,
			Stroke = isOn ? Resource("Primary") : Resource("Border"), StrokeThickness = 1,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }
		};
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => tapped();
		chip.GestureRecognizers.Add(tap);
		string[] names = across ? [L.T("Align left"), L.T("Align centre"), L.T("Align right")] : [L.T("Align top"), L.T("Align middle"), L.T("Align bottom")];
		SemanticProperties.SetDescription(chip, names[align]);
		return chip;
	}

	/// <summary>Four stripes of different lengths, lined up on one side or centred.</summary>
	private sealed class AlignStripes(bool across, int align, Color color) : IDrawable
	{
		private static readonly float[] Lengths = [1f, 0.6f, 0.85f, 0.5f];

		/// <summary>Draws the stripes of an alignment icon in its colour.</summary>
		public void Draw(ICanvas canvas, RectF rect)
		{
			canvas.StrokeColor = color;
			canvas.StrokeSize = 2.5f;
			canvas.StrokeLineCap = LineCap.Round;
			const float inset = 2f;
			var (along, width) = across ? (rect.Width - 2 * inset, rect.Height) : (rect.Height - 2 * inset, rect.Width);
			for (var stripe = 0; stripe < Lengths.Length; stripe++)
			{
				var length = along * Lengths[stripe];
				var start = align switch { 0 => inset, 2 => inset + along - length, _ => inset + (along - length) / 2 };
				var at = inset + (width - 2 * inset) * (stripe + 0.5f) / Lengths.Length;
				if (across)
					canvas.DrawLine(start, at, start + length, at);
				else
					canvas.DrawLine(at, start, at, start + length);
			}
		}
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
		var half = item.FrameHalf;
		var angle = item.Rotation * MathF.PI / 180;
		var (cos, sin) = (MathF.Abs(MathF.Cos(angle)), MathF.Abs(MathF.Sin(angle)));
		return new SizeF((half.Width * cos + half.Height * sin) * item.Scale, (half.Width * sin + half.Height * cos) * item.Scale);
	}

	/// <summary>The colour choices of an item.</summary>
	private View ColorChips(DrawItem item) =>
		SortedChips(L.T("Colour"), DrawItem.ColorNames, i => item.ColorIndex == i, i => item.ColorIndex = i);

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
	/// <param name="keepAligned">
	/// The selected item's aligned side (or middle) stays where it is: when
	/// the choice makes it bigger or smaller, it grows or shrinks from there.
	/// Off for choices that move the item on purpose.
	/// </param>
	private View Chips(string? title, string[] choices, Func<int, bool> isOn, Action<int> choose, bool keepAligned = true)
	{
		var row = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
		for (var choiceIndex = 0; choiceIndex < choices.Length; choiceIndex++)
		{
			var index = choiceIndex;
			var chip = Chip(choices[choiceIndex], isOn(choiceIndex), () =>
			{
				Checkpoint();
				if (keepAligned && _board.Selected is { } selected)
					KeepAligned(selected, () => choose(index));
				else
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

	/// <summary>One round choice button; filled when it's the chosen one.</summary>
	private static Button Chip(string text, bool isOn, Action tapped)
	{
		var chip = new Button
		{
			Text = text, HeightRequest = 48, Padding = new Thickness(14, 0), FontSize = 13, CornerRadius = 24,
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
	/// <param name="keepAligned">As with Chips: off for Across and Down, which move the item.</param>
	private View SliderRow(string title, string unit, int minimum, int maximum, int value, Action<int> change, bool keepAligned = true)
	{
		void set(int chosen)
		{
			if (keepAligned && _board.Selected is { } selected)
				KeepAligned(selected, () => change(chosen));
			else
				change(chosen);
		}

		value = Math.Clamp(value, minimum, maximum);
		var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = value, VerticalOptions = LayoutOptions.Center };
		var entry = new Entry
		{
			Text = value.ToString(), Keyboard = minimum < 0 ? Keyboard.Telephone : Keyboard.Numeric, MaxLength = 5, FontSize = 14,
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
				new ColumnDefinition(68), new ColumnDefinition(GridLength.Star), new ColumnDefinition(64), new ColumnDefinition(14)
			}
		};
		grid.Add(new Label { Text = title, FontSize = 12, TextColor = Resource("TextSecondary"), VerticalOptions = LayoutOptions.Center });
		grid.Add(slider, 1);
		grid.Add(entry, 2);
		grid.Add(new Label { Text = unit, FontSize = 14, VerticalOptions = LayoutOptions.Center }, 3);
		return grid;
	}

	/// <summary>A short line of grey text.</summary>
	private static Label Hint(string text) => new() { Text = text, FontSize = 13, TextColor = Resource("TextSecondary") };

	/// <summary>A colour from the app's resources (App.xaml).</summary>
	private static Color Resource(string key) => (Color)Application.Current!.Resources[key];
}
