using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>The user's stats of one game, with every time they played it.</summary>
public partial class GameStatsPage : ContentPage
{
	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public GameStatsPage(GameStatsViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
		InfoButton.Attach(this, () => Help.GameStats);
	}
}
