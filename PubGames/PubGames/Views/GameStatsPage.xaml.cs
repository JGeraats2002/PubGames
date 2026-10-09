using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class GameStatsPage : ContentPage
{
	public GameStatsPage(GameStatsViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
		InfoButton.Attach(this, () => Help.GameStats);
	}
}
