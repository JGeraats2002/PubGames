using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// Tidies up game images in the cloud. Every upload - and every edit of a
/// picture made in the image editor - is a new image, so after a game is
/// saved the ones it no longer shows would stay behind for good. Only admins
/// can delete images (firestore.rules), so this runs when an admin saves a
/// game or approves a moderator's changes.
/// </summary>
public static class ImageCleanup
{
	/// <summary>
	/// Deletes the images among <paramref name="noLongerNeeded"/> that the
	/// saved game doesn't show any more. An image that's still used anywhere
	/// else stays: in another game, or in a request that's waiting for review.
	/// Never fails the save: what can't be deleted now just stays.
	/// </summary>
	/// <param name="saved">The game as it was just saved.</param>
	/// <param name="noLongerNeeded">What the game showed before, and what was uploaded while editing it.</param>
	public static async Task AfterSaveAsync(ILocalDatabaseService local, IImageStore images, PubGame saved, IEnumerable<string> noLongerNeeded)
	{
		try
		{
			var used = RulesImages.Refs(saved.CoverImageUrl, saved.RulesText).ToHashSet();
			foreach (var game in (await local.GetManageableGamesAsync()).Where(g => g.Id != saved.Id))
				used.UnionWith(RulesImages.Refs(game.CoverImageUrl, game.RulesText));
			foreach (var request in await local.GetRequestsWaitingForReviewAsync(saved.TeamId))
				used.UnionWith(RulesImages.Refs(request.ProposedCoverImageUrl, request.ProposedRulesText));

			foreach (var reference in noLongerNeeded.Distinct().Where(r => !used.Contains(r)))
				await images.DeleteAsync(reference);
		}
		catch (Exception)
		{
			// Offline or refused: the images stay, which only costs a little room.
		}
	}
}
