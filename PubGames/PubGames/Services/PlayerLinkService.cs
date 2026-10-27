using System.Globalization;
using PubGames.Models;

namespace PubGames.Services;

/// <summary>Someone the user blocked: they can't add the user by code any more.</summary>
public sealed record BlockedPlayer(string Uid, string Name, DateTime BlockedAt)
{
	/// <summary>"Blocked on 27 Oct 2026".</summary>
	public string BlockedLabel => $"Blocked on {BlockedAt.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture)}";
}

/// <summary>
/// Links between players added by personal code, kept in the cloud:
/// - unlinks/{fromUid}_{toUid}: "remove the link both ways" - the other
///   person's phone removes its saved player for fromUid the next time it
///   loads the players, and then deletes the note;
/// - blocks/{blockerUid}_{blockedUid}: the blocked person can't add the
///   blocker by code, and their phone keeps the blocker out of its saved players.
/// </summary>
public interface IPlayerLinkService
{
	/// <summary>Asks the other person's phone to remove its link to the user too.</summary>
	Task UnlinkBothWaysAsync(string otherUid);

	/// <summary>Blocks someone (and removes the link both ways).</summary>
	Task BlockAsync(string otherUid, string name);

	Task UnblockAsync(string otherUid);

	/// <summary>Everyone the user blocked, by name.</summary>
	Task<List<BlockedPlayer>> GetBlockedAsync();

	/// <summary>Whether this person blocked the user.</summary>
	Task<bool> IsBlockedByAsync(string otherUid);

	/// <summary>
	/// The accounts whose saved players should go from this phone: who removed
	/// the link both ways (those notes are then cleared) and who blocked the user.
	/// </summary>
	Task<HashSet<string>> TakeRemovedLinksAsync();
}

/// <summary>
/// Removing links both ways and blocking, through the cloud "unlinks" and "blocks" collections.
/// </summary>
public class PlayerLinkService(FirestoreClient firestore, IAuthService auth) : IPlayerLinkService
{
	private const string Unlinks = "unlinks";
	private const string Blocks = "blocks";

	private string Me => auth.CurrentUser?.Uid ?? throw new InvalidOperationException("Sign in first.");

	/// <inheritdoc />
	public Task UnlinkBothWaysAsync(string otherUid) =>
		firestore.SetAsync(Unlinks, $"{Me}_{otherUid}", new Dictionary<string, object?>
		{
			["fromUid"] = Me,
			["toUid"] = otherUid,
			["createdAt"] = DateTime.UtcNow
		});

	/// <inheritdoc />
	public async Task BlockAsync(string otherUid, string name)
	{
		await firestore.SetAsync(Blocks, $"{Me}_{otherUid}", new Dictionary<string, object?>
		{
			["blockerUid"] = Me,
			["blockedUid"] = otherUid,
			["blockedName"] = name.Length > 40 ? name[..40] : name,
			["createdAt"] = DateTime.UtcNow
		});
		await UnlinkBothWaysAsync(otherUid);
	}

	/// <inheritdoc />
	public Task UnblockAsync(string otherUid) => firestore.DeleteAsync(Blocks, $"{Me}_{otherUid}");

	/// <inheritdoc />
	public async Task<List<BlockedPlayer>> GetBlockedAsync()
	{
		if (auth.CurrentUser is null) return [];
		return (await firestore.WhereEqualAsync(Blocks, "blockerUid", Me))
			.Where(f => f.GetValueOrDefault("blockedUid") is string)
			.Select(f => new BlockedPlayer((string)f["blockedUid"]!,
				f.GetValueOrDefault("blockedName") as string ?? "Unknown player",
				f.GetValueOrDefault("createdAt") as DateTime? ?? DateTime.UtcNow))
			.OrderBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase)
			.ToList();
	}

	/// <inheritdoc />
	public async Task<bool> IsBlockedByAsync(string otherUid) =>
		await firestore.GetAsync(Blocks, $"{otherUid}_{Me}") is not null;

	/// <inheritdoc />
	public async Task<HashSet<string>> TakeRemovedLinksAsync()
	{
		if (auth.CurrentUser is null) return [];
		var removed = new HashSet<string>();
		foreach (var note in await firestore.WhereEqualAsync(Unlinks, "toUid", Me))
		{
			if (note.GetValueOrDefault("fromUid") is not string from) continue;
			removed.Add(from);
			await firestore.DeleteAsync(Unlinks, $"{from}_{Me}");
		}
		foreach (var block in await firestore.WhereEqualAsync(Blocks, "blockedUid", Me))
			if (block.GetValueOrDefault("blockerUid") is string blocker)
				removed.Add(blocker);
		return removed;
	}
}
