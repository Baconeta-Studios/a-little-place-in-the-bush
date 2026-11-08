import sys, glob, os
import numpy as np, soundfile as sf
bad = 0
for path in sorted(glob.glob(os.path.join(sys.argv[1], "*", "*.wav"))):
    y, sr = sf.read(path)
    peak = 20 * np.log10(np.abs(y).max() + 1e-9)
    n = int(sr * 0.01)
    f = y[: len(y) // n * n].reshape(-1, n)
    db = 20 * np.log10(np.sqrt((f ** 2).mean(1)) + 1e-9)
    act = db[db > db.max() - 30]
    rms = 20 * np.log10(np.sqrt((10 ** (act / 10)).mean()))
    edge = max(db[:3].max(), db[-3:].max())
    flag = "" if peak <= -0.9 and edge < -45 else "  <-- check"
    bad += bool(flag)
    print(f"{os.path.relpath(path, sys.argv[1]):24s} {len(y)/sr:4.2f}s peak {peak:5.1f} active {rms:5.1f} edges {edge:6.1f}{flag}")
print("flagged:", bad)
