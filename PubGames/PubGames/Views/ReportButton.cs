using PubGames.Models;
using PubGames.Services;

namespace PubGames.Views;

/// <summary>
/// The page a report is made on, and who it's probably about (the one whose
/// request, message or game is shown), so the report says where it was made.
/// </summary>
public sealed record ReportContext(string PageName, string? AboutEmail = null);

/// <summary>
/// 📣 next to the ⓘ on the pages where admins and moderators see each
/// other's work: report someone who breaks the agreements. A report has a
/// category and an explanation, says which page it was made on, and goes to
/// the owners only, so the one reported never sees it. The owners get no
/// button: they receive the reports.
/// </summary>
public static class ReportButton
{
	/// <summary>Adds 📣 to the page's title bar (not for the owners).</summary>
	public static void Attach(ContentPage page, Func<ReportContext> context)
	{
		var team = IPlatformApplication.Current?.Services.GetService<ITeamService>();
		if (team?.Me is not { IsBuiltInAdmin: false }) return;
		var report = new ToolbarItem
		{
			Text = "📣",
			Order = ToolbarItemOrder.Primary,
			Command = new Command(async () => await ReportAsync(page, team, context()))
		};
		SemanticProperties.SetDescription(report, L.T("Report an admin or moderator"));
		page.ToolbarItems.Add(report);
	}

	/// <summary>Who, what kind and what happened; then, after the warning about false reports, it's sent.</summary>
	private static async Task ReportAsync(ContentPage page, ITeamService team, ReportContext context)
	{
		List<TeamMember> members;
		try
		{
			members = await team.PullMembersAsync();
		}
		catch (Exception)
		{
			members = await team.GetCachedMembersAsync();
		}
		var me = team.Me?.Email;
		// The owners receive the reports, so they aren't on the list; the one shown on the page comes first.
		var candidates = members.Where(m => !m.IsBuiltInAdmin && m.Email != me)
			.OrderBy(m => m.Email == context.AboutEmail ? 0 : 1).ThenBy(m => m.Label, StringComparer.CurrentCultureIgnoreCase)
			.ToList();
		if (candidates.Count == 0)
		{
			await page.DisplayAlertAsync(L.T("Report"), L.T("There's nobody on the team to report."), "OK");
			return;
		}

		var names = candidates.Select(m => $"{m.Label} ({m.RoleName})").ToArray();
		var who = Array.IndexOf(names, await page.DisplayActionSheetAsync(L.T("Who do you want to report?"), L.T("Cancel"), null, names));
		if (who < 0) return;
		var about = candidates[who];

		var categories = ReportCategory.All;
		var labels = categories.Select(c => c.Label).ToArray();
		var what = Array.IndexOf(labels, await page.DisplayActionSheetAsync(L.F("What did {0} do?", about.Label), L.T("Cancel"), null, labels));
		if (what < 0) return;

		var explanation = await page.DisplayPromptAsync(L.T("Report"),
			L.F("Explain what happened, as exactly as you can ({0} characters at most). A false report can lead to your own ban.", 400),
			L.T("Next"), L.T("Cancel"), maxLength: 400, keyboard: Keyboard.Chat);
		if (string.IsNullOrWhiteSpace(explanation)) return;

		if (!await page.DisplayAlertAsync(L.T("Send this report?"),
				L.F("Your report about {0} goes to the owner only; {0} doesn't see it. It says it was made on: {1}. Only report what really happened: a false report can lead to your own ban.", about.Label, context.PageName),
				L.T("Send report"), L.T("Cancel")))
			return;

		var messages = IPlatformApplication.Current?.Services.GetService<IMessageService>();
		if (messages is null) return;
		try
		{
			foreach (var owner in AuthConfig.AdminEmails)
				await messages.SendAsync(new InboxMessage
				{
					ToEmail = owner,
					Text = explanation,
					Kind = MessageKind.Report,
					AboutEmail = about.Email,
					AboutName = about.Label,
					ReportCategory = categories[what].Key,
					PageName = context.PageName
				});
			await page.DisplayAlertAsync(L.T("Report sent"), L.T("The owner looks into it. Thank you for helping to keep the library fair."), "OK");
		}
		catch (Exception ex)
		{
			await page.DisplayAlertAsync(L.T("Not sent"), ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message, "OK");
		}
	}
}
