# Forage Sandbox

A single web page for playing with and tuning the foraging system without Unity. It follows the rules in [`docs/FORAGING.md`](../../docs/FORAGING.md) step by step, so the same design data, setup and seed give the same result here and in the game.

## Open it

Double-click `index.html`. It works offline from disk and also when hosted anywhere; it makes no network requests.

Tabs:

- **Expedition**: pick a companion, a perk, a focus, day or night and a seed, see what each choice does, then press Go. Every result shows its seed.
- **Odds**: chance per roll, chance of at least one per trip and expected quantity for every item, compared with going alone.
- **Simulate**: thousands of trips, plus how many trips it takes before each recipe can be cooked or each quest finished.
- **Campaign**: a running basket across trips with companion trip counts, learned perks, found scraps, active quests, cooking and quest hand-ins.
- **Design data**: edit items, the forage table, companions, perks and focuses, with warnings as you go.

Edits and the campaign autosave in your browser. **Reset to defaults** goes back to the data built into the page.

## Getting changes into Unity

1. On the **Design data** tab press **Export JSON**. This downloads `design-data.json` in the same format as `Design/design-data.json`. (**Copy JSON** puts the same text on the clipboard.)
2. Either replace `Design/design-data.json` in the project with it and run **Tools > Bush > Import Design Data (Design folder)** in Unity, or run **Tools > Bush > Import Design Data From File...** and pick the downloaded file.
3. Commit `Design/design-data.json` and the changed assets in `Assets/Data` together.

To load someone else's data, use **Import JSON** on the same tab.

The page's built-in defaults are a copy of `Design/design-data.json`. When that file changes, update the page from the repo root with:

```
python Tools/ForageSandbox/embed-design.py
```

## Comparing with Unity

From the browser console:

```js
ForageSandbox.resolve(ForageSandbox.defaultDesign,
  { table: "table.bush", companion: "companion.weka", perks: ["perk.big_basket"], focus: "focus.bugs", time: "Night" },
  { activeQuests: ["quest.main_lost_recipe"], obtained: [] },
  12345);
ForageSandbox.preview(design, setup, context);
ForageSandbox.currentDesign(); // the edited copy
```

On load the page logs two `[ForageSandbox self-test]` results to the console. These, and 200 random setups across every companion, perk, focus, time of day and quest state, were checked against the C# `ForagingService` and match exactly.
