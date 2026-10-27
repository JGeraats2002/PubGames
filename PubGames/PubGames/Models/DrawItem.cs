using System.Text.Json;
using System.Text.Json.Serialization;
using Font = Microsoft.Maui.Graphics.Font;

namespace PubGames.Models;

/// <summary>What can be placed on a drawing in the image editor.</summary>
public enum DrawKind
{
	Arrow,
	Text,
	Card,
	Die,
	Hand,
	Drink,
	Person,
	Coin,
	Number,
	Check,
	Cross,
	Coaster,
	Table
}

/// <summary>
/// One thing on a drawing: where it is, how big and how turned, and the
/// choices for its kind (a card's suit and rank, a die's number, which
/// fingers a hand holds up, how full a cup is, ...). Everything is drawn from
/// shapes in one fixed style, so every explanation picture looks alike.
/// </summary>
public class DrawItem
{
	// Saved drawings keep a colour's place in this list, so new colours go at the end.
	public static readonly string[] ColorNames = ["Dark", "Red", "Blue", "Green", "Orange", "White", "Yellow", "Purple"];
	public static readonly string[] Suits = ["♠ Spades", "♥ Hearts", "♦ Diamonds", "♣ Clubs"];
	public static readonly string[] Ranks = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"];
	public static readonly string[] FingerNames = ["Thumb", "Index", "Middle", "Ring", "Pinky"];
	public static readonly string[] DrinkShapes = ["Cup", "Mug", "Shot glass", "Bottle"];
	public static readonly string[] FillNames = ["Empty", "¼", "½", "¾", "Full"];
	public static readonly string[] DiceShapes = ["Die", "Cup with dice", "Cup upside down"];
	public static readonly string[] CoinSides = ["Heads", "Tails"];
	public static readonly string[] CoasterShapes = ["Round", "Square"];
	public static readonly string[] TableShapes = ["Rectangle", "Round"];
	public static readonly string[] TableSurfaces = ["Wood", "Green felt"];

	private static readonly Color Outline = Color.FromArgb("#2C2C2A");
	private static readonly Color Red = Color.FromArgb("#C62828");
	private static readonly Color Blue = Color.FromArgb("#3C3489");
	private static readonly Color Green = Color.FromArgb("#0F6E56");
	private static readonly Color Orange = Color.FromArgb("#E69F00");
	private static readonly Color Skin = Color.FromArgb("#F2C7A0");
	private static readonly Color Liquid = Color.FromArgb("#F2B33D");
	private static readonly Color Glass = Color.FromArgb("#F4F8FA");
	private static readonly Color Gold = Color.FromArgb("#F6C445");
	private static readonly Color GoldDark = Color.FromArgb("#9A6B00");
	private static readonly Color Leather = Color.FromArgb("#9C6B3F");
	private static readonly Color LeatherDark = Color.FromArgb("#6E4524");
	private static readonly Color Cardboard = Color.FromArgb("#F7F1E3");
	private static readonly Color Wood = Color.FromArgb("#D2A46A");
	private static readonly Color WoodDark = Color.FromArgb("#8A5A2B");
	private static readonly Color Felt = Color.FromArgb("#3A8F6B");
	private static readonly Color[] Palette =
		[Outline, Red, Blue, Green, Orange, Colors.White, Color.FromArgb("#F2C200"), Color.FromArgb("#7B3FA0")];

	private const float Line = 6;

	public DrawKind Kind { get; init; }

	/// <summary>The centre, in the drawing's own units (see DrawingBoard).</summary>
	public float X { get; set; }
	public float Y { get; set; }

	public float Scale { get; set; } = 1;

	/// <summary>
	/// Which side of its frame the editor's Across number gives: 0 the left
	/// side, 1 the centre, 2 the right side. Null: the centre.
	/// </summary>
	public int? AlignX { get; set; }

	/// <summary>Which side Down gives: 0 the top, 1 the middle, 2 the bottom. Null: the middle.</summary>
	public int? AlignY { get; set; }

	/// <summary>Degrees, clockwise.</summary>
	public float Rotation { get; set; }

	/// <summary>Arrow, text, person, number and a coaster's print: index into ColorNames.</summary>
	public int ColorIndex { get; set; }

	public string Text { get; set; } = "Text";

	// Card
	public int Suit { get; set; }
	public int Rank { get; set; }
	public bool FaceDown { get; set; }

	/// <summary>A whole deck rather than one card; FaceDown then says whether it's closed, and Suit and Rank which card is on top.</summary>
	public bool Deck { get; set; }

	/// <summary>Coin: index into CoinSides. Table: index into TableSurfaces.</summary>
	public int Side { get; set; }

	/// <summary>The number on a die (1-6) or a number badge (1-9).</summary>
	public int Value { get; set; } = 1;

	// Hand
	/// <summary>A normal open hand. Off: a fist, with the fingers in Fingers held up.</summary>
	public bool Neutral { get; set; } = true;

	/// <summary>Which fingers stick out when not neutral; same order as FingerNames.</summary>
	public bool[] Fingers { get; set; } = new bool[5];

	public bool Mirrored { get; set; }

	// Drink, die, coaster and table
	/// <summary>Index into DrinkShapes, DiceShapes, CoasterShapes or TableShapes.</summary>
	public int Shape { get; set; }

	/// <summary>Index into FillNames: quarters full.</summary>
	public int Fill { get; set; } = 4;

	// Arrow
	public bool Curved { get; set; }
	public bool BothEnds { get; set; }

	/// <summary>
	/// An arrow with a corner: two arms from the corner, which is the arrow's
	/// centre (it turns around it). Arm 1 points right, arm 2 is BendAngle
	/// degrees further clockwise; the head is at the end of arm 2 (and of arm 1
	/// too with BothEnds) - two hands of a clock.
	/// </summary>
	public bool Bent { get; set; }

	/// <summary>Straight and curved arrows: the length from tip to tip.</summary>
	public float ArrowLength { get; set; } = DefaultArrowLength;

	/// <summary>The line's thickness; the heads grow with it.</summary>
	public float ArrowThickness { get; set; } = DefaultArrowThickness;

	/// <summary>Bent arrows: degrees from arm 1 to arm 2, clockwise (180 is straight).</summary>
	public float BendAngle { get; set; } = 90;

	/// <summary>Bent arrows: the length of each arm, from the corner to its end.</summary>
	public float Arm1Length { get; set; } = 150;
	public float Arm2Length { get; set; } = 110;

	public const float DefaultArrowLength = 230;
	public const float DefaultArrowThickness = 14;

	/// <summary>How big the heads are compared with a normal arrow: they grow with the line, but less than it does.</summary>
	[JsonIgnore]
	private float HeadScale => (ArrowThickness + DefaultArrowThickness) / (2 * DefaultArrowThickness);

	/// <summary>Bent arrows: the end of arm 2, with the corner at (0, 0).</summary>
	[JsonIgnore]
	private PointF Arm2End
	{
		get
		{
			var angle = BendAngle * MathF.PI / 180;
			return new PointF(Arm2Length * MathF.Cos(angle), Arm2Length * MathF.Sin(angle));
		}
	}

	private SizeF ArrowHalfSize()
	{
		// Room for the heads beside the line.
		var head = 32 * HeadScale;
		if (Bent)
		{
			// The corner is the centre, so the box reaches as far as the furthest arm in every direction.
			var end = Arm2End;
			return new(Math.Max(Arm1Length, Math.Abs(end.X)) + head / 2, Math.Max(head, Math.Abs(end.Y) + head / 2));
		}
		return new(ArrowLength / 2, Curved ? Math.Max(70, 40 + head) : Math.Max(45, head + 8));
	}

	[JsonIgnore]
	public string Label => Kind switch
	{
		DrawKind.Card => (Deck ? "Deck" : "Card") + (FaceDown ? Deck ? " (closed)" : " (face down)" : $" {Ranks[Rank]}{Suits[Suit][0]}"),
		DrawKind.Die => Shape == 0 ? $"Die {Value}" : DiceShapes[Shape],
		DrawKind.Coin => $"Coin ({CoinSides[Side].ToLowerInvariant()})",
		DrawKind.Drink => DrinkShapes[Shape],
		DrawKind.Number => $"Number {Value}",
		DrawKind.Check => "Check mark",
		_ => Kind.ToString()
	};

	public DrawItem Clone()
	{
		var copy = (DrawItem)MemberwiseClone();
		copy.Fingers = (bool[])Fingers.Clone();
		return copy;
	}

	/// <summary>Half the width and height of the box around it, before scaling and turning.</summary>
	[JsonIgnore]
	public SizeF HalfSize => Kind switch
	{
		DrawKind.Arrow => ArrowHalfSize(),
		DrawKind.Text => new(Math.Max(50, Text.Length * 15 + 20), 38),
		DrawKind.Card => Deck ? new(88, 118) : new(75, 105),
		DrawKind.Die => Shape == 0 ? new(75, 75) : new(80, 112),
		DrawKind.Coaster => new(78, 78),
		DrawKind.Table => Shape == 1 ? new(215, 215) : new(305, 195),
		DrawKind.Hand => new(115, 140),
		DrawKind.Drink => Shape switch { 1 => new(90, 90), 2 => new(45, 50), 3 => new(48, 140), _ => new(70, 90) },
		DrawKind.Person => new(60, 95),
		_ => new(55, 55)
	};

	/// <summary>Whether a point on the drawing is on this item (a little slack makes small things easier to grab).</summary>
	public bool Contains(PointF point)
	{
		var dx = point.X - X;
		var dy = point.Y - Y;
		var angle = -Rotation * MathF.PI / 180;
		var localX = (dx * MathF.Cos(angle) - dy * MathF.Sin(angle)) / Scale;
		var localY = (dx * MathF.Sin(angle) + dy * MathF.Cos(angle)) / Scale;
		var frame = ContentBounds();
		var slack = 12 / Scale;
		return localX >= frame.Left - slack && localX <= frame.Right + slack
			&& localY >= frame.Top - slack && localY <= frame.Bottom + slack;
	}

	public void Draw(ICanvas canvas, bool selected)
	{
		canvas.SaveState();
		canvas.Translate(X, Y);
		canvas.Rotate(Rotation);
		canvas.Scale(Scale, Scale);

		DrawContent(canvas);

		if (selected)
		{
			// Tightly around what's drawn.
			var frame = ContentBounds();
			canvas.StrokeColor = Blue;
			canvas.StrokeSize = 3 / Scale;
			canvas.StrokeDashPattern = [6, 6];
			canvas.DrawRectangle(frame.Left, frame.Top, frame.Width, frame.Height);
		}

		canvas.RestoreState();
	}

	/// <summary>The item itself, centred on (0, 0), at its normal size and unturned.</summary>
	private void DrawContent(ICanvas canvas)
	{
		canvas.StrokeColor = Outline;
		canvas.StrokeSize = Line;
		canvas.StrokeLineCap = LineCap.Round;
		canvas.StrokeLineJoin = LineJoin.Round;
		canvas.StrokeDashPattern = null;

		switch (Kind)
		{
			case DrawKind.Arrow: DrawArrow(canvas); break;
			case DrawKind.Text: DrawText(canvas); break;
			case DrawKind.Card: DrawCard(canvas); break;
			case DrawKind.Die: DrawDie(canvas); break;
			case DrawKind.Hand: DrawHand(canvas); break;
			case DrawKind.Drink: DrawDrink(canvas); break;
			case DrawKind.Person: DrawPerson(canvas); break;
			case DrawKind.Coin: DrawCoin(canvas); break;
			case DrawKind.Number: DrawNumber(canvas); break;
			case DrawKind.Check: DrawMark(canvas, Green, [(-40, 5), (-12, 35), (45, -35)]); break;
			case DrawKind.Cross: DrawMark(canvas, Red, [(-38, -38), (38, 38)], [(38, -38), (-38, 38)]); break;
			case DrawKind.Coaster: DrawCoaster(canvas); break;
			case DrawKind.Table: DrawTable(canvas); break;
		}
	}

	/// <summary>
	/// Half the width and height of the selection frame: tight around what's
	/// really drawn, before scaling and turning.
	/// </summary>
	[JsonIgnore]
	public SizeF FrameHalf
	{
		get
		{
			var content = ContentBounds();
			return new SizeF(content.Width / 2, content.Height / 2);
		}
	}

	/// <summary>
	/// The middle of the selection frame on the drawing. Not always the
	/// item's place (X, Y): that's the point it turns around, like a bent
	/// arrow's corner.
	/// </summary>
	[JsonIgnore]
	public PointF FrameCentre
	{
		get
		{
			var centre = ContentBounds().Center;
			var angle = Rotation * MathF.PI / 180;
			var (cos, sin) = (MathF.Cos(angle), MathF.Sin(angle));
			return new PointF(X + (centre.X * cos - centre.Y * sin) * Scale, Y + (centre.X * sin + centre.Y * cos) * Scale);
		}
	}

	// The measured drawing, kept until something about its look changes.
	private string? _boundsKey;
	private RectF _bounds;

	/// <summary>
	/// The area the drawing really covers, as drawn around (0, 0) at normal
	/// size and unturned: found by drawing it once out of sight and looking
	/// where the paint is. Without that (not on Android): the rough box (HalfSize).
	/// </summary>
	public RectF ContentBounds()
	{
		var half = HalfSize;
		var frame = new RectF(-half.Width, -half.Height, half.Width * 2, half.Height * 2);
		var key = LookKey();
		if (key == _boundsKey) return _bounds;
		_boundsKey = key;
		_bounds = Measure() ?? frame;
		return _bounds;
	}

	/// <summary>Everything that changes how the item looks, not where it is.</summary>
	private string LookKey()
	{
		var look = Clone();
		look.X = look.Y = look.Rotation = 0;
		look.Scale = 1;
		look.AlignX = look.AlignY = null;
		return JsonSerializer.Serialize(look);
	}

	private RectF? Measure()
	{
#if ANDROID
		var half = HalfSize;
		// Room around the rough box: some drawings reach past it (a hand's wrist; text, whose width is only estimated).
		const int margin = 60;
		var width = (int)MathF.Ceiling(half.Width * (Kind == DrawKind.Text ? 4 : 2)) + 2 * margin;
		var height = (int)MathF.Ceiling(half.Height * 2) + 2 * margin;
		try
		{
			using var context = new Microsoft.Maui.Graphics.Platform.PlatformBitmapExportContext(width, height, 1f);
			var canvas = context.Canvas;
			canvas.Translate(width / 2f, height / 2f);
			DrawContent(canvas);
			var pixels = new int[width * height];
			context.Bitmap.GetPixels(pixels, 0, width, 0, 0, width, height);
			int left = width, top = height, right = -1, bottom = -1;
			for (var y = 0; y < height; y++)
			{
				var row = y * width;
				for (var x = 0; x < width; x++)
				{
					if ((pixels[row + x] >>> 24) < 16) continue;
					if (x < left) left = x;
					if (x > right) right = x;
					if (y < top) top = y;
					if (y > bottom) bottom = y;
				}
			}
			if (right < 0) return null;
			return new RectF(left - width / 2f, top - height / 2f, right - left + 1, bottom - top + 1);
		}
		catch (Exception)
		{
			return null;
		}
#else
		return null;
#endif
	}

	private Color ItemColor => Palette[Math.Clamp(ColorIndex, 0, Palette.Length - 1)];

	private void DrawArrow(ICanvas canvas)
	{
		var color = ItemColor;
		var k = HeadScale;
		canvas.StrokeColor = color;
		canvas.StrokeSize = ArrowThickness;

		if (Bent)
		{
			// Arm 1 from its end to the corner, then arm 2; the line stops inside a head so the tip stays sharp.
			var arm1End = new PointF(Arm1Length, 0);
			var arm2End = Arm2End;
			var bent = new PathF();
			bent.MoveTo(BothEnds ? LineEndBefore(arm1End, new PointF(1, 0), k) : arm1End);
			bent.LineTo(0, 0);
			bent.LineTo(LineEndBefore(arm2End, Direction(arm2End), k));
			canvas.DrawPath(bent);
			DrawArrowHeadAtTip(canvas, color, arm2End, Direction(arm2End), k);
			if (BothEnds)
				DrawArrowHeadAtTip(canvas, color, arm1End, new PointF(1, 0), k);
			return;
		}

		var half = ArrowLength / 2;
		if (!Curved)
		{
			var tip = new PointF(half - 2, 0);
			var tail = new PointF(-tip.X, 0);
			var start = BothEnds ? LineEndBefore(tail, new PointF(-1, 0), k) : new PointF(-half + 15, 0);
			var end = LineEndBefore(tip, new PointF(1, 0), k);
			canvas.DrawLine(start, end);
			DrawArrowHeadAtTip(canvas, color, tip, new PointF(1, 0), k);
			if (BothEnds)
				DrawArrowHeadAtTip(canvas, color, tail, new PointF(-1, 0), k);
			return;
		}

		// An arc, stretched to the length; the heads point along it.
		var sx = ArrowLength / DefaultArrowLength;
		var control = new PointF(0, -75);
		var arcEnd = new PointF(82 * sx, 14);
		var arcStart = BothEnds ? new PointF(-82 * sx, 14) : new PointF(-100 * sx, 30);
		var path = new PathF();
		path.MoveTo(arcStart);
		path.QuadTo(control, arcEnd);
		canvas.DrawPath(path);
		var endDirection = Direction(new PointF(arcEnd.X - control.X, arcEnd.Y - control.Y));
		DrawArrowHeadAtTip(canvas, color, new PointF(arcEnd.X + endDirection.X * 36 * k, arcEnd.Y + endDirection.Y * 36 * k), endDirection, k);
		if (BothEnds)
		{
			var startDirection = Direction(new PointF(arcStart.X - control.X, arcStart.Y - control.Y));
			DrawArrowHeadAtTip(canvas, color, new PointF(arcStart.X + startDirection.X * 36 * k, arcStart.Y + startDirection.Y * 36 * k), startDirection, k);
		}
	}

	/// <summary>The direction of a point seen from (0, 0), as a length of 1.</summary>
	private static PointF Direction(PointF to)
	{
		var length = MathF.Max(0.001f, MathF.Sqrt(to.X * to.X + to.Y * to.Y));
		return new PointF(to.X / length, to.Y / length);
	}

	/// <summary>Where the line stops before a head with its tip at `tip`: inside the head, so the head covers the line's end.</summary>
	private static PointF LineEndBefore(PointF tip, PointF direction, float k) =>
		new(tip.X - direction.X * 43 * k, tip.Y - direction.Y * 43 * k);

	/// <summary>A filled triangle with its tip at `tip`, pointing in `direction`, `k` times the normal size.</summary>
	private static void DrawArrowHeadAtTip(ICanvas canvas, Color color, PointF tip, PointF direction, float k)
	{
		canvas.SaveState();
		canvas.Translate(tip.X, tip.Y);
		canvas.Rotate(MathF.Atan2(direction.Y, direction.X) * 180 / MathF.PI);
		var head = new PathF();
		head.MoveTo(0, 0);
		head.LineTo(-50 * k, -32 * k);
		head.LineTo(-50 * k, 32 * k);
		head.Close();
		canvas.FillColor = color;
		canvas.FillPath(head);
		canvas.RestoreState();
	}

	private void DrawText(ICanvas canvas)
	{
		var half = HalfSize;
		canvas.Font = Font.DefaultBold;
		canvas.FontSize = 48;
		canvas.FontColor = ItemColor;
		// Wider than the box: the width is an estimate, and text mustn't be cut off.
		canvas.DrawString(Text, -half.Width * 2, -half.Height, half.Width * 4, half.Height * 2,
			HorizontalAlignment.Center, VerticalAlignment.Center);
	}

	private void DrawCard(ICanvas canvas)
	{
		if (Deck)
		{
			// The cards underneath, each a little further down and to the right.
			for (var below = 4; below >= 1; below--)
			{
				canvas.SaveState();
				canvas.Translate(below * 4 - 8, below * 4 - 8);
				DrawCardSheet(canvas);
				canvas.RestoreState();
			}
			canvas.Translate(-8, -8);
		}
		DrawCardSheet(canvas);

		if (FaceDown)
		{
			canvas.FillColor = Blue;
			canvas.FillRoundedRectangle(-56, -86, 112, 172, 8);
			canvas.FillColor = Colors.White;
			canvas.FillPath(SuitPath(2, 0, 0, 34));
			return;
		}

		var color = Suit is 1 or 2 ? Red : Outline;
		canvas.Font = Font.DefaultBold;
		canvas.FontSize = 40;
		canvas.FontColor = color;
		canvas.DrawString(Ranks[Rank], -64, -96, 60, 48, HorizontalAlignment.Center, VerticalAlignment.Center);
		canvas.FillColor = color;
		canvas.FillPath(SuitPath(Suit, -34, -36, 12));
		canvas.FillPath(SuitPath(Suit, 0, 22, 42));
	}

	private static void DrawCardSheet(ICanvas canvas)
	{
		canvas.FillColor = Colors.White;
		canvas.FillRoundedRectangle(-70, -100, 140, 200, 14);
		canvas.DrawRoundedRectangle(-70, -100, 140, 200, 14);
	}

	/// <summary>A suit symbol around (cx, cy); s is roughly half its height. 0 spades, 1 hearts, 2 diamonds, 3 clubs.</summary>
	private static PathF SuitPath(int suit, float cx, float cy, float s)
	{
		var path = new PathF();
		switch (suit)
		{
			case 1:
				path.MoveTo(cx, cy + s * 0.95f);
				path.CurveTo(cx - s * 1.5f, cy - s * 0.1f, cx - s * 0.75f, cy - s * 1.25f, cx, cy - s * 0.45f);
				path.CurveTo(cx + s * 0.75f, cy - s * 1.25f, cx + s * 1.5f, cy - s * 0.1f, cx, cy + s * 0.95f);
				path.Close();
				break;

			case 2:
				path.MoveTo(cx, cy - s);
				path.LineTo(cx + s * 0.72f, cy);
				path.LineTo(cx, cy + s);
				path.LineTo(cx - s * 0.72f, cy);
				path.Close();
				break;

			case 0:
				// A heart upside down, on a stem.
				path.MoveTo(cx, cy - s);
				path.CurveTo(cx - s * 1.5f, cy + s * 0.05f, cx - s * 0.75f, cy + s * 1.1f, cx, cy + s * 0.35f);
				path.CurveTo(cx + s * 0.75f, cy + s * 1.1f, cx + s * 1.5f, cy + s * 0.05f, cx, cy - s);
				path.Close();
				AppendStem(path, cx, cy, s);
				break;

			default:
				path.AppendCircle(cx, cy - s * 0.48f, s * 0.42f);
				path.AppendCircle(cx - s * 0.46f, cy + s * 0.18f, s * 0.42f);
				path.AppendCircle(cx + s * 0.46f, cy + s * 0.18f, s * 0.42f);
				AppendStem(path, cx, cy, s);
				break;
		}
		return path;
	}

	private static void AppendStem(PathF path, float cx, float cy, float s)
	{
		path.MoveTo(cx, cy);
		path.LineTo(cx + s * 0.35f, cy + s);
		path.LineTo(cx - s * 0.35f, cy + s);
		path.Close();
	}

	private void DrawDie(ICanvas canvas)
	{
		switch (Shape)
		{
			case 1:
				// Two dice looking out over the rim; the cup covers their lower halves.
				DrawSmallDie(canvas, -28, -52, -14, Value);
				DrawSmallDie(canvas, 30, -58, 12, 7 - Math.Clamp(Value, 1, 6));
				DrawDiceCup(canvas, upsideDown: false);
				break;

			case 2:
				DrawDiceCup(canvas, upsideDown: true);
				break;

			default:
				DrawDieFace(canvas, Value);
				break;
		}
	}

	private static void DrawSmallDie(ICanvas canvas, float x, float y, float degrees, int value)
	{
		const float size = 0.45f;
		canvas.SaveState();
		canvas.Translate(x, y);
		canvas.Rotate(degrees);
		canvas.Scale(size, size);
		// The outline as thick as everything else's, not scaled down with the die.
		canvas.StrokeSize = Line / size * 0.8f;
		DrawDieFace(canvas, value);
		canvas.RestoreState();
	}

	private static void DrawDieFace(ICanvas canvas, int value)
	{
		canvas.FillColor = Colors.White;
		canvas.FillRoundedRectangle(-70, -70, 140, 140, 24);
		canvas.DrawRoundedRectangle(-70, -70, 140, 140, 24);

		const float d = 38;
		(float X, float Y)[] pips = value switch
		{
			1 => [(0, 0)],
			2 => [(-d, -d), (d, d)],
			3 => [(-d, -d), (0, 0), (d, d)],
			4 => [(-d, -d), (d, -d), (-d, d), (d, d)],
			5 => [(-d, -d), (d, -d), (0, 0), (-d, d), (d, d)],
			_ => [(-d, -d), (d, -d), (-d, 0), (d, 0), (-d, d), (d, d)]
		};
		canvas.FillColor = Outline;
		foreach (var (x, y) in pips)
			canvas.FillCircle(x, y, 14);
	}

	/// <summary>A leather dice cup: standing, or upside down as it's put on the table over the dice.</summary>
	private static void DrawDiceCup(ICanvas canvas, bool upsideDown)
	{
		canvas.SaveState();
		if (upsideDown)
			canvas.Scale(1, -1);
		var cup = new PathF();
		cup.MoveTo(-64, -40);
		cup.LineTo(64, -40);
		cup.LineTo(48, 100);
		cup.LineTo(-48, 100);
		cup.Close();
		canvas.FillColor = Leather;
		canvas.FillPath(cup);
		canvas.DrawPath(cup);
		// The rim.
		canvas.FillColor = LeatherDark;
		canvas.FillRoundedRectangle(-70, -52, 140, 22, 8);
		canvas.DrawRoundedRectangle(-70, -52, 140, 22, 8);
		canvas.RestoreState();
	}

	/// <summary>
	/// A hand seen from the back, fingers up. Neutral: a flat hand with the
	/// fingers together. Otherwise a fist, with the chosen fingers sticking
	/// out and spread. Folded fingers and a folded thumb are under the hand:
	/// they're drawn first and the hand over them, so only a knuckle shows.
	/// </summary>
	private void DrawHand(ICanvas canvas)
	{
		if (Mirrored)
			canvas.Scale(-1, 1);
		canvas.Translate(18, -8);
		canvas.FillColor = Skin;

		// Wrist.
		canvas.FillRoundedRectangle(-44, 80, 88, 68, 10);
		canvas.DrawRoundedRectangle(-44, 80, 88, 68, 10);

		// Index to pinky: where each starts on the hand, how long, and how far it leans when held up on its own.
		(float X, float Length, float Spread)[] fingers = [(-45, 98, -15), (-15, 112, -5), (15, 102, 5), (45, 78, 16)];
		for (var i = 0; i < fingers.Length; i++)
		{
			var (x, length, spread) = fingers[i];
			var isOut = Neutral || Fingers[i + 1];
			canvas.SaveState();
			canvas.Translate(x, 14);
			canvas.Rotate(isOut && !Neutral ? spread : 0);
			// Folded: only the knuckle comes out from under the hand.
			var shown = isOut ? length : 22;
			canvas.FillRoundedRectangle(-14, -shown, 28, shown + 16, 14);
			canvas.DrawRoundedRectangle(-14, -shown, 28, shown + 16, 14);
			canvas.RestoreState();
		}

		if (Neutral || Fingers[0])
		{
			// Neutral: along the side of the hand. Held up: sticking out sideways.
			canvas.SaveState();
			canvas.Translate(Neutral ? -60 : -54, Neutral ? 84 : 78);
			canvas.Rotate(Neutral ? -14 : -60);
			canvas.FillRoundedRectangle(-15, -90, 30, 106, 15);
			canvas.DrawRoundedRectangle(-15, -90, 30, 106, 15);
			canvas.RestoreState();
		}
		else
		{
			// Folded under the hand: only its knuckle shows at the side.
			canvas.FillRoundedRectangle(-78, 42, 40, 54, 16);
			canvas.DrawRoundedRectangle(-78, 42, 40, 54, 16);
		}

		// The back of the hand, over where the fingers and thumb start.
		canvas.FillRoundedRectangle(-62, 0, 124, 112, 30);
		canvas.DrawRoundedRectangle(-62, 0, 124, 112, 30);
	}

	private void DrawDrink(ICanvas canvas)
	{
		var body = new PathF();
		float top, bottom;
		switch (Shape)
		{
			case 1:
				// Mug: the handle first, so the body covers where it's attached.
				var handle = new PathF();
				handle.MoveTo(40, -42);
				handle.CurveTo(100, -42, 100, 48, 40, 48);
				canvas.StrokeSize = 16;
				canvas.DrawPath(handle);
				canvas.StrokeSize = Line;
				body.AppendRoundedRectangle(-62, -80, 110, 160, 10);
				(top, bottom) = (-80, 80);
				break;

			case 2:
				AppendTaperedGlass(body, topHalf: 36, bottomHalf: 26, halfHeight: 42);
				(top, bottom) = (-42, 42);
				break;

			case 3:
				body.MoveTo(-14, -118);
				body.LineTo(14, -118);
				body.LineTo(14, -55);
				body.CurveTo(14, -28, 40, -30, 40, -2);
				body.LineTo(40, 118);
				body.QuadTo(40, 130, 28, 130);
				body.LineTo(-28, 130);
				body.QuadTo(-40, 130, -40, 118);
				body.LineTo(-40, -2);
				body.CurveTo(-40, -30, -14, -28, -14, -55);
				body.Close();
				(top, bottom) = (-118, 130);
				break;

			default:
				AppendTaperedGlass(body, topHalf: 62, bottomHalf: 42, halfHeight: 82);
				(top, bottom) = (-82, 82);
				break;
		}

		canvas.FillColor = Glass;
		canvas.FillPath(body);

		if (Fill > 0)
		{
			// Full leaves a little room at the top, like a real drink.
			var level = bottom - (bottom - top) * 0.92f * Fill / 4;
			canvas.SaveState();
			canvas.ClipPath(body);
			canvas.FillColor = Liquid;
			canvas.FillRectangle(-120, level, 240, bottom - level);
			canvas.RestoreState();
		}

		canvas.DrawPath(body);

		if (Shape == 3)
		{
			canvas.FillColor = Outline;
			canvas.FillRoundedRectangle(-18, -132, 36, 16, 4);
		}
	}

	private static void AppendTaperedGlass(PathF path, float topHalf, float bottomHalf, float halfHeight)
	{
		path.MoveTo(-topHalf, -halfHeight);
		path.LineTo(topHalf, -halfHeight);
		path.LineTo(bottomHalf, halfHeight);
		path.LineTo(-bottomHalf, halfHeight);
		path.Close();
	}

	private void DrawPerson(ICanvas canvas)
	{
		canvas.FillColor = ItemColor;
		var body = new PathF();
		body.MoveTo(-52, 88);
		body.LineTo(-52, 30);
		body.CurveTo(-52, -22, 52, -22, 52, 30);
		body.LineTo(52, 88);
		body.Close();
		canvas.FillPath(body);
		canvas.DrawPath(body);
		canvas.FillCircle(0, -54, 30);
		canvas.DrawCircle(0, -54, 30);
	}

	private void DrawCoin(ICanvas canvas)
	{
		canvas.FillColor = Gold;
		canvas.FillCircle(0, 0, 50);
		canvas.DrawCircle(0, 0, 50);
		canvas.StrokeSize = 4;
		canvas.DrawCircle(0, 0, 40);

		if (Side == 0)
		{
			// Heads: a head and shoulders.
			canvas.FillColor = GoldDark;
			canvas.FillCircle(0, -12, 13);
			var shoulders = new PathF();
			shoulders.MoveTo(-24, 28);
			shoulders.CurveTo(-24, 0, 24, 0, 24, 28);
			shoulders.Close();
			canvas.FillPath(shoulders);
		}
		else
		{
			// Tails: what it's worth.
			canvas.Font = Font.DefaultBold;
			canvas.FontSize = 52;
			canvas.FontColor = GoldDark;
			canvas.DrawString("1", -40, -40, 80, 80, HorizontalAlignment.Center, VerticalAlignment.Center);
		}
	}

	/// <summary>A table top seen from above, to show where the game is played: wood, or green felt in a wooden rim.</summary>
	private void DrawTable(ICanvas canvas)
	{
		var felt = Side == 1;
		canvas.FillColor = felt ? WoodDark : Wood;
		if (Shape == 1)
		{
			canvas.FillCircle(0, 0, 205);
			canvas.DrawCircle(0, 0, 205);
			if (felt)
			{
				canvas.FillColor = Felt;
				canvas.FillCircle(0, 0, 183);
			}
			else
			{
				canvas.StrokeColor = WoodDark;
				canvas.StrokeSize = 4;
				canvas.DrawCircle(0, 0, 187);
			}
		}
		else
		{
			canvas.FillRoundedRectangle(-295, -185, 590, 370, 40);
			canvas.DrawRoundedRectangle(-295, -185, 590, 370, 40);
			if (felt)
			{
				canvas.FillColor = Felt;
				canvas.FillRoundedRectangle(-273, -163, 546, 326, 24);
			}
			else
			{
				canvas.StrokeColor = WoodDark;
				canvas.StrokeSize = 4;
				canvas.DrawRoundedRectangle(-277, -167, 554, 334, 26);
			}
		}
	}

	/// <summary>A beer mat, with a printed line in the chosen colour.</summary>
	private void DrawCoaster(ICanvas canvas)
	{
		canvas.FillColor = Cardboard;
		if (Shape == 1)
		{
			canvas.FillRoundedRectangle(-70, -70, 140, 140, 18);
			canvas.DrawRoundedRectangle(-70, -70, 140, 140, 18);
			canvas.StrokeColor = ItemColor;
			canvas.StrokeSize = 4;
			canvas.DrawRoundedRectangle(-56, -56, 112, 112, 10);
		}
		else
		{
			canvas.FillCircle(0, 0, 72);
			canvas.DrawCircle(0, 0, 72);
			canvas.StrokeColor = ItemColor;
			canvas.StrokeSize = 4;
			canvas.DrawCircle(0, 0, 58);
		}
	}

	private void DrawNumber(ICanvas canvas)
	{
		canvas.FillColor = ItemColor;
		canvas.FillCircle(0, 0, 50);
		canvas.Font = Font.DefaultBold;
		canvas.FontSize = 58;
		canvas.FontColor = Colors.White;
		canvas.DrawString(Value.ToString(), -50, -50, 100, 100, HorizontalAlignment.Center, VerticalAlignment.Center);
	}

	private static void DrawMark(ICanvas canvas, Color color, params (float X, float Y)[][] strokes)
	{
		canvas.StrokeColor = color;
		canvas.StrokeSize = 20;
		foreach (var stroke in strokes)
		{
			var path = new PathF();
			path.MoveTo(stroke[0].X, stroke[0].Y);
			foreach (var (x, y) in stroke.Skip(1))
				path.LineTo(x, y);
			canvas.DrawPath(path);
		}
	}
}

/// <summary>
/// A drawing made in the image editor: a sheet (white, or coloured for a cover) with items on it, the
/// last one on top. Sizes are in the drawing's own units (1000 wide), so it
/// looks the same on the phone's screen and in the saved picture.
/// </summary>
public class DrawingBoard : IDrawable
{
	public const float Width = 1000;

	/// <summary>Square for covers (they're shown square), wide for rules pictures.</summary>
	public bool IsSquare { get; set; }

	public float Height => IsSquare ? Width : 750;

	public static readonly string[] BackgroundNames = ["White", "Cream", "Yellow", "Orange", "Red", "Green", "Blue", "Purple", "Dark"];

	private static readonly Color[] BackgroundColors =
	[
		Colors.White, Color.FromArgb("#F1EFE8"), Color.FromArgb("#F7D774"), Color.FromArgb("#F2A65A"), Color.FromArgb("#D9534F"),
		Color.FromArgb("#3F9B7F"), Color.FromArgb("#3C3489"), Color.FromArgb("#7B3FA0"), Color.FromArgb("#2C2C2A")
	];

	/// <summary>The place of cream in BackgroundNames: the colour of every rules picture.</summary>
	public const int CreamBackground = 1;

	/// <summary>The sheet's colour, an index into BackgroundNames. Only covers get to choose; rules pictures are always cream.</summary>
	public int Background { get; set; }

	public List<DrawItem> Items { get; } = new();

	public DrawItem? Selected { get; set; }

	/// <summary>
	/// The drawing as text, saved with the picture so it can be opened in the
	/// editor again later (see IImageStore.GetDrawingAsync).
	/// </summary>
	public string ToJson() => JsonSerializer.Serialize(new Saved(IsSquare, Items, Background));

	/// <summary>Opens a saved drawing. False if the text isn't one.</summary>
	public bool Load(string json)
	{
		try
		{
			if (JsonSerializer.Deserialize<Saved>(json) is not { Items: not null } saved) return false;
			IsSquare = saved.IsSquare;
			Background = Math.Clamp(saved.Background, 0, BackgroundColors.Length - 1);
			Items.Clear();
			Items.AddRange(saved.Items.Where(i => i.Fingers is { Length: 5 }));
			return true;
		}
		catch (JsonException)
		{
			return false;
		}
	}

	private record Saved(bool IsSquare, List<DrawItem> Items, int Background = 0);

	/// <summary>The item under a point on the drawing: the one on top where they overlap.</summary>
	public DrawItem? ItemAt(PointF point) => Items.LastOrDefault(i => i.Contains(point));

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		canvas.SaveState();
		var scale = dirtyRect.Width / Width;
		canvas.Scale(scale, scale);
		canvas.FillColor = BackgroundColors[Math.Clamp(Background, 0, BackgroundColors.Length - 1)];
		canvas.FillRectangle(0, 0, Width, Height);
		foreach (var item in Items)
			item.Draw(canvas, item == Selected);
		canvas.RestoreState();
	}
}

/// <summary>What the image editor gives back: the picture as a PNG, and the drawing it was made from.</summary>
public record EditedImage(byte[] Image, string Drawing);
