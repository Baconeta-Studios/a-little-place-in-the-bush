"""Mixes the raw sounds in Assets/Audio/SFX/Source into the game's sounds, named for their Sound Library rows so they
file themselves: UI ones in Assets/Audio/UI, the rest in Assets/Audio/SFX.

Each recipe is a list of layers: (source, start seconds, pitch, gain dB). Pitch changes speed too, like a record.
Every result is trimmed, faded, brought to its loudness and peak-limited, as mono 44.1 kHz WAV.

    python Tools/SoundMaker/make_sfx.py Assets/Audio
"""
import os, sys
import numpy as np, soundfile as sf
from scipy.signal import resample_poly, butter, sosfiltfilt

AUDIO = sys.argv[1]
SRC = os.path.join(AUDIO, "SFX", "Source")
SR = 44100
# Everything at once, in dB: -6 is half as loud. At 0 they were too loud next to the music, even at half volume.
OVERALL = -6

SOURCES = {
    "hover": "hover.mp3",          # a tiny tick
    "discard": "discard1.mp3",     # a soft swipe away
    "draw": "draw1.mp3",           # a paper rustle
    "leaf1": "dropleaves1.mp3",    # one leaf, short
    "leaf2": "dropleaves2.mp3",    # one leaf, longer and softer
    "soul": "soul_collect.wav",    # a sparkle
}

# name: (folder, loudness target in dB RMS while sounding, layers, extra) ; extra: lowpass Hz
RECIPES = {
    # Buttons and screens
    "UiTap":        ("UI", -22, [("hover", 0, 1.0, 0)], None),
    "UiBack":       ("UI", -22, [("discard", 0, 1.0, 0)], None),
    "UiNope":       ("UI", -27, [("hover", 0, 0.72, 0)], 2200),          # lower and duller: it didn't work
    "PaperOpen":    ("UI", -24, [("draw", 0, 1.0, 0)], None),
    "PaperClose":   ("UI", -25, [("draw", 0, 0.86, 0)], None),           # the same paper, a touch lower
    # Foraging
    # A find dropping into the basket: the tick pitched well down into a soft plop, with a muffled leaf under it.
    "ForageFind":   ("SFX", -24, [("hover", 0, 0.62, 0), ("leaf1", 0.005, 0.85, -14)], 3500),
    "ForageFind2":  ("SFX", -24, [("hover", 0, 0.7, 0), ("leaf2", 0.005, 0.9, -15)], 3500),
    "ForageFind3":  ("SFX", -24, [("hover", 0, 0.56, 0), ("leaf1", 0.005, 0.8, -14)], 3200),
    "ForageRareFind": ("SFX", -21, [("hover", 0, 0.62, -4), ("leaf1", 0.005, 0.85, -18), ("soul", 0.06, 1.0, 0)], None),
    # A bush rustling: single leaves layered on top of each other a moment apart, so it sounds like a handful.
    "ForageRustle":  ("SFX", -22, [("leaf1", 0, 1.0, 0), ("leaf2", 0.07, 1.06, -2), ("leaf1", 0.16, 0.9, -3), ("leaf2", 0.24, 1.12, -6)], None),
    "ForageRustle2": ("SFX", -22, [("leaf2", 0, 0.95, 0), ("leaf1", 0.05, 1.1, -2), ("leaf1", 0.19, 1.0, -4), ("leaf2", 0.27, 0.88, -7)], None),
    "ForageRustle3": ("SFX", -22, [("leaf1", 0, 1.08, 0), ("leaf1", 0.09, 0.94, -1), ("leaf2", 0.13, 1.02, -3), ("leaf1", 0.3, 1.18, -8)], None),
    # Pushing off into the bush, and back out of it
    # Pushing off: a soft swish, not leaves, so the first rustle you hear is the bush you can tap.
    "ForageSetOff": ("SFX", -24, [("discard", 0, 0.8, 0)], 6000),
    "ForageHome":   ("SFX", -22, [("leaf2", 0, 1.1, -6), ("leaf1", 0.12, 1.0, -3), ("leaf2", 0.22, 0.95, 0), ("hover", 0.5, 0.9, -6)], None),
    # Build mode and the menu board
    "PickUpObject": ("SFX", -23, [("hover", 0, 1.22, 0)], None),
    "PlaceObject":  ("SFX", -23, [("hover", 0, 0.82, 0), ("leaf1", 0.01, 0.8, -12)], 4500),
    "PutAwayObject": ("SFX", -24, [("draw", 0, 1.18, 0)], None),         # into the bag
    "MenuChanged":  ("UI", -23, [("hover", 0, 1.0, 0), ("hover", 0.07, 1.26, -2)], None),
}


def load(name):
    x, sr = sf.read(os.path.join(SRC, SOURCES[name]), always_2d=True)
    x = x.mean(axis=1)
    if sr != SR:
        x = resample_poly(x, SR, sr)
    x = x - x.mean()
    return trim(x)


def level(x, win=0.005):
    n = int(SR * win)
    m = max(1, len(x) // n)
    return 20 * np.log10(np.sqrt((x[: m * n].reshape(m, n) ** 2).mean(axis=1)) + 1e-9), n


def trim(x, below=-42):
    """Cuts the silence off both ends (MP3s carry some), keeping a hair of lead-in."""
    db, n = level(x)
    on = np.nonzero(db > max(db.max() + below, -70))[0]
    if len(on) == 0:
        return x
    a = max(0, on[0] * n - int(SR * 0.004))
    b = min(len(x), (on[-1] + 1) * n + int(SR * 0.03))
    return x[a:b]


def pitch(x, p):
    if abs(p - 1) < 1e-3:
        return x
    return resample_poly(x, 1000, int(round(1000 * p)))


def fade(y, fin=0.003, fout=0.02):
    i, o = int(SR * fin), int(SR * fout)
    if len(y) > i + o:
        y[:i] *= np.linspace(0, 1, i)
        y[-o:] *= np.linspace(1, 0, o) ** 1.5
    return y


def loudness(y, target):
    db, n = level(y)
    active = db[db > db.max() - 20]
    rms = 10 * np.log10(np.mean(10 ** (active / 10)))
    return y * 10 ** ((target - rms) / 20)


def limit(y, ceiling=10 ** (-1.5 / 20)):
    peak = np.abs(y).max()
    if peak <= ceiling:
        return y
    # soft knee: squash only what's above three quarters of the ceiling
    knee = ceiling * 0.75
    s = np.sign(y)
    a = np.abs(y)
    over = a > knee
    a[over] = knee + (ceiling - knee) * np.tanh((a[over] - knee) / (ceiling - knee))
    return s * a


def make(layers, target, lowpass):
    parts = []
    for src, start, p, gain in layers:
        parts.append((int(start * SR), pitch(load(src), p) * 10 ** (gain / 20)))
    length = max(s + len(x) for s, x in parts)
    y = np.zeros(length)
    for s, x in parts:
        y[s: s + len(x)] += x
    if lowpass:
        y = sosfiltfilt(butter(2, lowpass, "lowpass", fs=SR, output="sos"), y)
    y = fade(trim(y))
    y = limit(loudness(y, target + OVERALL))
    return np.concatenate([y, np.zeros(int(SR * 0.01))])


for name, (folder, target, layers, lowpass) in RECIPES.items():
    y = make(layers, target, lowpass)
    os.makedirs(os.path.join(AUDIO, folder), exist_ok=True)
    path = os.path.join(AUDIO, folder, name + ".wav")
    sf.write(path, y.astype(np.float32), SR, subtype="PCM_16")
    print(f"{folder}/{name}.wav  {len(y) / SR:.2f}s  peak {20 * np.log10(np.abs(y).max()):.1f} dB")
