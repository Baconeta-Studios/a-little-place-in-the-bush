"""Split each creature's master WAV into short gibberish lines for Assets/Audio/Voices/<Folder>/.

Per creature: high-pass, find syllables by level, group them into phrases, then build 1-3 second "sentences" (joining
short phrases with tightened gaps, splitting long ones at their widest gap), plus a few remixed combos that splice
syllables from across the take. Each clip gets the room noise between syllables ducked, even loudness, a peak
limiter, short fades and optional character touches (echo).
"""
import os, sys, json, random, zlib
import numpy as np, soundfile as sf
from scipy.signal import butter, sosfiltfilt

SRC = sys.argv[1]
OUT = sys.argv[2]
# How hard to pull out the room's own reverb: off, moderate or strong. Added echoes (below) are untouched.
STRENGTH = sys.argv[3] if len(sys.argv) > 3 else "moderate"
DEREVERB = {"off": None,
            "moderate": dict(over=1.5, floor_db=-15, delay=0.025, noise_over=1.5, noise_floor_db=-15, tail=0.15, duck=-22),
            "strong": dict(over=2.2, floor_db=-22, delay=0.02, noise_over=2.2, noise_floor_db=-22, tail=0.10, duck=-28),
            }[STRENGTH]
FR = 0.01  # analysis frame, seconds

CREATURES = {
    # master file    folder       echo (delay s, level) or None, mixes
    "gecko girl.wav": ("Kowhai",    None,          3),
    "gus.wav":        ("Gus",       (0.11, 0.22),  3),
    "kunekune.wav":   ("Kunekune",  None,          3),
    "pekapeka.wav":   ("Pekapeka",  (0.06, 0.18),  3),
    "ruru.wav":       ("Ruru",      None,          3),
    "snial.wav":      ("Snail",     None,          3),
    "spider.wav":     ("Spider",    None,          3),
    "weka.wav":       ("Weka",      None,          3),
}

MIN_LEN, MAX_LEN = 0.8, 2.8      # target clip length
PHRASE_GAP = 0.30                # gaps shorter than this stay inside a phrase
JOIN_GAP = (0.10, 0.18)          # gap used when joining phrases into a sentence
PRE, POST = 0.04, 0.08           # natural audio kept around each piece


def level_db(x, sr):
    n = int(sr * FR)
    m = len(x) // n
    f = x[: m * n].reshape(m, n)
    return 20 * np.log10(np.sqrt((f ** 2).mean(axis=1)) + 1e-9)


def runs(mask):
    out, i = [], 0
    while i < len(mask):
        if mask[i]:
            j = i
            while j < len(mask) and mask[j]:
                j += 1
            out.append([i, j])
            i = j
        else:
            i += 1
    return out


def syllables(db):
    floor = np.percentile(db, 10)
    thr = max(floor + 12, db.max() - 38)
    segs = runs(db > thr)
    merged = []
    for s in segs:  # bridge tiny dips inside a syllable
        if merged and s[0] - merged[-1][1] <= 6:
            merged[-1][1] = s[1]
        else:
            merged.append(s)
    keep = []
    for a, b in merged:  # drop clicks: very short and not loud
        if (b - a) * FR < 0.07 and db[a:b].max() < thr + 10:
            continue
        keep.append((a * FR, b * FR))
    return keep, thr


def phrases(sylls):
    out = []
    for s in sylls:
        if out and s[0] - out[-1][-1][1] < PHRASE_GAP:
            out[-1].append(s)
        else:
            out.append([s])
    return out


def span(p):
    return p[-1][1] - p[0][0]


def split_long(p, most=MAX_LEN):
    """Split a phrase longer than `most` at its widest internal gap, recursively."""
    if span(p) <= most or len(p) < 2:
        return [p]
    gaps = [(p[i + 1][0] - p[i][1], i) for i in range(len(p) - 1)]
    # prefer a wide gap near the middle
    mid = p[0][0] + span(p) / 2
    _, i = max(gaps, key=lambda g: g[0] - 0.05 * abs(p[g[1]][1] - mid))
    return split_long(p[: i + 1], most) + split_long(p[i + 1:], most)


def sentences(phr):
    pieces = [q for p in phr for q in split_long(p)]
    out, cur, cur_len = [], [], 0.0
    for p in pieces:
        add = span(p) + (sum(JOIN_GAP) / 2 if cur else 0)
        if cur and cur_len + add > MAX_LEN:
            out.append(cur)
            cur, cur_len = [], 0.0
            add = span(p)
        cur.append(p)
        cur_len += add
        if cur_len >= MIN_LEN * 1.6:
            out.append(cur)
            cur, cur_len = [], 0.0
    if cur:
        if out and sum(span(p) for p in cur) < MIN_LEN and sum(span(p) for p in out[-1]) + span(cur[0]) < MAX_LEN + 0.4:
            out[-1].extend(cur)
        else:
            out.append(cur)
    return out


CUT = {}  # set per creature: the master's level and threshold, so cuts can wait for sounds to die away


def cut(x, sr, a, b):
    db, thr = CUT["db"], CUT["thr"]
    quiet = thr - 8
    i = int(a / FR)
    lo = max(0, i - 12)
    while i > lo and db[i - 1] > quiet:  # back up through a soft attack (max 120 ms)
        i -= 1
    j = int(b / FR)
    hi = min(len(db), j + int((DEREVERB["tail"] if DEREVERB else 0.3) / FR))
    while j < hi and db[j] > quiet:  # let the tail ring out (300 ms, less once the room's reverb is pulled out)
        j += 1
    return x[max(0, int((i * FR - PRE) * sr)): min(len(x), int((j * FR + POST) * sr))].copy()


def fade(y, sr, fin=0.006, fout=0.035):
    i, o = int(sr * fin), int(sr * fout)
    if len(y) > i + o:
        y[:i] *= np.linspace(0, 1, i)
        y[-o:] *= np.linspace(1, 0, o) ** 1.5
    return y


def join(parts, sr, rng):
    out = []
    for k, part in enumerate(parts):
        if k:
            out.append(np.zeros(int(sr * rng.uniform(*JOIN_GAP))))
        out.append(fade(part, sr, 0.004, 0.025))
    return np.concatenate(out)


def rt60(db, sylls):
    """How long the room rings: the first 100 ms of decay after syllables that end in a real gap, as seconds to fall
    60 dB. Ends running into the next syllable are skipped, so they can't make the room sound longer than it is."""
    slopes = []
    for k, (_, b) in enumerate(sylls):
        nxt = sylls[k + 1][0] if k + 1 < len(sylls) else b + 1
        if nxt - b < 0.25:
            continue
        i = int(b / FR) - 2
        seg = db[i: i + 10]
        if len(seg) == 10:
            slopes.append(np.polyfit(np.arange(10) * FR, seg, 1)[0])
    s = np.median(slopes) if slopes else -150.0
    return float(np.clip(-60.0 / min(s, -1.0), 0.2, 0.8))


def tail_db(db, sylls):
    """Lingering sound 30-150 ms after each isolated syllable, against its last 50 ms: lower is drier."""
    out = []
    for k, (_, b) in enumerate(sylls):
        nxt = sylls[k + 1][0] if k + 1 < len(sylls) else b + 1
        i = int(b / FR)
        if nxt - b < 0.25 or i < 5 or i + 15 > len(db):
            continue
        out.append(np.mean(db[i + 3: i + 15]) - np.mean(db[i - 5: i]))
    return float(np.median(out)) if out else 0.0


def dereverb(x, sr, t60, over, floor_db, delay=0.05, noise_over=0.0, noise_floor_db=0.0, quiet=None, **_):
    """Takes the room out of a take, per frequency, in two parts. Room tone: the steady hiss, learned from the quiet
    frames (`quiet`, a mask of 10 ms frames) and subtracted. Reverb: each moment's ring predicted from what was heard
    `delay` before, decayed by the room's T60, and subtracted. The voice's direct sound, and the echo some creatures
    get later on purpose, are left alone."""
    from scipy.signal import stft, istft
    from scipy.ndimage import uniform_filter
    n, hop = 1024, 256
    _, _, Z = stft(x, sr, nperseg=n, noverlap=n - hop)
    P = np.abs(Z) ** 2
    Ps = uniform_filter(P, size=(1, 3))
    d = max(1, int(round(delay * sr / hop)))
    decay = np.exp(-2 * (3 * np.log(10) / t60) * d * hop / sr)
    late = np.zeros_like(P)
    late[:, d:] = decay * Ps[:, :-d]
    noise = np.zeros((P.shape[0], 1))
    if noise_over and quiet is not None:
        frame_t = np.arange(P.shape[1]) * hop / sr
        q = quiet[np.minimum((frame_t / FR).astype(int), len(quiet) - 1)]
        if q.sum() > 10:
            noise = np.median(P[:, q], axis=1, keepdims=True)
    g = np.maximum(1 - over * late / (Ps + 1e-12), 10 ** (floor_db / 20))
    g = np.minimum(g, np.maximum(1 - noise_over * noise / (Ps + 1e-12), 10 ** (noise_floor_db / 20)))
    g = uniform_filter(g, size=(3, 3))  # smooth to avoid warbly "musical noise"
    _, y = istft(Z * g, sr, nperseg=n, noverlap=n - hop)
    return y[: len(x)] if len(y) >= len(x) else np.pad(y, (0, len(x) - len(y)))


def duck_gaps(y, sr, thr_db, depth_db=-14):
    """Gentle expander: pull room noise down between syllables without chopping the sounds."""
    db = level_db(y, sr)
    gain_f = np.where(db > thr_db - 4, 1.0, 10 ** (depth_db / 20))
    k = 5  # smooth over ~50 ms so it never clicks
    gain_f = np.convolve(np.pad(gain_f, k, mode="edge"), np.ones(2 * k + 1) / (2 * k + 1), mode="same")[k:-k]
    n = int(sr * FR)
    g = np.interp(np.arange(len(y)), np.arange(len(gain_f)) * n + n / 2, gain_f)
    return y * g


def loudness(y, sr, thr_db, target=-18.0):
    db = level_db(y, sr)
    n = int(sr * FR)
    active = np.concatenate([y[i * n:(i + 1) * n] for i in np.where(db > thr_db)[0]] or [y])
    rms = 20 * np.log10(np.sqrt((active ** 2).mean()) + 1e-9)
    return y * 10 ** ((target - rms) / 20)


def limit(y, sr, ceiling=10 ** (-1.0 / 20)):
    """Peak limiter with ~5 ms look-ahead and 60 ms release."""
    need = np.minimum(1.0, ceiling / (np.abs(y) + 1e-9))
    la = int(sr * 0.005)
    from scipy.ndimage import minimum_filter1d
    g = minimum_filter1d(need, size=2 * la + 1)
    rel = np.exp(-1.0 / (sr * 0.06))
    out = np.empty_like(g)
    cur = 1.0
    for i, v in enumerate(g):  # instant attack, smooth release
        cur = v if v < cur else v + (cur - v) * rel
        out[i] = cur
    return np.clip(y * out, -ceiling, ceiling)


def echo(y, sr, delay, level):
    d = int(sr * delay)
    out = np.concatenate([y, np.zeros(d * 3)])
    for k in (1, 2):
        out[d * k: d * k + len(y)] += y * level ** k
    return out


def finish(y, sr, thr_db, fx):
    y = duck_gaps(y, sr, thr_db, DEREVERB["duck"] if DEREVERB else -14)
    y = loudness(y, sr, thr_db)
    if fx:
        y = echo(y, sr, *fx)
    y = limit(y, sr)
    y = fade(y, sr)
    pad = np.zeros(int(sr * 0.02))
    return np.concatenate([pad, y, pad])


def main():
    report = {}
    for master, (folder, fx, n_mix) in CREATURES.items():
        x, sr = sf.read(os.path.join(SRC, master), always_2d=True)
        x = x.mean(axis=1)
        x = x - x.mean()
        x = sosfiltfilt(butter(2, 90, "highpass", fs=sr, output="sos"), x)
        db = level_db(x, sr)
        edge = int(0.15 / FR)  # record-button clicks at either end
        db[:edge] = db[-edge:] = db.min()
        e, r = edge * int(sr * FR), int(sr * 0.05)
        x[:e] = 0
        x[e:e + r] *= np.linspace(0, 1, r)
        x[-e:] = 0
        x[-e - r:-e] *= np.linspace(1, 0, r)
        sylls, thr = syllables(db)
        CUT.update(db=db, thr=thr)  # cut points come from the untouched take, so lines keep their names run to run
        room = rt60(db, sylls)
        if DEREVERB:
            quiet = db < np.percentile(db, 25)
            quiet[:edge] = quiet[-edge:] = False  # the zeroed ends aren't room tone
            x = dereverb(x, sr, room, quiet=quiet, **DEREVERB)
        before_tail, after_tail = tail_db(db, sylls), tail_db(level_db(x, sr), sylls)
        phr = phrases(sylls)
        sents = sentences(phr)
        rng = random.Random(zlib.crc32(folder.encode()))

        dest = os.path.join(OUT, folder)
        os.makedirs(dest, exist_ok=True)
        stem = folder.lower()
        made = []

        for i, sent in enumerate(sents, 1):
            parts = [cut(x, sr, p[0][0], p[-1][1]) for p in sent]
            y = finish(join(parts, sr, rng) if len(parts) > 1 else fade(parts[0], sr), sr, thr, fx)
            name = f"{stem}_{i:02d}.wav"
            sf.write(os.path.join(dest, name), y.astype(np.float32), sr, subtype="PCM_16")
            made.append((name, round(len(y) / sr, 2), "joined" if len(parts) > 1 else "as recorded"))

        # Remixed combos: 3-5 syllable groups taken from different parts of the take.
        bits = [q for p in phr for q in split_long(p, 0.9)]
        pool = [p for p in bits if 0.12 <= span(p) <= 1.1] or bits
        for m in range(1, n_mix + 1):
            picks, total = [], 0.0
            for p in rng.sample(pool, min(len(pool), 6)):
                if total + span(p) > 2.4:
                    continue
                picks.append(p)
                total += span(p) + 0.12
                if total > 1.4 and len(picks) >= 3:
                    break
            y = finish(join([cut(x, sr, p[0][0], p[-1][1]) for p in picks], sr, rng), sr, thr, fx)
            name = f"{stem}_mix{m}.wav"
            sf.write(os.path.join(dest, name), y.astype(np.float32), sr, subtype="PCM_16")
            made.append((name, round(len(y) / sr, 2), f"remix of {len(picks)} bits"))
        report[folder] = {"syllables": len(sylls), "phrases": len(phr), "room_t60": round(room, 2),
                          "tail_db": [round(before_tail, 1), round(after_tail, 1)], "clips": made}
    print(json.dumps(report, indent=1))


main()
