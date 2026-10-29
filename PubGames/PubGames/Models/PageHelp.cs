namespace PubGames.Models;

/// <summary>One heading and its explanation on an info page.</summary>
public sealed record HelpSection(string Heading, string Text);

/// <summary>
/// What the ⓘ button of a page shows: how to use that page, or (while
/// playing) the rules of the game. Rules pages also carry the game's own
/// "How to play" text and pictures.
/// </summary>
public sealed record PageHelp(string Title, IReadOnlyList<HelpSection> Sections, IReadOnlyList<RulesBlock>? RulesBlocks = null)
{
	/// <summary>Whether the help carries a game's own "How to play" text and pictures.</summary>
	public bool HasRules => RulesBlocks is { Count: > 0 };
}

/// <summary>
/// The "how to use this page" text of every page, kept together so the app
/// explains itself consistently. Role-specific pages get the admin or
/// moderator version.
/// </summary>
public static class Help
{
	/// <summary>The sign-in page's help.</summary>
	public static PageHelp Login => new(L.T("Signing in"),
	[
		new(L.T("Why sign in"), L.T("Sign in with your Google account. Your players, scores and unlocked games are saved to your account, so they're there on every phone you use.")),
		new(L.T("Admins and moderators"), L.T("If an admin has added your Google account to the team, you get the Admin tab after signing in.")),
		new(L.T("18 and older"), L.T("Many games in Pub Games involve alcohol, so the app is for adults (18+). The first time you sign in you're asked to confirm your age. Drink responsibly and never drink and drive."))
	]);

	/// <summary>The help of Who's playing?, the start of a game night.</summary>
	public static PageHelp Players => new(L.T("Who's playing?"),
	[
		new(L.T("Add a new player"), L.T("Type a name and tap Add. The player is added to this game and saved for next time.")),
		new(L.T("Add by player code"), L.T("Everyone with the app has a personal code under Account > Profile. Type someone's code and tap Add: they're added to this game and your saved players, with their profile picture. When you finish a game, the result counts in their stats - and in yours when you play yourself (you're one of the saved players).")),
		new(L.T("Your own name for a player"), L.T("✎ gives a player a name of your choice. For someone added by code it's only on your phone - their profile doesn't change.")),
		new(L.T("Add a saved player"), L.T("Tap a name under Saved players. While they're playing they disappear from that list, so you only see who's left to choose.")),
		new(L.T("Take a player out"), L.T("Tap a player's name in the list at the top. They go back to the saved players.")),
		new(L.T("Delete a saved player"), L.T("Hold a saved player until \"Delete player?\" appears, then choose Yes or No. Earlier games keep their name. You can't delete yourself.")),
		new(L.T("Delete a player added by code"), L.T("Holding a player you added by their code gives three choices. Only from my players: they're gone from your list only. Remove the link both ways: you're also removed from their saved players, the next time their app is online. Remove and block: both ways, and they can't add you by your code any more - Account > Blocked players shows who you blocked and unblocks them.")),
		new(L.T("Seat order"), L.T("▲ and ▼ move a player up or down. The order at the top is the seat order on the scoreboard.")),
		new(L.T("After a game"), L.T("When you quit a game, the players who were playing at the end are filled in again, including seat changes.")),
		new(L.T("Saved games"), L.T("A game is saved on this phone as soon as someone has a score. Saved games (top right) shows them: tap an unfinished game to continue where you left off."))
	]);

	/// <summary>The help of Saved games.</summary>
	public static PageHelp SavedGames => new(L.T("Saved games"),
	[
		new(L.T("Your games"), L.T("Every game played on this phone in which someone has a score, newest first. On two pages: Unfinished and Finished. Tap an unfinished game to continue it, or a finished one to see the scoreboard and the final result.")),
		new(L.T("Delete games"), L.T("Tap Select, then tap the games to delete - or Select all / Deselect all - and tap Delete. Cancel stops selecting.")),
		new(L.T("Keep saved games"), L.T("Each page has its own time for how long its games are kept: 1 week, 1 month (unless you change it), 6 months, 1 year, never delete, or don't save them at all - for example keep unfinished games but not finished ones. The time counts from the last score. Games are only kept on this phone; a finished game's result in the stats stays."))
	]);

	/// <summary>The help of Choose a game.</summary>
	public static PageHelp GameLibrary => new(L.T("Choose a game"),
	[
		new(L.T("Find a game"), L.T("Search by name, or tap Filter: set the number of players (all the way left: any number) and tap the categories you're looking for, per main category. Nothing there is required; the button shows how many filters are set and Clear filters shows everything again.")),
		new(L.T("Start"), L.T("Under each game's name you see how many players it's for and its categories. Tap a game to read its rules and start playing. Every game is in the list, but it only opens when you have the right number of players for it: if not, the app says how many players to add or take out. Paid games show their price.")),
		new(L.T("Paid games"), L.T("A paid game shows 🔒 and its price. Tap it to read its rules first; then Unlock to play lets you buy it once - it's yours on your account, to play with everyone at the table - use a code you were given (for that game only), or take the Pub Games Pass for every paid game. Going back costs nothing. Bought games and the pass also work on another phone where you sign in with the same account (Account > Pub Games Pass > Restore purchases)."))
	]);

	/// <summary>The help of a game's rules page, before starting.</summary>
	public static PageHelp GameStart => new(L.T("Rules and start"),
	[
		new(L.T("Read the rules"), L.T("The rules of the game are on this page, with pictures where the game has them. Under the name you see how many players the game is for: Any, or for example 2-6 or 2+.")),
		new(L.T("Number of subgames"), L.T("Use − and + to choose how many subgames make up the game. It's 1 game unless you change it; choose more for, say, a best of 3. During the game you can still add subgames, and a tie at the end offers an extra one.")),
		new(L.T("Start"), L.T("Skip rules and Start game both start the game with the chosen players and number of subgames."))
	]);

	/// <summary>The scoreboard's help: how to keep score with this game's scoring type and rules.</summary>
	public static PageHelp Scoreboard(ScoringType type, ScoringSettings settings) => new(L.T("Scoreboard"),
	[
		new(L.T("Keeping score"), type switch
		{
			ScoringType.PlusMinus => L.T("Tap + or − next to a player to add or subtract the game's step."),
			ScoringType.ManualPlusMinus => L.T("Type an amount in the box next to a player, then tap + to add it or − to subtract it. Only amounts the game's rules allow are accepted; if an amount isn't, the message at the top says what you can enter. Tap ⓘ and choose the rules to see them."),
			_ => L.T("Tap + or − next to a player, or tap a score to type any amount.")
		}),
		.. settings.FixedTurnOrder
			? [new HelpSection(L.T("Whose turn"), L.T("This game has a fixed input order: seat 1 first, then seat 2, and so on. The player whose turn it is has a thick dark border and ▶ instead of their seat number; only they can enter a score. ") +
				(type == ScoringType.ManualPlusMinus
					? L.T("Type the amount and tap + or −, or Next above the keyboard: the turn then passes on. No points? Type 0 - it has to be entered too.")
					: L.T("Tap + or − as often as needed, then Turn done to pass the turn on - also when the score stays the same.")))]
			: Array.Empty<HelpSection>(),
		.. settings.EndScore is { } end
			? [new HelpSection(L.T("End of a subgame"), settings.EndMode == EndMode.Manual
				? L.F("Once a player reaches {0}, the line above the cards says so. End the subgame yourselves with Next subgame or Finish game.", end)
				: settings.EqualizingTurn
					? L.F("Once a player reaches {0}, the players after them in seat order get one last turn. When the turn is back at seat 1, the subgame ends by itself and you note its result.", end)
					: L.F("Once a player reaches {0}, the subgame ends by itself and you note its result.", end))]
			: Array.Empty<HelpSection>(),
		.. type == ScoringType.ManualPlusMinus
			? [new HelpSection(L.T("Typing scores"), L.T("The screen moves up so the box you type in stays above the keyboard. ◀ Previous and Next ▶ above the keyboard (or the keyboard's own Next key) go to the other players' boxes; Next first adds the amount you typed - use a minus sign to subtract."))]
			: Array.Empty<HelpSection>(),
		new(L.T("Warning and limits"), L.T("A player at the game's warning score gets a dashed orange border and a ⚠ WARNING badge. At the maximum or minimum score the whole card turns dark blue with a ▲ MAX REACHED or ▼ MIN REACHED badge. Scores never go past the maximum or minimum.")),
		new(L.T("Subgames"), L.T("The top shows which subgame you're playing. + subgame and − subgame change how many there are. Next subgame lets you note the result of this one and then starts the next, with everyone back at the starting score. On the last subgame the button is Finish game.")),
		new(L.T("Stats"), L.T("Stats shows everyone's wins, losses or points so far. The winner stays a surprise until you finish the game.")),
		new(L.T("Change players"), L.T("Move players to another seat, replace someone, add a player or take someone out - see the ⓘ on that page."))
	]);

	/// <summary>The help of Change players, during a game.</summary>
	public static PageHelp SessionPlayers => new(L.T("Change players"),
	[
		new(L.T("Seat order"), L.T("▲ and ▼ move a player to another seat. The seat order is the order on the scoreboard. Nobody's score changes.")),
		new(L.T("Replace"), L.T("Someone else takes the seat, with everything that belongs to it: the score of this subgame and the results of earlier subgames.")),
		new(L.T("Take out"), L.T("✕ takes a player out of the game. Their results so far stay on record; if they join again later they continue with their score.")),
		new(L.T("Add a player"), L.T("Tap a saved player to add them at the last seat, or type a new name and tap Add. Tick the box to also save a new player for next time."))
	]);

	/// <summary>The subgame result's help, for how this game notes results and breaks ties.</summary>
	public static PageHelp RoundResult(ResultMode mode, bool lowestWins, ScoringSettings settings) => new(L.T("Subgame result"),
	[
		new(L.T("Filled in for you"), mode switch
		{
			ResultMode.Ranking => L.F("Everyone's place is filled in from the scores ({0} score first). Players with the same score share a place.", (lowestWins ? L.T("lowest") : L.T("highest"))),
			ResultMode.Loser => L.F("The loser is picked from the scores: the {0} score. Players with the same score lose together.", (lowestWins ? L.T("highest") : L.T("lowest"))),
			_ => L.F("The winner is picked from the scores: the {0} score. Players with the same score share the win.", (lowestWins ? L.T("lowest") : L.T("highest")))
		}),
		new(L.T("Change it"), mode == ResultMode.Ranking
			? L.T("Use − and + next to a player to change their place (− moves towards 1st). Several players can share a place.")
			: L.F("Tap a player to mark or unmark them as {0}. Several players can be {1}. A marked player has a dark blue card and a badge.", (mode == ResultMode.Loser ? L.T("loser") : L.T("winner")), (mode == ResultMode.Loser ? L.T("losers") : L.T("winners")))),
		new(L.T("Confirm"), L.T("Start next subgame saves this result and puts every score back at the starting score. After Finish game, it saves the result and shows the final result; if the winner or loser is tied, you can first play an extra subgame to decide.")),
		.. settings.AllowTies ? Array.Empty<HelpSection>() : [new HelpSection(L.T("No ties"), TiesText(settings, mode))],
		new(L.T("Changed your mind"), L.T("Go back without confirming: nothing is saved and you continue the subgame."))
	]);

	/// <summary>How a game without ties plays off a tie, for players.</summary>
	private static string TiesText(ScoringSettings settings, ResultMode mode)
	{
		var when = settings.TieBreakMoment switch
		{
			TieBreakMoment.SubgamesOnly => L.T("after a subgame"),
			TieBreakMoment.FinalOnly => L.T("in the final total"),
			_ => L.T("after a subgame and in the final total")
		};
		var which = settings.TieBreakScope == TieBreakScope.Every ? L.T("Players who share a place")
			: mode == ResultMode.Loser ? L.T("Players who share the loss") : L.T("Players who share 1st place");
		var duel = settings.DuelSameScoring || settings.DuelSettings is null ? settings : settings.DuelSettings;
		return L.F("This game has no ties {0}. {1} play a duel by its own rules, shown on the duel screen: {2}. ", when, which, duel.DuelEndSummary)
			+ L.T("Take turns in the order shown: the player whose turn it is has a thick border. With + and − tap Turn done when your turn is over; with a typed amount the turn passes on by itself; when you type in your points, tap Turn done. Undo takes back the last turn. ")
			+ L.T("The duel ends by itself; check the result and confirm it - or undo the last turn if something was entered wrong. The duel decides one winner or loser, or places one after another so the points differ. ")
			+ L.T("After a duel for a subgame its outcome is filled in on the result right away: change it there if needed, then confirm.");
	}

	/// <summary>The help of the standings: during a game, or the final result.</summary>
	public static PageHelp Standings(bool isFinal) => isFinal
		? new(L.T("Final result"),
		[
			new(L.T("The result"), L.T("The banner shows the winner(s), or the loser(s) in a game where you note losers. Below it you see everyone's place and total over all subgames. Equal totals share a place.")),
			new(L.T("Done"), L.T("Tap Done and choose Play again to start the same game with the same players, seats and number of subgames, straight on the scoreboard. Choose Quit to go back to Who's playing?, with these players filled in."))
		])
		: new(L.T("Stats"),
		[
			new(L.T("Totals so far"), L.T("Everyone's wins, losses or points over the subgames finished so far, in seat order. Places and the winner are only shown when you finish the game.")),
			new(L.T("Back"), L.T("Tap Back to the game to continue."))
		]);

	/// <summary>The library's help, for an admin or a moderator.</summary>
	public static PageHelp AdminLibrary(bool isModerator) => new(L.T("Library"),
	[
		new(isModerator ? L.T("Propose a game") : L.T("New game"), isModerator
			? L.T("Propose a new game proposes a game to the admins. It's published once an admin approves it.")
			: L.T("New game creates a game; it's published for players straight away.")),
		new(L.T("Find a game"), L.T("Search by game name, and/or tap Filter: set the number of players (all the way left: any number) and tap the categories you're looking for, per main category. Nothing there is required; the button shows how many filters are set and Clear filters shows everything again. Games are in alphabetical order.")),
		new(isModerator ? L.T("Change a game") : L.T("Edit a game"), isModerator
			? L.T("Tap a game to propose changes or its deletion. Players keep seeing the current version until an admin approves. While another moderator's changes to a game are waiting for review, the game shows a 🔒 with their name and can't be changed by anyone else: tap it to see what they submitted - switch between Original and With changes - and to send them a message. It's open again once an admin has approved or rejected the changes, or they withdraw them. Your own waiting changes you can still change: submitting again replaces them for the admin.")
			: L.T("Tap a game to change or delete it. Changes are live for players straight away.")),
		new(L.T("Still needs"), L.T("An orange line under a game says which main categories it has no category of yet. Edit the game and choose one for each; until then players can't find it with those filters. Once changes that fill them in have been submitted, the line says Waiting for review until an admin decides.")),
		new(L.T("Messages and requests"), isModerator
			? L.T("Your requests waiting for review are listed at the top; tap one to change or withdraw it. Decisions arrive in your Inbox.")
			: L.T("A banner at the top shows requests waiting for your review and new messages; tap it to go there."))
	]);

	/// <summary>
	/// The game form's help, for an admin or a moderator, with the scoring type that's chosen.
	/// </summary>
	public static PageHelp CreateGame(bool isModerator, ScoringType type)
	{
		var sections = new List<HelpSection>
		{
			new(L.T("Four pages"), L.T("The form has four pages at the top: General (name, language, cover, price, number of players, categories), Scoring (scoring type, subgame, ties), Rules (how to play) and Translations. Players don't see these pages - they see the game as before.")),
			new(L.T("Translations"), L.T("Write the game in your own language and choose it at Written in: that's always the original. After saving, it's translated automatically into every other language of the app, sentence by sentence (this takes about a minute); players see the game in their own language. If the text turns out to be in another language than the one at Written in, the cloud corrects Written in.")),
			new(L.T("Add your rules"), L.T("A game needs a name, at least one category per main category and a scoring type; everything else is optional. On the scoring page the scoring type is always there, at the top; tap + Add your rules to add Subgame or Ties. They always show in the same order - scoring type, Subgame, Ties - whatever order you add them in. The ✕ next to a part takes it off again and clears what you filled in. A part you don't add keeps its default: the winner is noted and the highest score wins, ties are allowed, the seats stay as they are, no end score.")),
			new(L.T("Going back"), L.T("Going back with the arrow at the top or the phone's back button while you've changed something asks whether to save your changes, not save them, or keep editing.")),
			new(L.T("Choices"), L.T("Every dropdown starts with \"Choose ...\": pick an option in each one. Saving tells you if a choice is still missing and opens the page it's on.")),
			new(L.T("Text in pictures"), L.T("Text you put in a picture with Create image is translated too, and players see the picture with the text in their language. On the Languages page each picture shows with the texts in that language, and the texts are below it (for the cover: below the name): the original small, the translation below it to correct. The picture follows what you type. Numbers and the game's name stay as they are.")),
			new(L.T("Your own language"), L.T("The Translations page opens in the app's language, so everyone checks a game in their own language. At the top you see per language how many sentences still need checking. The game's name isn't translated automatically: a name stays a name. Correct it per language if it's known by another name there.")),
			new(L.T("Checking translations"), L.T("Each sentence shows the original small above it. Yellow: not checked yet. Orange: the original changed after it was checked; compare with the new automatic translation and tap Take this or Keep it as it is. Translation is fine checks every sentence of the language. Add, Change and Remove: tap the button, then tap a sentence or picture. Add asks for text or a picture from the image editor and puts it after what you tapped; it goes into the original and every language after saving, and someone who speaks the original's language approves added text at the top. Change opens the sentence to correct, or the picture in the image editor. Remove takes out a sentence in this language, or a picture everywhere. A changed sentence counts as checked. Discard, next to Save, undoes what you changed here. When the original changes on the Rules page, only the sentences that changed are translated again.")),
			new(L.T("Name and cover"), L.T("Give the game a name. The cover image is shown square and as wide as the page, with the same centre crop as the small icon in the game lists - keep the important part in the middle.")),
			new(L.T("Pictures"), L.T("The cover and the pictures in the rules are made in the image editor, where you place arrows, text, cards, dice, hands, drinks and more - so every game is explained in the same style. Photos from your phone can't be used. A picture can be changed later: tap the cover button again and choose Edit this image, or hold the picture in the rules. Only a cover can have a background colour; pictures in the rules are always on cream.")),
			new(L.T("Price"), L.T("Choose Free or one of the fixed prices: €0.99 for a standard game, €1.99 for a big game. They're in line with the Pub Games Pass (a game costs as much as a week of it). The account that buys a game plays it with everyone at the table, for good; with the pass, every paid game is unlocked. A paid game is put up for sale in Google Play by itself when it's published."))
		};
		sections.Add(type == ScoringType.ManualPlusMinus
			? new(L.T("Scoring: Manual plus / minus"), L.T("On the scoreboard players type an amount next to a player and tap + or − to add or subtract it. Set the starting score, and use the switches for a maximum, a minimum and a warning score."))
			: new(L.T("Scoring: Plus / minus"), L.T("Every tap on + or − changes a player's score by the step. Set the starting score, and use the switches for a maximum, a minimum and a warning score.")));
		sections.Add(new(L.T("Warning and limits"), L.T("On the scoreboard a player at the warning gets a dashed orange border and a ⚠ WARNING badge; at the maximum or minimum the card turns dark blue with a ▲ MAX / ▼ MIN badge. Scores never go past the maximum or minimum.")));
		if (type == ScoringType.ManualPlusMinus)
			sections.Add(new(L.T("Rules for entered amounts"), L.T("+ Add rule adds a rule; ✕ removes it. Lower than 11 allows 10; lower than or equal to 11 also allows 11; between includes both numbers. Amounts may be negative. Tap AND / OR above a rule to switch how it links to the rule above: OR goes first, so rules linked with OR form a group where one must be true, and AND joins groups that must all be true. The bold line below the rules shows exactly what's allowed. No rules: any amount.")));
		sections.Add(new(L.T("Subgame"), L.T("Everything in it is optional. Result of each subgame: how it's noted - the winner(s), everyone's place, or the loser(s) - and which score wins, loses or is 1st; after each subgame the app fills this in from the scores and the players can change it. Not chosen: the winner is noted and the highest score wins. The players choose how many subgames they play (1 unless they change it). Who starts the next subgame: the seats turn so that player sits in seat 1 - everyone keeps their neighbours. Left of a player is the next seat, right of a player the seat before. Fixed input order: scores are entered seat by seat and everyone has to enter one, also 0. A subgame ends at score: once a player reaches it the subgame ends, automatically or when the players end it. With an automatic end and a fixed input order you can add an equalizing turn: the players after the one who reached it still get their turn in that round.")));
		sections.Add(new(L.T("Ties"), L.T("Ticked \"Ties are allowed\": tied players share the win, the loss or the place, and get the same points. Unticked, a tie is played off in a duel. Choose when: after every subgame, after the final total, or both. Choose which ties: only a shared 1st place (in a loser game: a shared loss), or every shared place. The duel always has its own rules, filled in the same way as the game's: its scoring type and numbers and which score wins. Copy the game's rules fills those in with the game's, so you only change what's different - for example a duel to 10 while the game goes to 100. Then choose how the duel ends: a number of turns each (then the best score wins; a tie plays another turn), first to reach a score (rounds are played until someone has reached it, so everyone had as many turns; the best score wins), or best of a number of rounds (each round's best player wins it; the first to win more than half wins). With turns or a score you can also set a lead needed to win, like 2 points ahead: until then another turn is played. With Type in the score the players play the duel their own way and type in everyone's score. Explain the duel itself in How to play.")));
		sections.Add(new(L.T("Number of players"), L.T("Switch on a minimum and/or a maximum and fill in the number. Only a maximum of 3 means 1, 2 or 3 players; only a minimum of 2 means 2 or more. Both off: any number, shown as Any. Players can filter games by how many they are, and can only start a game with a number of players it's made for.")));
		sections.Add(new(L.T("Categories"), L.T("The categories are listed under their main categories (Play style, Duration, ...). Open each main category and tap at least one category in it, so players can find the game; tap more if several fit. Tap again to unselect.")));
		sections.Add(new(L.T("How to play"), L.T("Write the rules as one text. To add a picture, tap in the text where it should go - also in the middle of a sentence - then tap Add picture at the cursor. The text is split there and the picture gets its own place, with an empty line above and below, on a cream background. The image editor opens to make the picture. Hold a picture to edit it, to move it (tap in the text where it should go, then Place here) or to remove it.")));
		sections.Add(new(L.T("Checks before saving"), L.T("Saving checks that everything fits together: for example the starting score between the minimum and maximum, the warning within them, and amount rules that allow at least one useful amount. The message above the button says what to fix.")));
		sections.Add(isModerator
			? new(L.T("Submitting"), L.T("As a moderator you submit for review: an admin decides and you get the decision in your Inbox. Players keep seeing the current version until then. Withdraw request takes back a request that's still waiting; Request deletion asks an admin to delete the game."))
			: new(L.T("Saving"), L.T("Publish game / Save changes makes it live for every player straight away. Delete game removes it from every player's library.")));
		return new(L.T("New or edit game"), sections);
	}

	/// <summary>
	/// The image editor's help, for a cover (with background colours) or a picture in the rules.
	/// </summary>
	public static PageHelp ImageEditor(bool cover) => new(L.T("Create image"),
	[
		new(L.T("Add things"), L.T("The buttons at the top add something to the image: arrows, text, cards, dice, hands, drinks and more. A table goes under everything else, to show where the game is played.")),
		new(L.T("Place them"), L.T("Tap something on the image to select it, then drag it. With two fingers you make it bigger or smaller and turn it. Below the image: Place across and Place up and down line it up with the edges, the middle or the quarter lines; Across and Down set its exact place; Size and Turn set its exact size and angle - type a number to make two things exactly alike.")),
		new(L.T("Align and exact place"), L.T("The dotted frame around a selected item fits tightly around what's drawn. The stripe icons choose which side of that frame Across and Down give: left, centre or right, and top, middle or bottom. Across always counts from the left edge of the drawing area, Down from the top edge. Left: Across is where the item's left side is. Centre: where its middle is. Right: where its right side is. The same for Down with top, middle and bottom. So Left with Across 0 puts the item against the left edge, and two items with the same alignment and number line up exactly. Changing the alignment leaves the item where it is: only the number changes.")),
		new(L.T("Growing from the aligned side"), L.T("Making something bigger or smaller keeps its aligned side where it is: aligned left, it grows to the right; aligned right, to the left; centred, the same in every direction. That goes for Size, pinching with two fingers, an arrow's length or arms, typing text and every other choice that changes the size. Turning keeps the aligned side too. New items get the alignment you chose last.")),
		new(L.T("Choose what it shows"), L.T("With something selected you see its choices: a card's suit and value or a whole deck, the number on a die or a dice cup, heads or tails, how full a drink is, which fingers a hand holds up (Neutral is a flat hand; Choose fingers starts as a fist).")),
		new(L.T("Arrows"), L.T("Length and Thickness make an arrow longer or thicker; turned upright, a longer arrow is higher. Bent gives the arrow a corner: Angle sets how far arm 2 is turned from arm 1, and Arm 1 and Arm 2 set how long each arm is. A bent arrow turns around its corner. With Both ways each arm gets a head, like the two hands of a clock: set the angle and the arm lengths, then turn it with Turn to show any time.")),
		new(L.T("Order and copies"), L.T("To front and To back decide what lies on top where things overlap. Copy makes another one just like it. Remove from image takes it away.")),
		new(L.T("Undo"), L.T("Undo takes back your last change. Tap it again to go back further.")),
		cover
			? new(L.T("Shape and background"), L.T("With nothing selected you choose the image's shape and its background colour. Covers are shown square."))
			: new(L.T("Shape and background"), L.T("With nothing selected you choose the image's shape. Pictures in the rules are always on cream, so it's clear in the text where they begin and end.")),
		new(L.T("Done"), L.T("Use this image puts it in the game. You can change it again later: it opens here just as you left it.")),
		new(L.T("Not allowed"), L.T("A hand showing only the middle finger can't be used: it's offensive. Misuse of the app can cost you your admin or moderator rights."))
	]);

	/// <summary>The categories page's help, for an admin or a moderator.</summary>
	public static PageHelp Categories(bool isAdmin) => new(L.T("Categories"),
	[
		new(L.T("What categories are for"), L.T("Categories are listed under main categories, like Play style or Duration. Every game has at least one category from each main category; players filter games by category.")),
		isAdmin
			? new(L.T("Add and fill"), L.T("+ New category adds a category: give it a name and choose its main category. A name can be used once in each main category. ▲ and ▼ on a category move it up or down within its main category. Tap a category to choose which games are in it, rename it, move it to another main category, or delete it. Deleting takes it off every game."))
			: new(L.T("Propose"), L.T("+ Propose a new category sends it to an admin for review: give it a name and choose its main category. It appears once an admin approves it. Tap a category to propose another name, another main category, or its deletion.")),
		isAdmin
			? new(L.T("Main categories"), L.T("+ New main category adds one. As soon as it has categories, a game can only be saved with at least one of them - existing games get theirs the next time they're edited. Tap a main category to add a category to it, rename it or delete it; deleting also deletes its categories and takes them off every game. Main categories are in alphabetical order, with new ones at the bottom. The order you give the categories is the order everyone sees: in the filters and when creating a game."))
			: new(L.T("Main categories"), L.T("+ Propose a new main category sends it to an admin for review. Tap a main category to propose a category in it, another name, or its deletion. Nothing changes until an admin approves; the decision arrives in your Inbox."))
	]);

	/// <summary>The help of My stats per game.</summary>
	public static PageHelp MyStats => new(L.T("My stats per game"),
	[
		new(L.T("Find a game"), L.T("Every game you finished at least once. Search by name, or tap Filter: set the number of players (all the way left: any number) and tap the categories you're looking for, per main category. Nothing there is required; the button shows how many filters are set and Clear filters shows everything again.")),
		new(L.T("Its stats"), L.T("Tap a game to see how often you played, won, lost and tied it, and every time you played it."))
	]);

	/// <summary>The help of one game's stats.</summary>
	public static PageHelp GameStats => new(L.T("Stats of a game"),
	[
		new(L.T("The numbers"), L.T("How many times you finished this game, and how often you won, lost or tied, with percentages.")),
		new(L.T("Won, lost, tied"), L.T("Winner and ranking games: you won if you finished 1st on your own, tied if you shared 1st place, and lost otherwise. Loser games: you lost if you were the only loser, tied if you shared the most losses, and won otherwise.")),
		new(L.T("History"), L.T("Below the numbers you see every time you played it, newest first, with how it ended."))
	]);

	/// <summary>The help of a category's games page.</summary>
	public static PageHelp CategoryGames => new(L.T("Games in a category"),
	[
		new(L.T("Choose games"), L.T("Tick every game that belongs in this category - tap the tick box or the game - then tap Save games in category. Players can then find these games by choosing this category.")),
		new(L.T("Find a game"), L.T("Search by game name, and/or tap Filter: set the number of players (all the way left: any number) and tap the categories you're looking for, per main category. Nothing there is required; the button shows how many filters are set and Clear filters shows everything again. Ticks you set stay when you search for another game."))
	]);

	/// <param name="thisDuel">How this duel is scored, who wins and how it ends (DuelViewModel.Explanation).</param>
	public static PageHelp Duel(string thisDuel) => new(L.T("Duel"),
	[
		new(L.T("This duel"), thisDuel),
		new(L.T("Taking turns"), L.T("Play in the order shown: the player whose turn it is has a thick border, and only they can enter a score. With + and − tap Turn done when your turn is over. With a typed amount, + or − ends the turn by itself. When you type in your points, tap Turn done (empty counts as 0).")),
		new(L.T("Undo"), L.T("↶ Undo takes back what was entered in this turn, or else the turn before it.")),
		new(L.T("The end"), L.T("The duel ends by itself as soon as it's decided. Check the result and confirm it, or undo the last turn if something was entered wrong. Cancel stops the duel without a result.")),
		new(L.T("Best of"), L.T("In a best-of duel each round's best player wins that round, and every round starts again from the starting score. Under each name you see the rounds won."))
	]);

	/// <summary>The help of the Pub Games Pass page.</summary>
	public static PageHelp Subscription => new(L.T("Pub Games Pass"),
	[
		new(L.T("What it is"), L.T("The Pub Games Pass unlocks every paid game for as long as it runs. Choose a week, a month or a year; each longer plan is cheaper per week. It renews by itself until you cancel it.")),
		new(L.T("Buying"), L.T("Tap a plan's price and choose Buy. Google Play's payment screen opens; once you've paid, the purchase is checked and every game is unlocked. The games you bought separately stay yours.")),
		new(L.T("Ending"), L.T("When your pass is about to end without renewing, or has just ended, the game list tells you once a day.")),
		new(L.T("Manage subscription"), L.T("Opens Google Play, where you cancel the pass or change how you pay. Cancelled, it still runs until the end of the time you paid for.")),
		new(L.T("Restore purchases"), L.T("On a new phone, or when a purchase didn't show up: checks everything you bought with Google Play on this account again and unlocks it."))
	]);

	/// <summary>The help of Profile.</summary>
	public static PageHelp Profile => new(L.T("Profile"),
	[
		new(L.T("Your picture and name"), L.T("Tap your picture or Change picture to choose a profile picture. Fill in your name and nickname and tap Save profile. Other players see your picture and nickname when they add you.")),
		new(L.T("Your player code"), L.T("Share this code with the people you play with. When they add you by it on Who's playing?, the games you play on their phone count in your stats. Copy puts it on the clipboard.")),
		new(L.T("Friend code"), L.T("Only the owner sees this. A friend who gets it can unlock one paid game for free: on the game's rules, Unlock to play > I have a code. Not the Pub Games Pass. A code works for 15 minutes; below it you see until when, and when it runs out the next one appears by itself. New code makes a new one right away that works for 30 minutes - so a friend has time to type it in - and the old code stops working at once. There's only ever one code that works."))
	]);

	/// <summary>The help of sending feedback.</summary>
	public static PageHelp Feedback => new(L.T("Feedback"),
	[
		new(L.T("Sending feedback"), L.T("First choose what it's about: a bug, a typo, a mistake in a game's rules, scoring that doesn't add up, an idea, or something else. Then say whether it's about a game and choose which. Last, explain it - that's required, at least a few words, so the admins know what to look at. Send feedback sends it to the admins.")),
		new(L.T("Your feedback"), L.T("Below the form you see your earlier feedback, with where the admins are: New, Being worked on, Resolved or Won't be changed - and their reply if they wrote one."))
	]);

	/// <summary>The help of Blocked players.</summary>
	public static PageHelp BlockedPlayers => new(L.T("Blocked players"),
	[
		new(L.T("Who's here"), L.T("Players you blocked when deleting them from your saved players (hold a player added by code on Who's playing?). They can't add you by your code, and you're not in their saved players.")),
		new(L.T("Unblock"), L.T("Unblock lets them add you by your code again. They don't come back in your saved players by themselves: add them again by their code if you want."))
	]);

	/// <summary>The help of handling feedback (admins).</summary>
	public static PageHelp AdminFeedback => new(L.T("Feedback"),
	[
		new(L.T("What arrives here"), L.T("Feedback from players: what it's about, which game (if any), who sent it and their explanation. Only admins see it.")),
		new(L.T("Show"), L.T("Open shows what still needs attention (New and Being worked on); choose another status or All to see the rest. Pull down to refresh.")),
		new(L.T("Handle"), L.T("Mark feedback as Being worked on, Resolved or Won't be changed, or only reply. You can add a reply each time; the sender sees the status and your reply under Settings > Feedback.")),
		new(L.T("Open game"), L.T("For feedback about a game: opens it in the editor, to fix the rules or scoring right away.")),
		new(L.T("Delete"), L.T("Removes the feedback for everyone, also for the sender."))
	]);

	/// <summary>The Inbox's help.</summary>
	public static PageHelp Inbox => new(L.T("Inbox"),
	[
		new(L.T("What arrives here"), L.T("Decisions on review requests, messages from your team, and translations to check in the language you use the app in.")),
		new(L.T("Translations to check"), L.T("Check translation opens the game on the Languages page, in your language. Correct what's needed, tap Translation is fine and save. The message goes away by itself once someone has checked that translation.")),
		new(L.T("Act on a message"), L.T("Reply answers the sender, Delete removes the message. After a rejected request, Edit and submit again opens it so you can change it and submit it again.")),
		new(L.T("New message"), L.T("+ New message writes to one or more people on the team - the owner, admins and moderators - or to whole groups at once. Everyone gets the message in their own Inbox."))
	]);

	/// <summary>The help of choosing who to message.</summary>
	public static PageHelp ChooseRecipient => new(L.T("Send a message to"),
	[
		new(L.T("Whole groups"), L.T("Everyone, All admins and All moderators tick everyone in that group; tap again to untick them. You can untick single people afterwards.")),
		new(L.T("Find people"), L.T("Search by name or email, and use All, Admins and Moderators to show only those. Tap people to tick or untick them; who's ticked stays ticked while you search. The button at the bottom says how many get the message.")),
		new(L.T("Flags"), L.T("The flag is the language someone uses the app in. Search for a language, like French, to find everyone who uses it.")),
		new(L.T("Write"), L.T("Write message opens the message. Everyone ticked gets it in their own Inbox."))
	]);

	/// <summary>The help of the requests waiting for review.</summary>
	public static PageHelp ReviewRequests => new(L.T("Requests"),
	[
		new(L.T("What's here"), L.T("Moderators' new games, changes and deletions, and what they propose for categories and main categories: new ones, another name, another main category or deletion. Nothing changes for players until you approve. New categories also appear here, so you can add games to them.")),
		new(L.T("Review"), L.T("Tap a request to see what would change and approve or reject it."))
	]);

	/// <summary>The help of reviewing one request.</summary>
	public static PageHelp ReviewRequest => new(L.T("Review a request"),
	[
		new(L.T("What changes"), L.T("For changes to a game you first see only what changes, sentence by sentence and with the flag of the language: Before, and below it After - in the original and in every translation. View the whole game shows the game as players would see it (Original and With changes) and the side-by-side comparison of a translation. For a new category you see which main category it's proposed for.")),
		new(L.T("Translations"), L.T("When a request corrects or checks translations, or makes another language the original, Translations lists each language with its flag and who on the team uses the app in it. Compare the translation puts the original and the translation next to each other, piece by piece, each with its flag: someone who speaks both can judge it. In the list of requests, the flags show which languages a request touches.")),
		new(L.T("Decide"), L.T("Approve makes it live for players. Reject asks for a reason, which goes to the moderator's Inbox so they can fix it and submit again. Moderators looking at someone else's request can't decide: they see what's waiting and can send that moderator a message."))
	]);

	/// <summary>The Team page's help.</summary>
	public static PageHelp Team => new(L.T("Team"),
	[
		new(L.T("Add a moderator"), L.T("Fill in their Google account email and tap Add moderator. They get an email explaining how it works, and get the Admin tab when they sign in with that account.")),
		new(L.T("Finding someone"), L.T("Search by name or email. All, Admins and Moderators choose who's shown. Admins and moderators are in two separate lists, each in alphabetical order.")),
		new(L.T("Flags"), L.T("The flag next to someone is the language they use the app in. Search for a language, like French, to find who can check a game in it, and ask them from the Inbox. A flag shows once that person has opened the app after this update.")),
		new(L.T("Moderators"), L.T("Moderators propose new games, changes, deletions and new (main) categories; you approve or reject them on the Requests page.")),
		new(L.T("Admins"), L.T("Only the owner of the app can make someone admin, turn an admin back into a moderator (Make moderator) or remove an admin. Other admins add and remove moderators; for a change to an admin, send the owner a message from the Inbox. The owner can't be removed.")),
		new(L.T("Remove"), L.T("Remove takes someone off the team. You pick a reason first, which they receive by email.")),
		new(L.T("Free games"), L.T("Admins and moderators play every paid game for free while they're on the team. Games they bought themselves stay theirs after that. The Pub Games Pass isn't included."))
	]);

	/// <summary>The Settings page's help, with what the user's role means.</summary>
	public static PageHelp Account(TeamMember? me) => new(L.T("Account"),
	[
		new(L.T("Settings"), L.T("Your account's main page, with a row per subject: tap one to open it.")),
		new(L.T("Profile"), L.T("Your picture, name, nickname and player code.")),
		new(L.T("My stats"), L.T("How many finished games you played, won, lost and tied, with percentages, over all games. Tap it to see them per game. Winner and ranking games: you won if you finished 1st on your own, tied if you shared 1st place, and lost otherwise. Loser games: you lost if you were the only loser, tied if you shared the most losses, and won otherwise.")),
		new(L.T("Blocked players"), L.T("Who you blocked when deleting a player added by code. They can't add you by your code; unblock them here.")),
		new(L.T("Feedback"), L.T("Tell the admins about a bug, a typo, a mistake in a game's rules or an idea, and see what they did with your earlier feedback.")),
		new(L.T("Signing out"), L.T("Sign out to switch Google accounts. The app asks first.")),
		new(L.T("Delete account"), L.T("Deletes your account for good: your profile, player code, stats, bought games, feedback, blocks and the players and games on this phone. First you see what goes, then you type DELETE to confirm. A running Pub Games Pass is billed by Google Play: cancel it there first.")),
		.. me switch
		{
			null => Array.Empty<HelpSection>(),
			{ IsAdmin: true } => [new HelpSection(L.T("Admin"), L.T("Full rights: publish, edit and delete games, manage categories and main categories directly, review moderators' requests and manage the team."))],
			_ => [new HelpSection(L.T("Moderator"), L.T("You propose new games, changes, deletions and new (main) categories as review requests. An admin approves them before players see anything; the decision arrives in your Inbox."))]
		}
	]);

	/// <summary>The rules of a game while playing it: its scoring, how results are noted, and its own "How to play".</summary>
	public static PageHelp GameRules(PubGame game)
	{
		var settings = game.ScoringSettings;
		var sections = new List<HelpSection>
		{
			new(L.T("Scoring"), ScoringRules.For(game).Explanation),
			new(L.T("Result of each subgame"), $"{ResultModeOption.LabelFor(settings.ResultMode)}: {settings.ScoreDirectionLabel}."),
			new(L.T("Ties"), settings.AllowTies ? L.T("Ties are allowed: tied players share the result.") : TiesText(settings, settings.ResultMode))
		};
		if (settings.TurnSummary.Length > 0)
			sections.Add(new(L.T("Turns and the end"), char.ToUpperInvariant(settings.TurnSummary[0]) + settings.TurnSummary[1..] + "."));
		var blocks = string.IsNullOrWhiteSpace(game.RulesText)
			? [new RulesBlock(L.T("No rules written for this game yet."), null)]
			: RulesBlock.Parse(game.RulesText);
		return new(L.F("Rules: {0}", game.Name), sections, blocks);
	}
}
