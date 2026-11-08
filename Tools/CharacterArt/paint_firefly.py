"""The firefly companion: a tiny flat bug with a glowing tail and a soft warm glow, on a small transparent canvas.
Deliberately plain: a few ellipses and a radial glow."""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

N, SS = 256, 4


def layer():
    return Image.new("RGBA", (N * SS, N * SS), (0, 0, 0, 0))


def ellipse(im, cx, cy, rx, ry, colour, angle=0):
    """An ellipse, rotated by `angle` degrees about its centre."""
    t = np.linspace(0, 2 * np.pi, 90)
    a = np.radians(angle)
    x, y = rx * np.cos(t), ry * np.sin(t)
    pts = [((cx + x[i] * np.cos(a) - y[i] * np.sin(a)) * SS, (cy + x[i] * np.sin(a) + y[i] * np.cos(a)) * SS) for i in range(len(t))]
    ImageDraw.Draw(im).polygon(pts, fill=colour)


TILT = -20  # leaning a little, as if flying
CX, CY = 128, 132

# Soft glow round the tail.
yy, xx = np.mgrid[0:N, 0:N]
gx, gy = CX + 12, CY + 22
r = np.hypot(xx - gx, yy - gy)
glow = np.clip(1 - r / 96, 0, 1) ** 2.2 * 0.55
glow_im = np.zeros((N, N, 4), np.uint8)
glow_im[..., :3] = (255, 226, 120)
glow_im[..., 3] = (glow * 255).astype(np.uint8)
out = Image.fromarray(glow_im, "RGBA")

bug = layer()
# wings: two pale see-through ovals behind the body
ellipse(bug, CX - 16, CY - 12, 22, 11, (246, 243, 236, 150), TILT - 35)
ellipse(bug, CX + 10, CY - 20, 22, 11, (246, 243, 236, 150), TILT + 35)
# glowing tail, then body and head
ellipse(bug, CX + 9, CY + 22, 15, 19, (250, 232, 140, 255), TILT)
ellipse(bug, CX + 9, CY + 26, 9, 11, (255, 248, 200, 255), TILT)
ellipse(bug, CX - 1, CY - 2, 12, 15, (64, 48, 40, 255), TILT)
ellipse(bug, CX - 8, CY - 20, 9, 8, (196, 112, 60, 255), TILT)  # the orange cap, like the other characters' feet
# feelers
d = ImageDraw.Draw(bug)
for dx in (-1, 1):
    d.line([((CX - 11 + dx * 3) * SS, (CY - 26) * SS), ((CX - 16 + dx * 9) * SS, (CY - 40) * SS)], fill=(64, 48, 40, 255), width=2 * SS)

bug = bug.resize((N, N), Image.LANCZOS)
# a thin off-white edge round the bug, like the chalky outline on everyone else
alpha = bug.split()[3].point(lambda v: 255 if v > 100 else 0)
edge = alpha.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(0.6))
rim = Image.new("RGBA", (N, N), (245, 243, 238, 0))
rim.putalpha(edge.point(lambda v: int(v * 0.9)))
out.alpha_composite(rim)
out.alpha_composite(bug)
out.save(sys.argv[1])
