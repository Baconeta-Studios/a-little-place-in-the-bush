# UI

The HUD, forage screen, menu board, quest screen, bag, upgrades, dialogue and cafe sign are all prefabs in `Assets/Resources/UI`, which you can lay out and restyle in the Editor. The build tray and recipe shop are still built in code, from the helpers in `UI/GameUi.cs`. The pause and settings menus are in `Assets/Prefabs/UI/Popups/PopupCanvas.prefab`.

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

## The shared look

Every screen uses the same few pieces, each with one job. Use these before adding anything new.

- **Torn paper** (`Images/UI/Panel.png`, Image Type Tiled): the big sheet holding a screen's content. It has a clear edge, about 30 at the sides and 55 top and bottom, so leave room for it.
- **Tan card** (`GameUi.CardBack`): something you can pick or open. Its colour is dark enough for the icons' pale line work to show.
- **Three buttons:** `GameUi.Apron` for the one main action on a screen, `GameUi.Cream` for other choices, and `GameUi.PanelDark` for leaving, always called Back.
- **Gold tape:** someone in the game is asking for something. Only that.
- **Hanging sign** (`Placeables/Cafe sign.png`): names a board, once per screen at most.
- **Koru markers:** trips left today, and nothing else.
- **Fonts:** two, across the whole game: Caveat Brush for titles and signs, and Pangolin for everything else, in bold for buttons and names. They're set on `Resources/UI/UiFonts.asset`, and text built in code picks the right one by itself. Add a third only for something special and important.
- **Colours:** green means done or ready, and red means a problem or shortage.

The main cafe view should show as little as it can. The recipe shop and build tray have the fonts, but not the rest of this yet.

### The title screen (`Assets/Scenes/Title.unity`)

The first scene in the build, and where Main Menu in the pause menu goes. Pressing Play with `Cafe` open still starts in the cafe. Everything is a plain object in the scene, so move, resize or swap whatever you like. `TitleScreen` on `TitleCanvas` wires up the buttons when the game runs, and its slots say which is which.

- `Buttons` lays out New game, Continue, Settings and Credits in a row. Continue only shows if there's a save, and the row closes the gap.
- `SafeArea` holds what should stay clear of phone notches. `Background` sits outside it and always fills the screen.
- `ConfirmNew` asks before a new game replaces a save. `Credits` holds the credits text. Both are switched off in the scene: turn one on to edit it, and turn it off again before saving.
- Settings comes from the `PopupCanvas` in the scene, with *Allow Pause* off on its `PauseInput`, which also hides the cog.

**Tools > Bush > Build Title Scene** makes the scene again from the default layout and puts it first in the build. It asks first, because that loses your edits.

### The HUD (`HUD.prefab`)

- `HudBar` lays its buttons out in a row. Build and Go to bed are hidden until they're needed, and Forage only shows where `GameHud` has *Forage Button* ticked.
- The text in `CoinsText` and `TrackerText` is filled in by the game.
- **The job note:** `PaperTracker` is the note and `JobButton` is what's left when it's tucked away. Whether it's tucked is remembered on the device. When it's tucked, it peeks out for a few seconds if the step changes.
- **Day dial:** tapping it opens `DayInfo`, with the part of the day, trips left, today's event and who's around. Its colours are on `DayDial` in the HUD prefab: *Day Colors* (morning, midday, afternoon and late afternoon, spread over however many trips there are), *Evening Color* and *Night Color*.
- **Build's `!`:** `Marker` on `BuildButton` shows while a quest under way wants something placed and you have something it would count.
- **Settings:** the cog is `PauseButton` in `PopupCanvas.prefab`, with `Placeholder-cog.png` for now.
- `DayDial`: the ring's parts are made when the game runs, one per part of the day, by filling and tinting *Segment Sprite* (a white circle). The hand, the middle and the label are in the prefab. The hand has to stay a child of the ring. The label's outline is added when the game runs, so it doesn't show while editing: set it on `DayDial` (0 for none).

### The quest screen (`QuestLogPopup.prefab`)

`PaperStory` is the story's sheet and `PaperDetail` (under `Detail`) is what opens when you tap a note. Both are the same `QuestDetailView`, with `RowTemplate`, `PieceTemplate` and the rewards' `RowTemplate` copied for what a quest needs. With three or more items to gather, they show as pieces saying where each one is. `NoteTemplate` under `Jobs` is copied for each side quest. Portraits come from *Portrait* on each NPC's asset.

### The bag (`InventoryPopup.prefab`)

`TabTemplate` is copied for each tab, tinted by the *Tab* colours on the root, and `CardTemplate` (under `ItemGrid`) for each item. Set the grid's cell size on its Grid Layout Group.

### Upgrades (`UpgradesPopup.prefab`)

`RowTemplate` is copied for each upgrade.

### Talking (`DialogueBox.prefab`)

`MainReply` is the first reply, `OtherReply` any others, and `LeaveReply` is for Bye and Not now. `Portrait` shows the character's *Portrait*, or a circle with their initial if they have no art yet.

### The cafe sign (`CafeSignPopup.prefab`)

`Group` keeps it all in the middle of any shape of screen. `Sign` and `SignTitle` are the hanging sign. `PipTemplate` is copied for each level of cosiness, with *Unreached Pip Alpha* on the root for the ones you haven't got to. `Stats` holds the four cards.

**Tools > Bush > Rebuild HUD, Quests, Bag, Upgrades, Talking and Cafe Sign** makes these from their default layouts, asking before replacing each one, because that loses your edits.

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

### The menu board (`CafePopup.prefab`)

Three parts under the root: `Board`, `Summary` (the end of the day) and `ConfirmEarly` (the box asking before you close early). `Board` has `Recipes` on the left and `MenuColumn` on the right.

- **Recipe cards:** `CardTemplate` (under `Recipes`, on `RecipeCardView`) is what they're copied from. Its *Normal*, *On Menu* and *Cant Make* colours are on the component, and `IngredientTemplate` is copied for each ingredient.
- **Menu pages:** `Slot0` to `Slot2` under `Slots`, on `MenuSlotView`. All three always show: the ones you haven't bought are the locked state, with the upgrade's name and price.
- **Dragging:** `DragGhost` is what follows your finger.
- **While you're out:** `Forecast`, with `SaleTemplate` copied for each dish that will sell.
- The recipe page picture comes from *Recipe Icon* on `GameDatabase`, and the trip markers are copies of `PipTemplate`.

**Tools > Bush > Rebuild Menu Board Prefab** makes it again from the default layout in `CafePopup.cs`, which loses your edits, so it asks first.

### Rebuilding the forage screen

**Tools > Bush > Rebuild Forage Screen Prefab** makes this prefab again from the default layout in `ForagePopup.cs`. It asks first, because that loses your edits. **Rebuild Forage Trip Scene** replaces only the `Out` step and keeps your edits to the rest.
