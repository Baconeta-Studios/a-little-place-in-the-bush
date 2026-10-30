# The cafe day

The core loop: **forage, put dishes on the menu, close for the day, get paid.** Deliberately simple so we can find out if it's fun. How the cafe sells through the day is still being looked at (see [DECISIONS.md](DECISIONS.md)).

## How it plays

- **No recipes at the start.** Ruru teaches leaf salad in the first conversation; others come from quests, rare forage encounters, or Kererū's recipe shop. The cheat **Cafe: learn every recipe** is there for testing.
- **One dish on the menu to start.** The board has **Upgrades** (more menu slots, more sales a day, an extra trip, a perk pouch), bought with coins.
- **The menu board** is behind the cafe door (`CafeSpot` opens `CafePopup`). Tap a recipe you know to see its ingredients (have/need), then **Add to menu** or **Take off menu**. Changes count straight away, any time of day. The board shows what each dish would sell if you closed now.
- **The forage trips are the day's clock.** A day has three parts, morning, midday and afternoon, and each forage trip uses one. When the last trip comes back it's evening.
- **Close for the day** (in the evening) sells a full day's dishes, shared evenly across the menu in order: each sale goes to the next dish that can still be made. Each sale uses that dish's ingredients and pays its coins, and counts as cooking it for quests. Ingredients are only used at this point, so changing the menu costs nothing. If you run out, the sale goes to another dish, or is missed. Then it's the next morning: the day number goes up, forage trips reset, and the menu stays.
- **Closing early** is allowed but costs you: the cafe sells only its share of the day so far, rounded down (after the morning, 3 x 1/3 = 1 dish; first thing, nothing), and the trips left are lost. The board asks first and says what you'd give up. Like going to bed early in Stardew: fair when you're out of ingredients or waiting for tomorrow, never free.
- **After the summary** the screen fades to black into the next morning, or into the night once night is unlocked (see [NIGHT.md](NIGHT.md)). The rules move to the next day as soon as you close, but the world keeps showing today (guests, seats, quest markers) until the screen is black (`DayTransition`), so nothing changes in view.
- **A cosy cafe** sometimes earns a **lucky-day tip** when you close (see [GUESTS-AND-PROGRESS.md](GUESTS-AND-PROGRESS.md)).
- No real-time clock: time moves with your trips, and the **day dial** on the HUD shows where you are.

## Tuning

On the **GameDatabase** asset (`Assets/Data/GameDatabase.asset`), under *Day loop tuning*:

| Setting | Default | What it does |
| --- | --- | --- |
| Sales Per Day | 3 | Dishes sold in a full day. `DayRules.FullDaySales` is where progression (story, upgrades, seats) can raise it later. |
| Menu Slots | 1 | How many dishes can be on the menu at once, at the start. `DayRules.MenuSlots` is where progression (story steps, upgrades) can add more later. |
| Forage Trips Per Day | 3 | Also the parts of the day. 0 means no limit and no day clock (closing always sells a full day). |
| Night Trips | 1 | Forage trips at night. |
| Perk Slots | 1 | Perks that can come along on a trip. |
| Luck Protected Quest | Leaves for the salad | While it's under way, a trip always finds what its item objectives still need. |

**Balance check** (from the current design data, day trips with no focus): a trip finds about 8 items, about 24 a day. That's roughly 2 leaf salads' worth of leaves a day, and under 1 a day of most other dishes. So 3 sales a day already needs a mix of dishes or a focused trip, and foraging isn't too generous yet. Watch for stockpiling over several days; spoiling ingredients or more sales per day are the levers if it gets too easy. The forage sandbox shows exact yields.

A recipe's coins and ingredients are in the design data as before.

## Code

- Rules: `CafeService` (`AddToMenu`, `RemoveFromMenu`, `PlanSales`, `CloseDay`, `GoToBed`, `BuyUpgrade`), `ForagingService.TripsLeftToday`, `DayRules` (`SalesFor`, `IsEvening`, `PartsPassed`, `MenuSlots`, upgrade bonuses). Tests: `Assets/Tests/EditMode/DayTests.cs`.
- State: `GameState.day`, `menu`, `forageTripsToday`, `isNight`, `upgrades`.
- UI: `CafePopup`, `ForagePopup`. World: `CafeSpot`, `ForageSpot`.
- Cheats: *Cafe: learn every recipe*, *Cafe: close for the day*, *Forage: reset today's trips*, *Bag: give 100 coins*.
