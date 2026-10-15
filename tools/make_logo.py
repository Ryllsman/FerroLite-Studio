"""Generates the FerroLite Studio logo:

  Ferrolite.UI/Resources/ferrolite.ico      multi-size Windows icon (16..256)
  Ferrolite.UI/Resources/ferrolite-mark.png 256px transparent PNG for in-app use

Design: a stylized italic "F" with a diagonal foot cut, gold gradient on the
app's dark rounded tile. Drawn from a polygon at 1024px, downsampled with
LANCZOS for crisp small sizes.

Usage:  python tools/make_logo.py
"""
from PIL import Image, ImageDraw

MASTER = 1024

# Stylized F polygon in unit coordinates (0..1, y down). An italic slant is
# applied afterwards, and the stem's foot is cut diagonally for a dynamic look.
F_POINTS = [
    (0.30, 0.08),   # top-left
    (0.74, 0.08),   # top bar right end
    (0.74, 0.235),  # top bar bottom-right
    (0.46, 0.235),  # top bar bottom-left (at stem's right edge)
    (0.46, 0.375),  # stem right edge, middle bar top
    (0.64, 0.375),  # middle bar right end top
    (0.64, 0.505),  # middle bar right end bottom
    (0.46, 0.505),  # middle bar bottom-left (back at stem)
    (0.46, 0.92),   # stem bottom-right
    (0.30, 0.86),   # diagonal foot cut to bottom-left
]
SLANT = 0.07  # top leans right; applied as x += SLANT * (1 - y)

BOLT = F_POINTS  # legacy name kept for the drawing helper

TILE_INSET = 0.06      # tile margin fraction
TILE_RADIUS = 0.22     # corner radius fraction of tile size

BG_TOP = (34, 34, 38, 255)       # #222226 dark tile top
BG_BOTTOM = (24, 24, 27, 255)    # #18181b dark tile bottom
GOLD_TOP = (255, 214, 112, 255)  # #FFD670
GOLD_BOT = (222, 138, 42, 255)   # #DE8A2A
RIM = (255, 214, 112, 70)        # faint gold rim


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def slanted(points):
    return [(x + SLANT * (1.0 - y), y) for x, y in points]


def draw_master(size: int, with_tile: bool = True) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    tile0 = TILE_INSET * size
    tile1 = size - tile0
    tw = tile1 - tile0

    if with_tile:
        # Rounded dark tile with subtle vertical gradient (drawn as 1px rows)
        mask = Image.new("L", (size, size), 0)
        md = ImageDraw.Draw(mask)
        md.rounded_rectangle(
            [tile0, tile0, tile1, tile1],
            radius=int(TILE_RADIUS * tw),
            fill=255,
        )
        grad = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        gd = ImageDraw.Draw(grad)
        for y in range(int(tile0), int(tile1) + 1):
            t = (y - tile0) / max(1, tw)
            gd.line([(int(tile0), y), (int(tile1), y)], fill=lerp(BG_TOP, BG_BOTTOM, t))
        img.paste(grad, (0, 0), mask)

        # Faint gold rim for a bit of polish
        d.rounded_rectangle(
            [tile0, tile0, tile1, tile1],
            radius=int(TILE_RADIUS * tw),
            outline=RIM,
            width=max(1, size // 170),
        )

    # Mark with vertical gold gradient, clipped to the F polygon.
    pts = slanted(F_POINTS)
    mx0 = min(p[0] for p in pts) * size
    mx1 = max(p[0] for p in pts) * size
    my0 = min(p[1] for p in pts) * size
    my1 = max(p[1] for p in pts) * size
    mh = max(1, my1 - my0)

    mark_mask = Image.new("L", (size, size), 0)
    mmd = ImageDraw.Draw(mark_mask)
    mmd.polygon([(x * size, y * size) for x, y in pts], fill=255)

    mark_grad = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    mgd = ImageDraw.Draw(mark_grad)
    for y in range(int(my0), int(my1) + 1):
        t = (y - my0) / mh
        mgd.line([(0, y), (size, y)], fill=lerp(GOLD_TOP, GOLD_BOT, t))

    img.paste(mark_grad, (0, 0), mark_mask)
    return img


def main():
    import os
    here = os.path.dirname(os.path.abspath(__file__))
    ui_res = os.path.normpath(os.path.join(here, "..", "Ferrolite.UI", "Resources"))
    os.makedirs(ui_res, exist_ok=True)

    # Master renders
    master_tiled = draw_master(MASTER, with_tile=True)     # .ico face
    master_bare = draw_master(MASTER, with_tile=False)     # in-app PNG (transparent bg)

    # --- ICO with several sizes, downsampled with LANCZOS for crisp edges ---
    ico_sizes = [16, 20, 24, 32, 48, 64, 128, 256]
    base = master_tiled.resize((256, 256), Image.LANCZOS)  # Pillow resizes from this
    ico_path = os.path.join(ui_res, "ferrolite.ico")
    base.save(ico_path, format="ICO", sizes=[(s, s) for s in ico_sizes])
    print("wrote", ico_path)

    # --- 256px transparent PNG for in-app drawing (no tile: matches dark UI) ---
    png_path = os.path.join(ui_res, "ferrolite-mark.png")
    master_bare.resize((256, 256), Image.LANCZOS).save(png_path, format="PNG")
    print("wrote", png_path)


if __name__ == "__main__":
    main()
