# Sound Maker

`make_sfx.py` mixes the raw sounds in `Assets/Audio/SFX/Source` into the game's sounds. Each one is named for its row in the Sound Library, so it files itself: buttons and screens go in `Assets/Audio/UI`, the rest in `Assets/Audio/SFX`.

```bash
pip install numpy scipy soundfile
python Tools/SoundMaker/make_sfx.py Assets/Audio
```

Each sound in `RECIPES` is a list of layers: which raw sound, when it starts, its pitch (which changes speed too, like a record) and how loud. Then comes how loud the finished sound should be. For example, the bush rustle is four single leaves landing a moment apart, at slightly different pitches, so they sound like a handful.

| Raw sound | Used for |
|---|---|
| `hover` (a tiny tick) | UiTap, UiNope (lower and duller), PickUpObject (higher), PlaceObject (lower), MenuChanged (two ticks), ForageFind (3 low plops), the plop before ForageRareFind |
| `discard1` | UiBack, ForageSetOff and ForageHome (soft swishes) |
| `draw1` (paper) | PaperOpen, PaperClose (a touch lower), PutAwayObject (higher, into the bag) |
| `dropleaves1`, `dropleaves2` | ForageRustle (3 layered takes) only, so the bush you can tap is the only thing that rustles. Also faintly under PlaceObject |
| `soul_collect` | ForageRareFind |

Files in a `Source` folder are never put in the library.

Unity's **Normalize** import option boosts every clip to full volume, which undoes the balancing here. It's off for these sounds and the creature voices. Turn it off on any new sound mixed this way.
