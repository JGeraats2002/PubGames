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

	/// <summary>Manual plus / minus: each player's amount box, read when their + or − is tapped.</summary>
	private readonly Dictionary<ParticipantRow, Entry> _amountBoxes = new();

	/// <summary>The amount boxes' placeholder: an amount the game's rules allow, or nothing without rules.</summary>
	private string _amountPlaceholder = string.Empty;

	private readonly ScoreboardViewModel _vm;
	private readonly GameNight _gameNight;

	/// <summary>False until the first load; after that, coming back to the page reloads the players.</summary>
	private bool _loaded;

	public ScoreboardPage(ScoreboardViewModel vm, GameNight gameNight)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.AttachWithRules(this, () => Help.Scoreboard(_vm.Game?.ScoringType ?? ScoringType.PlusMinus), () => _vm.Game is { } game ? Help.GameRules(game) : null);
		_gameNight = gameNight;
		_vm.RowsReloaded += (_, _) =>
		{
			_loaded = true;
			ScoreMessage.IsVisible = _vm.Rules.ManualAmounts is not null;
			_amountPlaceholder = _vm.Rules.ManualAmounts?.ExampleAmount()?.ToString() ?? string.Empty;
			ScoreMessage.Text = string.Empty;
			_builtForCount = -1; // other players may sit in the same number of seats
			UpdateFooter();
			BuildBoard();
		};
		// Rebuild when the screen size is known or changes (e.g. rotating the phone).
		SizeChanged += (_, _) => BuildBoard();
		_vm.SubgameChanged += (_, _) => UpdateFooter();
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
		var root = (Grid)Content;
		var header = Header.Height > 0 ? Header.Height : 56;
		var footer = Footer.Height > 0 ? Footer.Height : 104;
		return visible - root.Padding.VerticalThickness - header - footer - root.RowSpacing * 2;
	}

	private double _builtForHeight;
	private int _builtForCount = -1;

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
		if (_amountBoxes.Values.Any(b => b.IsFocused)) return;

		// Size events fire often; only rebuild when something that matters changed.
		if (Math.Abs(available - _builtForHeight) < 1 && rows.Count == _builtForCount && Board.Children.Count > 0)
			return;
		_builtForHeight = available;
		_builtForCount = rows.Count;

		Board.Children.Clear();
		_amountBoxes.Clear();
		Board.RowDefinitions.Clear();
		Board.ColumnDefinitions.Clear();
		Board.HeightRequest = available;

		if (rows.Count == 0)
		{
			Board.Children.Add(new Label
			{
				Text = "Nobody is playing. Tap \"Change players\" to add players.",
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
		for (var c = 0; c < columns; c++)
			Board.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		for (var r = 0; r < rowsPerColumn; r++)
			Board.RowDefinitions.Add(new RowDefinition(new GridLength(cardHeight)));

		// Text sizes follow the card height, so 3 players get big numbers and 12 still fit.
		var compact = columns > 1;

		for (var i = 0; i < rows.Count; i++)
		{
			var card = BuildCard(rows[i], cardHeight, compact);
			Grid.SetColumn(card, i / rowsPerColumn);
			Grid.SetRow(card, i % rowsPerColumn);
			Board.Children.Add(card);
		}
	}

	private View BuildCard(ParticipantRow row, double height, bool compact)
	{
		var scoreSize = Math.Clamp(height * 0.42, 16, 48);
		var nameSize = Math.Clamp(height * 0.2, 12, 22);
		var buttonSize = Math.Clamp(height * 0.55, 32, 52);
		var rules = _vm.Rules;

		var seat = new Label
		{
			FontSize = Math.Clamp(height * 0.16, 10, 14),
			TextColor = Color("TextSecondary"),
			VerticalOptions = LayoutOptions.Center
		};
		seat.SetBinding(Label.TextProperty, static (ParticipantRow r) => r.Seat);

		var name = new Label
		{
			Text = row.Player.Name,
			FontSize = nameSize,
			FontAttributes = FontAttributes.Bold,
			LineBreakMode = LineBreakMode.TailTruncation
		};

		// A badge under the name while a warning or limit applies ("⚠ WARNING", "▲ MAX REACHED").
		// The words and symbols carry the meaning, so it doesn't depend on seeing colour.
		var statusText = new Label
		{
			FontSize = Math.Clamp(height * 0.15, 11, 14),
			FontAttributes = FontAttributes.Bold,
			TextColor = Colors.Black
		};
		statusText.SetBinding(Label.TextProperty, static (ParticipantRow r) => r.Status);
		statusText.Triggers.Add(When<Label>(nameof(ParticipantRow.IsAtLimit), (Label.TextColorProperty, Color("ScoreLimit"))));
		var badge = new Border
		{
			Content = statusText,
			Padding = new Thickness(8, 1),
			StrokeThickness = 0,
			BackgroundColor = Color("ScoreWarning"),
			StrokeShape = new RoundRectangle { CornerRadius = 6 },
			HorizontalOptions = LayoutOptions.Start
		};
		badge.SetBinding(IsVisibleProperty, static (ParticipantRow r) => r.HasStatus);
		badge.Triggers.Add(When<Border>(nameof(ParticipantRow.IsAtLimit), (BackgroundColorProperty, Colors.White)));

		var nameAndStatus = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { name, badge } };

		var score = new Label
		{
			FontSize = scoreSize,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			HorizontalTextAlignment = TextAlignment.End,
			MinimumWidthRequest = 40
		};
		score.SetBinding(Label.TextProperty, static (ParticipantRow r) => r.Score);
		if (rules.AllowsTypedAmount)
		{
			var tap = new TapGestureRecognizer();
			tap.Tapped += async (_, _) => await TypeAmountAsync(row);
			score.GestureRecognizers.Add(tap);
		}

		// On the dark card of a player at a limit, all text turns white.
		foreach (var label in new[] { seat, name, score })
			label.Triggers.Add(When<Label>(nameof(ParticipantRow.IsAtLimit), (Label.TextColorProperty, Colors.White)));

		var spacing = compact ? 4 : 8;
		View content;
		if (rules.ManualAmounts is not null)
			content = ManualCardContent(row, rules, seat, nameAndStatus, score, height, compact, nameSize, spacing);
		else
		{
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
				ColumnSpacing = spacing
			};
			grid.Add(seat, 0);
			grid.Add(nameAndStatus, 1);
			grid.Add(score, 2);
			if (rules.MinusStep is { } minus)
				grid.Add(StepButton(rules.MinusLabel, minus, row, buttonSize, filled: false), 3);
			grid.Add(StepButton(rules.PlusLabel, rules.PlusStep, row, buttonSize, filled: true), 4);
			content = grid;
		}

		var card = new Border
		{
			Content = content,
			Padding = new Thickness(compact ? 8 : 12, 4),
			StrokeThickness = 1,
			Stroke = Color("Border"),
			StrokeShape = new RoundRectangle { CornerRadius = 10 },
			BindingContext = row
		};
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
		button.Clicked += async (_, _) =>
		{
			if (_vm.Rules.ManualAmounts is { } manual)
				await ApplyTypedAmountAsync(row, manual, subtract: delta < 0);
			else
				await _vm.AdjustScoreAsync(row, delta);
		};
		return button;
	}

	/// <summary>
	/// Manual plus / minus has an amount box and two buttons, which would leave
	/// hardly any room for the name on one line. So when the card is tall
	/// enough: seat, name and score on top, amount box with − and + below.
	/// With many players (low cards): one line, with a smaller box and buttons.
	/// </summary>
	private View ManualCardContent(ParticipantRow row, ScoringRules rules, Label seat, View nameAndStatus, Label score,
		double height, bool compact, double nameSize, double spacing)
	{
		const double twoLinesFrom = 84;
		if (height >= twoLinesFrom)
		{
			var lineHeight = (height - 8 - spacing / 2) / 2;
			var buttonSize = Math.Clamp(lineHeight - 2, 30, 48);
			score.FontSize = Math.Clamp(lineHeight * 0.75, 16, 36);

			var top = new Grid
			{
				ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
				ColumnSpacing = spacing
			};
			top.Add(seat, 0);
			top.Add(nameAndStatus, 1);
			top.Add(score, 2);

			var box = AmountBox(row, compact, Math.Min(nameSize, 18));
			box.WidthRequest = -1; // fills the line
			var bottom = new Grid
			{
				ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) },
				ColumnSpacing = spacing
			};
			bottom.Add(box, 0);
			bottom.Add(StepButton(rules.MinusLabel, -1, row, buttonSize, filled: false), 1);
			bottom.Add(StepButton(rules.PlusLabel, 1, row, buttonSize, filled: true), 2);

			return new VerticalStackLayout { Spacing = spacing / 2, VerticalOptions = LayoutOptions.Center, Children = { top, bottom } };
		}

		// Low cards: one line, kept small so the name keeps most of the width.
		var small = Math.Clamp(height - 10, 28, 36);
		var line = new Grid
		{
			ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) },
			ColumnSpacing = 4
		};
		var smallBox = AmountBox(row, compact: true, Math.Min(nameSize, 14));
		smallBox.WidthRequest = 48;
		line.Add(seat, 0);
		line.Add(nameAndStatus, 1);
		line.Add(score, 2);
		line.Add(smallBox, 3);
		line.Add(StepButton(rules.MinusLabel, -1, row, small, filled: false), 4);
		line.Add(StepButton(rules.PlusLabel, 1, row, small, filled: true), 5);
		return line;
	}

	/// <summary>The box a player's amount is typed in (Manual plus / minus).</summary>
	private Entry AmountBox(ParticipantRow row, bool compact, double fontSize)
	{
		var box = new Entry
		{
			Placeholder = _amountPlaceholder,
			Keyboard = Keyboard.Telephone, // has a minus sign, for negative amounts
			WidthRequest = compact ? 56 : 72,
			FontSize = fontSize,
			HorizontalTextAlignment = TextAlignment.End,
			VerticalOptions = LayoutOptions.Center,
			MaxLength = 7
		};
		// Stays readable on the dark card of a player at a limit.
		box.Triggers.Add(When<Entry>(nameof(ParticipantRow.IsAtLimit), (BackgroundColorProperty, Colors.White)));
		_amountBoxes[row] = box;
		return box;
	}

	/// <summary>
	/// Manual plus / minus: adds or subtracts the amount typed behind the
	/// player's name, if the game's rules allow it. Otherwise the message line
	/// at the top says why and what can be entered; the amount stays so it can
	/// be corrected.
	/// </summary>
	private async Task ApplyTypedAmountAsync(ParticipantRow row, Models.ScoringSettings manual, bool subtract)
	{
		if (!_amountBoxes.TryGetValue(row, out var box)) return;
		var text = box.Text?.Trim() ?? string.Empty;

		if (text.Length == 0)
		{
			ScoreMessage.Text = $"{row.Player.Name}: type an amount first, then tap + or −.";
			return;
		}
		// Some keyboards type a real minus sign (−) instead of a hyphen.
		if (!int.TryParse(text.Replace('−', '-'), System.Globalization.NumberStyles.AllowLeadingSign,
			System.Globalization.CultureInfo.CurrentCulture, out var amount))
		{
			ScoreMessage.Text = $"{row.Player.Name}: \"{text}\" isn't a whole number.";
			return;
		}
		if (!manual.AllowsAmount(amount))
		{
			ScoreMessage.Text = $"{row.Player.Name}: {amount} isn't allowed. {manual.AllowedAmountsHint()}";
			return;
		}

		ScoreMessage.Text = string.Empty;
		box.Text = string.Empty;
		box.Unfocus();
		await _vm.AdjustScoreAsync(row, subtract ? -amount : amount);
	}

	/// <summary>Add any amount at once, e.g. 25 points; a minus sign subtracts.</summary>
	private async Task TypeAmountAsync(ParticipantRow row)
	{
		var text = await DisplayPromptAsync(row.Player.Name,
			$"How many {_vm.Rules.Unit} to add? Use a minus sign to subtract.",
			"Add", "Cancel", placeholder: "e.g. 10 or -5", maxLength: 6, keyboard: Keyboard.Telephone);
		if (int.TryParse(text?.Trim().Replace('−', '-'), out var amount))
			await _vm.AdjustScoreAsync(row, amount);
	}

	private static Color Color(string key) =>
		Application.Current!.Resources.TryGetValue(key, out var value) && value is Color color ? color : Colors.Gray;
}
