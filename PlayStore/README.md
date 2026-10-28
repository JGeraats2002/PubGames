# Play Store – alles om in te vullen

Kopieer deze antwoorden in de Play Console. Afbeeldingen staan in deze map.

## Links

| Wat | URL |
|---|---|
| Privacybeleid | https://pubgames-92835.web.app/privacy |
| Account verwijderen | https://pubgames-92835.web.app/delete-account |
| Website | https://pubgames-92835.web.app |
| Contact-e-mail | geraatsdeveloping@gmail.com |

(Werkt pas na `firebase deploy --only hosting --project pubgames-92835`.)

## Uploadsleutel

- Keystore: `%USERPROFILE%\PubGamesKeys\pubgames-upload.keystore`, alias `pubgames-upload`
- Wachtwoord: `%USERPROFILE%\PubGamesKeys\pubgames-upload.pass`
- SHA-1 uploadsleutel: `9D:EA:53:5F:1D:04:BB:9B:40:7D:29:93:A8:D6:EF:6A:AC:C0:2C:0F`
- SHA-256: `24:02:69:49:BB:89:98:BB:7D:6F:7E:6C:CD:15:16:36:A8:26:9D:4B:36:52:B3:6E:EF:E8:90:3C:E7:4E:79:3F`
- Release-build: `dotnet publish PubGames/PubGames/PubGames.csproj -f net10.0-android36.0 -c Release`
  → `bin/Release/net10.0-android36.0/publish/com.pubgems.pubgames-Signed.aab`
- Bij elke nieuwe upload `ApplicationVersion` in de csproj met 1 ophogen.

---

## Store-vermelding (Hoofdvermelding)

**App-naam:** Pub Games

**Korte beschrijving (EN, max 80):**
Dice, card and party games for pub and home: learn the rules, keep the score.

**Volledige beschrijving (EN):**

Every game you play in the pub or at home, in one app.

Dice, cards, word games, hand games, heads or tails: if you can play it around a table with friends or family, it belongs in Pub Games. Each game shows its rules step by step, and the scoreboard is right there, so you can start playing straight away. Learning a new game has never been easier.

PLAY IT YOUR WAY
• Drinking games for a night out in the pub
• Games without drinks, for at home with family and friends
• Many games can be turned into a drinking game, or played for who buys the next round

EVERYTHING IN ONE PLACE
• A growing library of games, with clear rules for each one
• Find games by what you have at hand: dice, cards, a coin, or nothing at all
• Keep score right away, for any number of players
• Duels: settle it one-on-one
• Link up with friends using your player code and keep stats together
• See who wins, loses and ties the most
• Save a game and continue later
• Miss a game? Suggest it right from the app
• In 9 languages, and games are translated into yours

Most games are free. Extra games can be unlocked one by one, or all at once with the Pub Games Pass (weekly, monthly or yearly).

Because the app includes drinking games, Pub Games is for adults (18+) only. Drink responsibly, know your limits, and never drink and drive.

**Korte beschrijving (NL, max 80):**
Dobbel-, kaart- en partyspellen voor kroeg en thuis: regels én scorebord.

**Volledige beschrijving (NL):**

Alle spellen die je in de kroeg of thuis speelt, in één app.

Dobbelstenen, kaarten, woordspellen, handspellen, kop of munt: alles wat je met vrienden of familie aan tafel kunt spelen, hoort in Pub Games. Elk spel laat stap voor stap de regels zien, en het scorebord staat meteen klaar. Zo begin je direct met spelen, en leer je een nieuw spel in een handomdraai.

SPEEL HET ZOALS JIJ WILT
• Drankspellen voor een avond in de kroeg
• Spellen zonder drank, voor thuis met familie en vrienden
• Veel spellen maak je zo tot drankspel, of speel je om wie het volgende rondje betaalt

ALLES OP ÉÉN PLEK
• Een groeiende bibliotheek met spellen, elk met duidelijke regels
• Vind spellen op wat je bij de hand hebt: dobbelstenen, kaarten, een munt of helemaal niets
• Houd direct de score bij, voor elk aantal spelers
• Duels: los het één-tegen-één op
• Koppel met vrienden via je spelerscode en houd samen statistieken bij
• Zie wie het vaakst wint, verliest of gelijkspeelt
• Sla een spel op en speel later verder
• Mis je een spel? Stel het voor vanuit de app
• In 9 talen, en spellen worden vertaald naar jouw taal

De meeste spellen zijn gratis. Extra spellen ontgrendel je los, of allemaal tegelijk met de Pub Games Pass (per week, maand of jaar).

Omdat de app ook drankspellen bevat, is Pub Games alleen voor volwassenen (18+). Drink met mate, ken je grenzen en rijd nooit onder invloed.

**Andere talen:** zie `listings.md` (FR, DE, ES, IT, PT, ZH, AR).

**Afbeeldingen:**
- App-icoon: `icon-512.png`
- Feature graphic: `feature-graphic-1024x500.png`
- Telefoonscreenshots: minimaal 2 (zelf maken op de telefoon, staand, bv. bibliotheek, scorebord, duel, statistieken)

**Categorie:** Games → Casual (of Kaartspellen/Partyspellen als die er is)
**Tags:** party, drinking games, casual

---

## App-content

**Privacybeleid:** URL hierboven.

**Advertenties:** Nee, bevat geen advertenties.

**App-toegang:** Alle functionaliteit is beschikbaar zonder speciale toegang? → **Nee, sommige functies zijn beperkt.**
Instructie voor de reviewer:
> Confirm you are 18+. Sign in with any Google account on the Account page.
> Paid games show their rules first and can be bought through Google Play.

**Doelgroep en content:** alleen **18 jaar en ouder**. Niet aantrekkelijk voor kinderen.

**Contentbeoordeling (IARC-vragenlijst):**
- Categorie: Game (overige / casual)
- Geweld, angst, seksualiteit, grof taalgebruik, gokken: **Nee**
- Gereguleerde middelen: **Ja – alcohol**, de app verwijst naar en moedigt het drinken van alcohol aan
- Interactie tussen gebruikers: **Ja** (spelers koppelen; naam, bijnaam en profielfoto zichtbaar voor gekoppelde spelers). Geen openbare chat.
- Deelt locatie: **Nee**
- Digitale aankopen: **Ja**
- Verwacht resultaat: PEGI 18 / USK 18 / ESRB Mature 17+

**Gegevensveiligheid:**
- Verzamelt of deelt de app gegevens? **Ja** (verzamelen), delen met derden: **Nee** (Google/Firebase is verwerker)
- Versleuteld tijdens overdracht: **Ja**
- Gebruikers kunnen verwijdering aanvragen: **Ja** – URL account verwijderen hierboven
- Accountaanmaak: via **OAuth (Google-inloggen)**

| Gegevenstype | Verzameld | Verplicht? | Doel |
|---|---|---|---|
| Persoonlijk: Naam | Ja | Verplicht | App-functionaliteit, Accountbeheer |
| Persoonlijk: E-mailadres | Ja | Verplicht | App-functionaliteit, Accountbeheer, Communicatie van ontwikkelaar |
| Persoonlijk: Gebruikers-ID's | Ja | Verplicht | App-functionaliteit, Accountbeheer |
| Financieel: Aankoopgeschiedenis | Ja | Optioneel | App-functionaliteit |
| Foto's en video's: Foto's | Ja | Optioneel | App-functionaliteit |
| Berichten: Andere berichten in de app | Ja | Optioneel | App-functionaliteit, Communicatie van ontwikkelaar |
| App-activiteit: Andere door gebruikers gegenereerde content | Ja | Optioneel | App-functionaliteit |
| App-activiteit: Andere acties (spelresultaten) | Ja | Verplicht | App-functionaliteit |

Niet verzameld: locatie, contacten, apparaat-ID's, crashlogs, diagnostiek, gezondheid, audio, bestanden, agenda, webgeschiedenis.

**Overige verklaringen:** Overheidsapp: Nee · Financiële functies: Geen · Gezondheid: Nee · Nieuws: Nee.

---

## Producten (na stap D)

**Abonnement** `pubgames_pass` – naam "Pub Games Pass", voordeel: "All paid games unlocked".

| Basisplan-ID | Periode | Prijs (NL) | Automatisch verlengen |
|---|---|---|---|
| `week` | 1 week | € 0,99 | Ja |
| `month` | 1 maand | € 2,99 | Ja |
| `year` | 1 jaar | € 24,99 | Ja |

**Losse spellen** (`game_<id>`, € 0,99 of € 1,99) maakt de Cloud Function `syncGameProduct` zelf aan
zodra een admin een spel betaald maakt. Dat werkt pas als stap D (API + serviceaccount) klaar is.
