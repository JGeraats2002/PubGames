# Pub Games — .NET MAUI project

This is a working project scaffold for everything we designed: player entry,
searchable multi-category game library, a scoreboard where players can be
added/removed/reordered mid-game without touching anyone's score, a
game-creation flow (name, custom pricing, scoring type, rules with inline
pictures shown right in the editor), and team management.

Roles and terms used throughout the app and code:
- **Admin** — publishes, edits and deletes games directly, reviews requests,
  manages the team (add/remove moderators by email, with explanatory emails;
  make someone admin).
- **Moderator** — never changes games directly. Everything is a **review
  request** of one of three kinds: **New game**, **Changes to a game**,
  **Deletion**. Buttons: *Submit for review*, *Request deletion*, *Withdraw
  request*. Followed under **My requests** in the Library.
- **Review status** — *Waiting for review*, *Approved*, *Rejected* (with the
  admin's reason). Admins decide on the **Requests** page (*Approve* /
  *Reject*), which shows what changes plus a preview. Players keep seeing
  the current version until a request is approved. Moderators can also
  propose a **New category**.
- **Inbox** — short messages (max 500 characters) between admins and
  moderators, plus **review decisions** sent automatically when an admin
  approves or rejects. The recipient keeps or deletes each message; a
  rejected game/change offers *Edit and submit again*.
- **Categories** — every game is in at least one (defaults: Family, Friends,
  Pub, Home, Cards, Dice, Analog, Digital); players filter by category.
  Adding a category creates an **Add games to category** request for admins.

## 1. Open it

1. Install **Visual Studio 2022** (17.8+) with the **.NET Multi-platform App
   UI development** workload (Visual Studio Installer → Modify → check that
   workload).
2. Double-click `PubGames.sln`. First restore may take a minute.
3. Set the startup project's target to an Android emulator or device and hit
   Run.

The project currently targets `net8.0-android` only. If you later want iOS/
Windows too, add `;net8.0-ios;net8.0-maccatalyst;net8.0-windows10.0.19041.0`
to `<TargetFrameworks>` in `PubGames.csproj`.

## 2. What's real vs. what's stubbed

**Fully implemented, runs locally:**
- All data models (`Models/`) matching everything we discussed.
- Local SQLite storage (`Services/LocalDatabaseService.cs`) — offline-first,
  so the app works with no signal at the pub.
- Permission logic (`Services/PermissionService.cs`) — single source of truth
  for "can this person change games directly, or submit a review request".
  Team membership lives in the Firestore "team" collection
  (`Services/TeamService.cs`), review requests in "reviewRequests";
  `firestore.rules` enforces the same rules.
- All screens, wired to their ViewModels via dependency injection.
- The mid-game player add/remove/reorder logic
  (`ViewModels/ScoreboardViewModel.cs`) — read the comments there, this is
  the piece that keeps scores stable no matter what you do to the player
  list.

**Stubbed, needs your backend:**
- `Services/CloudSyncService.cs` — every method has a `TODO` marking exactly
  what HTTP/Firebase call needs to go there. Nothing here will work until you
  point `ApiBaseUrl` at a real API (or swap in the Firebase SDK).
- Google Play Billing — the "Publish" and unlock flows call into
  `ICloudSyncService.VerifyAndRecordPurchaseAsync`, but the actual purchase
  UI needs `Plugin.InAppBilling` (or the official Play Billing bindings)
  wired in. Never mark a game "owned" from the client alone — always verify
  server-side first, or people can fake unlocks.
- Push notifications for review requests — right now admins only see new
  requests when they open the Library or Requests page; wire a push
  notification (Firebase Cloud Messaging is the standard choice on Android)
  so an admin finds out immediately.
- Auth/accounts — `CurrentAccountId` / `CurrentUserId` / `CurrentTeamId`
  are hardcoded placeholders scattered through the ViewModels (search for
  `TODO: replace with real`). Wire these to whatever auth provider you pick
  (Firebase Auth is the easiest fit alongside Firestore).
- Real fonts/icons — `Resources/Images/*.svg` are flat-color placeholders
  and `Resources/Fonts/` is empty so the project builds cleanly out of the
  box; drop your real branding in when ready.

## 3. Suggested build order

1. Stand up the backend (auth + a `purchases`, `games`, `team`,
   `reviewRequests` schema matching the models 1:1 makes this fast).
2. Wire real auth, replace the `Current...Id` placeholders.
3. Wire `CloudSyncService` against it.
4. Wire Google Play Billing for the unlock flow.
5. Swap in real fonts/branding, polish the UI to match the mockups exactly
   (drag-to-reorder in particular — see the comment in
   `Views/ScoreboardPage.xaml` for the two implementation options).
