using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// Feedback from players to the admins, in the cloud "feedback" collection.
/// Everyone signed in sends feedback and sees their own; admins see all of it
/// and handle it (status and reply). Needs a connection: nothing is kept on
/// the phone.
/// </summary>
public interface IFeedbackService
{
	Task SendAsync(FeedbackCategory category, PubGame? game, string text);

	/// <summary>The signed-in user's own feedback, newest first.</summary>
	Task<List<Feedback>> GetMineAsync();

	/// <summary>Admins: all feedback, newest first.</summary>
	Task<List<Feedback>> GetAllAsync();

	/// <summary>Admins: a new status and (optionally) a reply for the sender.</summary>
	Task UpdateAsync(Feedback feedback, FeedbackStatus status, string? reply);

	Task DeleteAsync(Feedback feedback);
}

public class FeedbackService(FirestoreClient firestore, IAuthService auth, IProfileService profiles) : IFeedbackService
{
	private const string Collection = "feedback";

	public async Task SendAsync(FeedbackCategory category, PubGame? game, string text)
	{
		text = text.Trim();
		if (text.Length < Feedback.MinLength)
			throw new InvalidOperationException($"Explain your feedback (at least {Feedback.MinLength} characters).");
		if (text.Length > Feedback.MaxLength)
			throw new InvalidOperationException($"Feedback can be {Feedback.MaxLength} characters at most.");
		var user = auth.CurrentUser ?? throw new InvalidOperationException("Sign in to send feedback.");

		var feedback = new Feedback
		{
			FromUid = user.Uid,
			FromEmail = TeamMember.NormalizeEmail(user.Email),
			FromName = profiles.Mine?.Name is { Length: > 0 } name ? name : user.DisplayName,
			Category = category,
			GameId = game?.Id,
			GameName = game?.Name,
			Text = text
		};
		await firestore.SetAsync(Collection, feedback.Id, Fields(feedback));
	}

	public async Task<List<Feedback>> GetMineAsync()
	{
		if (auth.CurrentUser is not { } user) return [];
		return (await firestore.WhereEqualAsync(Collection, "fromUid", user.Uid))
			.Select(ToFeedback).OfType<Feedback>()
			.OrderByDescending(f => f.SentAt).ToList();
	}

	public async Task<List<Feedback>> GetAllAsync() =>
		(await firestore.GetAllAsync(Collection))
			.Select(ToFeedback).OfType<Feedback>()
			.OrderByDescending(f => f.SentAt).ToList();

	public async Task UpdateAsync(Feedback feedback, FeedbackStatus status, string? reply)
	{
		reply = string.IsNullOrWhiteSpace(reply) ? null : reply.Trim();
		if (reply?.Length > Feedback.MaxReplyLength)
			throw new InvalidOperationException($"A reply can be {Feedback.MaxReplyLength} characters at most.");
		var handledBy = auth.CurrentUser?.DisplayName ?? string.Empty;
		var now = DateTime.UtcNow;
		await firestore.UpdateFieldsAsync(Collection, feedback.Id, new Dictionary<string, object?>
		{
			["status"] = status.ToString(),
			["reply"] = reply,
			["handledBy"] = handledBy,
			["updatedAt"] = now
		});
		feedback.Status = status;
		feedback.Reply = reply;
		feedback.HandledBy = handledBy;
		feedback.UpdatedAt = now;
	}

	public Task DeleteAsync(Feedback feedback) => firestore.DeleteAsync(Collection, feedback.Id);

	private static Dictionary<string, object?> Fields(Feedback f) => new()
	{
		["id"] = f.Id,
		["fromUid"] = f.FromUid,
		["fromEmail"] = f.FromEmail,
		["fromName"] = f.FromName,
		["category"] = f.Category.ToString(),
		["gameId"] = f.GameId,
		["gameName"] = f.GameName,
		["text"] = f.Text,
		["sentAt"] = f.SentAt,
		["status"] = f.Status.ToString(),
		["reply"] = f.Reply,
		["handledBy"] = f.HandledBy,
		["updatedAt"] = f.UpdatedAt
	};

	private static Feedback? ToFeedback(Dictionary<string, object?> f)
	{
		if (f.GetValueOrDefault("id") is not string id) return null;
		return new Feedback
		{
			Id = id,
			FromUid = f.GetValueOrDefault("fromUid") as string ?? string.Empty,
			FromEmail = f.GetValueOrDefault("fromEmail") as string ?? string.Empty,
			FromName = f.GetValueOrDefault("fromName") as string ?? string.Empty,
			Category = Enum.TryParse<FeedbackCategory>(f.GetValueOrDefault("category") as string, out var c) ? c : FeedbackCategory.Other,
			GameId = f.GetValueOrDefault("gameId") as string,
			GameName = f.GetValueOrDefault("gameName") as string,
			Text = f.GetValueOrDefault("text") as string ?? string.Empty,
			SentAt = f.GetValueOrDefault("sentAt") as DateTime? ?? DateTime.UtcNow,
			Status = Enum.TryParse<FeedbackStatus>(f.GetValueOrDefault("status") as string, out var s) ? s : FeedbackStatus.New,
			Reply = f.GetValueOrDefault("reply") as string,
			HandledBy = f.GetValueOrDefault("handledBy") as string,
			UpdatedAt = f.GetValueOrDefault("updatedAt") as DateTime?
		};
	}
}
