# Guests, companions, upgrades and cosiness (prototype)

Everything here is placeholder content and numbers, made up to get the systems playable. Tweak freely in [`Design/design-data.json`](../Design/design-data.json) (then **Tools > Bush > Import Design Data**) or on the assets.

## Guests

Every character has a **Visits** setting (`visits` in the data):

| Visits | When they're at the cafe |
| --- | --- |
| **Story** | While they give the current story quest. When the story moves on to someone else, they move on too, and the next story character arrives. (Ruru.) |
| **Random** | Drops by on some days: `visitChance` per day (0-1), from `firstDay`, and only once `afterQuest` is completed. (Everyone else, after the tutorial.) |
| **Always** | Always there. |

- Today's random visitors are rolled **once each morning**, so reloading doesn't change who's in.
- A **cosier cafe** brings visitors more often (up to twice as often), and someone with a **quest of yours under way** is more likely to come (+25%), so you can hand it in. Nobody's chance goes above 90%.
- Whoever gives the current story quest is always there.
- **In the scene:** place every character where they'd sit, with the **Npc** component. The ones who aren't visiting today are hidden (their art and click areas switch off).
- Side quests come from any character, so random visitors give side quests when they drop by.

Placeholder visitors: Weka (35%), Snail (30%), Wētā (25%), Kea (40%), Pekapeka (30%) and Kererū the recipe trader (30%), all after *A cafe of my own*.

## The recipe shop

A character can sell recipes: list them in `shop`, and each recipe's `price` is what it costs. Talking to them gives a **Recipes** reply. Placeholder: **Kererū** sells fern fritters (120), mushroom soup (150) and cricket croissants (180). Most recipes should come from quests, rare finds and combos, not shops.

## Companions and perks

- **You start with no companions.** Each is befriended through their **starter quest** (a quest with an `UnlockCompanion` reward), which comes before that character's other side quests.
- **Once a day each:** a companion who's been out is tired until tomorrow.
- **Trips cost coins** (`tripCost`): early companions are cheap, better ones dearer.
- **After 5 trips together** (`tripsToTeach`), they teach their **perk**: a weaker version of what they do, which you can take foraging without them.
- **Perks stack:** take as many as you have **perk slots** (1 to start; the *perk pouch* upgrade adds one).

| Companion | Starter quest | Trip cost | Teaches |
| --- | --- | --- | --- |
| Kea | *Kea's dare*: bring 2 bark | 5 | Bark know-how |
| Weka | *Show me your moves*: go foraging twice | 10 | Shiny sense |
| Pekapeka | *A bat's breakfast*: serve 2 dishes | 15 | Night sight |
| Kunekune | *A very hungry snail* (Snail's quest) | 30 | Truffle nose |
| Spider | *Hollow home* (Wētā's quest) | 30 | Silk sense |

Weka's trade quest now needs *Show me your moves* first.

**No perks are known at the start.** Besides the companions' perks, they come from quests: *Big basket* from *Leaves for the salad*, *Keen eye* from *A cafe of my own*, *Sweet tooth* from Weka's *A fair trade*, and *Early bird* from the snail's *A very hungry snail*.

## Cafe upgrades

**Upgrades** on the menu board. Each is bought once, for a lot of coins, pricier further down; some come in ladders (`requires`), and the next step only shows once the one before is bought.

| Upgrade | Effect | Price |
| --- | --- | --- |
| A second menu slot | +1 menu slot | 150 |
| A busier cafe | +1 dish sold a day | 200 |
| An early start | +1 forage trip (and part of the day) | 300 |
| A perk pouch | +1 perk slot | 350 |
| A third menu slot | +1 menu slot (after the second) | 450 |
| A bustling cafe | +1 dish a day (after a busier cafe) | 600 |
| The talk of the bush | +1 dish a day (after a bustling cafe) | 900 |

For scale: early on a day earns about 10-20 coins.

## Cosiness, lucky days and the sign

- Each placeable has a **cosiness** value and a **style** (`garden`, `rustic`, `glow`, `fancy`...). The cafe's cosiness is the total of what's placed, plus 1 for each *different* kind of thing, so variety counts.
- **More visitors:** random visitors' chances are multiplied by up to 2 as the cafe gets cosier (maxes out at 60).
- **Lucky days:** when you close, there's a chance (cosiness / 125, up to 40%) a happy guest leaves a tip of about a quarter of the day's takings. More kinds of special events can hook in here later.
- **The sign** on the tree by the door (**CafeSign**) says what kind of cafe you've made, from the biggest style (*A garden cafe*, *A rustic bush cafe*, *A lantern-lit cafe*, *A rather fancy cafe*, or *An eclectic little cafe* when it's an even mix), how cosy it is, and what that does.

## Setting it up in the cafe

1. **Tools > Bush > Import Design Data.** Brings in the new characters, quests, perks, upgrades and the cosiness values.
2. **The sign:** an object on the tree by the door with **CafeSign**, its **Layer** on **Interactable**, a **Box Collider 2D** (Is Trigger) and a **StandPoint** child on the floor in front.
3. **Characters:** for each of Kea, Weka, Pekapeka, Snail, Wētā and Kererū, an object like Ruru's (**Npc** with their NPC asset, Interactable layer, collider, StandPoint), placed where they'd sit. They'll only show on the days they visit.
4. Play with **Save: reset everything**. Cheats: *Guests: everyone drops by today*, *Guests: reroll today's visitors*, *Forage: befriend every companion and learn every perk*, *Bag: give 100 coins*.

## Code

- Rules (`Bush.Core`): `Visitors` (who's in), `CafeStyle` (cosiness, lucky days, the sign), `CafeService` (`BuyUpgrade`, the tip in `CloseDay`), `ForagingService` (tired companions, trip costs, perk slots), `DayRules` (upgrade bonuses). Tests: `GuestsAndProgressTests`.
- Unity: `Npc` (hides when away), `CafeSign`, `CafeSignPopup`, `UpgradesPopup`, `RecipeShopPopup`, `ForagePopup` (companion and perk choices), `UpgradeDefinition`.
