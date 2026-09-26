# Pub Games — .NET MAUI project

This is a working project scaffold for everything we designed: player entry,
searchable multi-category game library, a scoreboard where players can be
added/removed/reordered mid-game without touching anyone's score, a host
game-creation flow (name, custom pricing, scoring type, rules with inline
images, direct-publish vs approval-request based on permission), and host
team management (subhosts, permission toggles, promotion to head host,
approve/deny requests).

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
  for "can this host member publish/delete directly, or does it need
  approval".
- All five screens, wired to their ViewModels via dependency injection.
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
- Push notifications for the head-host approval inbox — right now
  `PendingApprovals` only loads on page appear; wire a push notification
  (Firebase Cloud Messaging is the standard choice on Android) so a head
  host finds out immediately, not just next time they open the Team tab.
- Auth/accounts — `CurrentAccountId` / `CurrentUserId` / `CurrentHostOrgId`
  are hardcoded placeholders scattered through the ViewModels (search for
  `TODO: replace with real`). Wire these to whatever auth provider you pick
  (Firebase Auth is the easiest fit alongside Firestore).
- Real fonts/icons — `Resources/Images/*.svg` are flat-color placeholders
  and `Resources/Fonts/` is empty so the project builds cleanly out of the
  box; drop your real branding in when ready.

## 3. Suggested build order

1. Stand up the backend (auth + a `purchases`, `games`, `host_members`,
   `approval_requests` schema matching the models 1:1 makes this fast).
2. Wire real auth, replace the `Current...Id` placeholders.
3. Wire `CloudSyncService` against it.
4. Wire Google Play Billing for the unlock flow.
5. Swap in real fonts/branding, polish the UI to match the mockups exactly
   (drag-to-reorder in particular — see the comment in
   `Views/ScoreboardPage.xaml` for the two implementation options).
