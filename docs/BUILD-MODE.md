# Build mode (prototype)

Placing, moving and putting away things in the cafe's outdoor space, plus the first look at the bag (inventory). This is a prototype for us to play with: the item source is unlimited, the UI is built in code with placeholder styling, and the placeables are coloured boxes.

How it fits together is in [ARCHITECTURE.md](ARCHITECTURE.md) (sections 4 and 5).

## Set it up (about 5 minutes)

1. Open the project on this branch and let it compile.
2. **Tools > Bush > Import Design Data (Design folder)**: creates or updates the assets in `Assets/Data`, including the six placeables in `Assets/Data/Placeables`.
3. **Tools > Bush > Create Placeholder Placeables**: makes a simple prefab for each placeable in `Assets/Prefabs/Placeables` (a coloured box, a footprint, and seat points on the chair and bench).
4. Open `Cafe.unity` and run **Tools > Bush > Set Up Build Mode In This Scene**. It:
   - adds `GameSessionBootstrap` (with the GameDatabase) and `GameHud` to `Systems`;
   - adds `BuildArea` and `BuildMode` to `World`, placing into `World/Placed`;
   - turns the chairs and the plant in `World/Static` into the starting layout, so they can be moved like everything else (the scene objects are removed; Ctrl+Z undoes it all).
5. **Tools > Bush > Validate Design Data**, then run the tests (**Test Runner > EditMode > Run All**).
6. Press Play. Save the scene and commit, including `Assets/Data` and `Assets/Prefabs/Placeables`.

## How to play with it

- **Bag** (top left) opens the inventory: Larder, Materials, Treasures, Satchel and Placeables, with coins. It's empty on a new save; the cheat menu's *Bag: give 5 of every item* fills it.
- **Build** (top left, only in the cafe) enters build mode. The game pauses and the gecko stops taking clicks.
  - Tap an item in the tray at the bottom: it appears on a free spot. Drag it; it snaps to the 0.25 grid. On a phone it sits above your finger while you drag.
  - Green outline: it can go here. Red: it can't, and a label says why.
  - **Place** puts it down. **Flip** mirrors it (not everything can flip). **Cancel** drops it.
  - Tap something already placed to pick it up: drag it, **Flip** it, **Done** to put it down, or **Put away** to send it back to the bag.
  - Keyboard: Escape cancels or leaves, F flips, Enter places, Delete puts away.
  - **Done** (top right) leaves build mode. If the gecko ends up inside new furniture it steps out.
- Everything is saved straight away. Reload, or close the tab and come back, and it's all where you left it.

### Where things can't go

The ghost goes red, with a reason, if it would:
- go off the open ground (the walkable floor is the build area, so reshaping `WalkableArea` changes where you can build);
- overlap something else;
- cover a stand point (the cafe door) or the spawn point;
- **cut off a path**: the gecko must still be able to walk from the spawn point to every stand point.

### Unlimited items (prototype)

Build mode starts in unlimited mode: every placeable shows "free" and placing doesn't use anything up. Turn it off with the cheat menu's *Build: unlimited items* toggle to try the real rules: placing takes one from the bag, putting away gives it back (things placed for free don't turn into real items). *Build: give 3 of every placeable* stocks the bag for that.

### Cheats (` or F1, or triple-tap the top-left corner on a phone)

| Cheat | Does |
| --- | --- |
| Build: unlimited items | Free placing on or off (remembered on this device) |
| Build: give 3 of every placeable | Stocks the bag for the non-unlimited rules |
| Bag: give 5 of every item | Fills the bag (unique items: one each) |
| Bag: give 100 coins | |
| Save: save now / reset everything | Reset deletes the save and reloads the scene: back to the starting layout |

## Making a new placeable

1. In `Design/design-data.json` (or the sandbox), add an item with category `Placeable`, and a matching entry in `placeables`:
   ```json
   { "id": "placeable.planter", "item": "item.planter", "placement": "Floor",
     "footprintWidth": 1.0, "footprintDepth": 0.4, "canFlip": true, "category": "Plants", "seats": 0 }
   ```
   - **Footprint** is only the base that blocks walking (where it touches the floor), not the whole picture. Width is left to right; depth is front to back.
   - **Category** becomes a tray tab.
   - **Seats** is how many guests can perch on it. Guests aren't in yet, but quests can already count seats.
2. Import the design data, then either run **Create Placeholder Placeables**, or make a real prefab:
   - root at the **floor contact point** (centre of the base), no scale;
   - a child called **`Visual`** with the art (any scale; sorting layer World, sort point Pivot, pivot at the bottom);
   - one **`SeatPoint`** child per seat, where a guest's bottom goes, with the way they face;
   - no need for a collider: `PlacedObject` adds and sizes the footprint from the asset.
3. Assign the prefab on the placeable asset (`Assets/Data/Placeables`). **Validate Design Data** warns if its seat points don't match its seat count.

Placement types other than `Floor` (Wall for windows and fairy lights, Rug, Surface for things on tables) are reserved in the data but not built yet.

## Test checklist

**Build mode**
- [ ] Build appears in the cafe; Bag appears everywhere.
- [ ] Entering pauses the game and the gecko ignores clicks. Escape doesn't open the pause menu while building.
- [ ] Each tray tab shows the right items; tapping one gives a ghost on a free spot.
- [ ] Dragging is smooth and snaps; on a phone the item sits above your finger.
- [ ] Red with the right reason: off the ground, on another item, on the door's stand point, on the spawn point, closing the only path past something.
- [ ] Place, Flip, Cancel work. Picking up an existing item, moving it, Done, and Put away work.
- [ ] Tapping the UI (tray, buttons) never moves anything underneath.
- [ ] Leaving with the gecko inside new furniture: the gecko steps out.
- [ ] Walking afterwards goes around everything placed.

**Saving**
- [ ] Place a few things, reload (or close and reopen the browser tab): everything is where you left it.
- [ ] *Save: reset everything* goes back to the starting layout.
- [ ] Unlimited off: placing uses an item from the bag and putting away returns it.

**Bag**
- [ ] Opening pauses the game; Escape and Close close it.
- [ ] Tabs show counts; tapping an item shows its description.
- [ ] Coins on the HUD update after *Bag: give 100 coins*.

## Not built yet

- Earning placeables (shop, quest rewards) and turning unlimited mode off for good.
- Guests using seats.
- Wall, Rug and Surface placement.
- Real art and a designed UI (the prototype UI is built in `UI/BuildUi.cs`, `UI/InventoryPopup.cs` and `UI/GameHud.cs`).
- Using, selling and sorting from the bag.
