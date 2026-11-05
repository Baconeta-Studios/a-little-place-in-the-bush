# The story and the quest chains

All of this is data in [`Design/design-data.json`](../Design/design-data.json), so the words and numbers can change freely. Run **Tools > Bush > Import Design Data** afterwards, then run the tests in `WholeGameTests`, which play every quest with the real rules and say what they got stuck on.

## The story

You've come home to find the old cafe falling down, and you mean to fix it up to impress Kōwhai, a gecko you grew up with.

| Part | Quests | Giver |
|---|---|---|
| Coming home | Coming home, Leaves for the salad, Open for business sort of, Closing time, Somewhere to sit, Open for guests, After dark | Ruru |
| Ruru moves on | Ruru moves on. She then visits at random, at night, and sells recipes | Ruru |
| Someone's back | Someone's back, Somewhere worth sitting (4 seats and cosiness 20) | Kōwhai |
| Impressing them | A little taste (moss toast), Something sweet (berry cake), A proper menu (3 dishes up at once), That old recipe, The torn recipe (the 4 scraps), The kawakawa scroll | Kōwhai |
| The date | A table for two, A little place in the bush. The game carries on afterwards | Kōwhai |

Whoever gives the current story quest is always at the cafe, so Kōwhai is around for all of theirs.

## The recipe scraps

| Scrap | Comes from |
|---|---|
| Top | Foraging, only during *The torn recipe* |
| Left | Weka, *A fair trade* |
| Right | Snail, *A very hungry snail* |
| Bottom | Ruru, *One for the road*, only after *That old recipe*. She wants 3 fresh beetles |

The screens don't say who has them, only "Try talking to the visitors to your cafe".

## Character chains

Each step opens when the one before is done, and the giver is sure to come for it.

| Character | When | Quests | Companion |
|---|---|---|---|
| Kunekune | Day | Follow your nose, Dig here, Something smells amazing, Pig's breakfast | First quest |
| Snail | Day | A very hungry snail, Slow food, Somewhere damp | Last quest |
| Spider | Any time | Hollow home, A light for the moths, Silk by moonlight | Last quest |
| Weka | Day | Show me your moves, A fair trade, The bait | Last quest |
| Pekapeka | Night | A bat's breakfast, Midnight snack, Not so shy | Last quest |
| Gus | Night | Gus has a theory, Stinky science, Visitors from the sky | Firefly, last quest |

## Rare finds

These quests stay hidden until you own the item, which drops very rarely (`requiresItem` on the quest).

| Found | Where | Goes | Reward |
|---|---|---|---|
| Tiny ring | Any trip | Ruru, then Weka, then Snail | Green toadstool, the only one |
| Tattered map | Day trips | Kunekune, then 4 trips together | Blue fairy lights and a tree lantern |
| Sealed letter | Night trips | Gus, then Pekapeka | Gold fairy lights and a tree lantern |

## Who turns up

- **Story quest:** its giver is always there.
- **Waiting on someone:** if a quest was ready to hand in that morning, or a chain step was waiting (`giverComes`), its giver is sure to come that day. It's worked out once each morning, so they arrive the next day rather than the moment you finish.
- **Everyone else:** a random chance each day, raised by cosiness, by having a quest of theirs under way, and by the *A familiar face* day event.

## Quest types

| Objective | Means | Finished on accepting? |
|---|---|---|
| `HaveItems`, `DeliverItems` | In your bag | Yes, if you already have them |
| `FindItems` | Found on trips after accepting | No |
| `TripWith` | Trips with that companion | No |
| `CookRecipe` | Dishes served | No |
| `CompleteExpeditions` | Trips | No |
| `CloseDay` | Closings | No |
| `CloseWithOnMenu` | Closings with that dish on the menu | No |
| `PlaceObject` | Things placed after accepting | No |
| `HavePlaced` | Out at the cafe now | Yes, if it's already out |
| `HaveSeats` | Seats out now | Yes, if they're already out |
| `ReachCosiness` | The cafe's cosiness now | Yes, if it's already there |
| `HaveOnMenu` | On the menu now | Yes, if it's already up |
| `TalkTo` | Talk to someone | Yes, if it's the giver |

`nextDay` makes a quest wait until the day after the one before it. A character can have several quests at once: hand-ins come first, then offers, with an "Anything else?" reply between them.

## Adding to it

- **A new quest:** add it to `quests` in the design file. Order matters: the story runs in file order.
- **A new character:** add them to `npcs`, Import, give the asset a portrait, then run **Tools > Bush > Set Up Story Characters In This Scene** and save the scene.
- **A new placeable:** right-click its image: **Bush > Create Placeable...**, then add it as a quest reward. A placeable can be a reward as many times as you like.

Quest items, unique items and `requiresQuest` on forage entries work as before: a key item can't be sold or lost, and a unique one only ever comes once.
