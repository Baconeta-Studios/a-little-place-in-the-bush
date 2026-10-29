# The cafe day (prototype)

The first version of the core loop: **forage, put dishes on the menu, close for the day, get paid.** Deliberately simple so we can find out if it's fun.

## How it plays

- **No recipes at the start.** They're learned from quests (the first character's conversation), rare forage encounters, or bought. While there's no first quest yet, use the cheat **Cafe: learn every recipe**.
- **The menu board** is behind the cafe door (`CafeSpot` opens `CafePopup`). Tap a recipe you know to see its ingredients (have/need), then **Add to menu** or **Take off menu**. Changes count straight away, any time of day. The board shows what each dish would sell if you closed now.
- **Close for the day** sells a set number of dishes, shared evenly across the menu in order: each sale goes to the next dish that can still be made. Each sale uses that dish's ingredients and pays its coins, and counts as cooking it for quests. Ingredients are only used at this point, so changing the menu costs nothing. If you run out, the sale goes to another dish, or is missed. Then it's the next morning: the day number goes up, forage trips reset, and the menu stays.
- **Foraging** is always daytime for now, with a few trips a day.
- No night yet, and no clock: the day moves on when you close. Night comes later as a second step of the same button.

## Tuning

On the **GameDatabase** asset (`Assets/Data/GameDatabase.asset`), under *Day loop tuning*:

| Setting | Default | What it does |
| --- | --- | --- |
| Sales Per Day | 3 | Dishes sold when you close for the day. `DayRules.SalesFor` is where progression (story, upgrades, seats) can raise it later. |
| Menu Slots | 3 | How many dishes can be on the menu at once. |
| Forage Trips Per Day | 3 | 0 means no limit. |

A recipe's coins and ingredients are in the design data as before.

## Code

- Rules: `CafeService` (`AddToMenu`, `RemoveFromMenu`, `PlanSales`, `CloseDay`), `ForagingService.TripsLeftToday`, `DayRules`. Tests: `Assets/Tests/EditMode/DayTests.cs`.
- State: `GameState.day`, `menu`, `forageTripsToday`.
- UI: `CafePopup`, `ForagePopup`. World: `CafeSpot`, `ForageSpot`.
- Cheats: *Cafe: learn every recipe*, *Cafe: close for the day*, *Forage: reset today's trips*.
