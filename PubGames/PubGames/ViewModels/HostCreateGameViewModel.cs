using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// Create a new game, or edit/delete an existing one when opened with a
/// "gameId" navigation parameter from the host library.
/// </summary>
public partial class HostCreateGameViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly IPermissionService _permissions;
	private readonly IAuthService _auth;
	private readonly IImageStore _images;

	// TODO: replace with the org the user picked once multiple orgs exist.
	private const string CurrentHostOrgId = AuthService.DefaultHostOrgId;

	private string CurrentUserId => _auth.AccountId;

	/// <summary>The game being edited; null when creating a new one.</summary>
	private PubGame? _editing;

	/// <summary>Raised after a successful save or delete so the page can navigate back.</summary>
	public event EventHandler? Finished;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PageTitle), nameof(SaveButtonText))]
	private bool isEditing;

	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private string rulesText = string.Empty;

	/// <summary>Image reference from IImageStore (not necessarily a URL, despite the model's field name).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasCover), nameof(CoverButtonText))]
	private string? coverImageUrl;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(UploadCoverCommand), nameof(PublishCommand))]
	private bool isUploading;

	public bool HasCover => !string.IsNullOrEmpty(CoverImageUrl);
	public string CoverButtonText => HasCover ? "Change cover image" : "Upload cover image";

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

	public string PageTitle => IsEditing ? "Edit game" : "New game";
	public string SaveButtonText => IsEditing ? "Save changes" : "Publish game";

	/// <summary>Bound to the scoring type Picker.</summary>
	public List<ScoringType> ScoringTypeOptions { get; } = Enum.GetValues<ScoringType>().ToList();

	public HostCreateGameViewModel(ILocalDatabaseService local, ICloudSyncService cloud, IPermissionService permissions, IAuthService auth, IImageStore images)
	{
		_auth = auth;
		_images = images;
		_local = local;
		_cloud = cloud;
		_permissions = permissions;
	}

	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (!query.TryGetValue("gameId", out var id) || id is not string gameId) return;

		_editing = await _local.GetGameAsync(gameId);
		if (_editing is null) return;

		IsEditing = true;
		Name = _editing.Name;
		RulesText = _editing.RulesText;
		CoverImageUrl = _editing.CoverImageUrl;
		IsPaid = _editing.IsPaid;
		Price = _editing.IsPaid ? _editing.Price : 0.50m;
		ScoringType = _editing.ScoringType;
	}

	private bool CanUpload() => !IsUploading;

	[RelayCommand(CanExecute = nameof(CanUpload))]
	private async Task UploadCoverAsync()
	{
		var reference = await UploadImageAsync();
		if (reference is not null)
			CoverImageUrl = reference;
	}

	[RelayCommand]
	private void RemoveCover() => CoverImageUrl = null;

	/// <summary>Picks and uploads one image. Returns its reference, or null if cancelled or failed (StatusMessage says why).</summary>
	public async Task<string?> UploadImageAsync()
	{
		if (IsUploading) return null;

		IsUploading = true;
		StatusMessage = "Uploading image...";
		try
		{
			var reference = await _images.PickAndUploadAsync();
			StatusMessage = string.Empty;
			return reference;
		}
		catch (HttpRequestException)
		{
			StatusMessage = "No connection - couldn't upload the image.";
		}
		catch (Exception ex)
		{
			StatusMessage = ex.Message;
		}
		finally
		{
			IsUploading = false;
		}
		return null;
	}

	[RelayCommand(CanExecute = nameof(CanUpload))]
	private async Task PublishAsync()
	{
		if (!_auth.IsSignedIn)
		{
			StatusMessage = "Sign in to create games.";
			return;
		}

		if (string.IsNullOrWhiteSpace(Name))
		{
			StatusMessage = "Give the game a name first.";
			return;
		}

		var game = _editing ?? new PubGame
		{
			HostOrgId = CurrentHostOrgId,
			CreatedByUserId = CurrentUserId
		};
		game.Name = Name.Trim();
		game.RulesText = RulesText;
		game.CoverImageUrl = CoverImageUrl;
		game.ScoringType = ScoringType;
		game.IsPaid = IsPaid;
		game.Price = IsPaid ? Price : 0m;

		var members = await _local.GetHostMembersAsync(CurrentHostOrgId);
		// Signed-in users who aren't on the team yet can still propose games, but only via approval.
		var me = _permissions.ResolveMember(members, _auth.CurrentUser, CurrentHostOrgId)
			?? new HostMember { UserId = CurrentUserId, HostOrgId = CurrentHostOrgId, CanAddGames = false };

		if (_permissions.CanPublishDirectly(me))
		{
			game.Status = GameStatus.Published;
			game.PublishedAt ??= DateTime.UtcNow;

			// Cloud first: a change only counts once everyone else can actually see it.
			StatusMessage = IsEditing ? "Saving..." : "Publishing...";
			if (!await TrySaveToCloudAsync(game)) return;

			await _local.SaveGameAsync(game);
			StatusMessage = IsEditing
				? "Saved - everyone sees the updated game."
				: $"Published! \"{game.Name}\" is live in everyone's library.";
			Finished?.Invoke(this, EventArgs.Empty);
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

	/// <summary>Soft delete: the game is marked Deleted so it drops out of every player's library on their next refresh.</summary>
	public async Task DeleteAsync()
	{
		if (_editing is null) return;

		var members = await _local.GetHostMembersAsync(CurrentHostOrgId);
		var me = _permissions.ResolveMember(members, _auth.CurrentUser, CurrentHostOrgId);
		if (me is null || !_permissions.CanDeleteDirectly(me))
		{
			StatusMessage = "You don't have permission to delete games.";
			return;
		}

		var previousStatus = _editing.Status;
		_editing.Status = GameStatus.Deleted;
		StatusMessage = "Deleting...";
		if (!await TrySaveToCloudAsync(_editing))
		{
			_editing.Status = previousStatus;
			return;
		}

		await _local.SaveGameAsync(_editing);
		StatusMessage = string.Empty;
		Finished?.Invoke(this, EventArgs.Empty);
	}

	private async Task<bool> TrySaveToCloudAsync(PubGame game)
	{
		try
		{
			await _cloud.SaveGameAsync(game);
			return true;
		}
		catch (HttpRequestException)
		{
			StatusMessage = "No connection - try again when you're online.";
		}
		catch (Exception ex)
		{
			StatusMessage = ex.Message;
		}
		return false;
	}
}
