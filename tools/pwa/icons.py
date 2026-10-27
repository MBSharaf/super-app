"""Draws the Super App mark (the favicon's S and orange dot on dark green) as PNG icons for the web app manifest."""
import re
from PIL import Image, ImageDraw

PATH = ("M9 20.5c1.6 1.4 3.6 2.1 6 2.1 3.3 0 5.4-1.6 5.4-4 0-2.2-1.5-3.1-4.6-3.8-2-.5-2.6-.8-2.6-1.6 0-.7.7-1.2 1.9-1.2 "
        "1.4 0 2.6.5 3.7 1.4l1.7-2.3C19.1 9.9 17.4 9.3 15.2 9.3c-3.1 0-5.2 1.7-5.2 4 0 2.4 1.7 3.3 4.7 4 1.9.4 2.5.8 2.5 1.6 "
        "0 .8-.8 1.3-2.1 1.3-1.7 0-3.1-.6-4.4-1.8z")

def points():
    toks = re.findall(r"[MmCcLlZz]|-?\d*\.?\d+", PATH)
    pts, cur, cmd, i = [], (0.0, 0.0), None, 0
    def num():
        nonlocal i; v = float(toks[i]); i += 1; return v
    while i < len(toks):
        if toks[i].isalpha():
            cmd = toks[i]; i += 1
            if cmd in 'Zz': continue
        if cmd in 'Mm':
            x, y = num(), num(); cur = (x, y) if cmd == 'M' else (cur[0] + x, cur[1] + y); pts.append(cur)
        elif cmd in 'Ll':
            x, y = num(), num(); cur = (x, y) if cmd == 'L' else (cur[0] + x, cur[1] + y); pts.append(cur)
        elif cmd in 'Cc':
            c = [num() for _ in range(6)]
            if cmd == 'c': c = [c[k] + cur[k % 2] for k in range(6)]
            p0, p1, p2, p3 = cur, (c[0], c[1]), (c[2], c[3]), (c[4], c[5])
            for s in range(1, 17):
                t = s / 16; u = 1 - t
                pts.append((u**3*p0[0] + 3*u*u*t*p1[0] + 3*u*t*t*p2[0] + t**3*p3[0], u**3*p0[1] + 3*u*u*t*p1[1] + 3*u*t*t*p2[1] + t**3*p3[1]))
            cur = p3
    return pts

def draw(size, inset, rounded):
    k = 4; big = size * k
    img = Image.new('RGBA', (big, big), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    if rounded: d.rounded_rectangle([0, 0, big - 1, big - 1], radius=big * 9 / 32, fill='#0D1411')
    else: d.rectangle([0, 0, big, big], fill='#0D1411')
    scale = big * (1 - 2 * inset) / 32; off = big * inset
    d.polygon([(off + x * scale, off + y * scale) for x, y in points()], fill='#FFFFFF')
    cx, cy, r = off + 23.5 * scale, off + 9 * scale, 2.5 * scale
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill='#FF5B14')
    return img.resize((size, size), Image.LANCZOS)

import os
out = os.path.join(os.path.dirname(__file__), "..", "..", "icons") + os.sep
draw(192, 0, True).save(out + "icon-192.png"); draw(512, 0, True).save(out + "icon-512.png")
draw(512, 0.12, False).save(out + "icon-maskable-512.png"); draw(180, 0.06, False).save(out + "apple-touch-icon.png")
print('ok')
