using Microsoft.Maui.Controls.Shapes;

namespace PubGames.Views;

/// <summary>
/// A popup with a question and up to three buttons next to each other, for
/// when a normal alert's two buttons aren't enough (like "Keep editing /
/// Don't save / Save"). Shown over the page; use AskAsync.
/// </summary>
public class ChoicePopup : ContentPage
{
	private readonly TaskCompletionSource<string?> _choice = new();

	/// <summary>
	/// Builds the popup: the title, the message (if any) and the buttons - side by side up to three, under
	/// each other with more.
	/// </summary>
	private ChoicePopup(string title, string? message, string cancel, string[] buttons)
	{
		BackgroundColor = Color.FromRgba(0, 0, 0, 0.45);
		Shell.SetPresentationMode(this, PresentationMode.ModalNotAnimated);

		var row = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
		var all = buttons.Prepend(cancel).ToArray();
		// Up to three side by side; more under each other, the main choice on top and cancel at the bottom.
		var stacked = all.Length > 3;
		if (stacked)
			all = buttons.Reverse().Append(cancel).ToArray();
		for (var buttonIndex = 0; buttonIndex < all.Length; buttonIndex++)
		{
			if (stacked)
				row.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			else
				row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			var text = all[buttonIndex];
			var last = stacked ? buttonIndex == 0 : buttonIndex == all.Length - 1;
			var isCancelButton = stacked ? buttonIndex == all.Length - 1 : buttonIndex == 0;
			var button = new Button
			{
				Text = text, FontSize = 13, Padding = new Thickness(6, 0), HeightRequest = 48, LineBreakMode = LineBreakMode.WordWrap,
				// The main choice filled in; the others as outlines.
				BackgroundColor = last ? Resource("Primary") : Colors.Transparent,
				TextColor = last ? Colors.White : isCancelButton || stacked ? Resource("TextPrimary") : Resource("Danger"),
				BorderColor = last ? Resource("Primary") : Resource("Border"), BorderWidth = 1
			};
			button.Clicked += async (_, _) => await CloseAsync(isCancelButton ? null : text);
			if (stacked)
				row.Add(button, 0, buttonIndex);
			else
				row.Add(button, buttonIndex);
		}

		var content = new VerticalStackLayout { Spacing = 12 };
		content.Add(new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold });
		if (!string.IsNullOrEmpty(message))
			content.Add(new Label { Text = message, FontSize = 14 });
		content.Add(row);

		Content = new Border
		{
			Content = content,
			BackgroundColor = Resource("SurfaceLight"),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 14 },
			Padding = new Thickness(20),
			Margin = new Thickness(24),
			VerticalOptions = LayoutOptions.Center
		};
	}

	/// <summary>The button tapped, or null for the cancel button (also the phone's back button).</summary>
	public static async Task<string?> AskAsync(Page from, string title, string? message, string cancel, params string[] buttons)
	{
		var popup = new ChoicePopup(title, message, cancel, buttons);
		await from.Navigation.PushModalAsync(popup, false);
		return await popup._choice.Task;
	}

	/// <summary>Closes the popup and hands back the button that was tapped (null for cancel).</summary>
	private async Task CloseAsync(string? choice)
	{
		if (_choice.Task.IsCompleted) return;
		await Navigation.PopModalAsync(false);
		_choice.TrySetResult(choice);
	}

	/// <summary>The phone's back button cancels, like the cancel button.</summary>
	protected override bool OnBackButtonPressed()
	{
		_ = CloseAsync(null);
		return true;
	}

	/// <summary>A colour from the app's resources (App.xaml).</summary>
	private static Color Resource(string key) => (Color)Application.Current!.Resources[key];
}
