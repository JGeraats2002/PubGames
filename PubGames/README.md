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

## Waar we gebleven zijn (bijgewerkt 29-10-2026)

Laatste commit: "final version before publishing - tested" (29-10-2026, niet gepusht). Versie 4 (1.3) is
gebouwd als `.aab` (zie hieronder waar). Versie 3 (1.2) staat bij Closed testing. Functions en
firestore.rules zijn gedeployed.

**Volgende stap:** de gebruiker uploadt de `.aab` van versie 4 bij Closed testing (Play Console › Testing ›
Closed testing › Create new release). Daarna: testers uitnodigen (punt 11 hieronder). X3 is opgelost maar nog
niet opnieuw getest door de gebruiker.

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
     Functions gedeployed op 29-10-2026 (ronde 3).
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
11. Gebouwd 29-10-2026, nog te testen (afvinklijst, onderdeel "Ronde 2"):
   - Taal per account op één toestel (`AppLanguage.UseAccount`, Preferences `appLanguage:{accountId}`).
     Teamleden krijgen hun taal uit `team/{email}.language` op een nieuw toestel; anders het taalscherm.
     `UpdateMyLanguageAsync` schrijft alleen als dit account zijn taal gekozen heeft (anders werd de taal
     van het vorige account overgenomen).
   - Review requests: blok Vertalingen (vlag, wat er met die taal gebeurt, wie de app in die taal gebruikt),
     "Geschreven in: X → Y", en Vertaling vergelijken (origineel en vertaling per alinea, elk met vlag)
     via `Models/TranslationChange.cs`. Vlaggen in de lijst met verzoeken.
   - Talen-pagina legt uit dat een spel in je eigen taal het origineel is (testpunt D1).
   - Testpunt E3 leidde tot een nieuw ontwerp voor vertalingen: zie "Vertalingen per zin" hieronder.

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

### Vertalingen per zin (ontwerp 29-10-2026, goedgekeurd door de gebruiker; fase 1 t/m 4 gebouwd, nog te testen)

**Huidige stand:** ronde 2 (taal per account, vlaggen en vergelijken in verzoeken, uitleg D1) is gebouwd
maar nog niet getest of gecommit. Daarnaast staat er al ongecommitte basiscode voor dit ontwerp, nog niet
gedeployed: `functions/shared.js` (`segmentsOf`, `matchSegments` (LCS), `carryOver`), tests in
`functions/test/shared.test.js`, `translateRules`/`updateRules` in `functions/index.js` (nu per **alinea**;
moet naar per **zin**), en op de Languages-pagina de melding `HasExtraParagraphs` + knop naar Regels
(vervalt in het nieuwe ontwerp). De gebruiker gaf toestemming om deze code als basis te gebruiken.
**Niet deployen voordat fase 1 af en getest is.**

**Afspraken met de gebruiker (letterlijk zijn keuzes):**
1. Het origineel is altijd de taal waarin het spel geschreven is. "Dit het origineel maken" verdwijnt. De
   tab Languages heet voortaan **Vertalingen** (Translations).
2. Bij een nieuw spel worden alle talen automatisch vertaald vanuit het origineel.
3. Eenheid = **zin** (segment). Elk segment heeft per taal een status: automatisch / goedgekeurd / door een
   persoon aangepast / gewijzigd en wacht op controle.
4. Vertalingen-pagina per taal: tekst niet vrij bewerkbaar. Knoppen:
   - **Vertaling akkoord**: niets verandert, alles van die taal is gecontroleerd.
   - **Toevoegen**: plek in de tekst kiezen, pop-up om vrij te typen; de tekst eromheen blijft onaangetast.
     Een toevoeging vanuit een vertaling gaat eerst (automatisch vertaald) naar het origineel en vandaar
     naar alle talen; het stuk in het origineel blijft gemarkeerd tot iemand die die taal spreekt het goedkeurt.
   - **Aanpassen**: woorden aantikken; alles tussen eerste en laatste geselecteerde woord wordt geselecteerd,
     nog eens tikken haalt de selectie weg. Pop-up toont de geselecteerde zin met een invoerbalk eronder.
     Op de achtergrond hoort de wijziging bij de hele zin. Een aanpassing in een vertaling geldt alleen voor
     die taal (en telt als gecontroleerd); de andere talen worden niet opnieuw vertaald (die komen uit het
     origineel, opnieuw vertalen verandert niets).
   - **Verwijderen**: een stuk weghalen; in het origineel verdwijnt het in alle talen.
5. Origineel aanpassen gebeurt op de pagina **Regels**, zonder pop-ups: de cloud vergelijkt oud en nieuw per
   zin (LCS) en vertaalt alleen nieuwe en gewijzigde zinnen opnieuw, in alle talen waar die zin nog niet
   door een persoon is goedgekeurd of aangepast (daar: markeren als "opnieuw bekijken"). Voorwaarde van de
   gebruiker: er moet genoeg overzicht zijn van wat nog gecontroleerd moet worden; anders toch pop-ups, met
   de optie om afbeeldingen toe te voegen via de afbeeldingseditor.
6. Plaatjes blijven zoals nu (teksten onder het plaatje aanpassen). Een plaatje dat uit het origineel
   verwijderd wordt, verdwijnt na goedkeuring in alle talen.
7. Wijzigingen blijven gemarkeerd voor mods en admins tot iemand ze goedkeurt: mods via een request, admins
   direct.
8. Requests: één request per moderator per spel met **alle** wijzigingen (toevoegingen, aanpassingen,
   verwijderingen) onder elkaar, telkens "origineel eerst — daarna de nieuwe versie". Nieuwe wijzigingen
   van dezelfde mod komen bij hetzelfde request. Knop om het spel te openen om alles te zien.
9. Opruimen: bestaande vertalingen mogen opnieuw gemaakt worden (het origineel blijft overal staan). Mexen
   mag helemaal leeg (testspel). In de vertalingen van Toepen staat een niet-bewerkbare afbeelding
   (geüploade foto, geen editor-plaatje): die moet overal weg.

**Stand 29-10-2026: fase 1 GEBOUWD, nog niet gedeployed of getest door de gebruiker.**
- Cloud (`functions/shared.js`): `segmentize` (zinnen, regels, alinea's, plaatjes; elk segment met `sep`),
  `joinSegments`, `matchSegments` (LCS), `planSegments` (zelfde zin: vertaling+status blijven; gewijzigde
  zin die een persoon had gecontroleerd: tekst blijft, status `review`, `suggestion` = nieuwe automatische
  vertaling; anders opnieuw vertalen), `allChecked`, `storedSegments`. Statussen: auto / approved / edited /
  review. Tests: `npm test` (13 groen).
- Cloud (`functions/index.js`, `translateGame`): `TRANSLATOR_VERSION = 3`; vertaling per taal heeft nu
  `segments: [{src, sep, text, status, suggestion?}]`; `rulesText` = segmenten samengevoegd. Bij een oudere
  vertaling (engine < 3) wordt alles één keer opnieuw vertaald (mag van de gebruiker). Foto's (plaatjes
  zonder tekening) gaan uit de vertalingen (`drawnPictures`). "Vertaling akkoord" in de app (checkedHash ==
  hash) zet alle segmenten op approved. Plaatjesteksten die al vertaald waren blijven staan.
- App: `TranslationSegment` in `Models/GameTranslation.cs` (+ `Segments`, ToMap/FromMap/JSON), FirestoreClient
  kan lijsten van maps schrijven. Tussenoplossing tot fase 2: een correctie op de Languages-pagina houdt de
  segmenten en `Engine` van de vorige vertaling (anders zou de cloud alles opnieuw vertalen). Tests:
  `dotnet test tests/PubGames.Tests` (23 groen).
- Nog te doen door de gebruiker voor fase 1: `firebase deploy --only functions`, daarna Toepen één keer
  opslaan (alle talen worden per zin opnieuw vertaald, de niet-bewerkbare foto verdwijnt uit de
  vertalingen) en Mexen leegmaken in de app (regels wissen en opslaan). Controleren in Firestore of
  `translations.xx.segments` er staat.
- **Fase 2 GEBOUWD (29-10-2026), niet getest:** tab heet Vertalingen; per taal `SentenceRows`
  (`ViewModels/SentenceRow.cs`: zin, woorden als `WordChip`, kleur per status, suggestie met "Neem deze" /
  "Laat zo", plaatjes met teksten). Knoppen Toevoegen / Aanpassen / Verwijderen / Vertaling akkoord
  (`TranslationMode`); woordselectie = eerste t/m laatste aangetikte woord (`_anchors`); pop-ups in
  `AdminCreateGamePage.xaml.cs` (`DisplayPromptAsync`). Toevoeging = segment met status `added` en lege
  `src`; de cloud (`addToOriginal` in index.js) vertaalt die terug naar het origineel, zet hem op de goede
  plek en maakt het segment `edited` met `origin: "added"`; bovenaan de pagina staat "Nieuw in het
  origineel" met Akkoord (wist `origin`). Overzicht per taal (`ToCheckOverview`). "Dit het origineel
  maken" en de alinea-melding zijn weg. `Models/Sentences.cs` = zelfde zinsknip als de cloud (gedeelde
  testgevallen in `tests/shared-cases.json`). Tests: npm 14, dotnet 26 groen.
- **Fase 3 GEBOUWD (29-10-2026), niet getest:** `SentenceChange.Between` (Models/TranslationChange.cs)
  geeft per wijziging vlag + kop + Eerst/Daarna: origineel per zin (LCS via `Sentences.Match`) en per
  vertaling (toevoeging/aanpassing/verwijdering/aantal goedgekeurd). Op de verzoekpagina eerst die lijst,
  het hele spel en de vergelijking achter "Hele spel bekijken" (`ShowFullGame`). Eén request per mod per
  spel bestond al.
- **Fase 4 GEBOUWD:** ⓘ-teksten en vertalingen bijgewerkt, afvinklijst onderdeel "Vertalingen per zin"
  (V1–V13; D7, H7, R7 vervallen). Inbox-bericht voor de taal van het origineel opent ook Vertalingen.
- Functions gedeployed op 29-10-2026 (vertalingen per zin live). **Volgende stap:** gebruiker test V1–V13 + Ronde 2. Daarna fouten oplossen,
  committen op verzoek, en dan `.aab` versie 4.
- 29-10 ANR bij openen Toepen (app reageerde >10 s niet): een view per woord (FlexLayout met Border+Label+tap)
  en opbouw bij elke taalwissel. Opgelost met `Views/WordsLabel.cs` (één Label per zin, spans per woord) en
  zinnen alleen opbouwen als de Vertalingen-pagina open is (`ShowSegments`, `IsLanguagesPage`).
- Ronde 3 (29-10, na test V8/V10, gedeployed, nog niet getest — afvinklijst W1–W7):
  - Toevoegen: in de modus Toevoegen ligt een onzichtbare laag over elk zinsblok en elk plaatje (tikbaar), en
    `TapWord` meldt ook `AddPlaceChosen`. Keuze Tekst / Afbeelding (editor); een afbeelding wordt een
    `added`-segment met een plaatjestag, `addToOriginal` zet die zonder vertalen in het origineel.
    **Functions opnieuw deployen.**
  - V10-oorzaak: de editor laadde de lokale kopie. Nu `LoadGameAsync` → `PullGameAsync` eerst, en bij opslaan
    `MergeNewerAsync`: regels/taal/vertalingen die op dit formulier niet veranderd zijn komen uit de nieuwste
    cloudversie (anders maakte een oud geopend formulier goedkeuringen van anderen ongedaan). Nog niet voor
    review requests (`ApplyProposalTo` neemt de voorgestelde vertalingen helemaal over).
  - `PubGame.ShownName` (naam in app-taal) in de admin-spellenlijst, Categorie › spellen, verzoekenlijst
    (`ReviewRequest.ShownSubjectName`) en de kop van een verzoek.
  - `PubGame.ShownCoverImageUrl` (omslag met vertaalde tekst) in de admin-spellenlijst en Categorie › spellen.
  - Toetsenbord sluit bij elke paginawissel: `Views/SoftKeyboard.cs`, aangeroepen in `AppShell.OnNavigating`,
    bij een andere pagina van het spelformulier (`CurrentPage`) en bij het openen van de afbeeldingseditor.
- Ronde 4 (29-10, gebouwd): Google-profielfoto aanbieden (`AuthService.GetGooglePhotoUrlAsync` via
  accounts:lookup, vraag één keer op Account/Profiel, `AccountViewModel.AnswerGooglePhotoAsync`);
  `ImageCleanup` kijkt nu ook naar vertalingen (anders werd een in een vertaling toegevoegde afbeelding direct
  gewist) en ruimt na afwijzen de voorgestelde afbeeldingen op (let op: opnieuw indienen van een afgewezen
  verzoek mist die afbeeldingen dan). Ook gebouwd: Toevoegen/Aanpassen/Verwijderen werken per blok (tik op
  zin of afbeelding; `IsChoosingBlock` bepaalt de onzichtbare laag; woordselectie is vervallen, de pop-up van
  Aanpassen toont de hele zin). Afbeelding aanpassen = editor + nieuwe upload (segment `Text` = nieuwe tag,
  `Src` = oude); verwijderen = `Text` leeg. `addToOriginal` vervangt/verwijdert zo'n plaatje in het
  origineel (en dus overal). Knop Verwerpen naast Opslaan op Vertalingen (`DiscardTranslations` → terug naar
  `_opened`). Verzoeken tonen oude en nieuwe afbeelding (`SentenceChange.BeforeImage/AfterImage`).
  Functions gedeployed op 29-10-2026 (ronde 4). Getest 29-10: W1–W9 en X1–X7 werken, behalve X3 (editor
  opende een lege afbeelding: hij kreeg de voorbeeld-verwijzing `fsimg:ID@preview…`). Opgelost met
  `SentenceRow.ImageRef` (de echte verwijzing); nog te hertesten.
- **Volgende stap:** gebruiker hertest X3; daarna committen op verzoek en `.aab` versie 4.
- **Bekende grenzen:** zinnen worden geknipt na . ! ? … gevolgd door een spatie (afkortingen als "bijv."
  knippen ook). Een toevoeging komt in dezelfde alinea als de zin waarna hij staat. Woordselectie gaat
  per woord (spaties).

**Fasen (elke fase eindigt met: tests groen, README bijgewerkt, gebruiker test):**
- **Fase 1 – cloud:** segmenten per zin (`shared.js`: zinssplitsing naast alinea's en plaatjes, met tests),
  vertalingen opslaan als segmentenlijst met status per segment, eerste vertaling van een nieuw spel,
  bijwerken na wijziging van het origineel (LCS per zin), verwijderde plaatjes overal weg, berichten
  (`notifyTranslationChecks`) op basis van segmenten met status "wacht op controle". Model in de app
  (`GameTranslation` + segmenten) lezen/schrijven, met tests. Opruimen bestaande spellen (zie 9).
- **Fase 2 – app, pagina Vertalingen:** tekst per zin tonen met markering per status, knoppen Vertaling
  akkoord / Toevoegen / Aanpassen / Verwijderen, pop-ups, woordselectie. "Dit het origineel maken" en de
  melding `HasExtraParagraphs` weg. Tab hernoemen.
- **Fase 3 – requests:** wijzigingen bundelen per mod per spel, lijst "origineel → nieuw", knop spel openen,
  goedkeuren per request (mods) of direct (admins).
- **Fase 4 – afronden:** ⓘ-teksten en alle vertalingen, afvinklijst bijwerken
  (https://claude.ai/artifact/2bdNcCQqsLQbSR2ihAuRBD), dan pas de gebruiker vragen om te deployen en daarna
  `.aab` versie 4.

**Werkwijze:** de gebruiker heeft beperkte ruimte. Claude kan de accountstatus niet zien, dus: per fase
werken, na elke fase de README bijwerken met precies waar het staat en wat de volgende stap is, zodat een
nieuwe sessie zonder uitleg verder kan.

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

**Tests (sinds 28-10-2026)**
- App-logica: `dotnet test tests/PubGames.Tests` (xUnit, net10.0). Linkt bestanden uit de app (zie csproj);
  `Stubs.cs` vervangt L, AppLanguage, FirestoreClient en FirestoreImageStore. Staat niet in de solution, dus
  VS bouwt het niet mee. Nieuwe logica die getest moet worden: houd hem vrij van MAUI (zoals `DrawRules`).
- Cloud Functions: `npm test` in `functions` (node:test, `functions/test`). Kleine regels staan in
  `functions/shared.js`.
- `tests/shared-cases.json`: voorbeelden die app en cloud allebei precies zo moeten uitkomen (vingerafdruk
  van een spel, middelvinger-regel). Verander je `HashOf`/`sourceHash` of `IsRudeHand`, werk dan dit bestand bij.
- Afvinklijst voor de gebruiker: https://claude.ai/artifact/2bdNcCQqsLQbSR2ihAuRBD (antwoorden in de
  collectie `results`, te lezen met ArtifactData).

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
