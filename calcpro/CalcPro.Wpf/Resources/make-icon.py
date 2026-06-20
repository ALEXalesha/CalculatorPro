"""
Generate Calc Pro icon — Apple Liquid Glass look:
  - accent gradient (135°): #5B8DEF → #9D5BEF
  - top "display" panel: dark glass with a tiny accent dot indicator
  - bottom 3x2 grid of rounded "keys" in soft white
Saves icon.ico (multi-resolution) and icon.png (512px) into this directory.
"""
import os
from PIL import Image, ImageDraw, ImageFilter

OUT_DIR = os.path.dirname(os.path.abspath(__file__))


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def make_gradient(size, c1, c2):
    """135° linear gradient (top-left to bottom-right)."""
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    px = img.load()
    diag = size * 1.4142
    for y in range(size):
        for x in range(size):
            # project (x,y) onto 135° axis
            t = (x + y) / (2 * size - 2) if (2 * size - 2) else 0
            t = max(0.0, min(1.0, t))
            px[x, y] = lerp(c1, c2, t)
    return img


def make_icon(size):
    s = size
    accent_a = (91, 141, 239, 255)   # #5B8DEF
    accent_b = (157, 91, 239, 255)   # #9D5BEF

    # ----- background: rounded square with accent gradient
    icon = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    grad = make_gradient(s, accent_a, accent_b)

    # rounded mask
    mask = Image.new('L', (s, s), 0)
    mdraw = ImageDraw.Draw(mask)
    radius = int(s * 0.22)
    mdraw.rounded_rectangle([0, 0, s - 1, s - 1], radius=radius, fill=255)
    icon.paste(grad, (0, 0), mask)

    # specular top highlight: soft white gradient on top third
    spec = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    sdraw = ImageDraw.Draw(spec)
    for y in range(int(s * 0.45)):
        a = int(80 * (1 - y / (s * 0.45)))
        sdraw.rectangle([0, y, s, y + 1], fill=(255, 255, 255, a))
    spec_masked = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    spec_masked.paste(spec, (0, 0), mask)
    icon = Image.alpha_composite(icon, spec_masked)

    d = ImageDraw.Draw(icon)

    # ----- display panel (top half): dark glass
    pad = int(s * 0.13)
    disp_top = int(s * 0.16)
    disp_bot = int(s * 0.42)
    disp_radius = int(s * 0.07)
    d.rounded_rectangle(
        [pad, disp_top, s - pad, disp_bot],
        radius=disp_radius,
        fill=(14, 12, 26, 200),   # dark with translucency
        outline=(255, 255, 255, 90),
        width=max(1, int(s * 0.008)),
    )

    # tiny "12.3" rendered as 3 bars + a dot (readable down to 32px)
    bar_w = max(1, int(s * 0.018))
    bar_h = max(2, int(s * 0.08))
    bar_y = (disp_top + disp_bot) // 2 - bar_h // 2
    bar_gap = int(s * 0.045)
    total_w = bar_gap * 3
    bar_x = s - pad - int(s * 0.06) - total_w
    for i in range(3):
        cx = bar_x + bar_gap * i
        d.rounded_rectangle(
            [cx, bar_y, cx + bar_w * 2, bar_y + bar_h],
            radius=bar_w,
            fill=(244, 244, 248, 230),
        )
    # decimal point
    dot_r = max(1, int(s * 0.014))
    dot_cx = bar_x + bar_gap * 3 + int(s * 0.005)
    dot_cy = bar_y + bar_h - dot_r
    d.ellipse([dot_cx - dot_r, dot_cy - dot_r, dot_cx + dot_r, dot_cy + dot_r],
              fill=(244, 244, 248, 230))

    # ----- key grid (3 cols x 2 rows) below display
    grid_top = int(s * 0.50)
    grid_bot = int(s * 0.86)
    cols, rows = 3, 2
    cell_w = (s - 2 * pad) / cols
    cell_h = (grid_bot - grid_top) / rows
    key_inset = int(s * 0.015)
    key_radius = max(2, int(s * 0.04))

    for r in range(rows):
        for c in range(cols):
            x0 = pad + int(c * cell_w) + key_inset
            y0 = grid_top + int(r * cell_h) + key_inset
            x1 = pad + int((c + 1) * cell_w) - key_inset
            y1 = grid_top + int((r + 1) * cell_h) - key_inset
            # bottom-right key = "=" (accent gradient, bright)
            if r == rows - 1 and c == cols - 1:
                d.rounded_rectangle(
                    [x0, y0, x1, y1],
                    radius=key_radius,
                    fill=(255, 255, 255, 235),
                )
                # tiny "=" on it
                eq_w = (x1 - x0) // 2
                eq_h = max(1, int(s * 0.012))
                eq_cx = (x0 + x1) // 2
                eq_cy1 = (y0 + y1) // 2 - eq_h - 1
                eq_cy2 = (y0 + y1) // 2 + 1
                d.rounded_rectangle(
                    [eq_cx - eq_w // 2, eq_cy1, eq_cx + eq_w // 2, eq_cy1 + eq_h],
                    radius=eq_h // 2 + 1,
                    fill=accent_b,
                )
                d.rounded_rectangle(
                    [eq_cx - eq_w // 2, eq_cy2, eq_cx + eq_w // 2, eq_cy2 + eq_h],
                    radius=eq_h // 2 + 1,
                    fill=accent_b,
                )
            else:
                d.rounded_rectangle(
                    [x0, y0, x1, y1],
                    radius=key_radius,
                    fill=(255, 255, 255, 80),
                    outline=(255, 255, 255, 130),
                    width=max(1, int(s * 0.005)),
                )

    return icon


if __name__ == '__main__':
    largest = make_icon(256)
    ico_path = os.path.join(OUT_DIR, 'app.ico')
    largest.save(
        ico_path,
        format='ICO',
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )
    make_icon(512).save(os.path.join(OUT_DIR, 'app.png'))
    print('Icon created:', ico_path)
