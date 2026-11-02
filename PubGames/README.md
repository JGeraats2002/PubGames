# Pub Games

.NET MAUI-app voor Android (`net10.0-android36.0`, app-id `com.pubgems.pubgames`) voor
drankspellen in de kroeg: spelers kiezen, spellen uit een bibliotheek, scorebord, duels,
statistieken, feedback, en betaalde spellen of een Pub Games Pass. 9 talen.

- Repo: GitHub `JGeraats2002/PubGames`, branch `master`
- Firebase-project: `pubgames-92835` (Auth, Firestore `eur3`, Cloud Functions in `europe-west1`,
  Cloud Translation API aan)
- Eigenaar/admin: `jjh.geraats@gmail.com`; ontwikkelaarsaccount Play: `geraatsdeveloping@gmail.com`
  (Account owner, ontwikkelaarsnaam PubGems, ID `5864539126255989179`). Payments profile en
  account group (15% servicekosten) zijn ingesteld.
- Website (Firebase Hosting): https://pubgames-92835.web.app (`/privacy`, `/delete-account`)

## Mappen

| Map / bestand | Inhoud |
|---|---|
| `PubGames/PubGames/` | De app (Models, Services, ViewModels, Views, Platforms/Android) |
| `PubGames/PubGames/Models/PageHelp.cs` | Alle uitleg uit de ⓘ-knoppen |
| `PubGames/PubGames/Resources/Strings/*.json` | App-teksten in 9 talen |
| `functions/index.js`, `functions/shared.js` | Cloud Functions (aankopen, kortingscode, refunds, account verwijderen, vertalen, berichten, plaatjescontrole) |
| `firestore.rules` | Beveiligingsregels van Firestore |
| `tests/` | Automatische tests (zie Aantekeningen) |
| `public/` | Website: privacybeleid, account verwijderen |
| `PlayStore/` | Play Console-teksten (`README.md`, `listings.md`), icoon, feature graphic |
| `Builds/` | Kopieën van geüploade `.aab`-bestanden (`pubgames-v<code>-<versie>.aab`); niet in git |

---

## Waar we staan (02-11-2026)

- **Versie 6 (1.5)** is gebouwd (02-11-2026), nog te uploaden bij **Closed testing**. Bestand:
  `PubGames/PubGames/bin/Release/net10.0-android36.0/publish/com.pubgems.pubgames-Signed.aab`
  (kopie: `Builds/pubgames-v6-1.5.aab`). Versie 5 (1.4) is niet geüpload; versie 6 bevat alles van versie 5.
  Versie 4 (1.3) staat in closed testing (kopie: `Builds/pubgames-v4-1.3.aab`). Closed test loopt met 12+ testers.
- Functions (ook `translateAgreements` en `announceStaleDrafts`) en firestore.rules zijn gedeployed (01-11-2026). Alles is gecommit en gepusht (02-11-2026).
- Alles is door de gebruiker getest (afvinklijst https://claude.ai/artifact/2bdNcCQqsLQbSR2ihAuRBD),
  behalve één hertest: X3 (Vertalingen › Aanpassen › afbeelding opent de echte afbeelding in de editor).

- **Nieuw in versie 5: scoringtype Scoreblad** (Yatzy-achtig, eerste testerfeedback). Maker stelt lijsten
  in met rijen en bonussen. Een rij is getypt (met regels) of heeft een vaste score die spelers afvinken
  (✓, of ✗ voor 0; de score staat tussen haakjes achter de naam). Per rij instelbaar of 0 mag. Bonussen:
  automatisch bij een totaal, of per keer met − / + en een maximum; elke bonus kan afhangen van een score
  (niet 0) in een gekozen rij (🔒 tot dan). Onder Deelspel: welk totaal wint, en een deelspel eindigt óf als
  het blad vol is óf bij een eindscore (automatisch of door de spelers). Een duel bij gelijkspel wordt
  getypt (Score intypen), niet op het blad gespeeld. Op het scoreboard een tabel met een kolom per speler;
  de score is het totaal van het blad (`SessionParticipant.SheetText` bewaart de vakjes); ongeldige waarden
  blijven rood staan met een rode melding bovenaan en tellen niet mee.
  Code: `Models/ScoreSheet.cs`, `ViewModels/EditableScoreSheet.cs`, `Views/ScoreboardPage.Sheet.cs`.
  Oude app-versies zien een spel met dit type als Custom; zet zo'n spel dus pas op Published na versie 5.
- Ook in versie 5: toetsenbord weg = focus weg, overal in de app (`Platforms/Android/MainActivity.cs`).
  Handmatig plus/min telt een getypt bedrag pas op bij + of − (Volgende / Gereed tellen niets meer op).
  "+ Je regels toevoegen" staat vast onder de tabbladen van het spelformulier.
- **Nieuw in versie 6** (na versie 5 gebouwd):
  - Pagina **Afspraken** (laatste pagina van het Admin-tabblad, `Views/AgreementsPage`, `Models/AdminAgreements.cs`):
    regels voor admins en mods (rollen, prijzen gratis / €0,99 / €1,99, inhoud, drankspellen, kwaliteit, misbruik).
    Opent als eerste bij de eerste keer als admin/mod; tot "Gelezen en akkoord" zijn de andere admin-pagina's dicht
    (`AppShell.OnNavigating`). Akkoord per account: `team/{email}.agreedRules` (versie) en op de telefoon
    (`Preferences agreedRules:{email}`, eigenaars alleen daar). Team-pagina toont wie akkoord is.
    De eigenaar bewerkt de tekst met ✏ Bewerken: Firestore `agreements/current` (alleen eigenaars schrijven),
    vertaald door de functie `translateAgreements`. "Afspraken gewijzigd" verhoogt de versie: iedereen opnieuw akkoord.
  - **▶ Testen** in het spelformulier (`Services/TestRun.cs`): speelt het spel zoals ingevuld met Speler 1, 2, ...
    onder account `test-run` (niet in stats, opgeslagen spellen of spelerslijst), kopie van het spel als `Deleted`.
    ⏹ Test stoppen op elke spelpagina; ander tabblad of terug naar het formulier stopt de test ook.
  - **Afbeeldingseditor:** Selecteren + Groeperen (`DrawItem.Group`; groep alleen slepen, kopiëren, verwijderen,
    opheffen), ✓ / ✕ als 1 teken (Kind wisselt), tekst met Enter (Editor); vertaalde tekst breekt af op de
    langste regel van het origineel (`DrawItem.FitTranslation`, in `ImageStore`).
  - **Concepten** (`Firestore drafts/{id}`, `Services/DraftService.cs`, `Models/GameDraft.cs`): Opslaan als concept
    voor nieuwe spellen; lijst in de bibliotheek voor het hele team; alleen de eigenaar wijzigt en maakt af,
    anderen testen of sturen een tip. Na 3 dagen zonder wijziging overnemen (regels checken dit); de functie
    `announceStaleDrafts` (dagelijks 09:00) stuurt iedereen 1 bericht. Publiceren/indienen verwijdert het concept.
  - **Inbox:** filters per categorie met prioriteit (rapportage, beoordeling, concept, vertaling, tip, bericht).
  - **Rapporteren** (📣 naast ⓘ, `Views/ReportButton.cs`): wie, categorie, uitleg en pagina; alleen naar de eigenaar.
  - **Testerfeedback 02-11:**
    - Afbeeldingseditor: titel op een eigen regel boven Selecteren / Ongedaan / ⓘ; alles boven Annuleren / Gebruiken
      scrolt als één geheel. Een vinger op de tekening houdt het scrollen tegen (`KeepTouchOnCanvas`).
    - Bibliotheek (`GameLibraryPage`): lijst in een Grid in plaats van een stack, zodat hij scrolt en het laatste spel
      bereikbaar is. (Yatzee ontbrak bij een tester omdat het nog niet gepubliceerd is: spelers zien alleen Published.)
    - Categoriewolken en andere chips: `Views/WrapLayout.cs` in plaats van FlexLayout (die knipte de laatste letter af,
      "Kaarte"). Elke chip krijgt zijn volle breedte, plus 4dp speling.
    - Afspraken: "Gelezen en akkoord" staat onderaan, na de regels (scrollt mee), niet meer vast in beeld.
    - Mail aan een nieuwe moderator (`TeamEmails.ModeratorAdded`): kort, met de afspraken in het Engels
      (`AdminAgreements.EnglishSections`); alleen extra: spellen maken (mods met goedkeuring, admins direct), uitleg onder ⓘ.
    - **Weergave** (Account › Weergave, `Services/Appearance.cs`): Zoals telefoon (standaard) / Lichte modus / Donkere
      modus, per account op deze telefoon (zoals de taal). App.xaml heeft de lichte kleuren; `Appearance` zet bij donker
      de donkere waarden in de resources. **Gebruik in XAML altijd `{DynamicResource Kleur}`** (niet StaticResource),
      en voor tekst op een gekleurd vlak `OnPrimary` (niet White). De inlogpagina houdt de merkkleuren (`Brand`).
      Plaatjes houden hun eigen kleuren. Een andere keuze bouwt de schermen opnieuw op (zoals bij een andere taal).

### IN UITVOERING (02-11-2026): veiligheid – herstellen en rem (A, B, C, D). E en F wachten.

**Stand:** alleen ontworpen, nog **niets gebouwd of gewijzigd** in code. Gebruiker gaf toestemming voor A, B, C en D.
Wens gebruiker: verwijderde spellen krijgen **10 dagen** timer; ze blijven zichtbaar voor spelers met boven de regels
"Over X dagen, Y uur en Z minuten wordt dit spel verwijderd". Daarna weg; **betaalde spellen blijven voor altijd bij wie
ze los kocht** (niet via de Pass, niet via teamlidmaatschap), en zo'n spel kan dan niet meer gewijzigd worden.

Bevindingen: spellen kunnen nu al niet echt gewist worden (rules: geen delete op games; verwijderen = status Deleted).
Wel blijvende schade mogelijk: inhoud overschrijven, plaatjes/categorieën/hoofdcategorieën echt wissen (rules staan
delete toe), geen "wie deed het", en alles kan via REST buiten de app om (dus beveiliging in rules/functions).

**Ontwerp (zo bouwen):**
- `GameStatus` uitbreiden **aan het eind** (SQLite slaat int op): `Deleting` (10 dagen) en `Retired` (definitief, alleen
  voor kopers, bevroren). `PubGame` krijgt `DeleteAt` (DateTime?) en `RestoreStatus` (string?). `CloudSyncService.GameFields`
  /`ToGame` lezen/schrijven `deleteAt`, `restoreStatus` (alleen als niet null).
- Verwijderen (admin `AdminCreateGameViewModel.DeleteAsync` en goedkeuren Deletion in `ReviewRequestViewModel` ~r.559):
  `UpdateFieldsAsync(games, id, {status:"Deleting", deleteAt: nu+10d, restoreStatus: oude status})`.
- Rules games: lezen als `Published`, of `Deleting` met `restoreStatus=='Published'`, of team, of (`get` alleen)
  `Retired` en gameId in `entitlements/{uid}.games`. Create/update door admin alleen als huidige status niet
  Deleting/Retired is; nieuwe status in Draft/PendingPublishApproval/Published/PendingDeletionApproval, of `Deleting` met
  deleteAt ≈ request.time+10d (±1u), restoreStatus == oude status en affectedKeys hasOnly status/deleteAt/restoreStatus.
  Niemand zet zelf `Deleted`/`Retired` (alleen functions).
- Speler-app: `PullPublishedGamesAsync` ook `status==Deleting && restoreStatus==Published` (nieuwe
  `FirestoreClient.WhereEqualAllAsync` met compositeFilter AND) en per los gekocht spel-id dat er niet bij zit een GET
  (Retired → bewaren; 403 negeren). `LocalDatabaseService.GetGamesAsync` (nu alleen Published) ook Deleting/Retired; filter
  in `GameLibraryViewModel`: Deleting alleen met RestoreStatus Published, Retired alleen als `Current.Games` het id heeft.
  `StoreService.Owns`/`Entitlements.Owns`: Retired alleen bij los gekocht. Tijdens Deleting is het Play-product inactief
  (`syncGameProduct` verkoopt alleen Published): Unlock-knop verbergen, melding "kan niet meer gekocht worden".
- Regelspagina (`GameRulesPage`/`GameRulesViewModel`): banner bovenaan (ScoreWarningTint) met aftellen, elke minuut
  ververst (Dispatcher-timer); Retired: "uit de bibliotheek, blijft van jou omdat je het kocht". Teksthulp MAUI-vrij in
  `Models/DeletionCountdown.cs` (enkelvoud/meervoud: "1 day"/"{0} days" enz.) + xUnit-test (link in tests-csproj).
- Admin: formulier bevroren voor Deleting/Retired (melding, opslaan blokkeren); `CategoryService.DeleteAsync` slaat
  bevroren spellen over (anders weigeren de rules); `PubGame` statuslabel voor beide.
- **A Geschiedenis:** functions `onDocumentWrittenWithAuthContext` (firebase-functions 6.6 heeft dit) op games, categories,
  categoryGroups, images, team. Alleen `event.authType === "app_user"` (writes van functions zelf overslaan); wie =
  `admin.auth().getUser(event.authId).email` (niet te vervalsen, geen app-wijziging nodig). Overslaan: image-creates,
  team-updates met alleen displayName/language/agreedRules/suspended*, ids met "_", geen echte wijziging. Schrijft
  `history/{id}` {collection, docId, name, kind: created|changed|deleted|deleting, byEmail, at, removedCategoryIds,
  restored} en `historyData/{id}` {before}. Rules: history alleen lezen door eigenaars; historyData niet.
  Callables (alleen eigenaar): `restoreFromHistory({historyId})` zet `before` terug (bij spel ook gewiste plaatjes die
  het gebruikt; bij categorie terug op spellen via removedCategoryIds; bij hoofdcategorie ook zijn categorieën) en
  `restoreGame({gameId})` (Deleting → restoreStatus). `finishDeletions` elk uur: Deleting met deleteAt verstreken →
  `Retired` als iemand het in entitlements heeft, anders `Deleted` (query alleen op status, filter in code: geen index).
  `cleanHistory` dagelijks: ouder dan 10 dagen weg. `translateGame`/`notifyTranslationChecks`: Deleting/Retired als
  Deleted behandelen. Pure helpers in `shared.js` (isWorthKeeping, historyKind, removedCategories, pictureRefs,
  brakeReason) met tests in `test/shared.test.js`.
  App: nieuwe pagina **Prullenbak** (`TrashPage`, Admin-tab, alleen eigenaars, zichtbaarheid in `AppShell` zoals
  TeamContent): "Wordt verwijderd" (aftellen, wie, Herstellen) en "Wijzigingen laatste 10 dagen" (filter Alles /
  Verwijderingen; Zet terug met bevestiging). Aanroepen via `FunctionsClient.CallAsync`.
- **B Rem:** in de history-trigger, per niet-eigenaar binnen 1 uur (query op byEmail, tijd filteren in code): >3 spellen
  op Deleting, >3 teamleden weg, >2 hoofdcategorieën, >10 categorieën, >25 plaatjes, >40 spelwijzigingen → `team/{email}`
  `suspended:true` (+ suspendedAt/Reason) en elke eigenaar een Inbox-bericht (kind "Report", fromName "Pub Games", Engels).
  Rules: `isTeamMember()` eist `member().get('suspended', false) != true`; admins mogen `suspended` van moderators niet
  wijzigen. App: `TeamMember.IsSuspended`; geblokkeerde = speler; Team-pagina badge "Gepauzeerd" + eigenaarsknop
  "Pauze opheffen".
- **C Back-ups:** gebruiker zelf in console (Engelse knopnamen, Nederlandse uitleg): Firestore point-in-time recovery
  (7 dagen) aan + dagelijkse back-up. Evt. `gcloud firestore databases update --database="(default)" --enable-pitr` en
  `gcloud firestore backups schedules create --database="(default)" --recurrence=daily --retention=14d`.
- **D:** `geraatsdeveloping@gmail.com` als tweede eigenaar in `AuthConfig.AdminEmails`, `firestore.rules`
  builtInAdminEmails en `functions/index.js` OWNER_EMAILS. Gebruiker zet tweestapsverificatie aan op beide accounts.
- Daarna: ⓘ-teksten (Prullenbak, verwijderen, Team-pauze) en alle nieuwe teksten in 9 talen; bouwen in kladmap; tests;
  gebruiker deployt `firebase deploy --only functions,firestore:rules --project pubgames-92835`.

### Volgende stappen

1. Wachten tot Google de closed-test-release goedkeurt (Publishing overview).
2. Testers uitnodigen (zie hieronder). Minimaal **12**, liefst **15+**.
3. Zodra 12+ testers opted-in zijn: **14 dagen** laten lopen (persoonlijk ontwikkelaarsaccount).
   Tussentijds nieuwe versies uploaden mag; de 14 dagen beginnen dan niet opnieuw.
4. Daarna op het Dashboard **Apply for production** en de vragen over de test beantwoorden.

### Testers – wie en hoe

- **Wie:** iedereen met een **Android-telefoon met Play Store** en een **Google-account** (geen iPhone),
  **18 jaar of ouder** (de app is 18+ vanwege alcohol), in een land waar de app beschikbaar is
  (Saudi-Arabië is eruit gehaald, Korea niet beschikbaar door GRAC). Vrienden, familie en collega's mogen;
  het moeten echte mensen zijn die de app af en toe gebruiken.
- **Toevoegen:** de opt-in-link werkt alleen voor mensen op de testerslijst. Twee manieren:
  1. **E-maillijst:** Closed testing › Testers › e-maillijst met hun Gmail-adressen; daarna de opt-in-link sturen.
  2. **Google Groep:** groep maken op groups.google.com, mensen laten lid worden, de groep als tester toevoegen.
- **Testers doen:** link openen › **Become a tester** › installeren via de Play Store (niet via VS) ›
  14 dagen laten staan en af en toe gebruiken. Feedback via de feedbackpagina helpt bij de productieaanvraag.
- **Let op, aankopen zijn echt:** in de closed test werkt Google Play Billing echt. Alleen accounts in
  **License testers** (Settings › License testing, account-niveau, nu o.a. `jjh.geraats@gmail.com`,
  RESPOND_NORMALLY) betalen niets. Voeg testers daar ook toe als ze gratis moeten kunnen kopen, of zeg ze dat
  ze niets hoeven te kopen.

### Play- en Firebase-instellingen (al gedaan, om te onthouden)

- **Upload-keystore** in `%USERPROFILE%\PubGamesKeys` (wachtwoord in `pubgames-upload.pass`), buiten de
  repo. Release-builds worden er automatisch mee ondertekend (zie csproj). **Back-uppen: kwijt = geen
  updates meer.** Play App Signing staat aan (door Google beheerde sleutel). SHA-1 van beide sleutels staat
  in Firebase.
- Store listing EN + NL (overige talen klaar in `PlayStore/listings.md`), Game › Casual, content rating
  14+ t/m 18+ per land, doelgroep 18+. Antwoorden in `PlayStore/README.md`.
- Abonnement `pubgames_pass` met basisplannen `week` / `month` / `year` (€0,99 / €2,99 / €24,99).
- Serviceaccount `737835893040-compute@developer.gserviceaccount.com` heeft rechten in de Play Console
  (financiën/bestellingen, producten).
- API-sleutel: alleen API restrictions (Identity Toolkit, Token Service, Cloud Firestore). Application
  restrictions bewust op **None** (de app roept de REST-API's aan zonder Android-headers).
- Waarschuwingen in Play over deobfuscation file en native debug symbols: negeren (MAUI/.NET).

### Open punten / bekende beperkingen

- **Verzoek van een mod goedkeuren** neemt de voorgestelde vertalingen helemaal over
  (`ReviewRequest.ApplyProposalTo`); wat anderen intussen in andere talen goedkeurden, kan dan verloren gaan.
  Het spelformulier zelf voegt nieuwere wijzigingen wel samen (`MergeNewerAsync`).
- **Afgewezen verzoek opnieuw indienen:** bij afwijzen worden de voorgestelde afbeeldingen opgeruimd; de mod
  moet ze bij opnieuw indienen opnieuw maken.
- **Zinnen knippen:** na `. ! ? …` gevolgd door een spatie (dus ook na "bijv."). Een toevoeging komt in
  dezelfde alinea als de zin waarna hij staat.
- **`firebase-functions` bijwerken:** breaking changes; apart doen en testen.
- De Firebase CLI blijft hangen bij `firebase apps:list`; `firebase functions:log` geeft weinig. Console gebruiken.
- **Uitslagen niet gecontroleerd:** Firestore-regels kunnen niet nagaan of deelnemers in `gameResults`
  echt meespeelden. Oplossing: een serverfunctie die uitslagen wegschrijft.
- **Kleine scorebordknoppen** bij veel spelers (bewust, zodat iedereen in beeld past).
- **Push-meldingen** (Firebase Cloud Messaging) zijn er nog niet.
- **Meerdere teams:** TODO's "replace with the team the user picked"; nu is er één team.

---

## Aantekeningen voor Claude (lees dit eerst)

**Werkafspraken**
- Antwoord in het Nederlands, kort, met de uitkomst eerst.
- **Nooit** builds installeren of deployen naar de telefoon. De gebruiker test via VS 2026; logcat lezen
  mag (adb: `C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe`), verder niets op de telefoon.
- **Commit en push alleen op verzoek.**
- **Firebase-deploys draait de gebruiker zelf** (`! firebase deploy --only functions --project pubgames-92835`,
  of `--only firestore:rules`).
- **Een voorstel of onderzoek gevraagd "met mijn toestemming"?** Alleen voorleggen en vragen, niet bouwen.
- **Niet bouwen in de projectmap terwijl VS open is** (bestandslocks of kapotte `obj\...\lp`). Eerst vragen.
  Wel kan: de broncode zonder bin/obj naar een kladmap kopiëren en daar bouwen (ook een Release-`.aab`, het
  ondertekenen gebruikt vaste paden). Bij vreemde resource-fouten: VS dicht, MSBuild/VBCSCompiler stoppen,
  bin/obj weggooien. Na een icoonwijziging `obj/.../resizetizer` weggooien.
- Nieuwe `.aab`: `ApplicationVersion` en `ApplicationDisplayVersion` in `PubGames.csproj` ophogen, dan
  `dotnet publish PubGames/PubGames/PubGames.csproj -f net10.0-android36.0 -c Release`; upload het
  `-Signed.aab` uit `bin/Release/net10.0-android36.0/publish/`. Gebouwd in een kladmap: zet de `.aab` terug in die
  publish-map, en bewaar een kopie in `Builds/` als `pubgames-v<code>-<versie>.aab`.
- De gebruiker heeft beperkte ruimte en Claude ziet de accountstatus niet: werk in stukken en houd deze
  README bij, zodat een nieuwe sessie zonder uitleg verder kan.

**Tests**
- App-logica: `dotnet test tests/PubGames.Tests` (xUnit, net10.0). Linkt bestanden uit de app (zie csproj);
  `Stubs.cs` vervangt L, AppLanguage, FirestoreClient en FirestoreImageStore. Niet in de solution, dus VS
  bouwt het niet mee. Nieuwe logica die getest moet worden: houd hem vrij van MAUI (zoals `DrawRules`,
  `Sentences`).
- Cloud Functions: `npm test` in `functions` (node:test). Kleine regels staan in `functions/shared.js`.
- `tests/shared-cases.json`: voorbeelden die app en cloud precies gelijk moeten uitkomen (vingerafdruk van
  een spel, middelvinger-regel, zinnen knippen en samenvoegen). Werk dit bij als je die regels verandert.
- Afvinklijst voor de gebruiker: https://claude.ai/artifact/2bdNcCQqsLQbSR2ihAuRBD (antwoorden in de
  collectie `results`, te lezen met ArtifactData).

**Stijl (zo gewenst door de gebruiker)**
- Alle uitleg staat alleen onder de ⓘ-knop (`Models/PageHelp.cs`). Pas de ⓘ-tekst aan bij elke functiewijziging.
- Tijdelijke meldingen verdwijnen vanzelf via `ViewModels/MessageTimer.cs`.
- Elke klasse, methode en eigenschap heeft een `/// <summary>`; interface-implementaties `/// <inheritdoc />`.
- Lusvariabelen krijgen betekenisvolle namen (geen `i`/`x`). Korte lambda's (`p => p.Name`) mogen.
- Aanraakdoelen minimaal 48dp. Symboolknoppen krijgen `SemanticProperties.Description`.
- Pop-ups met meer dan 2 keuzes gebruiken `Views/ChoicePopup.AskAsync`.
- Lijsten die in het Engels alfabetisch zijn, blijven alfabetisch na vertaling (`SortedChips`,
  `CategoryGroup.ShownOrder`).
- Het toetsenbord sluit bij elke paginawissel (`Views/SoftKeyboard.cs`).

**App-teksten (9 talen)**
- Elke tekst in C# via `L.T("English text")` of `L.F("Text {0}", value)` (`Services/L.cs`). In XAML:
  `Text="{loc:T Page_Key}"` met de Engelse tekst in `Resources/Strings/en.json`. Vertalingen in
  `Resources/Strings/{taal}.json`, sleutel = Engelse tekst (of XAML-sleutel).
- **Nieuwe of gewijzigde tekst = ook alle 8 vertalingen** (nl, fr, de, es, it, pt, zh, ar). Gewijzigde
  Engelse tekst is een nieuwe sleutel. Let op botsingen bij korte sleutels ("Middle" vs "Middle finger").
- Geen vaste tekst in `const` of `case`. Lijsten met teksten zijn eigenschappen (`=>`).
- Categorieën: handmatige vertaling in de stringbestanden met sleutel `Category: <naam>` (`L.Known`), anders
  de cloudvertaling. E-mails aan teamleden (`TeamEmails.cs`) blijven Engels.
- Taal is per account (`AppLanguage.UseAccount`); teamleden hebben hun taal ook in `team/{email}.language`
  (vlag op de teampagina).

**Spelvertalingen (per zin)**
- Het origineel is altijd de taal waarin het spel geschreven is (`sourceLanguage`; de cloud corrigeert die
  als de tekst duidelijk een andere taal is). Spelnamen worden niet automatisch vertaald.
- `translations.{taal}` heeft `segments: [{src, sep, text, status, suggestion?, origin?}]` (één per zin of
  plaatje) en `rulesText` = de segmenten samengevoegd. Statussen: `auto`, `approved`, `edited`, `review`
  (origineel veranderd na controle, met `suggestion`), `added` (nog naar het origineel te brengen).
- Cloud (`translateGame`, `TRANSLATOR_VERSION = 3`): vergelijkt per zin (LCS, `planSegments`); alleen nieuwe en
  gewijzigde zinnen worden vertaald. `addToOriginal` zet toevoegingen uit een vertaling (terugvertaald) in
  het origineel, en vervangt of verwijdert plaatjes overal. `notifyTranslationChecks` stuurt teamleden met
  die taal één bericht per spel. Tekst in editor-plaatjes: `imageTexts` per plaatje-id; de app tekent het
  plaatje opnieuw (`fsimg:ID@nl`, `TranslatedPictures`).
- App: pagina Vertalingen (`AdminCreateGameViewModel.Languages.cs`, `SentenceRow`, `WordsLabel`) met
  Toevoegen / Aanpassen / Verwijderen per blok (zin of plaatje), Vertaling akkoord, Verwerpen. Zinnen alleen
  opbouwen als de pagina open is (anders ANR bij lange regels). `Models/Sentences.cs` knipt zoals de cloud.
- Verzoeken tonen per wijziging Eerst/Daarna (ook plaatjes) via `SentenceChange`; het hele spel achter een knop.
- Het spelformulier laadt de nieuwste versie uit de cloud en voegt bij opslaan nieuwere wijzigingen van
  anderen samen (`MergeNewerAsync`). `ImageCleanup` kijkt ook naar vertalingen.
- Admin-lijsten tonen naam en omslag in de app-taal (`PubGame.ShownName`, `ShownCoverImageUrl`).

**Architectuur in het kort**
- MVVM met CommunityToolkit.Mvvm, Shell-navigatie, DI in `MauiProgram.cs`.
- Cloud: Firestore via REST (`Services/FirestoreClient.cs`), Functions via `Services/FunctionsClient.cs`.
- Lokaal: SQLite (`LocalDatabaseService`). Refresh-token in SecureStorage, uitgesloten van back-up.
- **Plaatjes:** spelplaatjes alleen uit de afbeeldingseditor (geen eigen foto's; die alleen als profielfoto,
  of de Google-profielfoto na toestemming). Een hand met alleen de middelvinger wordt geweigerd
  (`DrawRules.IsRudeHand`, en `checkImage` in de cloud).
- **Betalingen:** prijzen en product-id's in `Models/Store.cs` (`game_<id>`, `pubgames_pass`); Android-kant in
  `Platforms/Android/StoreService.Android.cs` (Play Billing 9); de server controleert elke aankoop
  (`verifyPurchase` / `refreshPass`). Admins en moderators spelen betaalde spellen gratis zolang ze in het team zitten.
- **Kortingscode (vriendcode):** 100% korting op losse spellen (niet de Pass), wisselt elke 15 minuten
  ("New code": 30 minuten), alleen zichtbaar voor de eigenaar, max. 10 foute pogingen per dag.
- **Spelers koppelen** via spelerscode; ontkoppelen/blokkeren via `PlayerLinkService`.
- **Rollen:** admin wijzigt direct; moderator dient een review request in (één per spel, nieuwe wijzigingen
  komen erbij). Berichten via de Inbox (ook aan groepen en meerdere mensen tegelijk).
