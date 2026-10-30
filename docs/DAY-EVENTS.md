# Day events

Something nice about today, announced in the morning. Never bad. A cosier cafe makes them more frequent, makes the rarer ones likelier, and makes the doorstep gift more often something special.

## In the game

- Each morning (from day 3, after the tutorial) there's a chance of one event: about 1 morning in 3 in a bare cafe, up to 3 in 5 in a very cosy one. It's announced under the day's title as the screen fades in (*"Day 6. Rain overnight. The mushrooms will be out."*), shown in gold under the day dial (tap the dial for what it does), and called out where it matters: the forage screen for forage events, the menu board for the cafe ones.
- The same event never comes two days running. Rolled once per day from the save's daily dice, so reloading doesn't change it.

| Event | Rarity | What it does |
| --- | --- | --- |
| Shiny day | Common | Every trip has a 40% chance of a bonus shiny find (Weka's trick, for everyone). |
| Still air | Common | Peek-a-boo rustles twice as long, and the rustling bush is much likelier to hold something uncommon or rare. |
| Mushroom weather | Common | Fungi turn up twice as often on day trips. |
| Berry glut | Common | +1 fruit with every fruit find. |
| Busy day | Common | One extra dish sells today. |
| A familiar face | Common | A guest with a quest of yours under way is sure to drop by (or another visitor if none). |
| A food critic | Uncommon | The first dish sold today pays double. |
| Meteor shower | Uncommon | Night only (needs night unlocked): night trips get an extra find, and encounters (Kiwi) are four times likelier. |
| Left on the doorstep | Uncommon | A gift straight into the bag: ingredients or a shiny button, or on an **exclusive** roll something special (a lantern post, a flower barrel, a bench, kauri gum). |

## Tuning it (all in the Inspector)

**Each event** is an asset in `Assets/Data/DayEvents` (**Tools > Bush > Create Default Day Events** makes the set above; running it again leaves existing ones alone). Select one to edit:
- **Line** (the morning announcement) and **Effect** (a few words for the HUD and screens).
- **How likely:** **Weight** (against the others), **Rarity** (rarer ones get a boost in a cosy cafe), **Min Cosiness** (locked until the cafe is that cosy), **Needs Night**.
- **What it does** (any mix): **Forage** modifiers (the same kinds as companions and perks) and which trips they're for (**Forage At**: day, night or both); **Peek** modifiers and **Peek Longer**; **Extra Sales**; **Critic**; **Familiar Face**; **Gift** (items, quantity, weight, and whether each is **exclusive**).

New events: right-click in the folder, **Create > Bush > Day Event**, then add it to the GameDatabase's **Day Events** list (or **Tools > Bush > Collect All Definitions Into Database**).

**Overall tuning** is on the **GameDatabase**, under *Day events*:

| Setting | Default | What it does |
| --- | --- | --- |
| Chance | 0.3 | Chance of an event on a morning, in a bare cafe. |
| Chance From Cosiness | 0.3 | Added at full cosiness (so up to 60%). |
| Rarer From Cosiness | 1 | At full cosiness, an event's weight is multiplied by 1 + this x its rarity step (uncommon 1, rare 2, legendary 3). |
| Cosiness For Full Boost | 150 | The cosiness where all the boosts are full. |
| Exclusive Chance | 0.05 | Chance a doorstep gift is exclusive, in a bare cafe. |
| Exclusive From Cosiness | 0.3 | Added at full cosiness (so up to 35%). |
| First Day | 3 | No events before this day. |

So the way to roll something special more often is to make the cafe cosier: more events, more of the rarer ones, and more exclusive gifts.

## Testing

Cheat menu: **Day event: next one (cycles)** makes today's event the next in the list (giving the doorstep gift too). Reset the save to see events come naturally from day 3.

## Code

`DayEvents` (rolling, the exclusive chance, forage modifiers, the familiar face, gifts), `DayEventData` / `DayEventSettings` in the model, effects read in `DayRules.FullDaySales`, `CafeService` (the critic), `Visitors.RollToday` (the familiar face), `ForagingService.CollectModifiers` and `PeekABooFind`. In Unity: `DayEventDefinition` assets, `DayEventTools`, the morning line in `DayFlow`. Tests: `DayEventTests`.

The forage sandbox doesn't know about events (a trip's `eventId` is empty there), so a trip on an event day can't be reproduced in the sandbox exactly; the same setup and seed without the event can.
