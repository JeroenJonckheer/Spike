"""Renders logo candidates for Spike in the same style as the Tremors icon: a navy disc
with a white sign and one accent colour. Spike's accent is amber, the colour of a dog
that is awake. Shapes are bold so they still read at 16 px in the notification area.
The dog is a pointed-eared watchdog (shepherd type), deliberately not a bulldog.

Usage: python tools/make_icon.py <preview.png>
"""
import math
import sys
from PIL import Image, ImageDraw

S = 1024
NAVY = (28, 52, 78)
WHITE = (240, 246, 248)
AMBER = (255, 176, 46)
RED = (226, 62, 52)


def P(x, y, ox=0.0, oy=0.0, k=1.0):
    """Fraction of the icon to pixels, optionally moved and scaled around the centre."""
    return ((0.5 + (x - 0.5) * k + ox) * S, (0.5 + (y - 0.5) * k + oy) * S)


def disc(colour=NAVY):
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    m = 0.02 * S
    ImageDraw.Draw(im).ellipse([m, m, S - m, S - m], fill=colour + (255,))
    return im


def soft_polygon(d, pts, fill, round_px=0.02):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=fill, width=int(round_px * S), joint="curve")


# A watchdog's head in profile, looking right, ear up: alert.
PROFILE = [
    (0.28, 0.74), (0.27, 0.60), (0.29, 0.47), (0.34, 0.38),   # back of the neck and head
    (0.36, 0.30), (0.40, 0.11), (0.50, 0.28),                 # ear
    (0.57, 0.31), (0.64, 0.40),                               # forehead and stop
    (0.80, 0.46), (0.86, 0.49), (0.86, 0.54),                 # muzzle and nose
    (0.78, 0.58), (0.68, 0.60),                               # mouth and jaw
    (0.60, 0.66), (0.58, 0.77),                               # throat; the neck ends at the collar
]


def profile(d, colour, eye, k=1.0, ox=0.0, oy=0.0):
    soft_polygon(d, [P(x, y, ox, oy, k) for x, y in PROFILE], colour)
    ex, ey = P(0.585, 0.42, ox, oy, k)
    r = 0.035 * S * k
    d.ellipse([ex - r, ey - r * 0.8, ex + r, ey + r * 0.8], fill=eye)
    nx, ny = P(0.86, 0.505, ox, oy, k)
    rn = 0.03 * S * k
    d.ellipse([nx - rn, ny - rn, nx + rn * 0.6, ny + rn], fill=NAVY)


def collar_band(d, x0, y0, x1, y1, colour, spikes, k=1.0, ox=0.0, oy=0.0, out=(0.0, 1.0), length=0.075):
    """A slanted collar from (x0,y0) to (x1,y1) with spikes pointing in direction `out`."""
    w = 0.065
    ax, ay = P(x0, y0, ox, oy, k)
    bx, by = P(x1, y1, ox, oy, k)
    nx, ny = out
    half = w * S * k / 2
    band = [(ax - nx * half, ay - ny * half), (bx - nx * half, by - ny * half),
            (bx + nx * half, by + ny * half), (ax + nx * half, ay + ny * half)]
    soft_polygon(d, band, colour, 0.03)
    for i in range(spikes):
        t = (i + 0.5) / spikes
        cx, cy = ax + (bx - ax) * t, ay + (by - ay) * t
        base = 0.03 * S * k
        dx, dy = (bx - ax), (by - ay)
        ln = math.hypot(dx, dy)
        ux, uy = dx / ln, dy / ln
        bxp, byp = cx + nx * half, cy + ny * half
        tip = (bxp + nx * length * S * k, byp + ny * length * S * k)
        d.polygon([(bxp - ux * base, byp - uy * base), tip, (bxp + ux * base, byp + uy * base)], fill=colour)


def variant_e():
    """Profile of an alert watchdog wearing a spiked collar."""
    im = disc()
    d = ImageDraw.Draw(im)
    profile(d, WHITE, AMBER, k=0.92, ox=-0.02, oy=-0.01)
    collar_band(d, 0.26, 0.735, 0.60, 0.775, AMBER, 4, k=0.92, ox=-0.02, oy=-0.01, out=(-0.12, 0.99))
    return im


def variant_f():
    """The same dog, barking: three amber sound arcs in front of the muzzle."""
    im = disc()
    d = ImageDraw.Draw(im)
    profile(d, WHITE, AMBER, k=0.82, ox=-0.09, oy=0.0)
    collar_band(d, 0.26, 0.735, 0.60, 0.775, AMBER, 4, k=0.82, ox=-0.09, out=(-0.12, 0.99))
    cx, cy = P(0.86, 0.53, -0.09, 0.0, 0.82)
    for i, r in enumerate((0.07, 0.12, 0.17)):
        rr = r * S
        d.arc([cx - rr, cy - rr, cx + rr, cy + rr], start=-40, end=40, fill=AMBER, width=int(0.028 * S))
    return im


def front_head(d, colour, eye, k=1.0, oy=0.0):
    """Front view, slimmer than before: long muzzle, tall ears."""
    soft_polygon(d, [P(0.29, 0.44, 0, oy, k), P(0.27, 0.10, 0, oy, k), P(0.45, 0.30, 0, oy, k)], colour)
    soft_polygon(d, [P(0.71, 0.44, 0, oy, k), P(0.73, 0.10, 0, oy, k), P(0.55, 0.30, 0, oy, k)], colour)
    soft_polygon(d, [P(0.28, 0.34, 0, oy, k), P(0.72, 0.34, 0, oy, k), P(0.70, 0.52, 0, oy, k),
                     P(0.59, 0.76, 0, oy, k), P(0.41, 0.76, 0, oy, k), P(0.30, 0.52, 0, oy, k)], colour, 0.06)
    for x in (0.39, 0.61):
        ex, ey = P(x, 0.47, 0, oy, k)
        r = 0.037 * S * k
        d.ellipse([ex - r, ey - r, ex + r, ey + r], fill=eye)
    nx, ny = P(0.5, 0.71, 0, oy, k)
    rn = 0.05 * S * k
    d.ellipse([nx - rn, ny - rn * 0.75, nx + rn, ny + rn * 0.6], fill=NAVY)


def variant_g():
    """Front view with a full collar ring under the chin, spikes pointing outward."""
    im = disc()
    d = ImageDraw.Draw(im)
    front_head(d, WHITE, AMBER, k=0.9, oy=-0.04)
    cy = 0.80
    d.rounded_rectangle([*P(0.30, cy - 0.035), *P(0.70, cy + 0.035)], radius=0.035 * S, fill=AMBER)
    for i in range(5):
        x = 0.33 + i * 0.085
        d.polygon([P(x - 0.03, cy + 0.03), P(x, cy + 0.11), P(x + 0.03, cy + 0.03)], fill=AMBER)
    return im


def variant_h():
    """Rounded square app tile instead of a disc: profile with collar, amber tile edge."""
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    m = 0.03 * S
    d.rounded_rectangle([m, m, S - m, S - m], radius=0.22 * S, fill=NAVY + (255,))
    profile(d, WHITE, AMBER, k=0.95, ox=-0.02, oy=0.0)
    collar_band(d, 0.26, 0.735, 0.60, 0.775, AMBER, 4, k=0.95, ox=-0.02, out=(-0.12, 0.99))
    return im


def variant_d():
    """Previous favourite, for comparison."""
    im = disc()
    d = ImageDraw.Draw(im)
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    ld.polygon([P(0.26, 0.47), P(0.25, 0.17), P(0.45, 0.33)], fill=WHITE)
    ld.polygon([P(0.74, 0.47), P(0.75, 0.17), P(0.55, 0.33)], fill=WHITE)
    ld.ellipse([*P(0.25, 0.27), *P(0.75, 0.72)], fill=WHITE)
    ld.polygon([P(0.36, 0.58), P(0.64, 0.58), P(0.58, 0.80), P(0.42, 0.80)], fill=WHITE)
    ld.ellipse([*P(0.42, 0.72), *P(0.58, 0.84)], fill=WHITE)
    ld.ellipse([*P(0.445, 0.70), *P(0.555, 0.775)], fill=NAVY)
    for x in (0.38, 0.62):
        ld.ellipse([*P(x - 0.045, 0.47), *P(x + 0.045, 0.56)], fill=AMBER)
    layer = layer.resize((int(S * 0.9), int(S * 0.9)), Image.LANCZOS)
    im.alpha_composite(layer, (int(S * 0.05), int(S * -0.01)))
    d.rounded_rectangle([*P(0.32, 0.76), *P(0.68, 0.82)], radius=0.03 * S, fill=AMBER)
    for i in range(5):
        cx = 0.32 + 0.072 * (i + 0.5)
        d.polygon([P(cx - 0.035, 0.815), P(cx + 0.035, 0.815), P(cx, 0.88)], fill=AMBER)
    return im


def alarm(im):
    """The same sign with red instead of navy: how the icon looks while Spike barks."""
    px = im.copy()
    data = px.load()
    w, h = px.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = data[x, y]
            if a and abs(r - NAVY[0]) < 3 and abs(g - NAVY[1]) < 3 and abs(b - NAVY[2]) < 3:
                data[x, y] = RED + (a,)
    return px


if __name__ == "__main__":
    variants = [("D (vorige)", variant_d()), ("E", variant_e()), ("F", variant_f()),
                ("G", variant_g()), ("H", variant_h())]
    sheet = Image.new("RGBA", (len(variants) * 300 + 20, 540), (240, 240, 240, 255))
    d = ImageDraw.Draw(sheet)
    for i, (name, im) in enumerate(variants):
        x = 20 + i * 300
        sheet.alpha_composite(im.resize((256, 256), Image.LANCZOS), (x, 30))
        d.text((x + 100, 6), name, fill=(40, 40, 40))
        for row, bg in enumerate([(240, 240, 240, 255), (32, 32, 32, 255)]):
            strip = Image.new("RGBA", (280, 62), bg)
            for j, size in enumerate((48, 32, 24, 16)):
                strip.alpha_composite(im.resize((size, size), Image.LANCZOS), (10 + j * 66, (62 - size) // 2))
            sheet.alpha_composite(strip, (x - 10, 300 + row * 62))
        small = im.resize((256, 256), Image.LANCZOS)
        sheet.alpha_composite(alarm(small).resize((100, 100), Image.LANCZOS), (x + 78, 430))
    sheet.save(sys.argv[1])
    if len(sys.argv) > 3:
        # The chosen logo (13-11-2026): D, the front-facing dog with the spiked collar.
        chosen = variant_d()
        chosen.resize((256, 256), Image.LANCZOS).save(
            sys.argv[2], sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)])
        chosen.resize((128, 128), Image.LANCZOS).save(sys.argv[3])
    print("ok")
