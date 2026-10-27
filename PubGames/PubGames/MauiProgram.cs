using Microsoft.Extensions.Logging;
using PubGames.Services;
using PubGames.ViewModels;
using PubGames.Views;

namespace PubGames;

/// <summary>
/// Builds the app: registers every service, view model and page, so each page gets what it needs handed in
/// (dependency injection).
/// </summary>
public static class MauiProgram
{
	/// <summary>Builds the app with its services, view models and pages.</summary>
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
		builder.Services.AddSingleton<FirestoreClient>();
		builder.Services.AddSingleton<ITeamService, TeamService>();
		builder.Services.AddSingleton<ICategoryService, CategoryService>();
		builder.Services.AddSingleton<GameNight>();
		builder.Services.AddSingleton<SavedGames>();
		builder.Services.AddSingleton<IProfileService, ProfileService>();
		builder.Services.AddSingleton<IStatsService, StatsService>();
		builder.Services.AddSingleton<IMessageService, MessageService>();
		builder.Services.AddSingleton<IFeedbackService, FeedbackService>();
		builder.Services.AddSingleton<IPlayerLinkService, PlayerLinkService>();
		builder.Services.AddSingleton<FunctionsClient>();
		builder.Services.AddSingleton<IStoreService, StoreService>();
		// Opens the admin's mail app prefilled. For fully automatic emails, switch to FirestoreEmailSender (see its comments).
		builder.Services.AddSingleton<IEmailSender, ComposeEmailSender>();
		// Swap for a Firebase Storage implementation when moving to the Blaze plan; existing images keep working.
		builder.Services.AddSingleton<IImageStore, FirestoreImageStore>();

		// --- ViewModels (transient: fresh state each time you navigate to the page) ---
		builder.Services.AddTransient<PlayerEntryViewModel>();
		builder.Services.AddTransient<SavedGamesViewModel>();
		builder.Services.AddTransient<GameLibraryViewModel>();
		builder.Services.AddTransient<GameRulesViewModel>();
		builder.Services.AddTransient<ScoreboardViewModel>();
		builder.Services.AddTransient<SessionPlayersViewModel>();
		builder.Services.AddTransient<RoundResultViewModel>();
		builder.Services.AddTransient<StandingsViewModel>();
		builder.Services.AddTransient<MyStatsViewModel>();
		builder.Services.AddTransient<GameStatsViewModel>();
		builder.Services.AddTransient<AdminLibraryViewModel>();
		builder.Services.AddTransient<AdminCreateGameViewModel>();
		builder.Services.AddTransient<AdminTeamViewModel>();
		builder.Services.AddTransient<ReviewRequestsViewModel>();
		builder.Services.AddTransient<ReviewRequestViewModel>();
		builder.Services.AddTransient<CategoriesViewModel>();
		builder.Services.AddTransient<CategoryGamesViewModel>();
		builder.Services.AddTransient<InboxViewModel>();
		builder.Services.AddTransient<AccountViewModel>();
		builder.Services.AddTransient<FeedbackViewModel>();
		builder.Services.AddTransient<AdminFeedbackViewModel>();
		builder.Services.AddTransient<BlockedPlayersViewModel>();
		builder.Services.AddTransient<SubscriptionViewModel>();
		builder.Services.AddTransient<LoginViewModel>();

		// --- Pages ---
		builder.Services.AddTransient<PlayerEntryPage>();
		builder.Services.AddTransient<SavedGamesPage>();
		builder.Services.AddTransient<GameLibraryPage>();
		builder.Services.AddTransient<GameRulesPage>();
		builder.Services.AddTransient<ScoreboardPage>();
		builder.Services.AddTransient<SessionPlayersPage>();
		builder.Services.AddTransient<RoundResultPage>();
		builder.Services.AddTransient<StandingsPage>();
		builder.Services.AddTransient<MyStatsPage>();
		builder.Services.AddTransient<GameStatsPage>();
		builder.Services.AddTransient<AdminLibraryPage>();
		builder.Services.AddTransient<AdminCreateGamePage>();
		builder.Services.AddTransient<AdminTeamPage>();
		builder.Services.AddTransient<ReviewRequestsPage>();
		builder.Services.AddTransient<ReviewRequestPage>();
		builder.Services.AddTransient<CategoriesPage>();
		builder.Services.AddTransient<CategoryGamesPage>();
		builder.Services.AddTransient<InboxPage>();
		builder.Services.AddTransient<AccountPage>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<FeedbackPage>();
		builder.Services.AddTransient<AdminFeedbackPage>();
		builder.Services.AddTransient<BlockedPlayersPage>();
		builder.Services.AddTransient<SubscriptionPage>();
		builder.Services.AddTransient<LoginPage>();

		return builder.Build();
	}
}
