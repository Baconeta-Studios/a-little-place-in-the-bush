# Build mode

Placing, moving and putting away things in the cafe's outdoor space, plus the bag (inventory). You place what you own: furniture comes from quests (Ruru's *Somewhere to sit* gives the first), and the tray only shows what's in your bag. The UI is built in code with placeholder styling, and the placeables are coloured boxes until the art is in.

How it fits together is in [ARCHITECTURE.md](ARCHITECTURE.md) (sections 4 and 5).

## Set it up (about 5 minutes)

1. Open the project on this branch and let it compile.
2. **Tools > Bush > Import Design Data (Design folder)**: creates or updates the assets in `Assets/Data`, including the six placeables in `Assets/Data/Placeables`.
3. **Tools > Bush > Create Placeholder Placeables**: makes a simple prefab for each placeable in `Assets/Prefabs/Placeables` (a coloured box, a footprint, and seat points on the chair and bench).
4. Open `Cafe.unity` and run **Tools > Bush > Set Up Build Mode In This Scene**. It:
   - adds `GameSessionBootstrap` (with the GameDatabase) and `GameHud` to `Systems`;
   - adds `BuildArea` and `BuildMode` to `World`, placing into `World/Placed`;
   - turns the chairs and the plant in `World/Static` into the starting layout, so they can be moved like everything else (the scene objects are removed; Ctrl+Z undoes it all). The cafe now starts empty instead: the **Starting Layout** on `BuildArea` is cleared, and the first furniture comes from a quest.
5. **Tools > Bush > Validate Design Data**, then run the tests (**Test Runner > EditMode > Run All**). One of them, the save round trip, only means anything inside Unity (it checks Unity's own JSON serialiser).
6. Press Play. Save the scene and commit, including `Assets/Data` and `Assets/Prefabs/Placeables`.

## How to play with it

- **Bag** (top left) opens the inventory: Larder, Materials, Treasures, Satchel and Placeables, with coins. It's empty on a new save; the cheat menu's *Bag: give 5 of every item* fills it. Using and selling from the bag are set aside for later.
- **Build** (top left, only in the cafe) enters build mode. The game pauses, the gecko stops taking clicks, and the camera slides down so the whole floor sits above the tray.
  - Tap an item in the tray at the bottom: it appears on a free spot. Drag it; it snaps to the 0.25 grid. On a phone it sits above your finger while you drag.
  - Green outline: it can go here. Red: it can't, and a label says why.
  - **Place** puts it down. **Flip** mirrors it (not everything can flip). **Cancel** drops it. These buttons float just above the item, clear of your finger.
  - Tap something already placed to pick it up: drag it, **Flip** it, **Done** to put it down, or **Put away** to send it back to the bag.
  - **Look around:** pinch with two fingers to zoom and drag with two fingers to pan (touch); scroll to zoom towards the pointer and drag with the right or middle button to pan (mouse); or drag on empty ground when you're not holding anything. You can zoom in to 2.5x but not out past the normal view, and never past the edges of the backdrop.
  - Keyboard: Escape cancels or leaves, F flips, Enter places, Delete puts away.
  - **Done** (top right) leaves build mode and the camera eases back to the normal view. If the gecko ends up inside new furniture it steps out, and if it's been boxed in it's moved back to the spawn point.
- Everything is saved straight away. Reload, or close the tab and come back, and it's all where you left it.

### Where things can't go

The ghost goes red, with a reason, if it would:
- go off the open ground (the walkable floor is the build area, so reshaping `WalkableArea` changes where you can build);
- overlap something else;
- cover a stand point (the cafe door, the forage path, a guest who's here today) or the spawn point (guests who aren't in today don't take up any room);
- **cut off a path**: the gecko must still be able to walk from the spawn point to every stand point it can reach now.

Things that don't go on the ground have their own rules: see [Things on tables, on the tree, and fairy lights](#things-on-tables-on-the-tree-and-fairy-lights).

### What you can place

Build mode uses the real rules: placing takes one from the bag, putting away gives it back. The tray only shows what's in your bag (and only the tabs that have something), so things you haven't found stay a surprise and the tray stays short as the list grows. The cafe starts empty, and Ruru's *Somewhere to sit* quest gives the first furniture. For testing, the cheat menu's *Build: unlimited items* toggle makes every placeable free to place. Things placed for free don't turn into real items when put away, but the starting furniture does (it's yours), so nothing that came with the cafe is ever lost. *Build: give 3 of every placeable* stocks the bag for that.

### Cheats (` or F1, or triple-tap the top-left corner on a phone)

| Cheat | Does |
| --- | --- |
| Build: unlimited items | Free placing on or off (remembered on this device) |
| Build: give 3 of every placeable | Stocks the bag for the non-unlimited rules |
| Bag: give 5 of every item | Fills the bag (unique items: one each) |
| Bag: give 100 coins | |
| Save: save now / reset everything | Reset deletes the save and reloads the scene: an empty cafe again |

If a save can't be read (broken, or from a newer build), the game starts fresh and copies the old save to a backup slot first (`bush.save.v1.backup` on the web, `bush-save.backup.json` elsewhere), so it can be recovered.

## Making a new placeable

**The quick way:** right-click a sprite in the Project window and choose **Bush > Create Placeable**. Name it, and optionally pick a placeable to base it on, which copies its settings and prefab, including seat points. It makes the item (with the sprite as its icon), the placeable and its prefab, adds them to the database and to `Design/design-data.json`, and fits the art to the width you give it, standing on the ground line. It then needs a way into the game: a quest reward, say. To try it out, use the cheat *Build: give 3 of every placeable*.

**By hand:**

1. In `Design/design-data.json` (or the sandbox), add an item with category `Placeable`, and a matching entry in `placeables`:
   ```json
   { "id": "placeable.planter", "item": "item.planter", "placement": "Floor",
     "footprintWidth": 1.0, "footprintDepth": 0.4, "canFlip": true, "category": "Plants", "seats": 0 }
   ```
   - **Footprint** is only the base that blocks walking (where it touches the floor), not the whole picture. Width is left to right; depth is front to back.
   - **Category** becomes a tray tab.
   - **Seats** is how many guests can perch on it: visiting guests sit on free seats (see [GUESTS-AND-PROGRESS.md](GUESTS-AND-PROGRESS.md)).
   - **Cosiness** (how much it adds to the cafe's cosiness: basic furniture 1-3, special things much more) and **style** (`garden`, `rustic`, `glow`, `fancy`..., for the sign).
2. Import the design data, then either run **Create Placeholder Placeables**, or make a real prefab:
   - root at the **floor contact point** (centre of the base), no scale;
   - a child called **`Visual`** with the art (any scale; sorting layer World, sort point Pivot, pivot at the bottom);
   - one **`SeatPoint`** child per seat, where a guest's bottom goes, with the way they face;
   - no need for a collider: `PlacedObject` adds and sizes the footprint from the asset.
3. Assign the prefab on the placeable asset (`Assets/Data/Placeables`). **Validate Design Data** warns if its seat points don't match its seat count.

## Things on tables, on the tree, and fairy lights

**Importing overwrites these numbers.** `placement`, the footprint and `chainSpan` come from `Design/design-data.json`, so put them there as well as on the asset, or the next import resets them.

### On a table (`"placement": "Surface"`): the fern umbrella

**On the ground or a table:** set `"placement": "Floor"` and `"alsoOnTables": true`, as the potted plant and tree lantern are. Held near a free spot on a table, it snaps there; anywhere else, it goes on the ground. On a table it doesn't block walking and has no ground shadow. *Scale On Table* on the placeable asset sets its size up there.

The table's spots are `SurfacePoint`s, as below. The stump and the fancy table have one each, so each holds one thing. The game supports more, if a big table ever wants them.

1. **On the table's prefab:** add an empty child of the root (not inside `Visual`) for each spot, with the **SurfacePoint** component, where the small thing's base should touch. They show as small blue circles in the Scene view. Leave their order alone once saves exist: a save remembers things by which spot they're on.
2. **On the small thing's prefab:** root at the point its base touches, art in `Visual`, as usual. No footprint: the footprint numbers aren't used.

In play: it snaps to the nearest free spot (*Surface Snap Radius* on `BuildMode`) and goes red away from one. It moves and flips with the table. Putting the table away puts what's on it in the bag too.

### On the tree (`"placement": "Wall"`): windows

1. **In the scene:** add an empty object (under `World` is tidy) with **PlacementZone** (it adds a PolygonCollider2D). Click *Edit Collider* and trace the part of the tree things can go on. It doesn't block walking or clicks. *Label* is what the player's told: "It needs to fit on the tree." You can have several zones.
2. **On the window's prefab:** root at the **bottom middle** of the window, art in `Visual`.
3. **On the placeable:** the footprint is the window's size: `footprintWidth` across, `footprintDepth` up. That whole rectangle has to fit inside the polygon, so make it match the art.

In play: it snaps to the grid, goes red if any of it is off the polygon or on another window. It draws just above the backdrop (the Background sorting layer, order 10), set automatically, so anything in the world goes in front of it.

### Fairy lights (`"placement": "Floor"` with `"chainSpan"`)

In build mode, a tap on anything else under the lights picks that up first. Only the poles and the string pick up the lights, so the space between them is free.

A set is one placeable: the lights with a pole at each end.

1. **On the prefab:** root at the middle, on the ground line. `Visual` holds the lights (and no poles). Add two children called exactly **`PoleLeft`** and **`PoleRight`**, holding each pole's art, with each pole's base at the prefab's `chainSpan / 2` either side of the root.
2. **On the placeable:** `chainSpan` is the distance between the two poles' bases, in world units. The footprint is now **one pole's** base. Only the poles block walking, so the gecko walks underneath the lights.

In play: drag a set near another's free end (*Chain Snap Radius* on `BuildMode`) and it snaps on in a straight line, sharing a pole. You can join as many as fit, and different lengths can join. At a join, the set on the right hides its `PoleLeft`. Nothing about joins is saved: sets are joined when their ends are in the same place, so moving one away brings its pole back.

## Test checklist

**Build mode**
- [ ] Build appears in the cafe; Bag appears in every scene set up with the tool (it comes from `GameHud` on Systems).
- [ ] Entering pauses the game and the gecko ignores clicks. Escape doesn't open the pause menu while building.
- [ ] Each tray tab shows the right items; tapping one gives a ghost on a free spot.
- [ ] Dragging is smooth and snaps; on a phone the item sits above your finger.
- [ ] Zoom: pinch (phone) and scroll (mouse) zoom in to 2.5x and back; scrolling over the tray scrolls the tray, not the camera.
- [ ] Pan: two-finger drag, right/middle-button drag, and dragging empty ground all move the view, which stops at the backdrop's edges.
- [ ] Zoomed in, placing, moving and the floating buttons still line up with the item.
- [ ] Done eases back to the normal view (and the gecko's camera is unchanged afterwards).
- [ ] Red with the right reason: off the ground, on another item, on the door's stand point, on the spawn point, closing the only path past something.
- [ ] Place, Flip, Cancel work. Picking up an existing item, moving it, Done, and Put away work.
- [ ] Tapping the UI (tray, buttons) never moves anything underneath.
- [ ] Leaving with the gecko inside new furniture: the gecko steps out.
- [ ] Walking afterwards goes around everything placed.

**Saving**
- [ ] Place a few things, reload (or close and reopen the browser tab): everything is where you left it.
- [ ] *Save: reset everything* gives an empty cafe.
- [ ] Placing uses an item from the bag and putting away returns it; the tray only shows what's in the bag.

**Bag**
- [ ] Opening pauses the game; Escape and Close close it.
- [ ] Tabs show counts; tapping an item shows its description.
- [ ] Coins on the HUD update after *Bag: give 100 coins*.

## Not built yet

- More ways to earn placeables (more quest rewards, rare finds) and special high-cosiness pieces.
- Rug placement.
- Real art and a designed UI (the build tray and bag are still built in code, in `UI/BuildUi.cs` and `UI/InventoryPopup.cs`; the HUD is a prefab, see [UI.md](UI.md)).
- Using, selling and sorting from the bag (set aside for later).

## Shadows and bases

**Tools > Bush > Placeable Shadows and Bases** lists every floor placeable with its art, its base (green) and its shadow.

- **Base:** what blocks walking. *Fit Base To Art* measures the bottom of the picture, and you can type over it. It's saved to the asset and to `Design/design-data.json`, so an import keeps it. If the gecko clips round everything, raise *Clearance* on `WalkableGrid` too.
- **Shadow:** every floor placeable gets one by itself, sized from its base. Change the size, position and strength here, or untick it.
- **Your own shadow art:** add a child called `Shadow` to the prefab, and that's used instead.
- **Anything else:** add a **GroundShadow** component to the gecko, a character or a scene prop. On a character, a child called `Shadow` or `GroundShadow` beside `Visual` turns round with them by itself, and highlighting leaves it alone.

## Little lights

Add **NightLight** to any object, on a placeable prefab or in a scene. It brings a Light 2D with it, off by day, fading on at dusk and night, with a gentle flicker. Set the colour, brightness and size on the Light 2D, and *Flicker Amount* and *Flicker Speed* on NightLight. Include `World` and the floor layers in the light's *Target Sorting Layers*, or it won't light the ground. On something that can go on a table, make it a child of `Visual`, so it moves with the art. One soft light for a set of fairy lights is enough: lots of lights are slow on phones.
