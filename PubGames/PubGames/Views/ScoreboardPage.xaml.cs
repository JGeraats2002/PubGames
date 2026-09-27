using PubGames.ViewModels;

namespace PubGames.Views;

public partial class ScoreboardPage : ContentPage
{
	private readonly ScoreboardViewModel _vm;

	public ScoreboardPage(ScoreboardViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	private async void OnIncrementClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: ParticipantRow row })
			await _vm.AdjustScoreAsync(row, 1);
	}

	private void OnRemoveClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: ParticipantRow row })
			_vm.RemovePlayerCommand.Execute(row);
	}

	private void OnAddPlayerClicked(object? sender, EventArgs e)
	{
		// TODO: show a small picker (saved players not already in this
		// session), then call _vm.AddPlayerCommand.Execute(chosenPlayer).
	}
}
