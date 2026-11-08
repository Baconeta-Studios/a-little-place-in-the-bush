# Audio

Everything is hooked up and silent until it has clips. Any row with no clips does nothing, so add them in any order.

## Adding sounds

1. Drop files into `Assets/Audio`: `Music`, `Ambient`, `UI`, `SFX`, or `Voices/<Creature>`. New files get sensible import settings from their folder (music streams, short sounds are mono and ready instantly).
2. Name each file after its row below, with a number on the end if there are several: `UiTap.wav`, `UiTap2.wav`. Spaces, capitals and underscores don't matter.
3. Run **Tools > Bush > Audio > Fill Sound Library From Folders**. It adds them, keeps anything you put in by hand, and tells you about any names it couldn't match.

Or open **Tools > Bush > Audio > Open Sound Library** (`Resources/Audio/SoundLibrary.asset`) and drag clips into the rows. Each row also has a volume and a pitch variation. If a row has several clips, it picks one at random.

A sound named for its row joins the library by itself when Unity imports it, so Fill is only needed for a batch. Raw recordings kept in a `Source` folder are skipped. [`Tools/SoundMaker`](../Tools/SoundMaker/README.md) mixes the ones in `Assets/Audio/SFX/Source` into the game's sounds.

Volumes go through `Assets/Audio/AudioMixer.mixer`, so the Settings sliders work: music (with the bush sounds under it), sound effects, and dialogue, which is every creature voice.

## Creature voices

One line of gibberish plays each time a creature says something, and the next line cuts it off. That's every line in the dialogue box: chats, quests, shops and scenes like the date. A creature goes through all their clips in a shuffled order before any comes round again. Voices play on the Dialogue slider in Settings.

Narration is silent. Put actions in `<i>…</i>`, `(…)` or `*…*`. A line that's only that, like "<i>Gus gives you a friendly nod.</i>", plays no voice. A line with words as well, like "<i>(a bite of the scroll)</i> I was seven", still does (`DialogueText.IsSpoken`).

A clip dropped in a creature's folder joins their voice by itself when Unity imports it. Fill does the same for everything at once.

- **Recording:** 4 to 8 short clips per creature, 1 to 3 seconds each, is plenty. Leave a tiny bit of silence at each end, and record somewhere quiet.
- **Filing:** put them in `Assets/Audio/Voices/Ruru`, `Kowhai`, `Kunekune`, `Snail`, `Spider`, `Weka`, `Pekapeka` or `Gus`, then run Fill. Any file name works here.
- **One long take instead:** a creature's whole recording can sit loose in `Assets/Audio/Voices/` (Fill skips loose files there). [`Tools/VoiceSplitter`](../Tools/VoiceSplitter/README.md) cuts it into cleaned-up lines and remixes in their folder.
- **Sharing:** each creature's *Pitch* (0.5 to 2) can turn one recording into several voices. Clips in `Voices/Default` are for anyone without their own.

## The sounds

| Row | Plays | Suggestion |
|---|---|---|
| `MusicMainMenu` | The main menu | Loop, 1 to 2 minutes |
| `MusicCafeDay` | At the cafe, morning and afternoon | Loop, 2 to 3 minutes |
| `MusicCafeEvening` | At the cafe once foraging is done | Loop, can be a softer version |
| `MusicCafeNight` | At the cafe at night | Loop, sparse |
| `MusicForaging` | While the forage screen is open | Loop |
| `AmbientBushDay` | Under the music all day | Loop, birds and wind |
| `AmbientBushNight` | Under the music at night | Loop, crickets and moreporks |
| `UiTap` | Any button | Very short, soft |
| `UiBack` | Back, Bye, Not now, Resume, x | Very short, a little lower |
| `UiNope` | A greyed-out button | Very short, dull |
| `PaperOpen` | A screen opening | Soft paper rustle |
| `PaperClose` | A screen closing | Softer rustle |
| `Footstep` | Each step | Very quiet, 2 to 4 clips |
| `ForageSetOff` | Pressing Go on a trip | Leaves rustling |
| `ForageFind` | Each find landing in the basket | Soft plop, 2 to 3 clips |
| `ForageRareFind` | A rare find | Brighter, a small sparkle |
| `ForageHome` | Getting back from a trip | Short and warm |
| `ForageRustle` | The bush rustling in the peek-a-boo, each half second | Leaves, under a second |
| `Coins` | Coins going up | Short clink |
| `QuestStarted` | Taking on a quest | Pencil scribble |
| `QuestDone` | Finishing a quest | Small happy flourish |
| `RecipeLearned` | Learning a recipe | Page turn and chime |
| `PerkLearned` | Learning a companion's perk | Small chime |
| `UpgradeBought` | Buying an upgrade | Hammer tap and coins |
| `MenuChanged` | A dish going on or off the menu | Paper pinned |
| `DayEnded` | Closing the cafe | Door closing, bell |
| `NightFalls` | Night starting | Soft owl call |
| `Morning` | A new morning | Birdsong sting |
| `PlaceObject` | Putting something down in build mode | Soft thud |
| `PickUpObject` | Picking something up in build mode | Light lift |
| `PutAwayObject` | Back in the bag in build mode | Rustle |

## How it works

- `GameAudio.Play(GameSound.Coins)` is how the code asks for a sound. `PlayMusic` and `PlayAmbient` fade across. If the next row has the same track, it carries on rather than starting again. `Say(npc)` is a creature's voice.
- `AudioDirector` makes itself at startup. It picks the music and background sound for the moment, plays a sound for every button by itself (including ones you add later), does footsteps, and plays the sounds for game events.
- `SoundLibrary` holds the clips. `GameSound` in `Core/Services/SoundChoice.cs` lists every sound. To add one, add it there with a new number, say where it plays in `SoundLibrary.WhereItPlays`, and call `GameAudio.Play` where it happens.
- They play through the existing `AudioManager`, `CustomAudioSource` and mixer. `AudioManager` is made from its prefab if the scene has none. `AudioWrapper` is left as it was, with its own empty list: nothing uses it.
