"""Paints a forward-facing pekapeka in the game's character style: flat grainy watercolour shapes, darker blotches,
chalky white crayon marks and a chalky white outline, on a 1024 transparent canvas."""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy.ndimage import gaussian_filter, binary_dilation

N = 1024
SS = 2  # supersample for clean shape edges
rng = np.random.default_rng(7)


K, OY = 1.10, 952  # scale, and where the feet sit: leaves room for the outline above the ears


def T(x, y):
    return (CX0 + (x - CX0) * K, OY + (y - 900) * K)


CX0 = 512


def spline(pts, n=40):
    """Closed Catmull-Rom through the points."""
    p = np.array(pts, float)
    out = []
    for i in range(len(p)):
        p0, p1, p2, p3 = p[i - 1], p[i], p[(i + 1) % len(p)], p[(i + 2) % len(p)]
        for t in np.linspace(0, 1, n, endpoint=False):
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    return np.array(out)


def wobble(curve, amount=3.0, seed=0):
    r = np.random.default_rng(seed)
    n = len(curve)
    w = gaussian_filter(r.standard_normal(n), 6, mode="wrap")
    w = w / (np.abs(w).max() + 1e-9) * amount
    c = curve - curve.mean(0)
    d = c / (np.linalg.norm(c, axis=1, keepdims=True) + 1e-9)
    return curve + d * w[:, None]


def mask_of(pts, wob=3.0, seed=0, smooth=True):
    pts = [T(x, y) for x, y in pts]
    curve = spline(pts) if smooth else np.array(pts, float)
    curve = wobble(curve, wob, seed)
    im = Image.new("L", (N * SS, N * SS), 0)
    ImageDraw.Draw(im).polygon([tuple(q * SS) for q in curve], fill=255)
    return np.asarray(im.resize((N, N), Image.LANCZOS), float) / 255


def ellipse_mask(cx, cy, rx, ry):
    cx, cy = T(cx, cy); rx, ry = rx * K, ry * K
    im = Image.new("L", (N * SS, N * SS), 0)
    ImageDraw.Draw(im).ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS], fill=255)
    return np.asarray(im.resize((N, N), Image.LANCZOS), float) / 255


def noise(scale, seed, aniso=None):
    r = np.random.default_rng(seed).standard_normal((N, N))
    g = gaussian_filter(r, aniso if aniso else scale)
    return g / (np.abs(g).max() + 1e-9)


canvas = np.zeros((N, N, 4))


def paint(mask, color, grain=0.07, blotch=0.06, streak=0.05, seed=0, edge_dry=0.25):
    """Lays a grainy watercolour wash of `color` where `mask` is."""
    col = np.array(color, float)[None, None, :] / 255
    tex = (1 + grain * noise(1.2, seed) + blotch * noise(28, seed + 1) + streak * noise(0, seed + 2, aniso=(9, 1.3)))
    pool = 1 - 0.13 * np.clip(1 - gaussian_filter(mask, 14) * 1.6, 0, 1) * (mask > 0.05)
    rgb = np.clip(col * (tex * pool)[..., None], 0, 1)
    # Dry-brush edge: the outermost pixels break up a little.
    a = mask.copy()
    edge = (a > 0.02) & (a < 0.98)
    a[edge] *= np.clip(1 - edge_dry * (noise(0.8, seed + 3)[edge] > 0.3), 0, 1)
    over(rgb, a)


def over(rgb, a):
    global canvas
    a = a[..., None]
    out_a = a + canvas[..., 3:] * (1 - a)
    canvas[..., :3] = (rgb * a + canvas[..., :3] * canvas[..., 3:] * (1 - a)) / np.maximum(out_a, 1e-6)
    canvas[..., 3:] = out_a


def blotch(mask, color, thresh=0.15, seed=0, scale=34):
    """A darker patch, like the weka's wing: soft noise thresholded, kept inside the mask."""
    n = noise(scale, seed)
    a = np.clip((n - thresh) * 6, 0, 1) * mask
    a = gaussian_filter(a, 1.2) * mask
    col = np.array(color, float)[None, None, :] / 255
    rgb = np.clip(col * (1 + 0.06 * noise(1.2, seed + 9))[..., None], 0, 1)
    over(rgb, a * 0.85)


def strokes(lines, width=4, color=(250, 248, 242), seed=0):
    """Chalky crayon lines: each one swells and tapers, wobbles a little, and the paper's grain breaks it up."""
    r = np.random.default_rng(seed)
    im = Image.new("L", (N * SS, N * SS), 0)
    d = ImageDraw.Draw(im)
    for pts in lines:
        pts = np.array([T(x, y) for x, y in pts], float)
        # resample along the line so width can vary smoothly
        seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
        t = np.concatenate([[0], np.cumsum(seg)])
        n = max(8, int(t[-1] / 2))
        u = np.linspace(0, t[-1], n)
        path = np.stack([np.interp(u, t, pts[:, 0]), np.interp(u, t, pts[:, 1])], 1)
        jitter = gaussian_filter(r.standard_normal((n, 2)), 3) * 1.2
        path += jitter
        f = u / max(t[-1], 1e-6)
        taper = np.clip(np.sin(np.pi * np.clip(f * 0.9 + 0.05, 0, 1)) * 1.25, 0.35, 1)
        swell = 1 + 0.25 * gaussian_filter(r.standard_normal(n), 4) / 0.2
        wdt = np.clip(width * taper * np.clip(swell, 0.6, 1.3), 1.2, None)
        for (x, y), w in zip(path, wdt):
            rr = w * SS / 2
            d.ellipse([x * SS - rr, y * SS - rr, x * SS + rr, y * SS + rr], fill=255)
    a = np.asarray(im.resize((N, N), Image.LANCZOS), float) / 255
    a *= np.clip(1 - 0.45 * (noise(0.7, 99 + seed) > 0.35), 0, 1)  # paper tooth showing through
    a *= 0.95
    over(np.ones((N, N, 3)) * np.array(color)[None, None, :] / 255, a)


CX = 512
FUR = (126, 93, 68)
FUR_DARK = (92, 66, 50)
WING = (74, 56, 48)
WING_DARK = (58, 43, 38)
SKIN = (156, 112, 96)
SKIN_IN = (176, 128, 112)
CLAW = (196, 112, 60)

# ---- shapes ----
body_pts = [(CX, 400), (CX + 112, 432), (CX + 150, 540), (CX + 168, 680), (CX + 140, 800), (CX + 80, 862), (CX, 874),
            (CX - 80, 862), (CX - 140, 800), (CX - 168, 680), (CX - 150, 540), (CX - 112, 432)]
head_pts = [(CX, 238), (CX + 76, 246), (CX + 128, 288), (CX + 150, 360), (CX + 130, 432), (CX + 74, 478), (CX, 490),
            (CX - 74, 478), (CX - 130, 432), (CX - 150, 360), (CX - 128, 288), (CX - 76, 246)]


def ear(side):
    s = side
    return [(CX + s * 46, 276), (CX + s * 60, 190), (CX + s * 104, 104), (CX + s * 160, 80), (CX + s * 202, 118),
            (CX + s * 206, 210), (CX + s * 170, 296), (CX + s * 118, 320)]


def ear_inner(side):
    s = side
    return [(CX + s * 82, 270), (CX + s * 92, 196), (CX + s * 126, 130), (CX + s * 162, 114), (CX + s * 180, 148),
            (CX + s * 178, 222), (CX + s * 148, 284)]


def wing(side):
    """A folded wing wrapped down the side like a cloak, scalloped at the bottom between the finger bones."""
    s = side
    return [(CX + s * 104, 452), (CX + s * 168, 430), (CX + s * 214, 476), (CX + s * 236, 600), (CX + s * 232, 730),
            (CX + s * 212, 808), (CX + s * 190, 786), (CX + s * 172, 822), (CX + s * 150, 798), (CX + s * 128, 838),
            (CX + s * 116, 760), (CX + s * 124, 610)]


def thumb(side):
    s = side
    return [(CX + s * 176, 438), (CX + s * 186, 414), (CX + s * 200, 406), (CX + s * 198, 418), (CX + s * 190, 440)]


def foot(side):
    s = side
    x = CX + s * 58
    return [(x - 26, 846), (x + 26, 846), (x + 34, 880), (x + 20, 896), (x + 8, 882), (x, 900), (x - 8, 882),
            (x - 20, 896), (x - 34, 880)]


m_body = mask_of(body_pts, 4, 1)
m_head = mask_of(head_pts, 3, 2)
m_ears = [mask_of(ear(s), 2.5, 3 + i) for i, s in enumerate((-1, 1))]
m_ear_in = [mask_of(ear_inner(s), 2, 5 + i) for i, s in enumerate((-1, 1))]
m_wings = [mask_of(wing(s), 2.5, 7 + i) for i, s in enumerate((-1, 1))]
m_thumbs = [mask_of(thumb(s), 1, 9 + i) for i, s in enumerate((-1, 1))]
m_feet = [mask_of(foot(s), 1.5, 11 + i) for i, s in enumerate((-1, 1))]
m_face = mask_of([(CX, 330), (CX + 70, 350), (CX + 92, 410), (CX + 52, 456), (CX, 466), (CX - 52, 456),
                  (CX - 92, 410), (CX - 70, 350)], 2, 13)
m_snout = ellipse_mask(CX, 418, 34, 24)

# ---- rim: the thin off-white halo every character has ----
everything = np.maximum.reduce([m_body, m_head, *m_ears, *m_wings, *m_feet])
from scipy.ndimage import distance_transform_edt
dist = distance_transform_edt(everything < 0.5)
width_px = 9.5 + 2.6 * noise(40, 51) + 1.6 * noise(6, 52) + 0.9 * noise(1.2, 53)  # swells along the edge, ragged outside
rim_a = np.clip(width_px - dist, 0, 1)
rim_a = gaussian_filter(rim_a, 0.6)
rim_a *= np.clip(1 - 0.28 * (noise(0.7, 54) > 0.45) - 0.12 * (noise(0.7, 55) > 0.2), 0, 1)  # crayon grain
rim_rgb = np.ones((N, N, 3)) * np.array([245, 243, 238])[None, None, :] / 255 * (1 - 0.03 * noise(1, 56))[..., None]
over(rim_rgb, rim_a)

# ---- paint, back to front ----
for i, m in enumerate(m_feet):
    paint(m, CLAW, seed=20 + i, streak=0.02)
for i, m in enumerate(m_ears):
    paint(m, SKIN, seed=30 + i, blotch=0.08)
for i, m in enumerate(m_ear_in):
    paint(m, SKIN_IN, seed=32 + i, blotch=0.05)
paint(m_body, FUR, seed=40, streak=0.08)
blotch(m_body, FUR_DARK, 0.05, seed=41, scale=30)
chest = ellipse_mask(CX, 600, 70, 120)
over(np.ones((N, N, 3)) * np.array([150, 116, 88])[None, None, :] / 255 * (1 + 0.06 * noise(1.2, 77))[..., None], gaussian_filter(chest, 18) * m_body * 0.55)
for i, m in enumerate(m_wings):
    paint(m, WING, seed=44 + i, blotch=0.08)
    blotch(m, WING_DARK, 0.2, seed=46 + i, scale=22)
paint(m_head, FUR, seed=50, streak=0.06)
blotch(m_head * gaussian_filter((np.arange(N)[:, None] < T(0, 300)[1]).astype(float) * np.ones((1, N)), 12), FUR_DARK, 0.25, seed=51, scale=26)
paint(gaussian_filter(m_face, 6), (146, 110, 84), seed=52, blotch=0.04, edge_dry=0)
paint(gaussian_filter(m_snout, 7) * 0.6, SKIN, seed=53, edge_dry=0)

# eyes: plain soft dark dots, like the pig's. No mouth or nose, like the others.
for s in (-1, 1):
    ex, ey = CX + s * 54, 376
    over(np.ones((N, N, 3)) * np.array([38, 26, 22])[None, None, :] / 255, gaussian_filter(ellipse_mask(ex, ey, 12, 13), 1.4))

# ---- white scratches: finger bones on the wings, fur ticks, ear folds ----
lines = []
for s in (-1, 1):
    lines += [[(CX + s * 182, 446), (CX + s * 202, 570), (CX + s * 204, 700), (CX + s * 194, 786)],
              [(CX + s * 162, 458), (CX + s * 152, 620), (CX + s * 144, 776)]]
lines += [[(CX - 40, 580), (CX - 32, 596), (CX - 22, 580)], [(CX + 34, 640), (CX + 42, 656), (CX + 52, 640)],
          [(CX + 62, 540), (CX + 66, 554)]]
strokes(lines, width=4.5, seed=1)
chalk = [[(CX - 112, 468), (CX - 60, 490), (CX, 496), (CX + 60, 490), (CX + 112, 468)]]  # chin onto chest, like Ruru
for side in (-1, 1):
    chalk += [[(CX + side * 96, 262), (CX + side * 100, 190), (CX + side * 130, 128), (CX + side * 164, 112)]]  # inner ear rim
strokes(chalk, width=6, seed=2)
# claw ticks on the feet, like the weka's toes
toe = []
for s in (-1, 1):
    x = CX + s * 58
    toe += [[(x - 18, 856), (x + 18, 856)], [(x - 20, 866), (x + 20, 866)]]
strokes(toe, width=3, seed=4)

out = Image.fromarray((np.clip(canvas, 0, 1) * 255).astype(np.uint8), "RGBA")
out.save(sys.argv[1])
