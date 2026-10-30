# Main quest and quest items (placeholder story)

A first pass at the main story and the "quest items" it needs, so the data and code can be built ahead of the writing. **The real narrative, characters and quest list will replace this**; the structure (a main chain, side quests, quest items, rewards that unlock recipes, companions, perks, furniture and night) is what carries over. Everything here is placeholder content in [`Design/design-data.json`](../Design/design-data.json): rename, rewrite and re-tune freely.

## The idea: The lost recipe

You've taken over the old village cafe. **Ruru** (a morepork, the old owner) once made a famous **kawakawa scroll**, but the wind tore her recipe card into **four scraps** and scattered them. Find all four and bring them to Ruru, and she pieces the recipe back together for you. Finishing the main quests is the GDD's win condition; free play continues afterwards.

Each scrap comes from a different part of the game, so the main quest pulls the player through every system once:

| Scrap | Where it comes from | System it teaches |
| --- | --- | --- |
| 1 (top) | Found **foraging** in the bush, only while *The lost recipe* is active | Expeditions, companions, focus |
| 2 (left) | **Weka** "found" it and swaps it for a shiny button (*A fair trade*) | Treasures, trading, Weka's shiny bonus |
| 3 (right) | **Snail** gives it after you cook two worm pies (*A very hungry snail*); Kunekune pig joins as a companion | Cooking at the cafe |
| 4 (bottom) | **Wētā** gives it for fixing its hollow tree with 4 bark + 2 silk (*Hollow home*); Spider joins as a companion | Materials, Kea's bark bonus |

Quest chain:

The opening is a short tutorial chain, one system per step (placeholder text until the real opening is written):

0. **Hello, Ruru** (main, Ruru): talk to Ruru. Teaches the *leaf salad* recipe, since you start knowing none.
0. **Leaves for the salad** (main, Ruru): have 3 leaves. Teaches foraging: Ruru sends you down the path into the bush. **Luck protection:** while it's under way, a trip always finds the leaves you still need (the quest is set as *Luck Protected Quest* on the GameDatabase; the trip's seed is picked so it does, and the logged seed still reproduces it in the sandbox). Rewards 5 coins.
1. **A cafe of my own** (main, Ruru): serve a leaf salad. Teaches the menu board and closing for the day (the cafe cooks and sells at closing time). Rewards 20 coins and the *worm pie* recipe (the snail's quest needs it). Unlocks everything below.
1. **Somewhere to sit** (main, Ruru): talk to Ruru. She gives you two chairs, a table and a fern from her shed (the cafe starts empty).
1. **Open for guests** (main, Ruru): place 4 things in build mode. Teaches building, and that guests sit on chairs you've placed and come more often to a cosier cafe. Rewards 20 coins. Visitors only start dropping by after this.
1. **After dark** (main, Ruru): serve 3 dishes. Unlocks night time: after closing, a calm night with nocturnal guests and one night forage trip (see [NIGHT.md](NIGHT.md)).
2. **The lost recipe** (main, Ruru): hand in scraps 1-4. Rewards the *kawakawa scroll* recipe, 50 coins and the *Keen eye* perk. Completing it wins the game.
3. Side quests 2-4 above, each available after quest 1, in any order.

## How it's built

There's no special "quest inventory" system: quest items are ordinary items with two flags, so every existing rule (save, economy, validation, sandbox) already handles them.

- **`KeyItem` category:** recipe scraps, letters, keys and so on. The validator stops key items being used as recipe ingredients, and the UI should hide sell buttons for them.
- **`unique` flag:** at most one can be owned. Once it's collected (owned now, or handed in for a quest, tracked in `GameState.handedIn`) it never drops from foraging or comes from a reward again. Gaining a unique item you already have does nothing, so duplicate rewards are harmless.
- **Can't be lost:** key items can only leave the inventory through a quest hand-in. Removing, selling or cooking them is refused, so a scrap can never vanish before Ruru gets it, which would otherwise softlock the main quest (scraps 2-4 are one-off rewards).
- **Quest-gated forage entries:** a forage table entry can name `requiresQuest`; it only drops while that quest is active. Scrap 1 uses this, so it can't turn up before the story introduces it.
- **Recipe scraps:** `RecipeDefinition.scraps` lists the key items that make up a recipe. `CafeService.GetScrapProgress` gives "2 of 4 scraps found" for the recipe book, even before the recipe is unlocked.
- **Putting them together:** a quest with `DeliverItems` objectives for each scrap and an `UnlockRecipe` reward. `QuestService.Claim` takes the scraps and unlocks the recipe in one transaction, and only once.

All of this is covered by EditMode tests (`QuestAndCafeTests.RecipeScraps_DeliveredTogetherUnlockTheRecipe`, `ForagingTests.QuestScrap_*`, `ForagingTests.CollectedScrap_*`, `EconomyTests.UniqueItems_*`, `EconomyTests.KeyItems_*`).

## UI suggestions (not built)

- **Satchel tab** in the inventory for `KeyItem`s, separate from the larder (ingredients), materials and treasures. Quest items show which quest they're for.
- **Recipe book** shows locked recipes that have scraps as a torn card that fills in as scraps are found ("2 / 4"), which gives the player a visible goal between quests.
- **Finding a scrap** gets its own moment on the expedition results screen (it's flagged as a rare find already), and maybe a line from Ruru in the mailbox.
- **Quest log** lists the main quest first, with each objective's progress from `QuestService.GetProgress`.

## Questions for you

1. Is "pieces of Ruru's recipe" the main story you want, or a first chapter? The quest system supports longer chains (prerequisites), e.g. a second recipe or a cafe-reopening finale.
2. Should a found scrap be shown to the player before the quest that needs it starts? Right now scrap 1 only drops during the quest, but scraps 2-4 come from side quests that open after quest 1, so they can arrive early. That's harmless (they're held until handed in), but it could spoil the reveal.
3. Ruru, Weka, Snail and Wētā are placeholders from the Miro creature list. Who should the main characters be, and is Possum the "enemy" from the GDD's "friends and enemies"?
4. Should key items appear in the cafe menu or trading at all? Currently they can't be cooked; nothing stops a future trade feature from offering them, so decide that when trading exists.
