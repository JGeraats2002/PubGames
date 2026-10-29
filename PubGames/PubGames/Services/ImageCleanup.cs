using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// Removes game pictures nothing shows any more (from the cloud and this phone), so they don't take up room:
/// after a game is saved, and after a review request is decided. A picture still used by any game or waiting
/// request - in its cover, its rules or any of its translations - always stays.
/// </summary>
public static class ImageCleanup
{
	/// <summary>
	/// After saving a game: of the pictures it showed before and the ones tried out on the form, removes those
	/// nothing uses any more.
	/// </summary>
	public static Task AfterSaveAsync(ILocalDatabaseService local, IImageStore images, PubGame saved, IEnumerable<string> noLongerNeeded) =>
		ForgetUnusedAsync(local, images, saved.TeamId, noLongerNeeded, saved);

	/// <summary>
	/// After a review request is rejected: the pictures it proposed (new ones, or new versions of pictures) are
	/// removed when no game or other waiting request uses them.
	/// </summary>
	public static Task AfterRejectAsync(ILocalDatabaseService local, IImageStore images, ReviewRequest rejected) =>
		ForgetUnusedAsync(local, images, rejected.TeamId, RefsOf(rejected), null);

	/// <summary>Every picture a game shows: its cover, its rules and every translation's rules and sentences.</summary>
	public static IEnumerable<string> RefsOf(PubGame game) =>
		RulesImages.Refs(game.CoverImageUrl, game.RulesText).Concat(TranslationRefs(game.Translations));

	/// <summary>Every picture a review request proposes, in the same places.</summary>
	public static IEnumerable<string> RefsOf(ReviewRequest request) =>
		RulesImages.Refs(request.ProposedCoverImageUrl, request.ProposedRulesText)
			.Concat(TranslationRefs(GameTranslation.FromJson(request.ProposedTranslationsText)));

	/// <summary>The pictures in translations: in their rules and in their sentences (added or replaced pictures).</summary>
	private static IEnumerable<string> TranslationRefs(Dictionary<string, GameTranslation> translations) =>
		translations.Values.SelectMany(t => RulesImages.Refs(null, t.RulesText)
			.Concat(t.Segments.SelectMany(s => RulesImages.Refs(null, $"{s.Src} {s.Text}"))));

	/// <summary>Removes the candidates that no game (but the one given, as saved) and no waiting request uses.</summary>
	private static async Task ForgetUnusedAsync(ILocalDatabaseService local, IImageStore images, string teamId, IEnumerable<string> candidates, PubGame? saved)
	{
		try
		{
			var used = saved is null ? new HashSet<string>() : RefsOf(saved).ToHashSet();
			foreach (var game in (await local.GetManageableGamesAsync()).Where(g => g.Id != saved?.Id))
				used.UnionWith(RefsOf(game));
			foreach (var request in await local.GetRequestsWaitingForReviewAsync(teamId))
				used.UnionWith(RefsOf(request));

			foreach (var reference in candidates.Distinct().Where(r => !used.Contains(r)))
				await images.DeleteAsync(reference);
		}
		catch (Exception)
		{
			// Offline or refused: the pictures stay, which only costs a little room.
		}
	}
}
