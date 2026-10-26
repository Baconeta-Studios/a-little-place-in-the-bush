# A Little Place in the Bush

A cosy 2D RPG / adventure sim set in Aotearoa New Zealand's native bush, by **Baconeta Studios & Publishing**.

Play as a native NZ gecko with a dream of running your own cafe. Forage in the bush for supplies, help the neighbours with quests, learn recipes, and turn the barren space around your cafe into something that's your own, building relationships with friends and enemies along the way.

- **Genre:** RPG cosy adventure sim
- **Platform:** PC Standalone and Web
- **Audience / rating:** Ages 8+, E (Everyone)
- **View:** Third-person 2D "stage" view with depth: a walkable floor in front of the cafe, where higher up the screen is further back
- **Input:** Keyboard and mouse, touch screen
- **Mode:** Single-player (multiplayer marketplace planned for phase 2)

Full GDD (Canva): https://www.canva.com/design/DAHY_0K-xoA/AdOWDfjHg6R3XCjFi38QHQ/view

## Current state

**Greybox.** The project was created from the Baconeta [Ludum Dare template](#template-systems). The first game scene is in, but there's no gameplay code yet. What exists today:

- **Cafe greybox** (`Assets/Scenes/Cafe.unity`), the only enabled scene in the build list: backdrop, floor with its walkable area, placeholder props with footprints, and a capsule stand-in for the gecko. Its layout, sorting and camera setup are described in [ARCHITECTURE.md, section 6](docs/ARCHITECTURE.md#6-world-camera-and-movement).
- `CameraFit` (`Assets/Scripts/Camera`), which keeps the 16:9 safe area on screen at any aspect ratio.
- Sorting layers, physics layers and Y-axis depth sorting set up in Project Settings and the 2D renderer.
- Template menu and volume test scenes in `Assets/Scenes/Templates/`, kept for reference but disabled in the build list.
- **Game rules in `Bush.Core`** (`Assets/Scripts/Core`): economy, foraging, cooking and quests, as plain C# with EditMode tests. No UI uses them yet.
- **Design data**: items, recipes, companions, perks, forage tables, NPCs and a draft main quest in `Design/design-data.json`, imported into ScriptableObjects with **Tools > Bush > Import Design Data**. See [docs/MAIN-QUEST.md](docs/MAIN-QUEST.md).
- **Forage sandbox** (`Tools/ForageSandbox/index.html`): a standalone web page for trying out and tuning foraging without Unity. It uses the same rules and gives the same results for the same seed.
- Template systems: UI state switching, popups (pause/settings/end game/high scores), audio manager with mix groups, scene loader, achievements, Unity Analytics bootstrap, cheat menu, prefab pooling.
- URP 2D renderer setup.
- Firebase/Terraform scaffolding in `Infrastructure/`. Not provisioned for this game yet.

Still to do from the template: rename the product (Player Settings still says `ld-template`), build the game's own menu to replace the template one, and decide whether to enable Git LFS before art and audio go in.

## High-level design goals

From the GDD (v1.0, 25 Oct 2026):

**Core loop:** manage the cafe, forage for supplies, do quests for the neighbours, then use what you earn to learn recipes, grow the cafe and build up the space.

**Key features**
- A wide range of items to build and place
- A fun foraging system with RPG-style luck, companions and perks
- A narrative main quest layered over a semi-sandbox

**Game rules**
- One mode: free, semi-sandbox RPG.
- Time passes automatically, with a short day/night cycle. Some critters only show up at certain times.
- Click to walk and interact. Paths at the edges of each area lead to the other areas.
- **Win:** finish the main story quest line. Free play is encouraged afterwards.
- **Lose:** there is no explicit lose condition.

**Areas / screens**

| Area | What you do there |
| --- | --- |
| Cafe | Build mode (pauses) to place and move items from inventory; manage the menu and recipe list; talk to the day's guests; read mail at the snailbox |
| Garden | Plant seeds and manage what's growing; talk to garden critters; path to foraging |
| Village | Walk around, talk to the local critters, take and complete quests |
| Foraging | Paused setup screen: pick companions, perks and goals, send the expedition, then collect rewards and any learned perks |

**Player tracking:** main currency (earned from the cafe), quest completions, inventory, cafe larder.

**Art and audio:** character design inspired by the Moomin games, with an earthy, semi-realistic bush palette. Music should be gentle, curious, cosy and ethereal. SFX are organic and natural (water droplets, rustling leaves) over a soft wind and forest ambience.

**Future ideas (phase 2+):** online player marketplace, villager relationships, visiting other players' cafes, decorating the garden, and character customisation (aprons, hats, shoes).

## Getting started

### Requirements
- **Unity 6000.6.7f1** (exact version, see `ProjectSettings/ProjectVersion.txt`). Install it through Unity Hub. Opening with a different version will trigger an upgrade, so don't commit that.
- Git 2.40+ and [Git LFS](https://git-lfs.github.com/). LFS is installed but **not yet enabled** for this repo (see `.gitattributes`).
- An IDE: Rider or Visual Studio (both IDE packages are included).

### Opening the project
1. `git clone https://github.com/Baconeta-Studios/a-little-place-in-the-bush.git`
2. In Unity Hub, click **Add > Add project from disk** and select the cloned folder.
3. Open it with **6000.6.7f1**. The first import regenerates `Library/` and can take several minutes. It also pulls the `com.eflatun.scenereference` package from GitHub, so you need network access.
4. Open `Assets/Scenes/Cafe.unity` and press Play.

`Library/`, `Temp/`, `Logs/`, `UserSettings/`, `.csproj`/`.sln` files and builds are git-ignored and regenerated locally. Never commit them.

### Game view sizes
Unity saves custom Game view sizes per computer (in `%APPDATA%/Unity/Editor-5.x/Preferences/GameViewSizes.asset`, shared by all your projects), not in the project, so they aren't in git. Add these once with the **+** at the bottom of the Game view's resolution menu (type *Fixed Resolution*) to check the layout at each aspect ratio:

| Name | Size | Aspect |
| --- | --- | --- |
| 16:9 | 1920 × 1080 | 16:9, the safe area exactly |
| iPad | 2048 × 1536 | 4:3, the tallest screen we support |
| MacBook | 2560 × 1600 | 16:10 |
| iPhone | 2532 × 1170 | ~19.5:9, a wide phone |

The yellow `CameraFit` gizmo should stay fully on screen at every size.

### Design data and tests
- **First time, or after pulling design changes:** run **Tools > Bush > Import Design Data (Design folder)**. It creates or updates the assets in `Assets/Data` from `Design/design-data.json`. Then run **Tools > Bush > Validate Design Data**.
- **Tuning foraging without Unity:** open `Tools/ForageSandbox/index.html` in a browser (see its [README](Tools/ForageSandbox/README.md)). Export JSON from it, then import as above. The rules are in [docs/FORAGING.md](docs/FORAGING.md).
- **Tests:** **Window > General > Test Runner > EditMode > Run All**.

### Build targets
- **Primary (jam):** Web (WebGL) in desktop browsers, 960×600 default canvas. You need the **WebGL Build Support** module for 6000.6.7f1 (Unity Hub > Installs > Add modules). The template UI is also phone-browser friendly (see [Mobile web](#mobile-web)).
- **Later:** PC Standalone (Windows), 1920×1080 default.

Build via **File > Build Profiles**, choose the platform, and build into `Builds/` (git-ignored).

Hosted build: _TBD_ (add the itch.io link here once the first browser build is published).

## Architecture
See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for how the game code is structured, how it maps to the staged production plan, and the open design decisions.

## Branching and workflow

- `main` is always in a state that opens and plays. Don't push broken work directly to it.
- Work on short-lived branches off `main`, named by type:
  - `feature/<short-name>` for new gameplay or content, e.g. `feature/foraging-setup`
  - `fix/<short-name>` for bug fixes
  - `chore/<short-name>` for tooling, settings, package updates and docs
- Open a pull request into `main` and squash or merge once it opens cleanly and someone has had a look.
- Unity YAML (`.unity`, `.prefab`, `.asset`, `.meta`) is set up for Unity's Smart Merge (`unityyamlmerge`). Even so, avoid two people editing the same scene or prefab at once, and coordinate in chat.
- Always commit `.meta` files together with their assets.

## Development team
- Josh Villgren

---

## Template systems

This project started from the Baconeta Ludum Dare template. These notes cover the systems it ships with.

- Basic generic achievement system (uses PlayerPrefs, so it isn't cheat safe)
- Scalable and generic UI composite state switching system with template setups
- Editor tools, including ReadOnlyAttributes for inspector vars and in-place struct editing for inspector references
- Audio system with mix groups and easy volume integration
- A simple template main menu screen
- Analytics basics and startup logic
- Popups: pause, settings, end game (with high score entry) and high score table
- Local high scores (PlayerPrefs, top 10)
- Developer cheat menu
- Prefab pooling helper

### Popups
Drop `Assets/Prefabs/UI/Popups/PopupCanvas.prefab` into any gameplay scene (the scene needs an EventSystem).
- **Pause**: the on-screen pause button (top-right, inside the safe area), Escape, or gamepad Start opens the pause popup. Escape/Start also closes the top-most popup. The game auto-pauses when the player switches app or browser tab (not in the editor). Untick `Allow Pause` on `PauseInput` in menu scenes.
- **End game**: `FindAnyObjectByType<EndGamePopup>(FindObjectsInactive.Include).Show(won, score);` (or hold a serialized reference). Shows name entry automatically when the score makes the table.
- **High scores**: open `HighScoresPopup` from a main menu button via `SimplePopup.Open`.
- Popups with `Pauses Game` set freeze `Time.timeScale` while open. It's restored on close or scene load. Use unscaled time for any UI animation.
- Make your own popup by inheriting from `Popup` (or use `SimplePopup`). The prefab is generated by `Tools > Jam Template > Rebuild Popup Canvas Prefab`. You can restyle it by hand freely, but re-running the tool overwrites it.

### High scores
`HighScoreManager` is static: `IsHighScore(score)`, `SaveScore(name, score)` (returns rank), `GetTopScores()`, `ClearAll()`. Set `HighScoreManager.LowerIsBetter = true` for time trials. Local only. For online leaderboards, look at Unity Gaming Services Leaderboards (the project already uses UGS for analytics).

### Cheat menu
Press **`** or **F1** in the editor or a development build, or **triple-tap the top-left corner** on a phone. Built-in options: time scale, reload/next scene, clear high scores, clear PlayerPrefs, FPS counter. Add your own from game code:
```csharp
CheatMenu.AddButton("Add 100 score", () => score += 100, this);
CheatMenu.AddToggle("God mode", () => godMode, v => godMode = v, this);
```
To include it in a release build for playtesters, add `ENABLE_CHEATS` to Scripting Define Symbols.

### Object pooling
Use Unity's built-in `UnityEngine.Pool.ObjectPool<T>`, or the `Utils.PrefabPool<T>` wrapper for prefabs:
```csharp
_bullets = new PrefabPool<Bullet>(bulletPrefab, transform);
Bullet b = _bullets.Get(position, rotation);
_bullets.Release(b);
```
Reset per-use state in `OnEnable`. Only pool things that are spawned often (particles, pickups, critters). Otherwise `Instantiate`/`Destroy` is fine.

### Mobile web
The web build should be playable in a phone browser:
- Popup buttons are sized for thumbs (110px at 1080p reference). Keep your own buttons at least that size.
- Wrap HUD elements in an object with Unity's **Safe Area** component (Add Component > UI (Canvas) > Safe Area, tick all Edges) so they avoid notches and rounded corners. Don't put it on the root Canvas or on an object driven by a layout group.
- `UnityHelper.IsMobile` is compile-time (native Android/iOS builds only). Use `UnityHelper.IsMobileDevice` to detect phones running the WebGL build, e.g. to show on-screen controls.
- Don't rely on hover, right-click or keyboard-only input. For on-screen sticks/buttons, use the Input System's `OnScreenStick` / `OnScreenButton` components.
- Browsers block audio until the first tap, so start music from a menu button rather than on load.
- Test on a real phone early: upload a build to a private itch.io page and open it on your phone.

### Rendering (URP 2D)
The project uses the Universal Render Pipeline with the 2D Renderer (`Assets/URPDefaultResources`, one URP asset per quality level, all sharing `Default_2D_Renderer`).
- New sprites use the lit sprite material, so 2D lights (Light 2D) work out of the box. Add a Global Light 2D to a scene to control overall brightness, which is handy for the day/night cycle.
- WebGL uses the "High" quality level by default. Drop it to Medium in Project Settings > Quality if mobile browsers struggle.

### TextMesh Pro
TMP Essential Resources live in `Assets/TextMesh Pro`. Keep them, because the template UI depends on the LiberationSans SDF font.

## Optional services

### Unity Analytics
1. Set up a Unity project at https://dashboard.unity3d.com/gaming
2. Set up Analytics in that project by selecting 'Start Using Analytics'
3. Follow the steps to link the UGS project ID and organisation in Unity's Project Settings
4. Make sure the first screen you want analytics to start from has a game object with `UnityAnalytics.cs` attached before any other analytics code is called

Docs: https://docs.unity.com/analytics/manual/UnityAnalytics

To enable analytics debug logging in the editor, add `UNITY_ANALYTICS_EVENT_LOGS` to **Project Settings > Player > Script Compilation > Scripting Define Symbols**.

### Firebase infrastructure (not yet provisioned)
Kept from the template as a possible backend for the phase 2 marketplace.

Sign up for [Google Cloud](https://cloud.google.com/free), then, as per https://firebase.google.com/docs/projects/terraform/get-started:
1. Install [Terraform](https://learn.hashicorp.com/tutorials/terraform/install-cli?in=terraform/gcp-get-started)
2. Install the [Google Cloud CLI](https://cloud.google.com/sdk/docs/install-sdk)
3. Log in with `gcloud auth application-default login`
4. Edit [Infrastructure/vars.tf](./Infrastructure/vars.tf) and set the values you need. Make sure to set `unrelated_gcp_project_to_verify_billing` to the ID of the project Google Cloud created when you signed up.
5. Run `terraform -chdir=Infrastructure init`
6. Run `terraform -chdir=Infrastructure apply -auto-approve`
7. View the infrastructure at https://console.firebase.google.com/ or https://console.cloud.google.com/welcome

If you get a 403 saying the Firebase Management API (or another permission) has not been enabled, wait a little while and run `terraform apply` again.

To connect Unity (as per https://firebase.google.com/docs/unity/setup#add-config-file):
1. Open the [Firebase console](https://console.firebase.google.com/project/), select the project, then go to **Project Settings > General > Your apps**
2. Download `google-services.json` (Android) and `GoogleService-info.plist` (iOS) into [Assets](./Assets/)
3. Download [Cloud Firestore](https://developers.google.com/unity/packages#cloud_firestore) and [Firebase Authentication](https://developers.google.com/unity/packages#firebase_authentication) from https://developers.google.com/unity/packages#firebase
4. Import them via **Assets > Import Package > Custom Package**
