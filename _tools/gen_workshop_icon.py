# -*- coding: utf-8 -*-
"""Generate 512x512 Steam Workshop preview icon for RelicAnyMode."""
from PIL import Image, ImageDraw, ImageFont

SIZE = 512
img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 255))
d = ImageDraw.Draw(img)

# background vertical gradient
for y in range(SIZE):
    t = y / SIZE
    r = int(16 + 34 * t)
    g = int(10 + 18 * t)
    b = int(28 + 44 * t)
    d.line([(0, y), (SIZE, y)], fill=(r, g, b, 255))

# soft golden glow behind the statue
for rad in range(215, 40, -8):
    alpha = int(20 * (215 - rad) / 175)
    d.ellipse([256 - rad, 250 - rad, 256 + rad, 250 + rad], fill=(255, 200, 90, alpha))

# pedestal
d.polygon([(112, 432), (400, 432), (366, 474), (146, 474)], fill=(118, 90, 46, 255))
d.rectangle([134, 402, 378, 432], fill=(170, 132, 66, 255))

# statue body (robe)
d.polygon([(256, 150), (316, 208), (342, 400), (170, 400), (196, 208)], fill=(212, 166, 76, 255))
d.polygon([(256, 150), (316, 208), (342, 400), (256, 400), (256, 150)], fill=(240, 196, 104, 255))

# head
d.ellipse([222, 106, 290, 182], fill=(252, 221, 141, 255))
# halo
d.arc([190, 62, 322, 202], start=205, end=335, fill=(255, 231, 152, 255), width=7)

# small sparkles
for (x, y) in [(122, 180), (402, 168), (382, 320), (118, 330), (150, 120), (372, 110)]:
    d.line([(x - 7, y), (x + 7, y)], fill=(255, 238, 170, 210), width=2)
    d.line([(x, y - 7), (x, y + 7)], fill=(255, 238, 170, 210), width=2)

font_big = ImageFont.truetype(r"C:\Windows\Fonts\msyhbd.ttc", 58)
font_sub = ImageFont.truetype(r"C:\Windows\Fonts\msyhbd.ttc", 30)
font_line = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 23)
font_author = ImageFont.truetype(r"C:\Windows\Fonts\msyhbd.ttc", 25)


def center(text, y, font, fill):
    bb = d.textbbox((0, 0), text, font=font)
    w = bb[2] - bb[0]
    d.text(((SIZE - w) / 2, y), text, font=font, fill=fill)


# title
center("圣遗物全模式掉落", 26, font_big, (255, 226, 145, 255))
center("Relic Any Mode", 96, font_sub, (255, 204, 118, 255))
# feature line
center("任意难度 · 每次击杀必掉 · 彩色稀有度", 138, font_line, (226, 214, 196, 255))
# author
center("作者：B站 AAA狙神夏乌拉", 478, font_author, (255, 232, 168, 255))

img.save("icon_workshop.png", optimize=True)
print("icon_workshop.png generated:", img.size)
