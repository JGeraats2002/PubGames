using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

public partial class HostCreateGameViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly IPermissionService _permissions;

	// TODO: replace with real values from auth/org state.
	private const string CurrentUserId = "current-user";
	private const string CurrentHostOrgId = "current-org";

	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private string rulesText = string.Empty;

	[ObservableProperty]
	private string? coverImageUrl;

	[ObservableProperty]
	private bool isPaid;

	[ObservableProperty]
	private decimal price = 0.50m;

	[ObservableProperty]
	private ScoringType scoringType = ScoringType.PointTally;

	[ObservableProperty]
	private List<string> selectedCategories = new();

	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Bound to the scoring type Picker.</summary>
	public List<ScoringType> ScoringTypeOptions { get; } = Enum.GetValues<ScoringType>().ToList();

	public HostCreateGameViewModel(ILocalDatabaseService local, ICloudSyncService cloud, IPermissionService permissions)
	{
		_local = local;
		_cloud = cloud;
		_permissions = permissions;
	}

	[RelayCommand]
	private async Task PublishAsync()
	{
		if (string.IsNullOrWhiteSpace(Name))
		{
			StatusMessage = "Give the game a name first.";
			return;
		}

		var game = new PubGame
		{
			Name = Name.Trim(),
			RulesText = RulesText,
			CoverImageUrl = CoverImageUrl,
			ScoringType = ScoringType,
			IsPaid = IsPaid,
			Price = IsPaid ? Price : 0m,
			HostOrgId = CurrentHostOrgId,
			CreatedByUserId = CurrentUserId
		};

		var members = await _local.GetHostMembersAsync(CurrentHostOrgId);
		var me = members.FirstOrDefault(m => m.UserId == CurrentUserId)
			?? new HostMember { UserId = CurrentUserId, HostOrgId = CurrentHostOrgId, Role = HostRole.HeadHost };

		if (_permissions.CanPublishDirectly(me))
		{
			game.Status = GameStatus.Published;
			game.PublishedAt = DateTime.UtcNow;
			await _local.SaveGameAsync(game);
			StatusMessage = "Published! It's live in the library.";
		}
		else
		{
			game.Status = GameStatus.PendingPublishApproval;
			await _local.SaveGameAsync(game);

			await _cloud.SubmitApprovalRequestAsync(new ApprovalRequest
			{
				GameId = game.Id,
				HostOrgId = CurrentHostOrgId,
				Type = ApprovalRequestType.PublishGame,
				RequestedByUserId = CurrentUserId
			});

			StatusMessage = "You don't have publish permission - sent to your head host for approval.";
		}
	}
}
