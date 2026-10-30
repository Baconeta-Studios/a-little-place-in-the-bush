# Decisions and what's next

One place for what's been decided, what's been set aside for later, and what's still open. The other docs describe how each system works; this one says why things are the way they are. Placeholder content (characters, quests, dialogue, numbers) is expected to be rewritten when the real narrative and quest list are written.

## Decided

**The day**
- **Forage trips are the day's clock.** A day has three parts (morning, midday, afternoon), one per forage trip, then evening. No real-time clock: time moves with what you do. ([CAFE.md](CAFE.md))
- **Closing early costs you:** the cafe sells only its share of the day so far, rounded down, and the trips left are lost. No other penalty.
- **Night is earned through the story** (Ruru's *After dark*), then optional every day: after closing, a calm night with no sales, nocturnal guests and one night forage trip. **Go to bed** starts the next morning. ([NIGHT.md](NIGHT.md))
- **A day dial** on the HUD shows the parts of the day, and the lighting goes warm at dusk and blue at night.
- **Nothing changes in view while a day ends:** guests and lighting switch while the screen is black.

**The cafe**
- **No recipes at the start.** They come from quests, rare forage encounters and, for a few, Kererū's recipe shop.
- **One dish on the menu to start**; more menu slots, sales and trips come from cafe upgrades.
- **The cafe serves while you're out:** each forage trip is a part of the day, and as you set off the cafe serves that part's share (3 dishes over 3 trips: one each), shared evenly across the menu. The trip's results say what sold; closing for the day tallies it up. Menu changes count from your next trip.

**Foraging**
- **Pick a focus ("look out for") before each trip** to find more of something. Taught in Ruru's *Leaves for the salad*, the first trip, and by a tip on the forage screen.
- **Luck protection** in the tutorial: while *Leaves for the salad* is active, a trip always finds the leaves still needed.
- **Peek-a-boo** while you're out: something peeks out of a bush for a moment; grab it for a bonus find. It only adds, and can be skipped.
- **Rare finds stay a surprise:** the forage screen never shows odds, and leaves out Rare and Legendary items.
- **Rare recipe encounters** each have their own way to be found: Kiwi (night), Kunekune's scribble (with Kunekune), Weka's card (with Weka), Tūī's song (very rare, likelier with a Sweet focus).

**Companions and perks**
- **No companions at the start.** Each is befriended through a starter quest, from a character who visits later and not often.
- **Trips with companions are free**, but each companion goes **once a day**: by day, at night, or either (still once).
- **Perks are rare and earned:** almost all come from 5 trips with a companion (a weaker version of what they do); the rest are late quest rewards. None are known at the start or given in the tutorial. They stack, up to the perk slots (1, plus an upgrade).

**Guests**
- **Story characters** are there while the story is theirs. **Random visitors** drop by on some days, more often in a cosier cafe and when a quest of theirs is under way. Day and night guests are separate; Ruru keeps day hours until night is unlocked, then only comes out at night.
- **Guests sit on the seats of furniture you've placed**, spaced apart; without a free seat they use their own spot, or the nearest clear spot.
- **Guests who aren't in today are hidden and block nothing**, in build mode too.

**Building and cosiness**
- **The cafe starts empty.** Ruru's *Somewhere to sit* gives the first furniture, and *Open for guests* teaches building.
- **Build mode places only what you own**; the tray only shows what's in your bag. (A cheat turns unlimited placing on for testing.)
- **Cosy is hard on purpose:** basic furniture adds very little, so the big cosiness (and its rewards: more visitors, lucky-day tips) comes from special things earned later.
- **The sign** by the door describes what kind of cafe you've made.

**Upgrades and coins**
- **Coins buy progress:** cafe upgrades (pricey, pricier down the list) and a handful of recipes. Most of the cool stuff comes from quests, rare finds and combos, not shops.

**Tech**
- WebGL first, tested in the browser. Rules in `Bush.Core` (plain C#, unit tested); design data in `Design/design-data.json`, editable in the web sandbox and imported into Unity. ([ARCHITECTURE.md](ARCHITECTURE.md))

## Later (set aside on purpose)

- **Selling items from the bag.** Not sure it fits; revisit once the economy settles.
- **A second area** (garden, village, a shop). Out of scope for now; characters live at the cafe.
- **A recipe book.** For now the cafe's menu board already lists the recipes you know. The lost recipe's scrap progress ("2 / 4") could become part of it later.
- **The real narrative:** characters, names, quests and dialogue will be rewritten. The current ones are placeholders that show what the systems can do.
- **"Come back tomorrow"** timing for quests, and characters who only appear on certain days.
- **Night extras:** glow decorations bringing nocturnal guests, fireflies and stars, night sounds, a night backdrop.
- **Wall, rug and surface placement**, and special high-cosiness furniture.

## Open

- **Special orders:** now and then a visiting guest asks for a particular dish, and serving it gets a tip. An idea for later, to make serving more hands-on.
- **Peek-a-boo or a timing bar** for the forage minigame: peek-a-boo is in to try; a timing bar is the alternative if it doesn't feel right.
- **Expedition art and UI:** the screens are placeholder UI built in code, to be designed.
