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
OUT_DIR = os.path.join(os.path.dirname(__file__), '..', 'icons')


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


def render(n):
    canvas = n * SS
    bg = rounded_rect(0, 0, canvas, canvas, canvas * 0.22)
    shapes = build_shapes(canvas)

    rows = []
    for y in range(n):
        row = bytearray([0])            # PNG filter byte: none
        for x in range(n):
            r = g = b = a = 0
            for sy in range(SS):
                for sx in range(SS):
                    px = x * SS + sx + 0.5
                    py = y * SS + sy + 0.5
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


def write_png(path, n, raw):
    header = struct.pack('>IIBBBBB', n, n, 8, 6, 0, 0, 0)
    data = (b'\x89PNG\r\n\x1a\n' +
            chunk(b'IHDR', header) +
            chunk(b'IDAT', zlib.compress(raw, 9)) +
            chunk(b'IEND', b''))
    with open(path, 'wb') as fh:
        fh.write(data)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for n in SIZES:
        path = os.path.join(OUT_DIR, 'icon-%d.png' % n)
        write_png(path, n, render(n))
        print('wrote', os.path.relpath(path), os.path.getsize(path), 'bytes')


if __name__ == '__main__':
    main()
