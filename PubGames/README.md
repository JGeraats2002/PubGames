# Pub Games

.NET MAUI-app voor Android (`net10.0-android36.0`, app-id `com.pubgems.pubgames`) voor
drankspellen in de kroeg: spelers kiezen, spellen uit een bibliotheek, scorebord, duels,
statistieken, feedback, en betaalde spellen of een Pub Games Pass.

- Repo: GitHub `JGeraats2002/PubGames`, branch `master`
- Firebase-project: `pubgames-92835` (Auth, Firestore `eur3`, Cloud Functions in `europe-west1`)
- Eigenaar/admin: `jjh.geraats@gmail.com`; ontwikkelaarsaccount Play: `geraatsdeveloping@gmail.com`

## Mappen

| Map / bestand | Inhoud |
|---|---|
| `PubGames/PubGames/` | De app (Models, Services, ViewModels, Views, Platforms/Android) |
| `PubGames/PubGames/Models/PageHelp.cs` | Alle uitleg uit de ⓘ-knoppen |
| `functions/index.js` | Cloud Functions (aankopen controleren, kortingscode, refunds, account verwijderen) |
| `firestore.rules` | Beveiligingsregels van Firestore |
| `firebase.json`, `.firebaserc` | Firebase-deploy-instellingen |

---

## Waar we gebleven zijn (bijgewerkt 27-10-2026)

**Laatst gedaan:** opschoning op basis van literatuur (naamgeving, commentaar, toegankelijkheid,
veiligheid). De app bouwt met 0 fouten en 0 warnings. Alle Cloud Functions zijn gedeployed,
ook `deleteAccount`.

**Nog niet gecommit:** ongeveer 140 gewijzigde bestanden sinds commit
`42ea8dc Betaling toegevoegd en detaileringen`. Commit pas als de gebruiker erom vraagt.

### Open punten voor de gebruiker

1. **Identiteitsverificatie Google Play** loopt nog (id-kaart). Daarna:
   - **B.** Een interne testversie uploaden (`.aab`) in de Play Console.
   - **C.** Het abonnement `pubgames_pass` aanmaken met de basisplannen week, maand en jaar
     (€0,99 / €2,99 / €24,99).
   - **D.** De Google Play Android Developer API aanzetten. Het serviceaccount
     `737835893040-compute@developer.gserviceaccount.com` uitnodigen in de Play Console
     (Gebruikers en rechten) met rechten voor financiën/bestellingen en het beheren van producten.
   - **E.** Licentietesters toevoegen, zodat testaankopen gratis zijn.
2. **Privacybeleid:** Play vraagt een openbare URL voor de gegevensveiligheid.
3. **Leeftijdsclassificatie:** het content rating-formulier invullen. Alcohol telt mee, en de app
   vraagt zelf al om een 18+-bevestiging.
4. **API-sleutel beperken:** in de Google Cloud-console, onder APIs & Services > Credentials,
   de Android-sleutel beperken tot het app-pakket en de gebruikte API's.
5. **`firebase-functions` bijwerken:** niet dringend. Er komen breaking changes; doe het later
   apart en test het.

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
- **Niet bouwen terwijl VS bouwt:** dat geeft bestandslocks (XALNS7024). Bouw met
  `dotnet build PubGames/PubGames/PubGames.csproj -f net10.0-android36.0`. Bij vreemde
  resource-fouten: bin/obj weggooien.

**Stijl (zo gewenst door de gebruiker)**
- Alle uitleg staat alleen onder de ⓘ-knop (`Models/PageHelp.cs`), niet als tekst op het
  scherm. Pas de ⓘ-tekst aan bij elke functiewijziging.
- Tijdelijke meldingen verdwijnen vanzelf via `ViewModels/MessageTimer.cs`.
- Elke klasse, methode en eigenschap heeft een `/// <summary>`. Interface-implementaties krijgen
  `/// <inheritdoc />`.
- Lusvariabelen krijgen betekenisvolle namen (geen `i`/`x`). Korte lambda's (`p => p.Name`) mogen.
- Aanraakdoelen zijn minimaal 48dp. Symboolknoppen krijgen `SemanticProperties.Description`.
- Pop-ups met meer dan 2 keuzes gebruiken `Views/ChoicePopup.AskAsync`.
- UI-teksten in de app zijn Engels.

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
