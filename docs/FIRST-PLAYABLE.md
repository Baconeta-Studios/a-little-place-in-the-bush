# First playable: setup and testing

How to get click-to-walk, interaction and area travel running in a scene, and how to test them (Trello list 03). How it works is in [ARCHITECTURE.md, sections 5 and 6](ARCHITECTURE.md#5-game-layer-monobehaviours).

## Set up the cafe (about 5 minutes)

1. Open `Assets/Scenes/Cafe.unity`.
2. Run **Tools > Bush > Set Up Area Scene**. It only adds what's missing and is undoable. The Console lists what it did:
   - `WalkableGrid` + `GridPathfinder` on `World` (pointing at `Floor/WalkableArea`, obstacles on the `Obstacle` layer)
   - `CharacterMover`, `Interactor` and `PlayerPointerInput` on `Characters/Gecko` (with the input actions asset and the camera)
   - `AreaInfo` (display name, marked as the hub) and `GameplayCheats` on `Systems`
   - `Markers/Spawn_Default` where the gecko stands
   - a placeholder `Static/Counter` to test interaction, and `Markers/Exit_Garden` on the left edge
   - the template `PopupCanvas` (pause menu), if the scene has none
3. Select `World` and check the gizmos: green cells are walkable, red are blocked by footprints. Move the counter and the exit (and their `StandPoint` children) wherever you like. Each `StandPoint` must sit on a green cell.
4. Check `HUD` doesn't have a full-screen graphic with **Raycast Target** ticked, or every click will count as a UI click.
5. Press Play. Click the floor, the counter, the props, the sky and the HUD.
6. Save the scene and commit it.

The exit needs a target scene before it does anything; until then clicking it walks there and logs a warning.

## Make the garden (for the hub-to-garden transition)

1. Duplicate `Cafe.unity` as `Garden.unity` (same hierarchy, so the setup tool works), change the backdrop and props, and reshape `Floor/WalkableArea`.
2. On `Systems/AreaInfo`: set **Display Name** to "The Garden" and untick **Is Hub**.
3. Rename the exit to `Exit_Cafe`, move it to the right edge, set **Target Scene** to `Cafe` and **Destination Name** to "the cafe". Add a `SpawnPoint` next to it with id `from_cafe`.
4. Back in the cafe: set `Exit_Garden`'s **Target Scene** to `Garden` and **Target Spawn Id** to `from_cafe`. Add a `SpawnPoint` with id `from_garden` next to it, and set the garden's exit **Target Spawn Id** to `from_garden`.
5. Add `Garden.unity` to **File > Build Profiles > Scene List**. `AreaTravel` refuses scenes that aren't in the build.

## Test checklist

From the Trello cards *Check click-to-walk feel and edge cases* and *Test first playable in browser*. Do it in the editor first, then in a WebGL build on desktop and on a phone.

**Walking**
- [ ] Click the floor: the gecko walks there, faces the way it's going and stops cleanly.
- [ ] Click a new spot mid-walk: it turns and goes there straight away.
- [ ] Click rapidly in different places: no jitter, no getting stuck.
- [ ] Click behind a prop: it walks around, never through.
- [ ] Click on a prop or footprint: it walks to the nearest spot it can reach.
- [ ] Click the sky or off the floor: it walks to the closest edge of the floor.
- [ ] Hold the button or finger and drag: it keeps following.
- [ ] Click the HUD or the pause button: the gecko doesn't move.
- [ ] Open the pause menu: clicks behind it don't move the gecko, and the gecko stops while paused.

**Interacting**
- [ ] Walk near the counter: a marker appears above it (touch players need this, since they have no hover).
- [ ] Hover it with a mouse: it brightens and shows its prompt.
- [ ] Click it from far away: the gecko walks to the stand point, faces it, and the Console logs the interaction once.
- [ ] Click it while already standing at it: it interacts straight away.
- [ ] Click it, then click the floor before arriving: it cancels, no interaction.
- [ ] Click it while already walking somewhere else: it re-routes to the counter.
- [ ] On a phone, tap slightly off the counter: it still counts (tap tolerance).

**Areas** (once the garden exists)
- [ ] Click the exit: walk, fade out, arrive at the garden's matching spawn point, fade in, "The Garden" shows.
- [ ] Click the exit twice fast, or click during the fade: only one transition happens, and clicks during it do nothing.
- [ ] Go back and forth a few times: always the right spawn point, never stuck.
- [ ] Switch browser tabs during a fade: it finishes cleanly (the template auto-pause opens the pause menu).

**Getting unstuck** (cheat menu: ` or F1, or triple-tap the top-left corner on a phone)
- [ ] *Gecko: back to the spawn point*, *Area: return to the cafe* and *Area: rebuild the walkable grid* all work.
- [ ] *Gecko: walk 3x faster* is handy for testing big areas.

**Playtest** (*Test first playable in browser*): give someone the build without explaining the controls. Note where they click first, whether they find the counter and the exit, and anything they try that doesn't work.

## Tuning knobs

| Where | Setting | What it does |
| --- | --- | --- |
| Gecko > CharacterMover | Speed | Walk speed in units per second (3.5 by default; the safe area is 19.2 wide). |
| Gecko > PlayerPointerInput | Tap Tolerance | How far a tap can miss an interactable and still hit it. |
| Gecko > PlayerPointerInput | Hold To Steer | Keep walking towards the pointer while held. |
| Gecko > PlayerPointerInput | Click Marker Prefab | Optional little effect where you click. |
| World > WalkableGrid | Clearance | How much space to keep around prop footprints. |
| Interactable | Interact Range | How close counts as "already there". |
| InteractionPrompt | Show Distance | How near the gecko must be for the marker to show (0 = always). |
| AreaTravel (created at runtime) | Fade times, name time | Edit the defaults in `AreaTravel.cs`. |
