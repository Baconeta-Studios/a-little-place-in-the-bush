# Foraging rules (spec)

How an expedition turns a setup (companion, perk, focus) into items. This is the single source of truth for the rules: the C# `ForagingService` (`Assets/Scripts/Core/Foraging`) and the web sandbox (`Tools/ForageSandbox`) both follow it step by step, so **the same design data and the same seed give the same result in both**. If you change the rules, change this file, the C# and the sandbox together.

Design data (items, companions, perks, focuses, forage tables) lives in [`Design/design-data.json`](../Design/design-data.json). The sandbox edits and exports it; **Tools > Bush > Import Design Data** turns it into the Unity assets.

## Inputs

| Input | Notes |
| --- | --- |
| Forage table | Where you're foraging. The jam has one (`table.bush`). |
| Companion | Optional. Brings its own modifiers. |
| Perks | Zero or more (the jam UI offers one slot). Each brings modifiers. |
| Focus | Optional. Multiplies the chance of an item once if it has any of the focus's tags. |
| Time of day | `Day` or `Night`. Filters entries that only appear at one time. |
| Context | Which quests are active, and which unique items the player has collected (has now, or handed in for a quest). |
| Seed | A 32-bit unsigned integer for the random generator. |

## Modifiers

Companions and perks each have a list of modifiers. A focus isn't a modifier: it's applied once per item in step 3, so an item with two of the focus's tags still gets the multiplier only once.

| Type | Fields | Effect |
| --- | --- | --- |
| `TagWeight` | tag, value | Entries whose item has `tag` get their weight multiplied by `value` ("more bark"). |
| `TagQuantity` | tag, value | Each roll of an item with `tag` gives `floor(value)` extra (summed over modifiers). |
| `RarityWeight` | rarity, value | Entries whose item has that rarity get their weight multiplied by `value` ("luckier finds"). |
| `ExtraRolls` | value | `floor(value)` extra rolls (summed over modifiers). |
| `BonusRoll` | tag, value | After the normal rolls, a `value` (0-1) chance of one extra roll from only the items with `tag` ("chance of a shiny"). |

Modifiers are applied in this order: companion, then perks in order; the focus comes last. Weight modifiers multiply, so order doesn't change the result, but sums are taken in this order to keep floating-point results identical.

## Random numbers

Both implementations use **mulberry32**, seeded with the 32-bit seed. Each call returns a double in `[0, 1)`:

```
state = (state + 0x6D2B79F5) mod 2^32
t = state
t = (t XOR (t >> 15)) * (t OR 1)            (mod 2^32)
t = t XOR (t + (t XOR (t >> 7)) * (t OR 61)) (mod 2^32)
return (t XOR (t >> 14)) / 4294967296
```

## Resolving an expedition

1. **Collect modifiers:** companion, then each perk.
2. **Eligible entries:** go through the table's entries in order and keep an entry if:
   - its `time` is `Any` or matches the time of day,
   - its `requiresQuest` is empty or that quest is active,
   - its `requiresCompanion` is empty or that companion is on the expedition,
   - its item isn't `unique`, or the player hasn't collected it (doesn't have it now and hasn't handed it in).
3. **Effective weight:** `w = entry.weight`, then multiply by every matching `TagWeight` and `RarityWeight` modifier value, in modifier order, then by the focus multiplier if the item has any of the focus's tags. Entries with `w <= 0` can't be picked.
4. **Number of rolls:** `table.rolls + sum(floor(ExtraRolls values))`, at least 1.
5. **Each roll**, using exactly two random numbers:
   - Pool = eligible entries with `w > 0`, minus unique items already found on this expedition. If the pool is empty, the roll finds nothing and uses no random numbers.
   - `total` = sum of pool weights, added in entry order. `r = next() * total`. Walk the pool in order, doing `r -= w`, and pick the first entry where `r < 0` (if rounding means none is picked, take the last one).
   - Quantity = `min + floor(next() * (max - min + 1))`, plus the summed `TagQuantity` bonus for the item's tags. Unique items always give exactly 1.
   - Record the roll: item, quantity, its chance (`w / total`), and that it wasn't a bonus roll.
6. **Bonus rolls:** for each `BonusRoll` modifier in order, draw `x = next()`. If `x < value`, roll once (step 5's two random numbers) from the pool restricted to items with that tag, and mark it as a bonus. If the restricted pool is empty, nothing is found (and the pick's random numbers aren't drawn).
7. **Results:** merge rolls into stacks by item, in order of first appearance. Items with rarity `Rare` or `Legendary` are flagged as rare finds.

## After an expedition (not random)

- When the player presses Go, the game copies the setup, resolves it with the seed and stores the result in the save (`ForagingService.Begin`). The results screen shows that stored result, and `Claim` pays exactly that, once: it clears the pending expedition in the same step. Pressing Go twice, closing the results twice or refreshing mid-expedition can't duplicate or lose rewards.
- Items with `teachesRecipe` are rare encounters (e.g. meeting Kiwi at night). When one is paid out, its recipe is learned on the spot, if it isn't known already, and the item stays in the satchel as a keepsake. They're unique, so each one happens once. `Claim` reports the recipes learned so the results screen can celebrate them.
- `Begin` refuses companions and perks that aren't unlocked (cheats can force it) and ignores a perk listed twice.
- The companion's trip count goes up by 1. If the companion has `teachesPerk` and the count reaches `tripsToTeach`, the player learns that perk (shown on the results screen, per the GDD).

## Odds preview

For the setup screen ("show what each choice does before Go"), the preview reports for each eligible entry:
- chance per roll: `w / total`,
- chance of finding at least one on a trip: `1 - (1 - p)^rolls` (ignores bonus rolls and uniqueness, so it's a slight underestimate; only entries with `w > 0` count towards the total),
- expected quantity per trip: `(rolls * p + bonus finds) * average quantity`, where bonus finds = for each `BonusRoll` whose tag the item has, `chance * w / (total weight of eligible items with that tag)`, and average quantity = `(min + max) / 2 + TagQuantity bonus`. For unique items the expected finds are capped at 1 and the average quantity is 1.

The sandbox also runs thousands of simulated trips for exact averages.

## In the game

`ForagePopup` (`Assets/Scripts/UI/ForagePopup.cs`), opened by walking to a **ForageSpot** in the world (a path into the bush at the side of the cafe). A HUD button is still available for testing: tick **Forage Button** on `GameHud`. It's deliberately simple:

- **Setup:** the place (the first forage table), what to **look out for** (a focus, or Anything), who to **take along** (companions you've befriended who are about at this time of day and not tired) and **perks** (as many as your perk slots). The lines above each row say what the chosen companion and perks do. "You might find" shows the likeliest items for that setup, without odds, and leaves out Rare and Legendary things so they stay a surprise. Items the focus boosts are marked "more".
- **The focus is taught** by Ruru's *Leaves for the salad* (the first trip: "choose Greens and you'll find far more leaves"), and a tip on the screen says what it does until you pick one.
- **Trips:** three by day (they're the day's clock, see [CAFE.md](CAFE.md)) and one at night once night is unlocked (see [NIGHT.md](NIGHT.md)). Night trips find night things.
- **Go:** works out and saves the result straight away (and the cafe serves this part of the day while you're out, see [CAFE.md](CAFE.md)), then shows "Out foraging..." with **peek-a-boo**: one of three bushes rustles and something peeks out for a moment. Tap it to grab it as a **bonus find**; miss it and it's gone. It never takes anything away: the trip's own finds are fixed at Go. **Skip** goes straight to the results.
- **The peek-a-boo find** is one extra roll from the same pool (same focus, companion and perks), from the trip's seed mixed with `ForagingService.PeekABooSalt`, so the main result (and the sandbox's) is unchanged. One chance per trip, and it's saved, so a refresh can't give a second go.
- **Results:** what was found, rare finds and recipe encounters called out, then **Collect** (or closing) puts it all in the bag.
- **Companions** are free to take, once a day each, by day or night (see [GUESTS-AND-PROGRESS.md](GUESTS-AND-PROGRESS.md)). After 5 trips together they teach their perk.
- **Luck protection:** while Ruru's leaves quest is under way, the trip's seed is picked so it finds the leaves still needed. The logged seed still reproduces it.

**Debugging:** every trip logs its table, focus, time and seed to the Console, and the results show the seed in the editor and development builds. Put the same setup and seed into the web sandbox to get the same result. To open foraging from anything else, call `ForagePopup.Show()`.

## Decided

- **Keeping companions and perks special:** each companion goes once a day, perks are rare (mostly from 5 trips with a companion), and perk slots limit how many come along.
- **A foraging minigame:** peek-a-boo is in to try (a timing bar is the alternative); see [DECISIONS.md](DECISIONS.md).
