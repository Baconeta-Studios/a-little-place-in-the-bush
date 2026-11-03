# UI

The HUD and the forage screen are prefabs you can lay out and restyle in the Editor. The rest of the UI (build tray, bag, quest log, cafe board, dialogue, upgrades, recipe shop, cafe sign) is still built in code, from the helpers in `UI/GameUi.cs`.

## Make the prefabs (once)

1. Let Unity compile, then run **Tools > Bush > Create UI Prefabs**. It makes:
   - `Assets/Resources/UI/HUD.prefab`
   - `Assets/Resources/UI/ForagePopup.prefab`
   - `Assets/Images/UI/Generated/RoundedPanel.png` and `Circle.png`, the plain shapes the screens use until you swap in your art.
2. Press Play. It should look just as it did. Commit those files.

If a prefab isn't there, the game builds that screen's default layout in code, so it never goes missing.

Running the tool again asks before replacing a prefab, because that loses your edits.

## Editing them

Open the prefab and change what you like: move things, resize, swap sprites and fonts, add decoration. Prefabs are found by name, so keep them in `Assets/Resources/UI` with the same names.

You don't wire up any buttons: the scripts do that when the game runs. The scripts find things through the links on the prefab's root (`HudView`, `ForagePopup`), so:
- **Renaming or moving** something is fine.
- **Replacing** something (a new button rather than editing the old one): drag the new one into its slot on the root.
- **Deleting** a HUD button: fine, clear its slot too. The forage screen's slots all need something in them.

Unity's UI package has a bug: pressing Play with a prefab containing buttons open used to flood the Console with `IndexOutOfRangeException`s from `Selectable`. `Editor/SelectableCountRepair.cs` works around it, so you can leave prefabs open. Delete it if a Unity update fixes the bug.

### The HUD (`HUD.prefab`)

- `HudBar` lays its buttons out in a row. Build and Go to bed are hidden until they're needed, and Forage only shows where `GameHud` has *Forage Button* ticked.
- The text in `Coins` and `QuestTracker` is filled in by the game.
- `DayDial`: the ring's parts are made when the game runs, one per part of the day, by filling and tinting *Segment Sprite* (a white circle). The hand, the middle and the label are in the prefab. The hand has to stay a child of the ring. The label's outline is added when the game runs, so it doesn't show while editing: set it on `DayDial` (0 for none).

### The forage screen (`ForagePopup.prefab`)

It has three steps under `Panel`, only one showing at a time when the game runs. It always starts closed in the game, whether the root is on or off in the prefab. `Setup` is on in the prefab: turn on `Out` or `Results` to edit them, and it doesn't matter how you leave them.

- **Setup:** `Row`s hold the focus, companion and perk choices. Each choice is a copy of `ChoiceTemplate` (hidden, under `Setup`), with its background and text tinted by the *Choice* and *Chosen* colours on the root. For art, put a sprite on the template and use white or a light tint.
- **Out:** the three `Bush` objects are what rustles. Each needs a Button. You can have as many as you like, as long as the root's *Bushes* list matches.
- **Results:** `Found` is the grid of finds.
- **Item cards:** each `ItemGrid` has a hidden `CardTemplate` that its cards are copied from: background, icon, letter (shown until an item has art), title and badge. The card's colours are on its `ItemCardView`. In `Preview` (a row), the template's size is the card size. In `Found` (a grid), it's *Cell Size* on the `Content`'s Grid Layout Group.

Fixed labels like "Look out for" live in the prefab, so you can reword them there. Text that changes as you play (trips left, notes, results) comes from code, and the game colours some of its words with its own palette. The fonts and sizes are still yours.
