using Microsoft.Extensions.Logging;
using PubGames.Services;
using PubGames.ViewModels;
using PubGames.Views;

namespace PubGames;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();
		// TODO: drop real .ttf files into Resources/Fonts and re-add
		// .ConfigureFonts(...) here (see README) - using the system default
		// font for now so the project builds with zero extra setup.

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// --- Services (singletons: one shared local DB + one shared cloud client) ---
		builder.Services.AddSingleton<ILocalDatabaseService, LocalDatabaseService>();
		builder.Services.AddSingleton<ICloudSyncService, CloudSyncService>();
		builder.Services.AddSingleton<IPermissionService, PermissionService>();
		builder.Services.AddSingleton<IGoogleSignInProvider, GoogleSignInProvider>();
		builder.Services.AddSingleton<IAuthService, AuthService>();

		// --- ViewModels (transient: fresh state each time you navigate to the page) ---
		builder.Services.AddTransient<PlayerEntryViewModel>();
		builder.Services.AddTransient<GameLibraryViewModel>();
		builder.Services.AddTransient<ScoreboardViewModel>();
		builder.Services.AddTransient<HostCreateGameViewModel>();
		builder.Services.AddTransient<HostTeamViewModel>();
		builder.Services.AddTransient<AccountViewModel>();
		builder.Services.AddTransient<LoginViewModel>();

		// --- Pages ---
		builder.Services.AddTransient<PlayerEntryPage>();
		builder.Services.AddTransient<GameLibraryPage>();
		builder.Services.AddTransient<ScoreboardPage>();
		builder.Services.AddTransient<HostCreateGamePage>();
		builder.Services.AddTransient<HostTeamPage>();
		builder.Services.AddTransient<AccountPage>();
		builder.Services.AddTransient<LoginPage>();

		return builder.Build();
	}
}
