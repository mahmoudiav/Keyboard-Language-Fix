#!/usr/bin/env python3
"""Render the extension icons.

No image library is available in this repo's toolchain, so the icon is drawn
by hand: a rounded blue square with a white swap arrow (the universal
"turn this into that" mark), supersampled 4x for antialiasing and written out
through a minimal PNG encoder.

Run:  python3 scripts/make-icons.py
"""
import os
import struct
import zlib

SS = 4                       # supersampling factor
BG = (0x2F, 0x6D, 0xF6)      # accent blue, matches the UI
FG = (0xFF, 0xFF, 0xFF)
SIZES = (16, 32, 48, 128)

ROOT = os.path.join(os.path.dirname(__file__), '..')
OUT_DIR = os.path.join(ROOT, 'icons')
ICO_PATH = os.path.join(ROOT, 'windows', 'src', 'KeyboardLanguageFix.App', 'Assets', 'app.ico')
STORE_DIR = os.path.join(ROOT, 'windows', 'packaging', 'Images')

# Sizes embedded in the Windows .ico.
ICO_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def rounded_rect(x, y, w, h, r):
    """Return a predicate telling whether (px, py) is inside a rounded rect."""
    def inside(px, py):
        cx = min(max(px, x + r), x + w - r)
        cy = min(max(py, y + r), y + h - r)
        if x + r <= px <= x + w - r or y + r <= py <= y + h - r:
            return x <= px <= x + w and y <= py <= y + h
        return (px - cx) ** 2 + (py - cy) ** 2 <= r * r
    return inside


def triangle(a, b, c):
    def sign(p, q, r):
        return (p[0] - r[0]) * (q[1] - r[1]) - (q[0] - r[0]) * (p[1] - r[1])

    def inside(px, py):
        p = (px, py)
        d1, d2, d3 = sign(p, a, b), sign(p, b, c), sign(p, c, a)
        has_neg = d1 < 0 or d2 < 0 or d3 < 0
        has_pos = d1 > 0 or d2 > 0 or d3 > 0
        return not (has_neg and has_pos)
    return inside


def build_shapes(n):
    """Icon geometry, expressed as fractions of the canvas so it scales cleanly."""
    def u(v):
        return v * n

    bar = u(0.085)                      # arrow shaft thickness
    head = u(0.085)                     # arrowhead half-height beyond the shaft
    top_y, bottom_y = u(0.355), u(0.645)
    left_x, right_x = u(0.20), u(0.80)
    tip = u(0.15)                       # arrowhead length

    shapes = [
        # top arrow, pointing right
        rounded_rect(left_x, top_y - bar / 2, right_x - tip - left_x, bar, bar / 2),
        triangle((right_x, top_y),
                 (right_x - tip, top_y - bar / 2 - head),
                 (right_x - tip, top_y + bar / 2 + head)),
        # bottom arrow, pointing left
        rounded_rect(left_x + tip, bottom_y - bar / 2, right_x - tip - left_x, bar, bar / 2),
        triangle((left_x, bottom_y),
                 (left_x + tip, bottom_y - bar / 2 - head),
                 (left_x + tip, bottom_y + bar / 2 + head)),
    ]
    return shapes


def render(n, width=None, height=None):
    """Render an n-pixel logo, optionally centred on a larger transparent canvas."""
    width = width or n
    height = height or n
    offset_x = (width - n) / 2
    offset_y = (height - n) / 2

    canvas = n * SS
    bg = rounded_rect(0, 0, canvas, canvas, canvas * 0.22)
    shapes = build_shapes(canvas)

    rows = []
    for y in range(height):
        row = bytearray([0])            # PNG filter byte: none
        for x in range(width):
            r = g = b = a = 0
            for sy in range(SS):
                for sx in range(SS):
                    px = (x - offset_x) * SS + sx + 0.5
                    py = (y - offset_y) * SS + sy + 0.5
                    if not bg(px, py):
                        continue
                    colour = FG if any(s(px, py) for s in shapes) else BG
                    r += colour[0]
                    g += colour[1]
                    b += colour[2]
                    a += 255
            samples = SS * SS
            if a:
                # Un-premultiply so the edge pixels keep their true colour.
                covered = a / 255
                row += bytes((round(r / covered), round(g / covered),
                              round(b / covered), round(a / samples)))
            else:
                row += b'\0\0\0\0'
        rows.append(bytes(row))
    return b''.join(rows)


def chunk(tag, payload):
    return (struct.pack('>I', len(payload)) + tag + payload +
            struct.pack('>I', zlib.crc32(tag + payload) & 0xFFFFFFFF))


def png_bytes(width, height, raw):
    header = struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)
    return (b'\x89PNG\r\n\x1a\n' +
            chunk(b'IHDR', header) +
            chunk(b'IDAT', zlib.compress(raw, 9)) +
            chunk(b'IEND', b''))


def write_png(path, n, raw):
    header = struct.pack('>IIBBBBB', n, n, 8, 6, 0, 0, 0)
    data = (b'\x89PNG\r\n\x1a\n' +
            chunk(b'IHDR', header) +
            chunk(b'IDAT', zlib.compress(raw, 9)) +
            chunk(b'IEND', b''))
    with open(path, 'wb') as fh:
        fh.write(data)


def write_ico(path, sizes):
    """Vista-era .ico: a directory of embedded PNGs."""
    images = [png_bytes(n, n, render(n)) for n in sizes]
    offset = 6 + 16 * len(images)
    directory = b''
    for n, data in zip(sizes, images):
        # 256 is encoded as 0 in the single-byte width/height fields.
        dim = 0 if n >= 256 else n
        directory += struct.pack('<BBBBHHII', dim, dim, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as fh:
        fh.write(struct.pack('<HHH', 0, 1, len(images)) + directory + b''.join(images))


# Microsoft Store / MSIX assets. Each entry is (base name, width, height); the
# scale-N variants are the same image rendered at N% of that size.
STORE_ASSETS = (
    ('Square44x44Logo', 44, 44),
    ('Square71x71Logo', 71, 71),
    ('Square150x150Logo', 150, 150),
    ('Square310x310Logo', 310, 310),
    ('Wide310x150Logo', 310, 150),
    ('StoreLogo', 50, 50),
    ('SplashScreen', 620, 300),
)
STORE_SCALES = (100, 125, 150, 200, 400)
# Start menu, taskbar and Alt+Tab pick from these.
TARGET_SIZES = (16, 24, 32, 48, 256)


def write_store_asset(name, width, height):
    """A square asset is full-bleed; a wide one centres the logo on transparency."""
    logo = min(width, height)
    if width != height:
        logo = int(min(width, height) * 0.62)
    path = os.path.join(STORE_DIR, name)
    with open(path, 'wb') as fh:
        fh.write(png_bytes(width, height, render(logo, width, height)))
    return path


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for n in SIZES:
        path = os.path.join(OUT_DIR, 'icon-%d.png' % n)
        write_png(path, n, render(n))
        print('wrote', os.path.relpath(path), os.path.getsize(path), 'bytes')

    write_ico(ICO_PATH, ICO_SIZES)
    print('wrote', os.path.relpath(ICO_PATH), os.path.getsize(ICO_PATH), 'bytes')

    os.makedirs(STORE_DIR, exist_ok=True)
    count = 0
    for base, width, height in STORE_ASSETS:
        for scale in STORE_SCALES:
            w = max(1, round(width * scale / 100))
            h = max(1, round(height * scale / 100))
            write_store_asset('%s.scale-%d.png' % (base, scale), w, h)
            count += 1
        # Unscaled fallback, required by the packaging tools.
        write_store_asset('%s.png' % base, width, height)
        count += 1

    for size in TARGET_SIZES:
        write_store_asset('Square44x44Logo.targetsize-%d.png' % size, size, size)
        write_store_asset(
            'Square44x44Logo.targetsize-%d_altform-unplated.png' % size, size, size)
        count += 2

    print('wrote %d Microsoft Store assets into %s' % (count, os.path.relpath(STORE_DIR)))


if __name__ == '__main__':
    main()
