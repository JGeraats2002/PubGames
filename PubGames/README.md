# Pub Games

.NET MAUI-app voor Android (`net10.0-android36.0`, app-id `com.pubgems.pubgames`) voor
drankspellen in de kroeg: spelers kiezen, spellen uit een bibliotheek, scorebord, duels,
statistieken, feedback, en betaalde spellen of een Pub Games Pass.

- Repo: GitHub `JGeraats2002/PubGames`, branch `master`
- Firebase-project: `pubgames-92835` (Auth, Firestore `eur3`, Cloud Functions in `europe-west1`)
- Eigenaar/admin: `jjh.geraats@gmail.com`; ontwikkelaarsaccount Play: `geraatsdeveloping@gmail.com`
  (Account owner, ontwikkelaarsnaam PubGems, ID `5864539126255989179`). Payments profile en
  account group (15% servicekosten) zijn ingesteld; contactadres van de account group is
  `jjh.geraats@gmail.com`.

## Mappen

| Map / bestand | Inhoud |
|---|---|
| `PubGames/PubGames/` | De app (Models, Services, ViewModels, Views, Platforms/Android) |
| `PubGames/PubGames/Models/PageHelp.cs` | Alle uitleg uit de ⓘ-knoppen |
| `functions/index.js` | Cloud Functions (aankopen controleren, kortingscode, refunds, account verwijderen) |
| `firestore.rules` | Beveiligingsregels van Firestore |
| `public/` | Website (Firebase Hosting): privacybeleid, account verwijderen |
| `PlayStore/` | Play Console-teksten, antwoorden, icoon en feature graphic |
| `firebase.json`, `.firebaserc` | Firebase-deploy-instellingen |

---

## Waar we gebleven zijn (bijgewerkt 28-10-2026)

Laatste commit: "translation feedback fixed" (28-10-2026, gepusht). Versie 3 (1.2) staat bij Closed
testing. Functions en firestore.rules zijn gedeployed.

**Gebleven bij: `.aab` versie 4 maken.** Eerst test de gebruiker alles uit punt 10 hieronder in VS 2026.
Pas als hij zegt dat alles werkt: `ApplicationVersion` naar 4 (`ApplicationDisplayVersion` 1.3) in
`PubGames.csproj`, een Release-`.aab` bouwen (VS gesloten, zie werkafspraken) en die bij Closed testing
uploaden.

### Gedaan (27 en 28-10-2026)

- Identiteitsverificatie Google Play goedgekeurd.
- Upload-keystore gemaakt in `%USERPROFILE%\PubGamesKeys` (buiten de repo, wachtwoord in
  `pubgames-upload.pass`). Release-builds worden er automatisch mee ondertekend (zie csproj).
  De gebruiker moet keystore + wachtwoord back-uppen; kwijt = geen updates meer.
- Website live via Firebase Hosting: https://pubgames-92835.web.app (`/privacy`, `/delete-account`).
- App aangemaakt in de Play Console (`com.pubgems.pubgames`), Play App Signing aan
  (door Google gegenereerde sleutel). Versie 1 (1.0) staat bij Internal testing.
- SHA-1 van de app-signeersleutel én de uploadsleutel staan in Firebase.
- Store listing (EN + NL), Store settings (Game → Casual) en alle App content-formulieren
  ingevuld. Content rating 14+ t/m 18+ per land; doelgroep 18+. Alle antwoorden en teksten staan
  in `PlayStore/README.md`.
- Closed testing-release 1 (1.0) + alles ingediend ter beoordeling (28-10-2026).
  Saudi-Arabië uit de landen gehaald (alcohol). Korea: niet beschikbaar door GRAC, geen actie.
- Warnings over deobfuscation file en native debug symbols: negeren (MAUI/.NET).

### Nog te doen – volgorde

**Fase 1 – Play-koppeling (samen, ±30 min, met de gebruiker door de console lopen)**
1. ~~**D.** Google Play Android Developer API aanzetten in Google Cloud. Het serviceaccount
   `737835893040-compute@developer.gserviceaccount.com` uitnodigen in de Play Console
   (Users and permissions) met rechten voor financiën/bestellingen en het beheren van producten.~~
   **Gedaan 28-10-2026.** De rechten kunnen tot 24 uur nodig hebben voordat ze werken.
2. ~~**C.** Abonnement `pubgames_pass` aanmaken met basisplannen `week`, `month`, `year`
   (€0,99 / €2,99 / €24,99). Mogelijk eerst een payments profile koppelen.~~
   **Gedaan 28-10-2026** (incl. payments profile en account group). Afbeelding van het
   abonnement: `PlayStore/pass-image-512.png` (32-bit kopie van het icoon).
3. ~~**E.**~~ **Gedaan 28-10-2026.** License testers: Settings → License testing (account-niveau), `jjh.geraats@gmail.com`
   en de andere testers, RESPOND_NORMALLY.
4. ~~**API-sleutel beperken**~~ **Gedaan 28-10-2026:** alleen API restrictions (Identity Toolkit,
   Token Service, Cloud Firestore). Application restrictions bewust op **None**: de app roept de
   REST-API's direct aan zonder `X-Android-Package`/`X-Android-Cert`-headers, dus een
   Android-app-beperking zou inloggen breken.

**Fase 2 – App-aanpassingen, nodig vóórdat de testers worden uitgenodigd**
(De gebruiker wil deze vóór de closed test. Daarna een nieuwe `.aab` bouwen met
`ApplicationVersion` 2 en die bij Closed testing uploaden.)

5. ~~Teampagina – zoeken en sorteren~~ **Gebouwd 28-10-2026**: zoekbalk (naam + e-mail),
   knoppen All / Admins / Moderators, twee alfabetische lijsten.
6. ~~Ingebouwde vertaler voor spellen~~ **Gebouwd 28-10-2026**:
   - Spel heeft `sourceLanguage` en `translations.{taal} = { name, rulesText, manual, sourceHash }`
     (`Models/GameTranslation.cs`). Editor: "Written in" op General + tabblad **Languages** om
     vertalingen te verbeteren (`AdminCreateGameViewModel.Languages.cs`). Verbeterd = `manual`,
     wordt nooit overschreven. Loopt via review requests mee (`proposedTranslations`).
   - Cloud Functions `translateGame`, `translateCategory`, `translateCategoryGroup`
     (Cloud Translation API v3, plaatjes-tags blijven staan). Spelers zien `PubGame.InAppLanguage()`,
     categorieën `ShownName`.
   - Cloud Translation API staat aan en de functions zijn gedeployed (28-10-2026, nog zonder de
     plaatjesteksten). Opnieuw gedeployed op 28-10 's avonds (plaatjesteksten,
     taalherkenning, `checkImage`). Elk bestaand spel moet één keer worden opgeslagen om opnieuw vertaald te worden.
   - Tekst in plaatjes (Text-items van de editor) wordt vertaald: `translations.{taal}.imageTexts`
     per plaatje-id. De app verwijst dan naar `fsimg:ID@nl`; `FirestoreImageStore.OpenTranslatedAsync`
     tekent het plaatje opnieuw met de vertaalde teksten (`Services/TranslatedPictures.cs`).
   - De cloud herkent de taal van de regels (Cloud Translation detect, ≥ 40 tekens, zekerheid ≥ 0,7)
     en corrigeert `sourceLanguage` als "Written in" fout staat.
7. ~~Taalkeuze in de app~~ **Gebouwd 28-10-2026**: 9 talen (en, nl, fr, de, es, it, pt-BR, zh-Hans,
   ar met rechts-naar-links). Vlaggen in `Resources/Images/flag_*.svg` (Engels half VS/half VK,
   Chinees = China, Arabisch = VAE, Portugees = Brazilië). Taalscherm bij eerste login en via
   Account > Language (`Views/LanguagePage`). Teksten: zie "Vertalingen" bij de aantekeningen.
8. ~~Store listing in die talen~~ **Klaar om te plakken**: `PlayStore/listings.md`.
   Editor (28-10-2026, UX): tabbladen 48dp, duidelijke sectiekoppen, een vaste balk onderaan met de
   melding en de opslaan-knop; intrekken en verwijderen staan los daarvan onderaan het formulier.
   Ook 28-10-2026: admins en moderators spelen alle betaalde spellen gratis zolang ze in het team
   zitten (`StoreService.Owns`, niet de Pass; eerder gekochte spellen blijven). Crash "No view found
   for id ... is_pooling_container_tag" aangepakt: taalscherm eerste keer als modal, root-page wissels
   via `App.SetRootPageAsync` (na de lopende schermwissel). App-icoon = kaart + dobbelsteen (zoals in
   de Play Store); logo en splash blijven de versie met de beker. Na icoonwijziging `obj/.../resizetizer`
   weggooien, anders bouwt hij het oude icoon.
9. ~~Nieuwe `.aab`~~ Versie 3 (1.2) is geüpload bij Closed testing. **Volgende `.aab` (versie 4) pas
   bouwen als de gebruiker alles getest heeft.**
10. Gebouwd 28-10-2026 (avond), nog te testen door de gebruiker:
   - Spelplaatjes (omslag en regels) alleen nog uit de afbeeldingseditor; eigen foto's alleen voor de
     profielfoto. CAMERA-permissie uit het manifest gehaald.
   - Editor weigert een hand met alleen de middelvinger (duim maakt niet uit): `DrawItem.IsRudeHand`;
     de Cloud Function `checkImage` verwijdert zo'n plaatje als het toch binnenkomt.
   - Categorieën handmatig vertaald in `Resources/Strings/*.json` met sleutel `Category: <naam>`
     (`L.Known`); die gaan vóór de cloudvertaling. Nieuwe categorieën vertaalt de cloud.
   - Categoriewolken werden afgekapt ("Dic", "Mou"): `FlexLayout.Shrink="0"` in `CategoryClouds.xaml`.
   - Nieuw bericht: `ChooseRecipientPage` met snelkeuzes Everyone / All admins / All moderators,
     zoeken en All/Admins/Moderators zoals Team, meerdere mensen aanvinken. Elk krijgt een eigen
     bericht (de gedeelde `admins`-inbox wordt niet meer gebruikt voor nieuwe berichten).
   - Ronde 2 (28-10 avond): "Middle" botste (Middelvinger bij Place across) → vingers hebben nu eigen
     sleutels (`Index finger`, `Middle finger`, `Ring finger`). Let op zulke botsingen bij korte sleutels.
   - Languages-pagina opent in de app-taal; plaatjes tonen een live voorbeeld met de vertaalde teksten
     (`TranslatedPictures.Preview`). Knop **Make this the original** (`MakeOriginalAsync`): vertaling wordt
     origineel, plaatjes met tekst opnieuw geüpload met de nieuwe teksten, oud origineel blijft als
     handmatige vertaling.
   - Cloud-vertaler versie 2 (`TRANSLATOR_VERSION`, `GameTranslation.Engine`): spelnamen worden niet meer
     vertaald, plaatjestekst gelijk aan de naam of zonder letters blijft staan, een vertaling in de eigen
     brontaal wordt verwijderd. Oudere automatische vertalingen worden opnieuw gemaakt bij opslaan.
     **Functions opnieuw deployen.**
   - Lijsten die in het Engels alfabetisch zijn, blijven alfabetisch na vertaling: knoppen in de
     afbeeldingseditor (`SortedChips`, `AddChoices`) en categorieën (`CategoryGroup.ShownOrder`, alleen
     voor spelers en het spelformulier; de categoriepagina houdt de volgorde van de admins).
   - Teampagina en ontvangerskeuze tonen een vlag (taal waarin iemand de app gebruikt; zoeken op
     taalnaam werkt). Veld `language` in `team/{email}`, bijgewerkt door `TeamService.UpdateMyLanguageAsync`
     (bij start en bij taalkeuze, `AppLanguage.Chosen`). De eigenaar krijgt een eigen `team`-document met
     alleen zijn taal (wordt in de lijst genegeerd via `WithBuiltInAdmins`). **firestore.rules deployen.**
   - Vertaalcontrole (zoals Weblate/Crowdin/Lokalise): `GameTranslation.CheckedHash`/`CheckedBy`, status
     via `CheckFor` (NotChecked / Checked / CheckAgain). Knop **Translation is fine** op de pagina
     Languages; met de hand verbeteren telt als controleren. De Cloud Function
     `notifyTranslationChecks` (in `translateGame`) stuurt per spel en taal één bericht
     (`kind: TranslationCheck`, met `gameId`, `gameName`, `language`) aan teamleden met die taal en
     verwijdert het als de vertaling gecontroleerd is. Inbox-knop **Check translation** opent het spel
     op die taal (query `language`). Let op: `MessageService.SaveAsync` schrijft de extra velden alleen
     voor TranslationCheck, anders weigeren de regels het markeren als gelezen.
   - Functions en firestore.rules gedeployed op 28-10-2026 (avond), inclusief vertaalcontrole en vlaggen.

**Fase 3 – Closed test en productie**
11. Na goedkeuring door Google: opt-in-link (Closed testing → Testers) naar **15+ testers** sturen
   (minimaal 12 nodig, buffer voor uitvallers). Testers: link openen → Become a tester →
   installeren via Play, 14 dagen laten staan en af en toe gebruiken.
12. Zodra er 12+ testers opted-in zijn: **14 dagen** laten lopen (persoonlijk ontwikkelaarsaccount).
13. Daarna op het Dashboard **Apply for production** en de vragen over de test beantwoorden.

**Later / niet dringend**
- **`firebase-functions` bijwerken:** er komen breaking changes; apart doen en testen.
- De Firebase CLI blijft op deze pc hangen bij `firebase apps:list` (wacht vermoedelijk op een
  vraag); console gebruiken voor zulke dingen.

### Bekende beperkingen / mogelijke vervolgstappen

- **Uitslagen niet gecontroleerd:** Firestore-regels kunnen niet nagaan of de deelnemers in
  `gameResults` echt hebben meegespeeld. Een aangepaste app kan zo statistieken van anderen
  beïnvloeden. De oplossing is een serverfunctie die uitslagen wegschrijft.
- **Kleine scorebordknoppen:** de +/−-knoppen worden bij veel spelers bewust kleiner dan 48dp,
  zodat iedereen in beeld past.
- **Push-meldingen** voor nieuwe review-aanvragen zijn er nog niet (Firebase Cloud Messaging).
- **Meerdere teams:** de TODO's "replace with the team the user picked" gaan over meerdere
  teams. Nu is er één team.

---

## Aantekeningen voor Claude (lees dit eerst)

**Werkafspraken**
- Antwoord in het Nederlands, kort, met de uitkomst eerst.
- **Nooit** builds installeren of deployen naar de telefoon. De gebruiker test via VS 2026;
  logcat lezen mag.
- **Commit en push alleen op verzoek.** Commitberichten in de stijl van de gebruiker.
- **Firebase-deploys draait de gebruiker zelf** met `! firebase deploy --only functions --project pubgames-92835`
  (of `--only firestore:rules`).
- **Niet bouwen terwijl VS open is:** dat geeft bestandslocks (XALNS7024) of een kapotte
  `obj\...\lp`-map (FileNotFound `values-*.xml`). Eerst vragen of VS dicht is. Bouw met
  `dotnet build PubGames/PubGames/PubGames.csproj -f net10.0-android36.0`. Bij vreemde
  resource-fouten: VS sluiten, achtergebleven MSBuild/VBCSCompiler stoppen, bin/obj weggooien.

**Stijl (zo gewenst door de gebruiker)**
- Alle uitleg staat alleen onder de ⓘ-knop (`Models/PageHelp.cs`), niet als tekst op het
  scherm. Pas de ⓘ-tekst aan bij elke functiewijziging.
- Tijdelijke meldingen verdwijnen vanzelf via `ViewModels/MessageTimer.cs`.
- Elke klasse, methode en eigenschap heeft een `/// <summary>`. Interface-implementaties krijgen
  `/// <inheritdoc />`.
- Lusvariabelen krijgen betekenisvolle namen (geen `i`/`x`). Korte lambda's (`p => p.Name`) mogen.
- Aanraakdoelen zijn minimaal 48dp. Symboolknoppen krijgen `SemanticProperties.Description`.
- Pop-ups met meer dan 2 keuzes gebruiken `Views/ChoicePopup.AskAsync`.
- UI-teksten worden in het Engels geschreven en vertaald (zie Vertalingen).

**Vertalingen (sinds 28-10-2026)**
- Elke tekst in C# gaat via `L.T("English text")` of `L.F("Text {0}", value)` (`Services/L.cs`,
  globale alias in `GlobalUsings.cs`). In XAML: `Text="{loc:T Page_Key}"` met de Engelse tekst in
  `Resources/Strings/en.json`. Vertalingen staan in `Resources/Strings/{taal}.json`, sleutel =
  Engelse tekst (of XAML-sleutel). Ontbreekt een vertaling, dan toont de app Engels.
- **Nieuwe of gewijzigde tekst = ook alle 8 vertalingen bijwerken** (nl, fr, de, es, it, pt, zh, ar).
  Gewijzigde Engelse tekst is een nieuwe sleutel.
- Geen vaste tekst in `const` of `case`: die kunnen niet vertaald worden. Lijsten met teksten zijn
  eigenschappen (`=>`), zodat ze na een taalwissel opnieuw vertaald worden.
- E-mails aan teamleden (`TeamEmails.cs`) en standaardcategorieën blijven bewust Engels.
- Berichten in de Inbox worden opgeslagen in de taal van de afzender.

**Architectuur in het kort**
- MVVM met CommunityToolkit.Mvvm, Shell-navigatie, DI in `MauiProgram.cs`.
- Cloud: Firestore via REST (`Services/FirestoreClient.cs`) en Functions via
  `Services/FunctionsClient.cs` (callable-protocol, Bearer-token).
- Lokaal: SQLite (`LocalDatabaseService`). De refresh-token staat in SecureStorage en is
  uitgesloten van back-up (`Platforms/Android/Resources/xml/*backup*`).
- **Betalingen:**
  - `Models/Store.cs` bevat de prijzen en product-id's (`game_<id>`, `pubgames_pass`).
  - De Android-kant zit in `Platforms/Android/StoreService.Android.cs` (Play Billing 9).
  - De server controleert elke aankoop (`verifyPurchase` / `refreshPass`). Een spel staat nooit
    alleen op basis van de app op "gekocht".
  - Betaalde spellen tonen eerst de regels, daarna "🔒 Unlock to play".
- **Kortingscode (vriendcode):**
  - Geeft 100% korting, alleen op losse spellen (niet op de Pass).
  - De code verandert elke 15 minuten. "New code" maakt een code die 30 minuten geldig is.
  - De eigenaar ziet de code op de profielpagina onder zijn spelerscode.
  - Maximaal 10 foute pogingen per dag.
- **Spelers koppelen:** via spelerscode. Ontkoppelen of blokkeren loopt via
  `PlayerLinkService` (collecties `unlinks`, `blocks`).
- **Rollen:** Admin wijzigt direct. Moderator dient review-aanvragen in (nieuw spel, wijziging,
  verwijdering). Berichten lopen via de Inbox.
