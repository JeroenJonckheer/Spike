"""Renders logo candidates for Spike in the same style as the Tremors icon: a navy disc
with a white sign and one accent colour. Spike's accent is amber, the colour of a dog
that is awake. Shapes are kept bold so they still read at 16 px in the notification area.

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


def disc(colour=NAVY):
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    m = 0.02 * S
    ImageDraw.Draw(im).ellipse([m, m, S - m, S - m], fill=colour + (255,))
    return im


def P(x, y):
    """Fraction of the icon to pixels."""
    return (x * S, y * S)


def dog_head(d, colour, eye, nose=NAVY):
    """A front-facing dog with pointed ears: a watchful shepherd type."""
    d.polygon([P(0.26, 0.47), P(0.25, 0.17), P(0.45, 0.33)], fill=colour)   # left ear
    d.polygon([P(0.74, 0.47), P(0.75, 0.17), P(0.55, 0.33)], fill=colour)   # right ear
    d.ellipse([*P(0.25, 0.27), *P(0.75, 0.72)], fill=colour)                # skull
    d.polygon([P(0.36, 0.58), P(0.64, 0.58), P(0.58, 0.80), P(0.42, 0.80)], fill=colour)  # muzzle
    d.ellipse([*P(0.42, 0.72), *P(0.58, 0.84)], fill=colour)
    d.ellipse([*P(0.445, 0.70), *P(0.555, 0.775)], fill=nose)               # nose
    for x in (0.38, 0.62):                                                  # eyes
        d.ellipse([*P(x - 0.045, 0.47), *P(x + 0.045, 0.56)], fill=eye)


def collar(d, cy, colour, spikes=5, width=0.40):
    """A band with spikes pointing down: the 'Spike' in the name."""
    x0, x1 = 0.5 - width / 2, 0.5 + width / 2
    d.rounded_rectangle([*P(x0, cy), *P(x1, cy + 0.06)], radius=0.03 * S, fill=colour)
    step = width / spikes
    for i in range(spikes):
        cx = x0 + step * (i + 0.5)
        d.polygon([P(cx - 0.035, cy + 0.055), P(cx + 0.035, cy + 0.055), P(cx, cy + 0.12)], fill=colour)


def variant_a():
    """Dog head with amber eyes and an amber spiked collar."""
    im = disc()
    d = ImageDraw.Draw(im)
    dog_head(d, WHITE, AMBER)
    return im


def variant_b():
    """Shield with dog ears: protection first, dog second."""
    im = disc()
    d = ImageDraw.Draw(im)
    d.polygon([P(0.27, 0.36), P(0.26, 0.15), P(0.42, 0.27)], fill=WHITE)
    d.polygon([P(0.73, 0.36), P(0.74, 0.15), P(0.58, 0.27)], fill=WHITE)
    shield = [P(0.25, 0.26), P(0.75, 0.26), P(0.75, 0.52), P(0.5, 0.86), P(0.25, 0.52)]
    d.polygon(shield, fill=WHITE)
    for x in (0.39, 0.61):
        d.ellipse([*P(x - 0.05, 0.40), *P(x + 0.05, 0.50)], fill=AMBER)
    d.ellipse([*P(0.45, 0.56), *P(0.55, 0.63)], fill=NAVY)
    return im


def variant_c():
    """Spiked collar ring guarding a mouse pointer: family of Tremors."""
    im = disc()
    d = ImageDraw.Draw(im)
    r_out, r_in = 0.36, 0.29
    d.ellipse([*P(0.5 - r_out, 0.5 - r_out), *P(0.5 + r_out, 0.5 + r_out)], fill=AMBER)
    d.ellipse([*P(0.5 - r_in, 0.5 - r_in), *P(0.5 + r_in, 0.5 + r_in)], fill=NAVY)
    for i in range(8):
        a = math.radians(i * 45 + 22.5)
        tip, base = 0.47, r_out - 0.01
        w = math.radians(11)
        d.polygon([P(0.5 + base * math.cos(a - w), 0.5 + base * math.sin(a - w)),
                   P(0.5 + tip * math.cos(a), 0.5 + tip * math.sin(a)),
                   P(0.5 + base * math.cos(a + w), 0.5 + base * math.sin(a + w))], fill=AMBER)
    arrow = [(0, 0), (0, 0.895), (0.211, 0.684), (0.368, 1.0), (0.474, 0.947), (0.316, 0.632), (0.632, 0.632)]
    h = 0.34
    tip = (0.5 - 0.632 * h / 2, 0.5 - h / 2)
    d.polygon([P(tip[0] + x * h, tip[1] + y * h) for x, y in arrow], fill=WHITE)
    return im


def variant_d():
    """Dog head with a spiked collar: the name in one picture."""
    im = disc()
    d = ImageDraw.Draw(im)
    # Head a little higher so the collar fits under the chin.
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    dog_head(ImageDraw.Draw(layer), WHITE, AMBER)
    layer = layer.resize((int(S * 0.9), int(S * 0.9)), Image.LANCZOS)
    im.alpha_composite(layer, (int(S * 0.05), int(S * -0.01)))
    collar(d, 0.76, AMBER, spikes=5, width=0.36)
    return im


def alarm(im):
    """The same sign on red: how the icon looks while Spike barks."""
    red = disc(RED)
    mask = disc()
    sign = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sign.alpha_composite(im)
    px, rp = sign.load(), red.load()
    for y in range(0, S):
        for x in range(0, S):
            r, g, b, a = px[x, y]
            if a and (r, g, b) == NAVY:
                px[x, y] = rp[x, y]
    return sign


if __name__ == "__main__":
    variants = [("A", variant_a()), ("B", variant_b()), ("C", variant_c()), ("D", variant_d())]
    cols = len(variants)
    sheet = Image.new("RGBA", (cols * 300 + 20, 520), (240, 240, 240, 255))
    d = ImageDraw.Draw(sheet)
    for i, (name, im) in enumerate(variants):
        x = 20 + i * 300
        sheet.alpha_composite(im.resize((256, 256), Image.LANCZOS), (x, 30))
        d.text((x + 120, 6), name, fill=(40, 40, 40))
        for row, bg in enumerate([(240, 240, 240, 255), (32, 32, 32, 255)]):
            strip = Image.new("RGBA", (280, 62), bg)
            for j, size in enumerate((48, 32, 24, 16)):
                strip.alpha_composite(im.resize((size, size), Image.LANCZOS), (10 + j * 66, (62 - size) // 2))
            sheet.alpha_composite(strip, (x - 10, 300 + row * 62))
        sheet.alpha_composite(alarm(im).resize((96, 96), Image.LANCZOS), (x + 80, 420))
    sheet.save(sys.argv[1])
    print("ok")
