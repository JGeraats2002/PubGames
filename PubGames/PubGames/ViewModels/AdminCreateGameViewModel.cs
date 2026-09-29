using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// Create a new game, or edit/delete an existing one when opened with a
/// "gameId" navigation parameter from the admin library.
/// </summary>
public partial class AdminCreateGameViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly IPermissionService _permissions;
	private readonly IAuthService _auth;
	private readonly ITeamService _team;
	private readonly IImageStore _images;

	// TODO: replace with the team the user picked once multiple teams exist.
	private const string CurrentTeamId = AuthService.DefaultTeamId;

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

	/// <summary>The rules as text boxes with the pictures shown in between; saved as RulesText.</summary>
	public ObservableCollection<EditableRulesBlock> RulesBlocks { get; private set; } = EditableRulesBlock.FromRulesText(string.Empty);

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

	public AdminCreateGameViewModel(ILocalDatabaseService local, ICloudSyncService cloud, IPermissionService permissions,
		IAuthService auth, ITeamService team, IImageStore images)
	{
		_auth = auth;
		_team = team;
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
		RulesBlocks = EditableRulesBlock.FromRulesText(_editing.RulesText);
		OnPropertyChanged(nameof(RulesBlocks));
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

	/// <summary>Uploads a picture and shows it in the rules where the cursor was (at the end when no text box was tapped).</summary>
	public async Task InsertRulesImageAsync(EditableRulesBlock? at, int cursor)
	{
		var reference = await UploadImageAsync();
		if (reference is not null)
			EditableRulesBlock.InsertImage(RulesBlocks, at, cursor, reference);
	}

	[RelayCommand]
	private void RemoveRulesImage(EditableRulesBlock image) =>
		EditableRulesBlock.RemoveImage(RulesBlocks, image);

	/// <summary>Picks and uploads one image. Returns its reference, or null if cancelled or failed (StatusMessage says why).</summary>
	private async Task<string?> UploadImageAsync()
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

		var me = _team.Me;
		if (me is null)
		{
			StatusMessage = "Only admins and moderators can create games.";
			return;
		}

		var canPublish = _permissions.CanPublishDirectly(me);
		if (!canPublish && _editing is { Status: GameStatus.Published or GameStatus.PendingDeletionApproval })
		{
			StatusMessage = "You can't change published games - ask an admin for publish rights or to make the change.";
			return;
		}

		var game = _editing ?? new PubGame
		{
			TeamId = CurrentTeamId,
			CreatedByUserId = CurrentUserId
		};
		var previousStatus = game.Status;
		game.Name = Name.Trim();
		game.RulesText = EditableRulesBlock.ToRulesText(RulesBlocks);
		game.CoverImageUrl = CoverImageUrl;
		game.ScoringType = ScoringType;
		game.IsPaid = IsPaid;
		game.Price = IsPaid ? Price : 0m;

		if (canPublish)
		{
			game.Status = GameStatus.Published;
			game.PublishedAt ??= DateTime.UtcNow;

			// Cloud first: a change only counts once everyone else can actually see it.
			StatusMessage = IsEditing ? "Saving..." : "Publishing...";
			if (!await TrySaveToCloudAsync(game))
			{
				game.Status = previousStatus;
				return;
			}

			await _local.SaveGameAsync(game);
			StatusMessage = IsEditing
				? "Saved - everyone sees the updated game."
				: $"Published! \"{game.Name}\" is live in everyone's library.";
			Finished?.Invoke(this, EventArgs.Empty);
		}
		else
		{
			// The game goes to the cloud (hidden from players) so the admin who approves it can see it.
			game.Status = GameStatus.PendingPublishApproval;
			StatusMessage = "Sending for approval...";
			if (!await TrySaveToCloudAsync(game))
			{
				game.Status = previousStatus;
				return;
			}
			await _local.SaveGameAsync(game);

			try
			{
				await _cloud.SubmitApprovalRequestAsync(new ApprovalRequest
				{
					GameId = game.Id,
					GameName = game.Name,
					TeamId = CurrentTeamId,
					Type = ApprovalRequestType.PublishGame,
					RequestedByUserId = CurrentUserId,
					RequestedByEmail = TeamMember.NormalizeEmail(_auth.CurrentUser!.Email),
					RequestedByName = _auth.CurrentUser.DisplayName
				});
			}
			catch (Exception ex)
			{
				StatusMessage = ex is HttpRequestException
					? "No connection - the game is saved, tap Publish again when you're online."
					: ex.Message;
				return;
			}

			StatusMessage = "You don't have publish permission - sent to an admin for approval.";
		}
	}

	/// <summary>Soft delete: the game is marked Deleted so it drops out of every player's library on their next refresh.</summary>
	public async Task DeleteAsync()
	{
		if (_editing is null) return;

		var me = _team.Me;
		if (me is null || !_permissions.CanDeleteDirectly(me))
		{
			StatusMessage = "You don't have permission to delete games - ask an admin.";
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
