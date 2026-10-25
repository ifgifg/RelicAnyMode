#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate a simple 80x80 mod icon: a golden relic statue on a dark pedestal."""
from PIL import Image, ImageDraw

SIZE = 80
img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# dark rounded background
d.rounded_rectangle([2, 2, SIZE - 3, SIZE - 3], radius=14, fill=(24, 18, 34, 255))
d.rounded_rectangle([5, 5, SIZE - 6, SIZE - 6], radius=12, outline=(96, 72, 40, 255), width=2)

# glow behind statue
for r in range(30, 10, -2):
    alpha = int(26 * (30 - r) / 20)
    d.ellipse([SIZE // 2 - r, 34 - r // 2, SIZE // 2 + r, 34 + r // 2], fill=(255, 200, 90, alpha))

# pedestal
d.polygon([(22, 64), (58, 64), (54, 70), (26, 70)], fill=(120, 92, 48, 255))
d.rectangle([25, 60, 55, 64], fill=(168, 130, 66, 255))

# statue body (robe)
d.polygon([(40, 20), (49, 30), (52, 58), (28, 58), (31, 30)], fill=(214, 168, 78, 255))
d.polygon([(40, 20), (49, 30), (52, 58), (40, 58), (40, 20)], fill=(240, 196, 104, 255))

# head
d.ellipse([35, 12, 45, 24], fill=(252, 220, 140, 255))
# halo
d.arc([28, 6, 52, 30], start=200, end=340, fill=(255, 230, 150, 255), width=2)

# tiny sparkles
for (x, y, c) in [(18, 26, (255, 238, 160, 220)), (62, 22, (255, 238, 160, 220)), (60, 46, (255, 220, 120, 200)), (17, 50, (255, 220, 120, 200))]:
    d.point((x, y), fill=c)
    d.line([(x - 2, y), (x + 2, y)], fill=c)
    d.line([(x, y - 2), (x, y + 2)], fill=c)

img.save("icon.png")
img.resize((40, 40), Image.LANCZOS).save("icon_small.png")
print("icon written")
