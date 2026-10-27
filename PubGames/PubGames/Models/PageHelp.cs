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
	public bool HasRules => RulesBlocks is { Count: > 0 };
}

/// <summary>
/// The "how to use this page" text of every page, kept together so the app
/// explains itself consistently. Role-specific pages get the admin or
/// moderator version.
/// </summary>
public static class Help
{
	public static PageHelp Login { get; } = new("Signing in",
	[
		new("Why sign in", "Sign in with your Google account. Your players, scores and unlocked games are saved to your account, so they're there on every phone you use."),
		new("Admins and moderators", "If an admin has added your Google account to the team, you get the Admin tab after signing in.")
	]);

	public static PageHelp Players { get; } = new("Who's playing?",
	[
		new("Add a new player", "Type a name and tap Add. The player is added to this game and saved for next time."),
		new("Add by player code", "Everyone with the app has a personal code under Account > Profile. Type someone's code and tap Add: they're added to this game and your saved players, with their profile picture. When you finish a game, the result counts in their stats - and in yours when you play yourself (you're one of the saved players)."),
		new("Your own name for a player", "✎ gives a player a name of your choice. For someone added by code it's only on your phone - their profile doesn't change."),
		new("Add a saved player", "Tap a name under Saved players. While they're playing they disappear from that list, so you only see who's left to choose."),
		new("Take a player out", "Tap a player's name in the list at the top. They go back to the saved players."),
		new("Delete a saved player", "Hold a saved player until \"Delete player?\" appears, then choose Yes or No. Earlier games keep their name. You can't delete yourself."),
		new("Delete a player added by code", "Holding a player you added by their code gives three choices. Only from my players: they're gone from your list only. Remove the link both ways: you're also removed from their saved players, the next time their app is online. Remove and block: both ways, and they can't add you by your code any more - Account > Blocked players shows who you blocked and unblocks them."),
		new("Seat order", "▲ and ▼ move a player up or down. The order at the top is the seat order on the scoreboard."),
		new("After a game", "When you quit a game, the players who were playing at the end are filled in again, including seat changes."),
		new("Saved games", "A game is saved on this phone as soon as someone has a score. Saved games (top right) shows them: tap an unfinished game to continue where you left off.")
	]);

	public static PageHelp SavedGames { get; } = new("Saved games",
	[
		new("Your games", "Every game played on this phone in which someone has a score, newest first. On two pages: Unfinished and Finished. Tap an unfinished game to continue it, or a finished one to see the scoreboard and the final result."),
		new("Delete games", "Tap Select, then tap the games to delete - or Select all / Deselect all - and tap Delete. Cancel stops selecting."),
		new("Keep saved games", "Each page has its own time for how long its games are kept: 1 week, 1 month (unless you change it), 6 months, 1 year, never delete, or don't save them at all - for example keep unfinished games but not finished ones. The time counts from the last score. Games are only kept on this phone; a finished game's result in the stats stays.")
	]);

	public static PageHelp GameLibrary { get; } = new("Choose a game",
	[
		new("Find a game", "Search by name, or tap Filter: set the number of players (all the way left: any number) and tap the categories you're looking for, per main category. Nothing there is required; the button shows how many filters are set and Clear filters shows everything again."),
		new("Start", "Under each game's name you see how many players it's for and its categories. Tap a game to read its rules and start playing. Every game is in the list, but it only opens when you have the right number of players for it: if not, the app says how many players to add or take out. Paid games show their price."),
		new("Paid games", "A paid game shows 🔒 and its price. Tap it to read its rules first; then Unlock to play lets you buy it once - it's yours on your account, to play with everyone at the table - use a code you were given (for that game only), or take the Pub Games Pass for every paid game. Going back costs nothing. Bought games and the pass also work on another phone where you sign in with the same account (Account > Pub Games Pass > Restore purchases).")
	]);

	public static PageHelp GameStart { get; } = new("Rules and start",
	[
		new("Read the rules", "The rules of the game are on this page, with pictures where the game has them. Under the name you see how many players the game is for: Any, or for example 2-6 or 2+."),
		new("Number of subgames", "Use − and + to choose how many subgames make up the game. It's 1 game unless you change it; choose more for, say, a best of 3. During the game you can still add subgames, and a tie at the end offers an extra one."),
		new("Start", "Skip rules and Start game both start the game with the chosen players and number of subgames.")
	]);

	public static PageHelp Scoreboard(ScoringType type, ScoringSettings settings) => new("Scoreboard",
	[
		new("Keeping score", type switch
		{
			ScoringType.PlusMinus => "Tap + or − next to a player to add or subtract the game's step.",
			ScoringType.ManualPlusMinus => "Type an amount in the box next to a player, then tap + to add it or − to subtract it. Only amounts the game's rules allow are accepted; if an amount isn't, the message at the top says what you can enter. Tap ⓘ and choose the rules to see them.",
			_ => "Tap + or − next to a player, or tap a score to type any amount."
		}),
		.. settings.FixedTurnOrder
			? [new HelpSection("Whose turn", "This game has a fixed input order: seat 1 first, then seat 2, and so on. The player whose turn it is has a thick dark border and ▶ instead of their seat number; only they can enter a score. " +
				(type == ScoringType.ManualPlusMinus
					? "Type the amount and tap + or −, or Next above the keyboard: the turn then passes on. No points? Type 0 - it has to be entered too."
					: "Tap + or − as often as needed, then Turn done to pass the turn on - also when the score stays the same."))]
			: Array.Empty<HelpSection>(),
		.. settings.EndScore is { } end
			? [new HelpSection("End of a subgame", settings.EndMode == EndMode.Manual
				? $"Once a player reaches {end}, the line above the cards says so. End the subgame yourselves with Next subgame or Finish game."
				: settings.EqualizingTurn
					? $"Once a player reaches {end}, the players after them in seat order get one last turn. When the turn is back at seat 1, the subgame ends by itself and you note its result."
					: $"Once a player reaches {end}, the subgame ends by itself and you note its result.")]
			: Array.Empty<HelpSection>(),
		.. type == ScoringType.ManualPlusMinus
			? [new HelpSection("Typing scores", "The screen moves up so the box you type in stays above the keyboard. ◀ Previous and Next ▶ above the keyboard (or the keyboard's own Next key) go to the other players' boxes; Next first adds the amount you typed - use a minus sign to subtract.")]
			: Array.Empty<HelpSection>(),
		new("Warning and limits", "A player at the game's warning score gets a dashed orange border and a ⚠ WARNING badge. At the maximum or minimum score the whole card turns dark blue with a ▲ MAX REACHED or ▼ MIN REACHED badge. Scores never go past the maximum or minimum."),
		new("Subgames", "The top shows which subgame you're playing. + subgame and − subgame change how many there are. Next subgame lets you note the result of this one and then starts the next, with everyone back at the starting score. On the last subgame the button is Finish game."),
		new("Stats", "Stats shows everyone's wins, losses or points so far. The winner stays a surprise until you finish the game."),
		new("Change players", "Move players to another seat, replace someone, add a player or take someone out - see the ⓘ on that page.")
	]);

	public static PageHelp SessionPlayers { get; } = new("Change players",
	[
		new("Seat order", "▲ and ▼ move a player to another seat. The seat order is the order on the scoreboard. Nobody's score changes."),
		new("Replace", "Someone else takes the seat, with everything that belongs to it: the score of this subgame and the results of earlier subgames."),
		new("Take out", "✕ takes a player out of the game. Their results so far stay on record; if they join again later they continue with their score."),
		new("Add a player", "Tap a saved player to add them at the last seat, or type a new name and tap Add. Tick the box to also save a new player for next time.")
	]);

	public static PageHelp RoundResult(ResultMode mode, bool lowestWins, ScoringSettings settings) => new("Subgame result",
	[
		new("Filled in for you", mode switch
		{
			ResultMode.Ranking => $"Everyone's place is filled in from the scores ({(lowestWins ? "lowest" : "highest")} score first). Players with the same score share a place.",
			ResultMode.Loser => $"The loser is picked from the scores: the {(lowestWins ? "highest" : "lowest")} score. Players with the same score lose together.",
			_ => $"The winner is picked from the scores: the {(lowestWins ? "lowest" : "highest")} score. Players with the same score share the win."
		}),
		new("Change it", mode == ResultMode.Ranking
			? "Use − and + next to a player to change their place (− moves towards 1st). Several players can share a place."
			: $"Tap a player to mark or unmark them as {(mode == ResultMode.Loser ? "loser" : "winner")}. Several players can be {(mode == ResultMode.Loser ? "losers" : "winners")}. A marked player has a dark blue card and a badge."),
		new("Confirm", "Start next subgame saves this result and puts every score back at the starting score. After Finish game, it saves the result and shows the final result; if the winner or loser is tied, you can first play an extra subgame to decide."),
		.. settings.AllowTies ? Array.Empty<HelpSection>() : [new HelpSection("No ties", TiesText(settings, mode))],
		new("Changed your mind", "Go back without confirming: nothing is saved and you continue the subgame.")
	]);

	/// <summary>How a game without ties plays off a tie, for players.</summary>
	private static string TiesText(ScoringSettings settings, ResultMode mode)
	{
		var when = settings.TieBreakMoment switch
		{
			TieBreakMoment.SubgamesOnly => "after a subgame",
			TieBreakMoment.FinalOnly => "in the final total",
			_ => "after a subgame and in the final total"
		};
		var which = settings.TieBreakScope == TieBreakScope.Every ? "Players who share a place"
			: mode == ResultMode.Loser ? "Players who share the loss" : "Players who share 1st place";
		var duel = settings.DuelSameScoring || settings.DuelSettings is null ? settings : settings.DuelSettings;
		return $"This game has no ties {when}. {which} play a duel by its own rules, shown on the duel screen: {duel.DuelEndSummary}. "
			+ "Take turns in the order shown: the player whose turn it is has a thick border. With + and − tap Turn done when your turn is over; with a typed amount the turn passes on by itself; when you type in your points, tap Turn done. Undo takes back the last turn. "
			+ "The duel ends by itself; check the result and confirm it - or undo the last turn if something was entered wrong. The duel decides one winner or loser, or places one after another so the points differ. "
			+ "After a duel for a subgame its outcome is filled in on the result right away: change it there if needed, then confirm.";
	}

	public static PageHelp Standings(bool isFinal) => isFinal
		? new("Final result",
		[
			new("The result", "The banner shows the winner(s), or the loser(s) in a game where you note losers. Below it you see everyone's place and total over all subgames. Equal totals share a place."),
			new("Done", "Tap Done and choose Play again to start the same game with the same players, seats and number of subgames, straight on the scoreboard. Choose Quit to go back to Who's playing?, with these players filled in.")
		])
		: new("Stats",
		[
			new("Totals so far", "Everyone's wins, losses or points over the subgames finished so far, in seat order. Places and the winner are only shown when you finish the game."),
			new("Back", "Tap Back to the game to continue.")
		]);

	public static PageHelp AdminLibrary(bool isModerator) => new("Library",
	[
		new(isModerator ? "Propose a game" : "New game", isModerator
			? "Propose a new game proposes a game to the admins. It's published once an admin approves it."
			: "New game creates a game; it's published for players straight away."),
		new("Find a game", "Search by game name, and/or tap Filter: set the number of players (all the way left: any number) and tap the categories you're looking for, per main category. Nothing there is required; the button shows how many filters are set and Clear filters shows everything again. Games are in alphabetical order."),
		new(isModerator ? "Change a game" : "Edit a game", isModerator
			? "Tap a game to propose changes or its deletion. Players keep seeing the current version until an admin approves. While another moderator's changes to a game are waiting for review, the game shows a 🔒 with their name and can't be changed by anyone else: tap it to see what they submitted - switch between Original and With changes - and to send them a message. It's open again once an admin has approved or rejected the changes, or they withdraw them. Your own waiting changes you can still change: submitting again replaces them for the admin."
			: "Tap a game to change or delete it. Changes are live for players straight away."),
		new("Still needs", "An orange line under a game says which main categories it has no category of yet. Edit the game and choose one for each; until then players can't find it with those filters. Once changes that fill them in have been submitted, the line says Waiting for review until an admin decides."),
		new("Messages and requests", isModerator
			? "Your requests waiting for review are listed at the top; tap one to change or withdraw it. Decisions arrive in your Inbox."
			: "A banner at the top shows requests waiting for your review and new messages; tap it to go there.")
	]);

	public static PageHelp CreateGame(bool isModerator, ScoringType type)
	{
		var sections = new List<HelpSection>
		{
			new("Three pages", "The form has three pages at the top: General (name, cover, price, number of players, categories), Scoring (scoring type, subgame, ties) and Rules (how to play). Players don't see these pages - they see the game as before."),
			new("Add your rules", "A game needs a name, at least one category per main category and a scoring type; everything else is optional. On the scoring page the scoring type is always there, at the top; tap + Add your rules to add Subgame or Ties. They always show in the same order - scoring type, Subgame, Ties - whatever order you add them in. The ✕ next to a part takes it off again and clears what you filled in. A part you don't add keeps its default: the winner is noted and the highest score wins, ties are allowed, the seats stay as they are, no end score."),
			new("Going back", "Going back with the arrow at the top or the phone's back button while you've changed something asks whether to save your changes, not save them, or keep editing."),
			new("Choices", "Every dropdown starts with \"Choose ...\": pick an option in each one. Saving tells you if a choice is still missing and opens the page it's on."),
			new("Name and cover", "Give the game a name. The cover image is shown square and as wide as the page, with the same centre crop as the small icon in the game lists - keep the important part in the middle."),
			new("Pictures", "For the cover and for pictures in the rules you choose: upload an image from your phone, take a photo, or create an image. Create an image opens an editor where you place arrows, text, cards, dice, hands, drinks and more - handy for explaining a game, and always in the same style. A picture made there can be changed later: tap the cover button again and choose Edit this image, or hold the picture in the rules. Only a cover can have a background colour; pictures in the rules are always on cream."),
			new("Price", "Choose Free or one of the fixed prices: €0.99 for a standard game, €1.99 for a big game. They're in line with the Pub Games Pass (a game costs as much as a week of it). The account that buys a game plays it with everyone at the table, for good; with the pass, every paid game is unlocked. A paid game is put up for sale in Google Play by itself when it's published.")
		};
		sections.Add(type == ScoringType.ManualPlusMinus
			? new("Scoring: Manual plus / minus", "On the scoreboard players type an amount next to a player and tap + or − to add or subtract it. Set the starting score, and use the switches for a maximum, a minimum and a warning score.")
			: new("Scoring: Plus / minus", "Every tap on + or − changes a player's score by the step. Set the starting score, and use the switches for a maximum, a minimum and a warning score."));
		sections.Add(new("Warning and limits", "On the scoreboard a player at the warning gets a dashed orange border and a ⚠ WARNING badge; at the maximum or minimum the card turns dark blue with a ▲ MAX / ▼ MIN badge. Scores never go past the maximum or minimum."));
		if (type == ScoringType.ManualPlusMinus)
			sections.Add(new("Rules for entered amounts", "+ Add rule adds a rule; ✕ removes it. Lower than 11 allows 10; lower than or equal to 11 also allows 11; between includes both numbers. Amounts may be negative. Tap AND / OR above a rule to switch how it links to the rule above: OR goes first, so rules linked with OR form a group where one must be true, and AND joins groups that must all be true. The bold line below the rules shows exactly what's allowed. No rules: any amount."));
		sections.Add(new("Subgame", "Everything in it is optional. Result of each subgame: how it's noted - the winner(s), everyone's place, or the loser(s) - and which score wins, loses or is 1st; after each subgame the app fills this in from the scores and the players can change it. Not chosen: the winner is noted and the highest score wins. The players choose how many subgames they play (1 unless they change it). Who starts the next subgame: the seats turn so that player sits in seat 1 - everyone keeps their neighbours. Left of a player is the next seat, right of a player the seat before. Fixed input order: scores are entered seat by seat and everyone has to enter one, also 0. A subgame ends at score: once a player reaches it the subgame ends, automatically or when the players end it. With an automatic end and a fixed input order you can add an equalizing turn: the players after the one who reached it still get their turn in that round."));
		sections.Add(new("Ties", "Ticked \"Ties are allowed\": tied players share the win, the loss or the place, and get the same points. Unticked, a tie is played off in a duel. Choose when: after every subgame, after the final total, or both. Choose which ties: only a shared 1st place (in a loser game: a shared loss), or every shared place. The duel always has its own rules, filled in the same way as the game's: its scoring type and numbers and which score wins. Copy the game's rules fills those in with the game's, so you only change what's different - for example a duel to 10 while the game goes to 100. Then choose how the duel ends: a number of turns each (then the best score wins; a tie plays another turn), first to reach a score (rounds are played until someone has reached it, so everyone had as many turns; the best score wins), or best of a number of rounds (each round's best player wins it; the first to win more than half wins). With turns or a score you can also set a lead needed to win, like 2 points ahead: until then another turn is played. With Type in the score the players play the duel their own way and type in everyone's score. Explain the duel itself in How to play."));
		sections.Add(new("Number of players", "Switch on a minimum and/or a maximum and fill in the number. Only a maximum of 3 means 1, 2 or 3 players; only a minimum of 2 means 2 or more. Both off: any number, shown as Any. Players can filter games by how many they are, and can only start a game with a number of players it's made for."));
		sections.Add(new("Categories", "The categories are listed under their main categories (Play style, Duration, ...). Open each main category and tap at least one category in it, so players can find the game; tap more if several fit. Tap again to unselect."));
		sections.Add(new("How to play", "Write the rules as one text. To add a picture, tap in the text where it should go - also in the middle of a sentence - then tap Add picture at the cursor. The text is split there and the picture gets its own place, with an empty line above and below, on a cream background. Hold a picture to move it (tap in the text where it should go, then Place here), to remove it, or - if you made it with Create an image - to edit it."));
		sections.Add(new("Checks before saving", "Saving checks that everything fits together: for example the starting score between the minimum and maximum, the warning within them, and amount rules that allow at least one useful amount. The message above the button says what to fix."));
		sections.Add(isModerator
			? new("Submitting", "As a moderator you submit for review: an admin decides and you get the decision in your Inbox. Players keep seeing the current version until then. Withdraw request takes back a request that's still waiting; Request deletion asks an admin to delete the game.")
			: new("Saving", "Publish game / Save changes makes it live for every player straight away. Delete game removes it from every player's library."));
		return new("New or edit game", sections);
	}

	public static PageHelp ImageEditor(bool cover) => new("Create image",
	[
		new("Add things", "The buttons at the top add something to the image: arrows, text, cards, dice, hands, drinks and more. A table goes under everything else, to show where the game is played."),
		new("Place them", "Tap something on the image to select it, then drag it. With two fingers you make it bigger or smaller and turn it. Below the image: Place across and Place up and down line it up with the edges, the middle or the quarter lines; Across and Down set its exact place; Size and Turn set its exact size and angle - type a number to make two things exactly alike."),
		new("Align and exact place", "The dotted frame around a selected item fits tightly around what's drawn. The stripe icons choose which side of that frame Across and Down give: left, centre or right, and top, middle or bottom. Across always counts from the left edge of the drawing area, Down from the top edge. Left: Across is where the item's left side is. Centre: where its middle is. Right: where its right side is. The same for Down with top, middle and bottom. So Left with Across 0 puts the item against the left edge, and two items with the same alignment and number line up exactly. Changing the alignment leaves the item where it is: only the number changes."),
		new("Growing from the aligned side", "Making something bigger or smaller keeps its aligned side where it is: aligned left, it grows to the right; aligned right, to the left; centred, the same in every direction. That goes for Size, pinching with two fingers, an arrow's length or arms, typing text and every other choice that changes the size. Turning keeps the aligned side too. New items get the alignment you chose last."),
		new("Choose what it shows", "With something selected you see its choices: a card's suit and value or a whole deck, the number on a die or a dice cup, heads or tails, how full a drink is, which fingers a hand holds up (Neutral is a flat hand; Choose fingers starts as a fist)."),
		new("Arrows", "Length and Thickness make an arrow longer or thicker; turned upright, a longer arrow is higher. Bent gives the arrow a corner: Angle sets how far arm 2 is turned from arm 1, and Arm 1 and Arm 2 set how long each arm is. A bent arrow turns around its corner. With Both ways each arm gets a head, like the two hands of a clock: set the angle and the arm lengths, then turn it with Turn to show any time."),
		new("Order and copies", "To front and To back decide what lies on top where things overlap. Copy makes another one just like it. Remove from image takes it away."),
		new("Undo", "Undo takes back your last change. Tap it again to go back further."),
		cover
			? new("Shape and background", "With nothing selected you choose the image's shape and its background colour. Covers are shown square.")
			: new("Shape and background", "With nothing selected you choose the image's shape. Pictures in the rules are always on cream, so it's clear in the text where they begin and end."),
		new("Done", "Use this image puts it in the game. You can change it again later: it opens here just as you left it.")
	]);

	public static PageHelp Categories(bool isAdmin) => new("Categories",
	[
		new("What categories are for", "Categories are listed under main categories, like Play style or Duration. Every game has at least one category from each main category; players filter games by category."),
		isAdmin
			? new("Add and fill", "+ New category adds a category: give it a name and choose its main category. A name can be used once in each main category. ▲ and ▼ on a category move it up or down within its main category. Tap a category to choose which games are in it, rename it, move it to another main category, or delete it. Deleting takes it off every game.")
			: new("Propose", "+ Propose a new category sends it to an admin for review: give it a name and choose its main category. It appears once an admin approves it. Tap a category to propose another name, another main category, or its deletion."),
		isAdmin
			? new("Main categories", "+ New main category adds one. As soon as it has categories, a game can only be saved with at least one of them - existing games get theirs the next time they're edited. Tap a main category to add a category to it, rename it or delete it; deleting also deletes its categories and takes them off every game. Main categories are in alphabetical order, with new ones at the bottom. The order you give the categories is the order everyone sees: in the filters and when creating a game.")
			: new("Main categories", "+ Propose a new main category sends it to an admin for review. Tap a main category to propose a category in it, another name, or its deletion. Nothing changes until an admin approves; the decision arrives in your Inbox.")
	]);

	public static PageHelp MyStats { get; } = new("My stats per game",
	[
		new("Find a game", "Every game you finished at least once. Search by name, or tap Filter: set the number of players (all the way left: any number) and tap the categories you're looking for, per main category. Nothing there is required; the button shows how many filters are set and Clear filters shows everything again."),
		new("Its stats", "Tap a game to see how often you played, won, lost and tied it, and every time you played it.")
	]);

	public static PageHelp GameStats { get; } = new("Stats of a game",
	[
		new("The numbers", "How many times you finished this game, and how often you won, lost or tied, with percentages."),
		new("Won, lost, tied", "Winner and ranking games: you won if you finished 1st on your own, tied if you shared 1st place, and lost otherwise. Loser games: you lost if you were the only loser, tied if you shared the most losses, and won otherwise."),
		new("History", "Below the numbers you see every time you played it, newest first, with how it ended.")
	]);

	public static PageHelp CategoryGames { get; } = new("Games in a category",
	[
		new("Choose games", "Tick every game that belongs in this category - tap the tick box or the game - then tap Save games in category. Players can then find these games by choosing this category."),
		new("Find a game", "Search by game name, and/or tap Filter: set the number of players (all the way left: any number) and tap the categories you're looking for, per main category. Nothing there is required; the button shows how many filters are set and Clear filters shows everything again. Ticks you set stay when you search for another game.")
	]);

	/// <param name="thisDuel">How this duel is scored, who wins and how it ends (DuelViewModel.Explanation).</param>
	public static PageHelp Duel(string thisDuel) => new("Duel",
	[
		new("This duel", thisDuel),
		new("Taking turns", "Play in the order shown: the player whose turn it is has a thick border, and only they can enter a score. With + and − tap Turn done when your turn is over. With a typed amount, + or − ends the turn by itself. When you type in your points, tap Turn done (empty counts as 0)."),
		new("Undo", "↶ Undo takes back what was entered in this turn, or else the turn before it."),
		new("The end", "The duel ends by itself as soon as it's decided. Check the result and confirm it, or undo the last turn if something was entered wrong. Cancel stops the duel without a result."),
		new("Best of", "In a best-of duel each round's best player wins that round, and every round starts again from the starting score. Under each name you see the rounds won.")
	]);

	public static PageHelp Subscription { get; } = new("Pub Games Pass",
	[
		new("What it is", "The Pub Games Pass unlocks every paid game for as long as it runs. Choose a week, a month or a year; each longer plan is cheaper per week. It renews by itself until you cancel it."),
		new("Buying", "Tap a plan's price and choose Buy. Google Play's payment screen opens; once you've paid, the purchase is checked and every game is unlocked. The games you bought separately stay yours."),
		new("Ending", "When your pass is about to end without renewing, or has just ended, the game list tells you once a day."),
		new("Manage subscription", "Opens Google Play, where you cancel the pass or change how you pay. Cancelled, it still runs until the end of the time you paid for."),
		new("Restore purchases", "On a new phone, or when a purchase didn't show up: checks everything you bought with Google Play on this account again and unlocks it.")
	]);

	public static PageHelp Profile { get; } = new("Profile",
	[
		new("Your picture and name", "Tap your picture or Change picture to choose a profile picture. Fill in your name and nickname and tap Save profile. Other players see your picture and nickname when they add you."),
		new("Your player code", "Share this code with the people you play with. When they add you by it on Who's playing?, the games you play on their phone count in your stats. Copy puts it on the clipboard."),
		new("Friend code", "Only the owner sees this. A friend who gets it can unlock one paid game for free: on the game's rules, Unlock to play > I have a code. Not the Pub Games Pass. A code works for 15 minutes; below it you see until when, and when it runs out the next one appears by itself. New code makes a new one right away that works for 30 minutes - so a friend has time to type it in - and the old code stops working at once. There's only ever one code that works.")
	]);

	public static PageHelp Feedback { get; } = new("Feedback",
	[
		new("Sending feedback", "First choose what it's about: a bug, a typo, a mistake in a game's rules, scoring that doesn't add up, an idea, or something else. Then say whether it's about a game and choose which. Last, explain it - that's required, at least a few words, so the admins know what to look at. Send feedback sends it to the admins."),
		new("Your feedback", "Below the form you see your earlier feedback, with where the admins are: New, Being worked on, Resolved or Won't be changed - and their reply if they wrote one.")
	]);

	public static PageHelp BlockedPlayers { get; } = new("Blocked players",
	[
		new("Who's here", "Players you blocked when deleting them from your saved players (hold a player added by code on Who's playing?). They can't add you by your code, and you're not in their saved players."),
		new("Unblock", "Unblock lets them add you by your code again. They don't come back in your saved players by themselves: add them again by their code if you want.")
	]);

	public static PageHelp AdminFeedback { get; } = new("Feedback",
	[
		new("What arrives here", "Feedback from players: what it's about, which game (if any), who sent it and their explanation. Only admins see it."),
		new("Show", "Open shows what still needs attention (New and Being worked on); choose another status or All to see the rest. Pull down to refresh."),
		new("Handle", "Mark feedback as Being worked on, Resolved or Won't be changed, or only reply. You can add a reply each time; the sender sees the status and your reply under Settings > Feedback."),
		new("Open game", "For feedback about a game: opens it in the editor, to fix the rules or scoring right away."),
		new("Delete", "Removes the feedback for everyone, also for the sender.")
	]);

	public static PageHelp Inbox { get; } = new("Inbox",
	[
		new("What arrives here", "Decisions on review requests and messages from your team."),
		new("Act on a message", "Reply answers the sender, Delete removes the message. After a rejected request, Edit and submit again opens it so you can change it and submit it again."),
		new("New message", "+ New message writes to anyone on the team - the owner, an admin or a moderator - or to all admins. Search by name or email to find them.")
	]);

	public static PageHelp ChooseRecipient { get; } = new("Send a message to",
	[
		new("Find someone", "Search by name or email, then tap who the message is for. All admins sends it to everyone with admin rights.")
	]);

	public static PageHelp ReviewRequests { get; } = new("Requests",
	[
		new("What's here", "Moderators' new games, changes and deletions, and what they propose for categories and main categories: new ones, another name, another main category or deletion. Nothing changes for players until you approve. New categories also appear here, so you can add games to them."),
		new("Review", "Tap a request to see what would change and approve or reject it.")
	]);

	public static PageHelp ReviewRequest { get; } = new("Review a request",
	[
		new("What changes", "For changes to a game, the list shows exactly what would change. Below it is a preview laid out like the page players see: Original shows the game as it is now, With changes how it would become. For a new category you see which main category it's proposed for."),
		new("Decide", "Approve makes it live for players. Reject asks for a reason, which goes to the moderator's Inbox so they can fix it and submit again. Moderators looking at someone else's request can't decide: they see what's waiting and can send that moderator a message.")
	]);

	public static PageHelp Team { get; } = new("Team",
	[
		new("Add a moderator", "Fill in their Google account email and tap Add moderator. They get an email explaining how it works, and get the Admin tab when they sign in with that account."),
		new("Moderators", "Moderators propose new games, changes, deletions and new (main) categories; you approve or reject them on the Requests page."),
		new("Admins", "Only the owner of the app can make someone admin, turn an admin back into a moderator (Make moderator) or remove an admin. Other admins add and remove moderators; for a change to an admin, send the owner a message from the Inbox. The owner can't be removed."),
		new("Remove", "Remove takes someone off the team. You pick a reason first, which they receive by email.")
	]);

	public static PageHelp Account(TeamMember? me) => new("Account",
	[
		new("Settings", "Your account's main page, with a row per subject: tap one to open it."),
		new("Profile", "Your picture, name, nickname and player code."),
		new("My stats", "How many finished games you played, won, lost and tied, with percentages, over all games. Tap it to see them per game. Winner and ranking games: you won if you finished 1st on your own, tied if you shared 1st place, and lost otherwise. Loser games: you lost if you were the only loser, tied if you shared the most losses, and won otherwise."),
		new("Blocked players", "Who you blocked when deleting a player added by code. They can't add you by your code; unblock them here."),
		new("Feedback", "Tell the admins about a bug, a typo, a mistake in a game's rules or an idea, and see what they did with your earlier feedback."),
		new("Signing out", "Sign out to switch Google accounts. The app asks first."),
		.. me switch
		{
			null => Array.Empty<HelpSection>(),
			{ IsAdmin: true } => [new HelpSection("Admin", "Full rights: publish, edit and delete games, manage categories and main categories directly, review moderators' requests and manage the team.")],
			_ => [new HelpSection("Moderator", "You propose new games, changes, deletions and new (main) categories as review requests. An admin approves them before players see anything; the decision arrives in your Inbox.")]
		}
	]);

	/// <summary>The rules of a game while playing it: its scoring, how results are noted, and its own "How to play".</summary>
	public static PageHelp GameRules(PubGame game)
	{
		var settings = game.ScoringSettings;
		var sections = new List<HelpSection>
		{
			new("Scoring", ScoringRules.For(game).Explanation),
			new("Result of each subgame", $"{ResultModeOption.LabelFor(settings.ResultMode)}: {settings.ScoreDirectionLabel}."),
			new("Ties", settings.AllowTies ? "Ties are allowed: tied players share the result." : TiesText(settings, settings.ResultMode))
		};
		if (settings.TurnSummary.Length > 0)
			sections.Add(new("Turns and the end", char.ToUpperInvariant(settings.TurnSummary[0]) + settings.TurnSummary[1..] + "."));
		var blocks = string.IsNullOrWhiteSpace(game.RulesText)
			? [new RulesBlock("No rules written for this game yet.", null)]
			: RulesBlock.Parse(game.RulesText);
		return new($"Rules: {game.Name}", sections, blocks);
	}
}
