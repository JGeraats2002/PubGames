using Microsoft.Maui.Controls.Shapes;
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

	private readonly ScoreboardViewModel _vm;

	/// <summary>False until the first load; after that, coming back to the page reloads the players.</summary>
	private bool _loaded;

	public ScoreboardPage(ScoreboardViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_vm.RowsReloaded += (_, _) =>
		{
			_loaded = true;
			_builtForCount = -1; // other players may sit in the same number of seats
			BuildBoard();
		};
		// Rebuild when the screen size is known or changes (e.g. rotating the phone).
		SizeChanged += (_, _) => BuildBoard();
		Explanation.SizeChanged += (_, _) => BuildBoard();
	}

	/// <summary>
	/// The height the cards may use: the visible page minus padding, the
	/// explanation line, the button and the spacing between them. Computed
	/// from the page itself rather than the board, so the cards can never grow
	/// past the bottom of the screen.
	/// </summary>
	private double AvailableBoardHeight()
	{
		if (Height <= 0) return 0;

		// Never taller than the window minus the title bar and tab bar, even if the page reports more.
		var visible = Window is { Height: > 0 } window ? Math.Min(Height, window.Height - 160) : Height;
		var root = (Grid)Content;
		var explanation = Explanation.Height > 0 ? Explanation.Height : 32;
		var button = ChangePlayersButton.Height > 0 ? ChangePlayersButton.Height : 48;
		return visible - root.Padding.VerticalThickness - explanation - button - root.RowSpacing * 2;
	}

	private double _builtForHeight;
	private int _builtForCount = -1;

	/// <summary>Back from "Change players": show the new line-up.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_loaded)
			await _vm.ReloadPlayersAsync();
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

		// Size events fire often; only rebuild when something that matters changed.
		if (Math.Abs(available - _builtForHeight) < 1 && rows.Count == _builtForCount && Board.Children.Count > 0)
			return;
		_builtForHeight = available;
		_builtForCount = rows.Count;

		Board.Children.Clear();
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
			LineBreakMode = LineBreakMode.TailTruncation,
			VerticalOptions = LayoutOptions.Center
		};

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
			ColumnSpacing = compact ? 4 : 8
		};
		grid.Add(seat, 0);
		grid.Add(name, 1);
		grid.Add(score, 2);
		if (rules.MinusStep is { } minus)
			grid.Add(StepButton(rules.MinusLabel, minus, row, buttonSize, filled: false), 3);
		grid.Add(StepButton(rules.PlusLabel, rules.PlusStep, row, buttonSize, filled: true), 4);

		var card = new Border
		{
			Content = grid,
			Padding = new Thickness(compact ? 8 : 12, 4),
			StrokeThickness = 1,
			Stroke = Color("Border"),
			StrokeShape = new RoundRectangle { CornerRadius = 10 },
			BindingContext = row
		};
		// The leader's card is outlined, so everyone sees who's winning.
		var leader = new DataTrigger(typeof(Border)) { Binding = new Binding(nameof(ParticipantRow.IsLeader)), Value = true };
		leader.Setters.Add(new Setter { Property = Border.StrokeProperty, Value = Color("Accent") });
		leader.Setters.Add(new Setter { Property = Border.StrokeThicknessProperty, Value = 3.0 });
		card.Triggers.Add(leader);
		return card;
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
		}
		button.Clicked += async (_, _) => await _vm.AdjustScoreAsync(row, delta);
		return button;
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
