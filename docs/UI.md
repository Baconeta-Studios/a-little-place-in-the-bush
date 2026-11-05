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

Three steps under the root, over one `Backdrop`, only one showing at a time when the game runs. `Setup` is on in the prefab: turn on `Out` or `Results` to edit them, and it doesn't matter how you leave them. The game always starts it closed.

**Fonts have jobs.** Titles are Caveat Brush, buttons and numbers Grandstander Bold, everything you read Pangolin, and notes from characters Mynerve.

- **Setup:** `Sheet` sizes itself to what's in it, so hidden sections leave no gap. `Focus` is a grid (*Cell Size* on it); `Companions` and `Perks` stay hidden until you have one. `Side` holds the gecko, the trip markers, *Head out* and the status line.
- **Choices** are copies of `ChoiceTemplate`, tinted by the *Choice* and *Chosen* colours on the root. For art, put a sprite on the template and use a white or light tint. Its child `NudgeTag` is the note ("Ruru needs 3 leaves") on the option a quest needs.
- **Trip markers** are copies of `PipTemplate`, faded once used (*Used Pip Alpha* on the root). It uses the koru icon, which Rebuild Forage Screen Prefab resets to a dot, so set it again after a rebuild.
- **Out:** the three `Bush` objects are what rustles. Each needs a Button. You can have as many as you like, as long as the root's *Bushes* list matches. `Sky` and `Ground` are crops of the backdrop (move and resize the `Painting` inside them to change the crop), and the `Foliage` under `Scenery` are shaded layers of the Bush art, drifting slowly during the trip. `Gecko` stays where you put it, pads on the spot and turns to face the bush that rustles. A painted forage background could replace all of this later.
- **Results:** finds land in `Pile`, between `BasketBack` and `BasketFront`, tilted a little and centred along the pile's width. Lower `Pile` to tuck them further in, widen it to spread them out, and resize `FoundTemplate` to change their size. *Finds In A Row*, *Back Row Rise*, *Find Tilt* and *Find Jitter* are on the root, and `BasketShadow` is the soft shadow on the ground. Their names are listed in the summary. `Side` has the summary, the quest's progress and the next-step buttons.
- **Icons** are in `Assets/Images/UI/Icons`, set on each item asset, with their pale line work drawn to sit on `GameUi.CardBack` or darker. Rarer finds share `Rare`, finds that teach a recipe share `Recipe`, and every recipe card uses the *Recipe Icon* on `GameDatabase`. `Beetle`, `Nectar`, `Honeydew` and `Pohutukawa` are recoloured copies of `Cricket`, `Berries` and `Leaf`, standing in until they have their own art.
- **Item cards:** `CardTemplate` (under the preview's `ItemGrid`) and `FoundTemplate` are what the cards are copied from: icon, letter (shown until an item has art), name and badge.
- **Companion art** comes from *Portrait* on each companion's asset (`Assets/Data/Companions`). Weka and Kunekune have theirs; a companion without one just doesn't appear beside the gecko.

Fixed labels like "What are we after?" live in the prefab, so you can reword them there. Text that changes as you play comes from code.

**Tools > Bush > Rebuild Forage Screen Prefab** makes this prefab again from the default layout in `ForagePopup.cs`. It asks first, because that loses your edits. **Rebuild Forage Trip Scene** replaces only the `Out` step and keeps your edits to the rest.
