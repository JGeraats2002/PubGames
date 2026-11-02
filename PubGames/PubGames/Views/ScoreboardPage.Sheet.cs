using System.ComponentModel;
using System.Globalization;
using PubGames.Models;
using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// The score sheet (Yatzy-style scoring): a table with the rows' names in the
/// first column and a column per player. Players type a score in a cell, tick
/// off a fixed score, or count a bonus with + and −; the totals add up by
/// themselves. Wider than the screen with many players: the table then
/// scrolls sideways too.
/// </summary>
public partial class ScoreboardPage
{
	/// <summary>The width of the first column, with the names of the rows.</summary>
	private const double SheetNameWidth = 128;

	/// <summary>The height of every line in the table: big enough to tap (48dp).</summary>
	private const double SheetLineHeight = 48;

	/// <summary>A bonus counted with + and −: the points above the buttons.</summary>
	private const double SheetBonusLineHeight = 72;

	/// <summary>Every cell a player can type in, so typing isn't interrupted by a rebuild.</summary>
	private readonly List<Entry> _sheetBoxes = new();

	/// <summary>What updates after a player's change: their totals, bonuses and ticked cells.</summary>
	private readonly List<(ParticipantRow Row, Action Refresh)> _sheetRefreshers = new();

	/// <summary>The players whose turn changes are followed, so their cells turn on and off.</summary>
	private readonly HashSet<ParticipantRow> _sheetFollowed = new();

	/// <summary>
	/// Typed scores that aren't allowed: they stay in their cell until fixed,
	/// also when the table is built again. By player and row id.
	/// </summary>
	private readonly Dictionary<(ParticipantRow Row, string CellId), (string Text, string Message)> _sheetDrafts = new();

	/// <summary>A cell of the score sheet has the keyboard.</summary>
	private bool IsTypingInSheet => _sheetBoxes.Any(b => b.IsFocused);

	/// <summary>Builds the whole table: a header with the players, then every list with its rows, bonuses and totals.</summary>
	private void BuildSheet(double width)
	{
		var sheet = _vm.Rules.Sheet ?? [];
		var rows = _vm.Rows;
		_sheetBoxes.Clear();
		_sheetRefreshers.Clear();
		foreach (var row in rows)
			if (_sheetFollowed.Add(row))
				row.PropertyChanged += OnSheetPlayerChanged;

		// Every player's column at least 96 wide (two buttons side by side); with few players they share the width.
		var playerWidth = Math.Max(96, Math.Min(120, (width - SheetNameWidth) / Math.Max(1, rows.Count)));
		Board.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(SheetNameWidth)));
		for (var column = 0; column < rows.Count; column++)
			Board.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(playerWidth)));
		Board.RowSpacing = 2;
		Board.ColumnSpacing = 2;

		var line = 0;
		void NewLine(double height = SheetLineHeight) => Board.RowDefinitions.Add(new RowDefinition(new GridLength(height)));

		// The players' names, in seat order; the player whose turn it is gets ▶.
		NewLine();
		for (var column = 0; column < rows.Count; column++)
			Board.Add(PlayerHeader(rows[column]), column + 1, line);
		line++;

		foreach (var section in sheet)
		{
			NewLine(36);
			var title = SheetText(section.Name, bold: true);
			title.BackgroundColor = Color("SurfaceMuted");
			title.Padding = new Thickness(8, 0);
			Board.Add(title, 0, line);
			Grid.SetColumnSpan(title, rows.Count + 1);
			line++;

			foreach (var sheetRow in section.Rows)
			{
				NewLine();
				Board.Add(SheetText(sheetRow.Title, bold: false), 0, line);
				for (var column = 0; column < rows.Count; column++)
					Board.Add(sheetRow.FixedScore is { } fixedScore
						? SheetTick(rows[column], sheetRow, fixedScore)
						: SheetBox(rows[column], sheetRow), column + 1, line);
				line++;
			}

			// An automatic bonus counts the rows above it: show what they add up to first.
			if (section.Bonuses.Any(b => b.Kind == ScoreSheetBonusKind.TotalAtLeast))
			{
				NewLine();
				Board.Add(SheetText(L.T("Subtotal"), bold: false, secondary: true), 0, line);
				for (var column = 0; column < rows.Count; column++)
					Board.Add(SheetSum(rows[column], cells => ScoreSheet.RowsTotal(section, cells), bold: false), column + 1, line);
				line++;
			}

			foreach (var bonus in section.Bonuses)
			{
				var perTime = bonus.Kind == ScoreSheetBonusKind.PerTime;
				NewLine(perTime ? SheetBonusLineHeight : SheetLineHeight);
				Board.Add(SheetText($"{bonus.Name} ({ScoreSheet.Describe(bonus)})", bold: false), 0, line);
				for (var column = 0; column < rows.Count; column++)
					Board.Add(perTime
						? SheetBonusCounter(rows[column], bonus, section)
						: SheetSum(rows[column], cells => ScoreSheet.BonusPoints(bonus, section, cells), bold: false), column + 1, line);
				line++;
			}

			// Only with more than one list: each list's own total.
			if (sheet.Count > 1)
			{
				NewLine();
				Board.Add(SheetText(L.F("Total {0}", section.Name), bold: true), 0, line);
				for (var column = 0; column < rows.Count; column++)
					Board.Add(SheetSum(rows[column], cells => ScoreSheet.SectionTotal(section, cells), bold: true), column + 1, line);
				line++;
			}
		}

		NewLine();
		var total = SheetText(L.T("Total"), bold: true);
		total.FontSize = 16;
		Board.Add(total, 0, line);
		for (var column = 0; column < rows.Count; column++)
		{
			var sum = SheetSum(rows[column], cells => _vm.Rules.StartScore + ScoreSheet.Total(sheet, cells), bold: true);
			sum.FontSize = 18;
			sum.BackgroundColor = Color("SurfaceMuted");
			// The best total right now stands out.
			sum.BindingContext = rows[column];
			sum.Triggers.Add(When<Label>(nameof(ParticipantRow.IsLeader),
				(Label.TextColorProperty, Color("OnPrimary")), (BackgroundColorProperty, Color("PrimaryDark"))));
			Board.Add(sum, column + 1, line);
		}

		Board.WidthRequest = SheetNameWidth + rows.Count * (playerWidth + Board.ColumnSpacing);
		ShowSheetDraftMessage();
	}

	/// <summary>A player's turn changed: their ticks and bonus buttons turn on or off.</summary>
	private void OnSheetPlayerChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ParticipantRow.CanEnter) && sender is ParticipantRow row)
			RefreshSheet(row);
	}

	/// <summary>A player's name at the top of their column; ▶ in front while it's their turn.</summary>
	private Label PlayerHeader(ParticipantRow row)
	{
		var name = SheetText(row.Player.Name, bold: true);
		name.HorizontalTextAlignment = TextAlignment.Center;
		name.BindingContext = row;
		name.Triggers.Add(When<Label>(nameof(ParticipantRow.IsTurn),
			(Label.TextProperty, $"▶ {row.Player.Name}"), (Label.TextColorProperty, Color("PrimaryDark"))));
		return name;
	}

	/// <summary>A text in the table: a row's name, a list's name or a total's.</summary>
	private static Label SheetText(string text, bool bold, bool secondary = false)
	{
		var label = new Label
		{
			Text = text,
			FontSize = 13,
			FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
			VerticalTextAlignment = TextAlignment.Center,
			LineBreakMode = LineBreakMode.WordWrap,
			MaxLines = 3
		};
		if (secondary)
			label.TextColor = Color("TextSecondary");
		return label;
	}

	/// <summary>A total or automatic bonus for one player; it follows every change they make.</summary>
	private Label SheetSum(ParticipantRow row, Func<IReadOnlyDictionary<string, int>, int> value, bool bold)
	{
		var label = new Label
		{
			FontSize = 15,
			FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalTextAlignment = TextAlignment.Center
		};
		void Refresh() => label.Text = value(row.Cells).ToString(CultureInfo.CurrentCulture);
		Refresh();
		_sheetRefreshers.Add((row, Refresh));
		return label;
	}

	/// <summary>
	/// A cell a player types a score in. When they're done (the keyboard's key,
	/// closing the keyboard, or tapping elsewhere) it's checked: allowed, it
	/// counts; not allowed, it stays in the cell in red and the message at the
	/// top says what can be filled in, until it's changed. Emptying a cell
	/// takes the score out again.
	/// </summary>
	private Entry SheetBox(ParticipantRow row, ScoreSheetRow sheetRow)
	{
		var key = (row, sheetRow.Id);
		string Saved() => row.Cells.TryGetValue(sheetRow.Id, out var value) ? value.ToString(CultureInfo.CurrentCulture) : string.Empty;
		var box = new Entry
		{
			Text = _sheetDrafts.TryGetValue(key, out var draft) ? draft.Text : Saved(),
			Keyboard = Keyboard.Telephone, // has a minus sign, for negative scores
			FontSize = 15,
			MaxLength = 6,
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalOptions = LayoutOptions.Fill,
			BindingContext = row,
			ReturnType = ReturnType.Done
		};
		box.SetBinding(IsEnabledProperty, static (ParticipantRow r) => r.CanEnter);
		void MarkWrong(bool wrong)
		{
			box.TextColor = wrong ? Color("Danger") : Color("TextPrimary");
			box.FontAttributes = wrong ? FontAttributes.Bold : FontAttributes.None;
		}
		MarkWrong(_sheetDrafts.ContainsKey(key));

		var busy = false;
		async Task CommitAsync()
		{
			var text = box.Text?.Trim().Replace('−', '-') ?? string.Empty;
			if (busy) return;
			if (text == Saved())
			{
				// Back to what was saved: nothing to fix any more.
				if (_sheetDrafts.Remove(key)) ShowSheetDraftMessage();
				MarkWrong(false);
				return;
			}
			busy = true;
			try
			{
				string? problem = null;
				int? value = null;
				if (text.Length > 0)
				{
					if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out var score))
						problem = L.F("{0}, {1}: \"{2}\" isn't a whole number.", row.Player.Name, sheetRow.Name, text);
					else if (!ScoreSheet.Allows(sheetRow, score))
						problem = L.F("{0}, {1}: {2} isn't allowed here. {3}", row.Player.Name, sheetRow.Name, score, ScoreSheet.Hint(sheetRow));
					else
						value = score;
				}

				if (problem is not null)
				{
					// Keep what was typed, so it can be corrected; it doesn't count until then.
					_sheetDrafts[key] = (box.Text ?? string.Empty, problem);
					MarkWrong(true);
					ShowSheetDraftMessage(problem);
					return;
				}

				_sheetDrafts.Remove(key);
				MarkWrong(false);
				await _vm.SetSheetCellAsync(row, sheetRow.Id, value);
				box.Text = Saved();
				ShowSheetDraftMessage();
				RefreshSheet(row);
			}
			finally
			{
				busy = false;
			}
		}

		box.Completed += (_, _) => box.Unfocus();
		box.Unfocused += async (_, _) =>
		{
			await CommitAsync();
			// Back to the normal layout once no cell has the keyboard any more.
			Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), () =>
			{
				if (!IsTypingInSheet) Footer.IsVisible = true;
			});
		};
		box.Focused += async (_, _) =>
		{
			Footer.IsVisible = false;
			// Wait for the keyboard to push the page up, then keep the cell in sight.
			await Task.Delay(250);
			if (box.IsFocused)
				await BoardScroll.ScrollToAsync(box, ScrollToPosition.MakeVisible, true);
		};
		_sheetBoxes.Add(box);
		return box;
	}

	/// <summary>
	/// The red line at the top: what's wrong with a typed score that doesn't
	/// count yet (`latest` first, otherwise any other), or nothing once all are fixed.
	/// </summary>
	private void ShowSheetDraftMessage(string? latest = null)
	{
		var message = latest ?? _sheetDrafts.Values.Select(d => d.Message).FirstOrDefault() ?? string.Empty;
		_scoreMessageTimer.ShowLasting(message, text => ScoreMessage.Text = text);
	}

	/// <summary>
	/// A row with a fixed score (like Full house, 25): empty, it has ✓ to tick
	/// off the score and, if the row allows it, ✗ for 0 when the throw failed.
	/// Ticked, it shows what counts; tapping it empties the cell again.
	/// </summary>
	private ContentView SheetTick(ParticipantRow row, ScoreSheetRow sheetRow, int fixedScore)
	{
		var cell = new ContentView();

		Button TickButton(string text, string description, bool filled, Func<Task> tapped)
		{
			var button = new Button
			{
				Text = text,
				Padding = 0,
				FontSize = 15,
				CornerRadius = 6,
				HeightRequest = SheetLineHeight,
				IsEnabled = row.CanEnter
			};
			SemanticProperties.SetDescription(button, description);
			if (!filled)
			{
				button.BackgroundColor = Colors.Transparent;
				button.TextColor = Color("Primary");
				button.BorderColor = Color("Border");
				button.BorderWidth = 1;
			}
			button.Clicked += async (_, _) =>
			{
				await tapped();
				RefreshSheet(row);
			};
			return button;
		}

		void Render()
		{
			var cells = row.Cells;
			if (cells.TryGetValue(sheetRow.Id, out var value))
			{
				var shown = value == 0 ? "✗ 0" : $"✓ {value.ToString(CultureInfo.CurrentCulture)}";
				var ticked = TickButton(shown, L.F("{0}: {1}. Tap to empty it.", sheetRow.Name, value), filled: value != 0,
					() => _vm.SetSheetCellAsync(row, sheetRow.Id, null));
				if (value == 0)
					ticked.TextColor = Color("TextSecondary");
				cell.Content = ticked;
				return;
			}

			var tick = TickButton("✓", L.F("Tick off {0}: {1}", sheetRow.Name, fixedScore), filled: false,
				() => _vm.SetSheetCellAsync(row, sheetRow.Id, fixedScore));
			if (!sheetRow.AllowsZero)
			{
				cell.Content = tick;
				return;
			}
			var cross = TickButton("✗", L.F("{0}: failed, 0", sheetRow.Name), filled: false,
				() => _vm.SetSheetCellAsync(row, sheetRow.Id, 0));
			cross.TextColor = Color("TextSecondary");
			var both = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 2 };
			both.Add(tick, 0);
			both.Add(cross, 1);
			cell.Content = both;
		}

		Render();
		_sheetRefreshers.Add((row, Render));
		return cell;
	}

	/// <summary>
	/// A bonus that's scored per time (like an extra Yatzy): the points it's
	/// worth now, with − and + for the number of times. + only works while the
	/// bonus can be scored: below its most times, and once the row it needs
	/// has a score. Doesn't end the player's turn: it comes with filling in a row.
	/// </summary>
	private View SheetBonusCounter(ParticipantRow row, ScoreSheetBonus bonus, ScoreSheetSection section)
	{
		var points = new Label
		{
			FontSize = 15,
			HorizontalTextAlignment = TextAlignment.Center,
			VerticalTextAlignment = TextAlignment.Center
		};

		Button CountButton(string text, string description, int change)
		{
			var button = new Button
			{
				Text = text,
				Padding = 0,
				FontSize = 18,
				CornerRadius = 6,
				HeightRequest = SheetLineHeight,
				BackgroundColor = Colors.Transparent,
				TextColor = Color("Primary"),
				BorderColor = Color("Border"),
				BorderWidth = 1
			};
			SemanticProperties.SetDescription(button, description);
			button.Clicked += async (_, _) =>
			{
				var times = row.Cells.GetValueOrDefault(bonus.Id) + change;
				if (times < 0 || change > 0 && !ScoreSheet.CanAddTime(bonus, row.Cells)) return;
				await _vm.SetSheetCellAsync(row, bonus.Id, times == 0 ? null : times, endsTurn: false);
				RefreshSheet(row);
			};
			return button;
		}

		var minus = CountButton("−", L.F("{0}: one time less", bonus.Name), -1);
		var plus = CountButton("+", L.F("{0}: one time more", bonus.Name), +1);

		void Refresh()
		{
			var cells = row.Cells;
			var times = cells.GetValueOrDefault(bonus.Id);
			var unlocked = ScoreSheet.IsUnlocked(bonus, cells);
			points.Text = unlocked || times > 0
				? ScoreSheet.BonusPoints(bonus, section, cells).ToString(CultureInfo.CurrentCulture)
				: "🔒";
			minus.IsEnabled = row.CanEnter && times > 0;
			plus.IsEnabled = row.CanEnter && ScoreSheet.CanAddTime(bonus, cells);
		}

		var buttons = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 2 };
		buttons.Add(minus, 0);
		buttons.Add(plus, 1);
		var counter = new Grid { RowDefinitions = [new(GridLength.Star), new(new GridLength(SheetLineHeight))], RowSpacing = 0 };
		counter.Add(points, 0, 0);
		counter.Add(buttons, 0, 1);

		Refresh();
		_sheetRefreshers.Add((row, Refresh));
		return counter;
	}

	/// <summary>Updates a player's totals, bonuses and ticked cells after a change.</summary>
	private void RefreshSheet(ParticipantRow row)
	{
		foreach (var refresher in _sheetRefreshers.Where(r => r.Row == row).ToList())
			refresher.Refresh();
	}
}
