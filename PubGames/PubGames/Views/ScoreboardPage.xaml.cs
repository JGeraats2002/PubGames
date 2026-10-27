using PubGames.Models;
using Microsoft.Maui.Controls.Shapes;
using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// The live scoreboard. The player cards are built here rather than in XAML
/// because their number and size depend on how many players there are: every
/// player must fit on the screen at once, so cards shrink as players are
/// added, and above MaxPerColumn they split into two columns.
/// </summary>
public partial class ScoreboardPage : ContentPage
{
	private const int MaxPerColumn = 8;

	/// <summary>The page's padding; at the bottom the keyboard's height is added while it's open.</summary>
	private const double RootPadding = 12;

	/// <summary>Manual plus / minus: each player's amount box, read when their + or − is tapped.</summary>
	private readonly Dictionary<ParticipantRow, Entry> _amountBoxes = new();

	private readonly ScoreboardViewModel _vm;
	private readonly GameNight _gameNight;

	/// <summary>False until the first load; after that, coming back to the page reloads the players.</summary>
	private bool _loaded;

	public ScoreboardPage(ScoreboardViewModel vm, GameNight gameNight)
	{
		InitializeComponent();
		// The page ends above the keyboard, so Previous / Next stay right on top of it.
		KeyboardWatcher.Track(this, covered => Root.Padding = new Thickness(RootPadding, RootPadding, RootPadding, RootPadding + covered));
		BindingContext = _vm = vm;
		InfoButton.AttachWithRules(this, () => Help.Scoreboard(_vm.Game?.ScoringType ?? ScoringType.PlusMinus, _vm.Settings), () => _vm.Game is { } game ? Help.GameRules(game) : null);
		_gameNight = gameNight;
		_vm.RowsReloaded += (_, _) =>
		{
			_loaded = true;
			_typingRow = null;
			KeyboardBar.IsVisible = false;
			Footer.IsVisible = true;
			ScoreMessage.IsVisible = _vm.Rules.ManualAmounts is not null;
			TurnDoneButton.IsVisible = _vm.FixedTurnOrder && _vm.Rules.ManualAmounts is null;
			ScoreMessage.Text = string.Empty;
			_builtForCount = -1; // other players may sit in the same number of seats
			UpdateFooter();
			BuildBoard();
		};
		// Rebuild when the screen size is known or changes (e.g. rotating the phone).
		SizeChanged += (_, _) => BuildBoard();
		_vm.SubgameChanged += (_, _) => UpdateFooter();
		_vm.SubgameEnded += async (_, _) => await OnSubgameEndedAsync();
		Header.SizeChanged += (_, _) => BuildBoard();
		Footer.SizeChanged += (_, _) => BuildBoard();
	}

	/// <summary>
	/// Next subgame and Finish game share the bottom row: when only one of
	/// them shows, it takes the full width. Once finished, Stats becomes the
	/// way to the final result.
	/// </summary>
	private void UpdateFooter()
	{
		var both = _vm.CanGoNext && _vm.CanFinish;
		Grid.SetColumn(FinishButton, both ? 1 : 0);
		Grid.SetColumnSpan(NextButton, both ? 1 : 2);
		Grid.SetColumnSpan(FinishButton, both ? 1 : 2);

		StatsButton.Text = _vm.IsFinished ? "Final result" : "Stats";
		Grid.SetColumn(StatsButton, _vm.IsFinished ? 0 : 1);
		Grid.SetColumnSpan(StatsButton, _vm.IsFinished ? 2 : 1);
	}

	/// <summary>
	/// The height the cards may use: the visible page minus padding, the
	/// header, the buttons and the spacing between them. Computed
	/// from the page itself rather than the board, so the cards can never grow
	/// past the bottom of the screen.
	/// </summary>
	private double AvailableBoardHeight()
	{
		if (Height <= 0) return 0;

		// Never taller than the window minus the title bar and tab bar, even if the page reports more.
		var visible = Window is { Height: > 0 } window ? Math.Min(Height, window.Height - 160) : Height;
		var root = Root;
		var header = Header.Height > 0 ? Header.Height : 56;
		var footer = Footer.Height > 0 ? Footer.Height : 104;
		return visible - RootPadding * 2 - header - footer - root.RowSpacing * 2;
	}

	private double _builtForHeight;
	private double _builtForWidth;
	private int _builtForCount = -1;

	/// <summary>Each player's card, to scroll it into view above the keyboard.</summary>
	private readonly Dictionary<ParticipantRow, View> _cards = new();

	/// <summary>The player whose amount box has the keyboard; null when nobody's typing.</summary>
	private ParticipantRow? _typingRow;

	/// <summary>An amount box has the keyboard (Previous / Next show instead of the buttons).</summary>
	private bool IsTyping => _typingRow is not null || _amountBoxes.Values.Any(b => b.IsFocused);

	/// <summary>
	/// Back from "Change players" or a subgame's result: show the new line-up,
	/// subgame and scores. Back from "Play again": switch to the new game.
	/// </summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_gameNight.NextSessionId is { } next)
		{
			_gameNight.NextSessionId = null;
			await _vm.LoadAsync(next);
		}
		else if (_loaded && _vm.Session is { } session)
			await _vm.LoadAsync(session.Id);
	}

	private async void OnFewerSubgamesClicked(object? sender, EventArgs e) => await _vm.ChangeSubgameCountAsync(-1);

	private async void OnMoreSubgamesClicked(object? sender, EventArgs e) => await _vm.ChangeSubgameCountAsync(+1);

	/// <summary>Note this subgame's result; the scores then start over for the next one.</summary>
	private async void OnNextClicked(object? sender, EventArgs e)
	{
		if (_vm.Session is null) return;
		await Shell.Current.GoToAsync(nameof(RoundResultPage), new Dictionary<string, object>
		{
			["sessionId"] = _vm.Session.Id,
			["finish"] = false
		});
	}

	/// <summary>Fixed input order with + and − by a step: the player whose turn it is is done.</summary>
	private async void OnTurnDoneClicked(object? sender, EventArgs e) => await _vm.EndTurnAsync();

	/// <summary>
	/// The end score ended the subgame by itself: note its result. On the last
	/// subgame that finishes the game; when the players didn't fix a number of
	/// subgames, they choose.
	/// </summary>
	private async Task OnSubgameEndedAsync()
	{
		if (_vm.Session is null) return;
		foreach (var box in _amountBoxes.Values)
			box.Unfocus();

		bool finish;
		if (_vm.CanGoNext && _vm.CanFinish)
		{
			const string next = "Next subgame";
			const string end = "Finish game";
			var choice = await DisplayActionSheetAsync($"{_vm.EndMessage.Replace("🏁 ", string.Empty)}\nPlay another subgame?", "Not yet", null, next, end);
			if (choice is not (next or end)) return;
			finish = choice == end;
		}
		else
			finish = !_vm.CanGoNext;

		await Shell.Current.GoToAsync(nameof(RoundResultPage), new Dictionary<string, object>
		{
			["sessionId"] = _vm.Session.Id,
			["finish"] = finish
		});
	}

	private async void OnFinishClicked(object? sender, EventArgs e)
	{
		if (_vm.Session is null) return;
		var confirmed = await DisplayAlertAsync("Finish the game?",
			"This will end the whole game. You'll note the result of this last subgame and then see who won" +
			(_vm.Settings.ResultMode == Models.ResultMode.Loser ? " and who lost." : "."),
			"Finish game", "Keep playing");
		if (!confirmed) return;

		await Shell.Current.GoToAsync(nameof(RoundResultPage), new Dictionary<string, object>
		{
			["sessionId"] = _vm.Session.Id,
			["finish"] = true
		});
	}

	/// <summary>Totals so far; after finishing, the final result.</summary>
	private async void OnStatsClicked(object? sender, EventArgs e)
	{
		if (_vm.Session is null) return;
		await Shell.Current.GoToAsync(nameof(StandingsPage), new Dictionary<string, object>
		{
			["sessionId"] = _vm.Session.Id,
			["final"] = _vm.IsFinished
		});
	}

	private async void OnChangePlayersClicked(object? sender, EventArgs e)
	{
		if (_vm.Session is null) return;
		await Shell.Current.GoToAsync(nameof(SessionPlayersPage), new Dictionary<string, object> { ["sessionId"] = _vm.Session.Id });
	}

	private void BuildBoard()
	{
		var rows = _vm.Rows;
		var available = AvailableBoardHeight();
		if (available <= 0) return;
		// Typing (the keyboard is up): keep the cards, or the box being typed in would disappear.
		if (IsTyping) return;

		var width = BoardWidth();
		// Size events fire often; only rebuild when something that matters changed.
		if (Math.Abs(available - _builtForHeight) < 1 && Math.Abs(width - _builtForWidth) < 1 && rows.Count == _builtForCount && Board.Children.Count > 0)
			return;
		_builtForHeight = available;
		_builtForWidth = width;
		_builtForCount = rows.Count;

		Board.Children.Clear();
		_amountBoxes.Clear();
		_cards.Clear();
		Board.RowDefinitions.Clear();
		Board.ColumnDefinitions.Clear();
		Board.HeightRequest = available;

		if (rows.Count == 0)
		{
			Board.Children.Add(new Label
			{
				Text = "Nobody is playing.",
				TextColor = Color("TextSecondary"),
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center
			});
			return;
		}

		var columns = rows.Count > MaxPerColumn ? 2 : 1;
		var rowsPerColumn = (int)Math.Ceiling(rows.Count / (double)columns);

		// Every card gets an exact height, so together they fill the screen and never more.
		// With few players they'd be huge, so cap them at a comfortable size.
		var cardHeight = Math.Min(140, (available - Board.RowSpacing * (rowsPerColumn - 1)) / rowsPerColumn);
		var cardWidth = (width - Board.ColumnSpacing * (columns - 1)) / columns;
		for (var c = 0; c < columns; c++)
			Board.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		for (var r = 0; r < rowsPerColumn; r++)
			Board.RowDefinitions.Add(new RowDefinition(new GridLength(cardHeight)));

		for (var i = 0; i < rows.Count; i++)
		{
			var card = BuildCard(rows[i], new CardSize(cardWidth, cardHeight, Compact: columns > 1));
			Grid.SetColumn(card, i / rowsPerColumn);
			Grid.SetRow(card, i % rowsPerColumn);
			Board.Children.Add(card);
		}
	}

	/// <summary>The board's width: the page minus its padding.</summary>
	private double BoardWidth()
	{
		var root = (Grid)Content;
		return Math.Max(0, Width - root.Padding.HorizontalThickness);
	}

	/// <summary>A card's outer size; Compact with two columns of cards.</summary>
	private readonly record struct CardSize(double Width, double Height, bool Compact)
	{
		public double PaddingX => Compact ? 8 : 12;
		public const double PaddingY = 4;

		/// <summary>The room inside the border and padding.</summary>
		public double InnerHeight => Height - PaddingY * 2 - 2;
		public double InnerWidth => Width - PaddingX * 2 - 2;

		public double Spacing => Compact ? 4 : 8;
	}

	/// <summary>
	/// The biggest font size at which a score fits in `width`: digits are
	/// about 0.6 of the font size wide in bold.
	/// </summary>
	private double ScoreFontFor(double width, double height, double max) =>
		Math.Clamp(Math.Min(Math.Min(height * 0.8, max), width / (_vm.Rules.ScoreLength * 0.62)), 12, max);

	private View BuildCard(ParticipantRow row, CardSize size)
	{
		var rules = _vm.Rules;
		var content = rules.ManualAmounts is not null ? ManualCardContent(row, size) : StepCardContent(row, size);

		var card = new Border
		{
			Content = content,
			Padding = new Thickness(size.PaddingX, CardSize.PaddingY),
			StrokeThickness = 1,
			Stroke = Color("Border"),
			StrokeShape = new RoundRectangle { CornerRadius = 10 },
			BindingContext = row
		};
		_cards[row] = card;
		// Fixed input order: the player whose turn it is gets a thick dark outline (after the leader's, so it wins).
		card.Triggers.Add(When<Border>(nameof(ParticipantRow.IsTurn),
			(Border.StrokeProperty, Color("PrimaryDark")),
			(Border.StrokeThicknessProperty, 4.0)));
		// The leader's card is outlined, so everyone sees who's winning.
		card.Triggers.Add(When<Border>(nameof(ParticipantRow.IsLeader),
			(Border.StrokeProperty, Color("Accent")),
			(Border.StrokeThicknessProperty, 3.0)));
		// Warning: a thick DASHED orange border on a light yellow card.
		card.Triggers.Add(When<Border>(nameof(ParticipantRow.IsWarning),
			(Border.StrokeProperty, Color("ScoreWarning")),
			(Border.StrokeThicknessProperty, 4.0),
			(Border.StrokeDashArrayProperty, new DoubleCollection { 3, 1.5 }),
			(BackgroundColorProperty, Color("ScoreWarningTint"))));
		// Max or min reached: the whole card turns solid dark blue - a big change in lightness,
		// clearly different from the warning even without telling orange and blue apart.
		card.Triggers.Add(When<Border>(nameof(ParticipantRow.IsAtLimit),
			(Border.StrokeProperty, Color("ScoreLimit")),
			(Border.StrokeThicknessProperty, 4.0),
			(BackgroundColorProperty, Color("ScoreLimit"))));
		return card;
	}

	private Label SeatLabel(double fontSize)
	{
		var seat = new Label { FontSize = fontSize, TextColor = Color("TextSecondary"), VerticalOptions = LayoutOptions.Center };
		seat.SetBinding(Label.TextProperty, static (ParticipantRow r) => r.Seat);
		seat.Triggers.Add(When<Label>(nameof(ParticipantRow.IsAtLimit), (Label.TextColorProperty, Colors.White)));
		// Whose turn it is: an arrow instead of the seat number.
		seat.Triggers.Add(When<Label>(nameof(ParticipantRow.IsTurn), (Label.TextProperty, "▶"), (Label.FontAttributesProperty, FontAttributes.Bold)));
		return seat;
	}

	private static Label NameLabel(ParticipantRow row, double fontSize)
	{
		var name = new Label
		{
			Text = row.Player.Name,
			FontSize = fontSize,
			FontAttributes = FontAttributes.Bold,
			LineBreakMode = LineBreakMode.TailTruncation,
			MaxLines = 1,
			VerticalOptions = LayoutOptions.Center
		};
		name.Triggers.Add(When<Label>(nameof(ParticipantRow.IsAtLimit), (Label.TextColorProperty, Colors.White)));
		return name;
	}

	private Label ScoreLabel(ParticipantRow row, double fontSize)
	{
		var score = new Label
		{
			FontSize = fontSize,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			HorizontalTextAlignment = TextAlignment.End,
			LineBreakMode = LineBreakMode.NoWrap,
			MaxLines = 1
		};
		score.SetBinding(Label.TextProperty, static (ParticipantRow r) => r.Score);
		score.Triggers.Add(When<Label>(nameof(ParticipantRow.IsAtLimit), (Label.TextColorProperty, Colors.White)));
		if (_vm.Rules.AllowsTypedAmount)
		{
			var tap = new TapGestureRecognizer();
			tap.Tapped += async (_, _) => await TypeAmountAsync(row);
			score.GestureRecognizers.Add(tap);
		}
		return score;
	}

	/// <summary>
	/// The badge while a warning or limit applies: "⚠ WARNING", "▲ MAX REACHED",
	/// or on a small card "▲ MAX". The words and symbols carry the meaning, so
	/// it doesn't depend on seeing colour. Never wraps, so nothing falls off.
	/// </summary>
	private Border StatusBadge(double fontSize, bool shortText)
	{
		var text = new Label
		{
			FontSize = fontSize,
			FontAttributes = FontAttributes.Bold,
			TextColor = Colors.Black,
			LineBreakMode = LineBreakMode.NoWrap,
			MaxLines = 1,
			VerticalTextAlignment = TextAlignment.Center
		};
		if (shortText)
			text.SetBinding(Label.TextProperty, static (ParticipantRow r) => r.ShortStatus);
		else
			text.SetBinding(Label.TextProperty, static (ParticipantRow r) => r.Status);
		text.Triggers.Add(When<Label>(nameof(ParticipantRow.IsAtLimit), (Label.TextColorProperty, Color("ScoreLimit"))));
		var badge = new Border
		{
			Content = text,
			Padding = new Thickness(6, 1),
			StrokeThickness = 0,
			BackgroundColor = Color("ScoreWarning"),
			StrokeShape = new RoundRectangle { CornerRadius = 6 },
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.Center
		};
		badge.SetBinding(IsVisibleProperty, static (ParticipantRow r) => r.HasStatus);
		badge.Triggers.Add(When<Border>(nameof(ParticipantRow.IsAtLimit), (BackgroundColorProperty, Colors.White)));
		return badge;
	}

	/// <summary>
	/// The name with its badge: under the name when the line is high enough
	/// for both, otherwise next to it in short form, so neither is cut off.
	/// </summary>
	private View NameAndStatus(ParticipantRow row, double lineHeight, double nameSize)
	{
		var badgeSize = Math.Clamp(lineHeight * 0.26, 10, 13);
		var stacked = lineHeight >= nameSize * 1.35 + badgeSize * 1.5 + 4;
		var name = NameLabel(row, nameSize);
		if (stacked)
			return new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { name, StatusBadge(badgeSize, shortText: false) } };

		var line = new Grid
		{
			ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) },
			ColumnSpacing = 4,
			VerticalOptions = LayoutOptions.Center
		};
		line.Add(name, 0);
		line.Add(StatusBadge(badgeSize, shortText: true), 1);
		return line;
	}

	/// <summary>Plus / minus (and the older types): seat, name, score, − and + on one line.</summary>
	private View StepCardContent(ParticipantRow row, CardSize size)
	{
		var rules = _vm.Rules;
		var inner = size.InnerHeight;
		var buttonSize = Math.Min(Math.Clamp(inner * 0.62, 30, 52), size.InnerWidth * 0.16);
		var nameSize = Math.Clamp(inner * 0.22, 12, 22);
		var buttons = rules.MinusStep is null ? 1 : 2;
		// The score gets at most a third of the card, and never the room the seat, the name and the buttons need.
		var scoreWidth = Math.Clamp(size.InnerWidth - buttons * (buttonSize + size.Spacing) - 24 - 80, 40, size.InnerWidth * 0.34);
		var scoreSize = ScoreFontFor(scoreWidth, inner, 48);

		var grid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Auto)
			},
			ColumnSpacing = size.Spacing
		};
		grid.Add(SeatLabel(Math.Clamp(inner * 0.16, 10, 14)), 0);
		grid.Add(NameAndStatus(row, inner, nameSize), 1);
		grid.Add(ScoreLabel(row, scoreSize), 2);
		if (rules.MinusStep is { } minus)
			grid.Add(StepButton(rules.MinusLabel, minus, row, buttonSize, filled: false), 3);
		grid.Add(StepButton(rules.PlusLabel, rules.PlusStep, row, buttonSize, filled: true), 4);
		return grid;
	}

	/// <summary>
	/// Manual plus / minus has an amount box and two buttons, which would leave
	/// hardly any room for the name on one line. So when the card is high
	/// enough: seat, name (with its badge) and score on the top line, the
	/// amount box with − and + on the bottom line. Each line gets exactly half
	/// the card, and every size is worked out from that, so nothing overlaps.
	/// With many players (low cards): one line, with a smaller box and buttons.
	/// </summary>
	private View ManualCardContent(ParticipantRow row, CardSize size)
	{
		var rules = _vm.Rules;
		const double twoLinesFrom = 84;
		var seat = SeatLabel(Math.Clamp(size.InnerHeight * 0.16, 10, 14));

		if (size.Height >= twoLinesFrom)
		{
			var lineHeight = (size.InnerHeight - size.Spacing / 2) / 2;
			var nameSize = Math.Clamp(lineHeight * 0.48, 12, 22);
			var scoreSize = ScoreFontFor(size.InnerWidth * 0.34, lineHeight, 36);
			var buttonSize = Math.Clamp(lineHeight - 2, 28, 48);

			var top = new Grid
			{
				ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
				ColumnSpacing = size.Spacing
			};
			top.Add(seat, 0);
			top.Add(NameAndStatus(row, lineHeight, nameSize), 1);
			top.Add(ScoreLabel(row, scoreSize), 2);

			var box = AmountBox(row, Math.Clamp(lineHeight * 0.42, 13, 18));
			box.HeightRequest = Math.Min(lineHeight, 44);
			var bottom = new Grid
			{
				ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) },
				ColumnSpacing = size.Spacing
			};
			bottom.Add(box, 0);
			bottom.Add(StepButton(rules.MinusLabel, -1, row, buttonSize, filled: false), 1);
			bottom.Add(StepButton(rules.PlusLabel, 1, row, buttonSize, filled: true), 2);

			var lines = new Grid
			{
				RowDefinitions = { new(GridLength.Star), new(GridLength.Star) },
				RowSpacing = size.Spacing / 2
			};
			lines.Add(top, 0, 0);
			lines.Add(bottom, 0, 1);
			return lines;
		}

		// Low cards: one line, kept small so the name keeps most of the width.
		var inner = size.InnerHeight;
		var small = Math.Clamp(inner - 2, 26, 36);
		var line = new Grid
		{
			ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) },
			ColumnSpacing = 4
		};
		var smallBox = AmountBox(row, Math.Clamp(inner * 0.35, 12, 14));
		smallBox.WidthRequest = 56;
		line.Add(seat, 0);
		line.Add(NameAndStatus(row, inner, Math.Clamp(inner * 0.3, 11, 16)), 1);
		line.Add(ScoreLabel(row, ScoreFontFor(size.InnerWidth * 0.2, inner, 24)), 2);
		line.Add(smallBox, 3);
		line.Add(StepButton(rules.MinusLabel, -1, row, small, filled: false), 4);
		line.Add(StepButton(rules.PlusLabel, 1, row, small, filled: true), 5);
		return line;
	}

	/// <summary>Applies the setters while the row's flag (a bool property on ParticipantRow) is true.</summary>
	private static DataTrigger When<T>(string flag, params (BindableProperty Property, object Value)[] setters) where T : BindableObject
	{
		var trigger = new DataTrigger(typeof(T)) { Binding = new Binding(flag), Value = true };
		foreach (var (property, value) in setters)
			trigger.Setters.Add(new Setter { Property = property, Value = value });
		return trigger;
	}

	private Button StepButton(string text, int delta, ParticipantRow row, double size, bool filled)
	{
		var button = new Button
		{
			Text = text,
			WidthRequest = size,
			HeightRequest = size,
			CornerRadius = (int)(size / 2),
			Padding = 0,
			FontSize = Math.Clamp(size * 0.35, 12, 18),
			VerticalOptions = LayoutOptions.Center
		};
		if (!filled)
		{
			button.BackgroundColor = Colors.Transparent;
			button.TextColor = Color("Primary");
			button.BorderColor = Color("Border");
			button.BorderWidth = 1;
			// Stays readable on the dark card of a player at a limit.
			button.Triggers.Add(When<Button>(nameof(ParticipantRow.IsAtLimit), (BackgroundColorProperty, Colors.White)));
		}
		else
			button.Triggers.Add(When<Button>(nameof(ParticipantRow.IsAtLimit),
				(Button.BorderColorProperty, Colors.White), (Button.BorderWidthProperty, 2.0)));
		button.SetBinding(IsEnabledProperty, static (ParticipantRow r) => r.CanEnter);
		button.Clicked += async (_, _) =>
		{
			if (_vm.Rules.ManualAmounts is { } manual)
			{
				// With a fixed input order the typed amount is the player's whole turn.
				if (await ApplyTypedAmountAsync(row, manual, subtract: delta < 0) && _vm.FixedTurnOrder)
					await _vm.EndTurnAsync();
			}
			else
				await _vm.AdjustScoreAsync(row, delta);
		};
		return button;
	}

	/// <summary>The box a player's amount is typed in (Manual plus / minus).</summary>
	private Entry AmountBox(ParticipantRow row, double fontSize)
	{
		var box = new Entry
		{
			Placeholder = "0",
			Keyboard = Keyboard.Telephone, // has a minus sign, for negative amounts
			FontSize = fontSize,
			HorizontalTextAlignment = TextAlignment.End,
			VerticalOptions = LayoutOptions.Center,
			MaxLength = 7,
			// The keyboard's action key goes to the next player; on the last one it closes the keyboard.
			ReturnType = row == _vm.Rows.LastOrDefault() ? ReturnType.Done : ReturnType.Next
		};
		// Stays readable on the dark card of a player at a limit.
		box.Triggers.Add(When<Entry>(nameof(ParticipantRow.IsAtLimit), (BackgroundColorProperty, Colors.White)));
		box.SetBinding(IsEnabledProperty, static (ParticipantRow r) => r.CanEnter);
		box.Focused += (_, _) => OnAmountBoxFocused(row);
		box.Unfocused += (_, _) => OnAmountBoxUnfocused();
		box.Completed += async (_, _) => await MoveToBoxAsync(row, +1, apply: true);
		_amountBoxes[row] = box;
		return box;
	}

	private readonly ViewModels.MessageTimer _scoreMessageTimer = new();

	/// <summary>Why an amount wasn't accepted; it clears itself after a few seconds.</summary>
	private void ShowScoreMessage(string text)
	{
		ScoreMessage.Text = text;
		_scoreMessageTimer.Shown(text, () => ScoreMessage.Text ?? string.Empty, v => ScoreMessage.Text = v);
	}

	/// <summary>A player's amount box has the keyboard: show Previous / Next above it and keep the card in sight.</summary>
	private async void OnAmountBoxFocused(ParticipantRow row)
	{
		_typingRow = row;
		KeyboardBarLabel.Text = row.Player.Name;
		// With a fixed input order the earlier players already had their turn.
		PreviousBoxButton.IsEnabled = !_vm.FixedTurnOrder && _vm.Rows.IndexOf(row) > 0;
		NextBoxButton.Text = row == _vm.Rows.LastOrDefault() ? "Done ✓" : "Next ▶";
		Footer.IsVisible = false;
		KeyboardBar.IsVisible = true;

		// Wait for the keyboard to push the page up, then scroll the card into view.
		await Task.Delay(250);
		if (_typingRow == row && _amountBoxes.TryGetValue(row, out var box) && box.Parent is not null)
			await BoardScroll.ScrollToAsync(_cards.GetValueOrDefault(row) ?? (Element)box, ScrollToPosition.MakeVisible, true);
	}

	/// <summary>
	/// The keyboard went away (or focus moves to another box): put the buttons
	/// back once no box has focus any more.
	/// </summary>
	private void OnAmountBoxUnfocused()
	{
		Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), () =>
		{
			if (_amountBoxes.Values.Any(b => b.IsFocused)) return;
			_typingRow = null;
			KeyboardBar.IsVisible = false;
			Footer.IsVisible = true;
			_ = BoardScroll.ScrollToAsync(0, 0, false);
		});
	}

	/// <summary>
	/// Previous (-1) or Next (+1) above the keyboard, or the keyboard's own
	/// action key. Going forward first adds the typed amount (a minus sign
	/// subtracts it); if it isn't allowed, the box stays so it can be fixed.
	/// Past the last player the keyboard closes.
	/// </summary>
	private async Task MoveToBoxAsync(ParticipantRow from, int direction, bool apply)
	{
		if (apply && _vm.Rules.ManualAmounts is { } manual && _amountBoxes.TryGetValue(from, out var box))
		{
			if (string.IsNullOrWhiteSpace(box.Text))
			{
				if (_vm.FixedTurnOrder)
				{
					ShowScoreMessage($"{from.Player.Name}: enter a score first - type 0 for no points.");
					return;
				}
			}
			else if (!await ApplyTypedAmountAsync(from, manual, subtract: false, keepFocus: true))
				return;
			if (_vm.FixedTurnOrder)
				await _vm.EndTurnAsync();
		}

		var index = _vm.Rows.IndexOf(from) + direction;
		if (index < 0) return;
		if (index >= _vm.Rows.Count)
		{
			foreach (var b in _amountBoxes.Values)
				b.Unfocus();
			return;
		}
		if (_amountBoxes.TryGetValue(_vm.Rows[index], out var next))
			next.Focus();
	}

	private async void OnPreviousBoxClicked(object? sender, EventArgs e)
	{
		if (_typingRow is { } row)
			await MoveToBoxAsync(row, -1, apply: false);
	}

	private async void OnNextBoxClicked(object? sender, EventArgs e)
	{
		if (_typingRow is { } row)
			await MoveToBoxAsync(row, +1, apply: true);
	}

	/// <summary>
	/// Manual plus / minus: adds or subtracts the amount typed behind the
	/// player's name, if the game's rules allow it. Otherwise the message line
	/// at the top says why and what can be entered; the amount stays so it can
	/// be corrected. True when the amount was added.
	/// </summary>
	private async Task<bool> ApplyTypedAmountAsync(ParticipantRow row, Models.ScoringSettings manual, bool subtract, bool keepFocus = false)
	{
		if (!_amountBoxes.TryGetValue(row, out var box)) return false;
		var text = box.Text?.Trim() ?? string.Empty;

		if (text.Length == 0)
		{
			ShowScoreMessage($"{row.Player.Name}: type an amount first, then tap + or −.");
			return false;
		}
		// Some keyboards type a real minus sign (−) instead of a hyphen.
		if (!int.TryParse(text.Replace('−', '-'), System.Globalization.NumberStyles.AllowLeadingSign,
			System.Globalization.CultureInfo.CurrentCulture, out var amount))
		{
			ShowScoreMessage($"{row.Player.Name}: \"{text}\" isn't a whole number.");
			return false;
		}
		if (!manual.AllowsAmount(amount) && !(amount == 0 && _vm.FixedTurnOrder))
		{
			ShowScoreMessage($"{row.Player.Name}: {amount} isn't allowed. {manual.AllowedAmountsHint()}");
			return false;
		}

		ScoreMessage.Text = string.Empty;
		box.Text = string.Empty;
		if (!keepFocus)
			box.Unfocus();
		await _vm.AdjustScoreAsync(row, subtract ? -amount : amount);
		return true;
	}

	/// <summary>Add any amount at once, e.g. 25 points; a minus sign subtracts.</summary>
	private async Task TypeAmountAsync(ParticipantRow row)
	{
		var text = await DisplayPromptAsync(row.Player.Name,
			$"How many {_vm.Rules.Unit} to add? Use a minus sign to subtract.",
			"Add", "Cancel", placeholder: "0", maxLength: 6, keyboard: Keyboard.Telephone);
		if (int.TryParse(text?.Trim().Replace('−', '-'), out var amount))
			await _vm.AdjustScoreAsync(row, amount);
	}

	private static Color Color(string key) =>
		Application.Current!.Resources.TryGetValue(key, out var value) && value is Color color ? color : Colors.Gray;
}
