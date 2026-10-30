# Night time and the day dial (prototype)

## How a day goes

> **Morning, midday, afternoon** (a forage trip each) → **Evening** (close the cafe) → **Night** (optional) → **Go to bed** → next morning

- **Night is earned, not forced.** Until Ruru's *After dark* quest (serve 3 dishes, after *Open for guests*) closing goes straight to the next morning, as before. Her reward unlocks night (`UnlockNight`, a quest reward type).
- Once unlocked, the day's summary ends with **Into the night**: the screen fades to black and back to the cafe at night.
- **Night is calm.** The cafe's closed (no sales). Nocturnal guests are about, and you get **one night forage trip**, which finds night things (Kiwi's note, glowworms). **Go to bed** (on the HUD, or the cafe door) fades into the next morning; you can go to bed straight away.
- **Who's about:** each character has **Active At**: Day, Night or Both. Day guests go home at night; nocturnal ones only come out at night. Placeholder: Ruru (a morepork) is about both, Wētā and Pekapeka are nocturnal, everyone else is a day guest.
- Nothing changes in view while a day ends: guests and lighting switch while the screen is black (`DayTransition`, `DayFlow`).

## Showing it

- **The day dial** (top right, beside the quest tracker): a ring split into the parts of the day (one per forage trip, then evening, then night once unlocked), the day number in the middle, and a hand that turns to the current part. Parts already passed are dimmed; the time of day is written underneath. Tap it for what's next. Built in code with flat colours; swap in art later.
- **Lighting** (`DayNightLighting`, set up automatically): the scene's **Global Light 2D** as you set it is the daytime look. At **dusk** (evening) it warms a little; at **night** it turns deep blue and dims to half. It eases between them. Sprites need the lit sprite material (Sprite-Lit-Default, the URP 2D default) to be affected.
- **Lanterns and windows:** add a **Light 2D** (Point or Freeform, warm colour) to anything that should glow, plus **NightLight**: it's off by day and fades on at dusk and night. Put one on the lantern post prefab and placed lanterns light up the cafe at night.

## Tuning

On the **GameDatabase**: **Night Trips** (1). The dusk and night colours are at the top of `DayNightLighting.cs`.

## Cheats

*Night: unlock night time*, *Night: go to bed (next morning)*, *Forage: reset today's trips* (also tonight's).

## Ideas for later

Glow (lantern-lit) decorations bringing nocturnal guests more often, a few stars and fireflies, night sounds (crickets, Ruru's *more-pork*), night-only quests from Wētā and Pekapeka, and maybe a night version of the backdrop.
