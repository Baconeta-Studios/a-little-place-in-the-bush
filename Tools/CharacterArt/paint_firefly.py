"""The firefly companion: a dark dot with a glowing tail and a soft warm glow. Two ellipses and a radial gradient."""
import sys
import numpy as np
from PIL import Image, ImageDraw

N, SS = 256, 4
CX, CY = 128, 128

# Soft warm glow.
yy, xx = np.mgrid[0:N, 0:N]
glow = np.clip(1 - np.hypot(xx - CX, yy - (CY + 10)) / 90, 0, 1) ** 2.2 * 0.6
pixels = np.zeros((N, N, 4), np.uint8)
pixels[..., :3] = (255, 226, 120)
pixels[..., 3] = (glow * 255).astype(np.uint8)
out = Image.fromarray(pixels, "RGBA")

# The bug: a dark body and a pale glowing tail.
bug = Image.new("RGBA", (N * SS, N * SS), (0, 0, 0, 0))
d = ImageDraw.Draw(bug)
d.ellipse([(CX - 9) * SS, (CY - 22) * SS, (CX + 9) * SS, (CY + 2) * SS], fill=(64, 48, 40, 255))
d.ellipse([(CX - 11) * SS, (CY - 2) * SS, (CX + 11) * SS, (CY + 24) * SS], fill=(255, 240, 170, 255))
out.alpha_composite(bug.resize((N, N), Image.LANCZOS))
out.save(sys.argv[1])
