"""Draws the icon of Blyant.exe: a pencil in the colors of the Default scheme (cyan, yellow) on a dark terminal
square, writing a cursor. It is drawn big, then made smaller for each size of the .ico (16 to 256 pixels; the small
sizes are drawn simpler, and 16 without the cursor).

    python tools/icon.py [directory]

writes Blyant/Blyant.ico, and Blyant/Blyant.svg (the detailed icon as SVG, for a web site or print). With a
directory given, it writes them there, with the sizes as PNG and a preview of them all.
Needs Pillow (pip install pillow): only to draw the icon, not to build Blyant.
"""
import os
import sys
from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
OUT = sys.argv[1] if len(sys.argv) > 1 else None
if OUT:
    os.makedirs(OUT, exist_ok=True)
S = 1024  # The size it is drawn in

# Windows Terminal's "Campbell" colors, which the Default scheme is seen in on Windows
BG = (16, 20, 24, 255)
BG_EDGE = (52, 62, 70, 255)
CYAN = (97, 214, 214, 255)
CYAN_LIGHT = (160, 236, 236, 255)
CYAN_DARK = (40, 150, 160, 255)
YELLOW = (249, 241, 165, 255)
YELLOW_DARK = (215, 196, 92, 255)
GRAY = (204, 204, 204, 255)
GRAY_DARK = (140, 140, 140, 255)
WOOD = (238, 206, 160, 255)
WOOD_DARK = (205, 166, 118, 255)
LEAD = (40, 44, 48, 255)


def pencil(simple):
    """A pencil lying flat, the point at the left: drawn on its own image, to be turned"""
    width = 190 if not simple else 230
    length = 712 + max(110, width // 2 + 30)
    img = Image.new('RGBA', (length, width), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    third = width / 3
    tip, cone, body_end, ferrule_end = 70, 230, 640, 712
    # The wood of the point, and the lead
    d.polygon([(0, width / 2), (cone, 0), (cone, width)], fill=WOOD)
    d.polygon([(0, width / 2), (cone, width * 2 / 3), (cone, width)], fill=WOOD_DARK)
    d.polygon([(0, width / 2), (tip, width / 2 - width * tip / cone / 2),
               (tip, width / 2 + width * tip / cone / 2)], fill=LEAD)
    # The body: three sides, light at the top
    if simple:
        d.rectangle([cone, 0, body_end, width], fill=CYAN)
    else:
        d.rectangle([cone, 0, body_end, third], fill=CYAN_LIGHT)
        d.rectangle([cone, third, body_end, 2 * third], fill=CYAN)
        d.rectangle([cone, 2 * third, body_end, width], fill=CYAN_DARK)
        # The scalloped edge where the wood begins
        for k in range(3):
            y0 = k * third
            d.pieslice([cone - third / 2, y0, cone + third / 2, y0 + third], 90, 270,
                       fill=(CYAN_LIGHT, CYAN, CYAN_DARK)[k])
    # The metal band
    d.rectangle([body_end, 0, ferrule_end, width], fill=GRAY)
    if not simple:
        for x in (body_end + 18, body_end + 46):
            d.rectangle([x, 0, x + 8, width], fill=GRAY_DARK)
    # The eraser, rounded at the end
    r = width / 2
    d.rectangle([ferrule_end, 0, length - r, width], fill=YELLOW)
    d.pieslice([length - 2 * r, 0, length, width], 270, 90, fill=YELLOW)
    if not simple:
        d.rectangle([ferrule_end, 2 * third, length - r, width], fill=YELLOW_DARK)
        d.pieslice([length - 2 * r, 0, length, width], 0, 90, fill=YELLOW_DARK)
    return img


def icon(simple, cursor=True):
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # The terminal: a dark rounded square
    m = 40
    d.rounded_rectangle([m, m, S - m, S - m], radius=190, fill=BG, outline=BG_EDGE, width=0 if simple else 18)
    # The pencil, a little larger than drawn, from the top right down to the point at the bottom left
    flat = pencil(simple)
    scale = 1.12
    flat = flat.resize((int(flat.size[0] * scale), int(flat.size[1] * scale)), Image.LANCZOS)
    p = flat.rotate(45, expand=True, resample=Image.BICUBIC)
    # The point: the pixel furthest down and to the left
    alpha = p.getchannel('A').load()
    pw, ph = p.size
    tip = max(((x, y) for y in range(0, ph, 2) for x in range(0, pw, 2) if alpha[x, y] > 128),
              key=lambda xy: xy[1] - xy[0])
    # The cursor it writes: a yellow underscore on the line of the point (its top a little above the point, the
    # rest below it), in the middle of the square from left to right. The pencil and that line are put in the
    # middle of the square together
    height = 56 if not simple else 84
    width = 200 if not simple else 240
    bar_top, bar_bottom = tip[1] - height // 3, tip[1] + height * 2 // 3
    group = Image.new('RGBA', (pw + 300, ph + 100), (0, 0, 0, 0))
    group.alpha_composite(p, (0, 0))
    # (the place of the line, for the size of the group: the cursor itself is drawn after)
    ImageDraw.Draw(group).rectangle((tip[0] + 80, bar_top, tip[0] + 80 + width, bar_bottom), fill=(0, 0, 0, 1))
    box = group.getbbox()
    pencil_only = Image.new('RGBA', group.size, (0, 0, 0, 0))
    pencil_only.alpha_composite(p, (0, 0))
    part = pencil_only.crop(box)
    left, top = (S - part.size[0]) // 2, (S - part.size[1]) // 2
    img.alpha_composite(part, (left, top))
    bar = ((S - width) // 2, top + bar_top - box[1], (S + width) // 2, top + bar_bottom - box[1])
    if cursor:
        d.rectangle(bar, fill=YELLOW)
    if not simple:
        # Where everything went, for the SVG: the middle of the turned pencil, and the size it was drawn in
        LAYOUT.update(center=(left - box[0] + pw / 2, top - box[1] + ph / 2), flat=pencil(False).size,
                      scaled=flat.size, bar=bar)
    return img


LAYOUT = {}


def hexcolor(color):
    return '#%02x%02x%02x' % color[:3]


def svg():
    """The detailed icon as SVG (for a web site or print): the same shapes as the big drawing"""
    length, width = LAYOUT['flat']
    scaled = LAYOUT['scaled']
    cx, cy = LAYOUT['center']
    third = width / 3
    tip, cone, body_end, ferrule_end = 70, 230, 640, 712
    r = width / 2
    out = []

    def poly(points, color):
        out.append('<polygon points="%s" fill="%s"/>' % (' '.join('%g,%g' % p for p in points), hexcolor(color)))

    def rect(x0, y0, x1, y1, color):
        out.append('<rect x="%g" y="%g" width="%g" height="%g" fill="%s"/>' % (x0, y0, x1 - x0, y1 - y0,
                                                                              hexcolor(color)))

    def half(x, y, radius, start, end, color):
        # A slice of a circle from one point of its edge to another, clockwise (as Pillow's pieslice)
        out.append('<path d="M%g %gL%g %gA%g %g 0 0 1 %g %gZ" fill="%s"/>' % (x, y, *start, radius, radius, *end,
                                                                             hexcolor(color)))

    poly([(0, width / 2), (cone, 0), (cone, width)], WOOD)
    poly([(0, width / 2), (cone, width * 2 / 3), (cone, width)], WOOD_DARK)
    poly([(0, width / 2), (tip, width / 2 - width * tip / cone / 2), (tip, width / 2 + width * tip / cone / 2)],
         LEAD)
    for k, color in enumerate((CYAN_LIGHT, CYAN, CYAN_DARK)):
        rect(cone, k * third, body_end, (k + 1) * third, color)
    for k, color in enumerate((CYAN_LIGHT, CYAN, CYAN_DARK)):
        mid = k * third + third / 2
        half(cone, mid, third / 2, (cone, mid + third / 2), (cone, mid - third / 2), color)
    rect(body_end, 0, ferrule_end, width, GRAY)
    for x in (body_end + 18, body_end + 46):
        rect(x, 0, x + 8, width, GRAY_DARK)
    rect(ferrule_end, 0, length - r, width, YELLOW)
    half(length - r, r, r, (length - r, 0), (length - r, width), YELLOW)
    rect(ferrule_end, 2 * third, length - r, width, YELLOW_DARK)
    half(length - r, r, r, (length, r), (length - r, width), YELLOW_DARK)

    m, edge = 40, 18
    x0, y0, x1, y1 = LAYOUT['bar']
    return '\n'.join([
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d" width="%d" height="%d">' % (S, S, S, S),
        '<title>Blyant</title>',
        # The terminal: the edge is drawn inside the square, as in the PNG
        '<rect x="%g" y="%g" width="%g" height="%g" rx="%g" fill="%s" stroke="%s" stroke-width="%d"/>' % (
            m + edge / 2, m + edge / 2, S - 2 * m - edge, S - 2 * m - edge, 190 - edge / 2, hexcolor(BG),
            hexcolor(BG_EDGE), edge),
        # The pencil: drawn lying flat, made a little larger, turned 45 degrees and put where it is in the PNG
        '<g transform="translate(%g %g) rotate(-45) translate(%g %g) scale(%g %g)">' % (
            cx, cy, -scaled[0] / 2, -scaled[1] / 2, scaled[0] / length, scaled[1] / width),
        *out,
        '</g>',
        '<rect x="%g" y="%g" width="%g" height="%g" fill="%s"/>' % (x0, y0, x1 - x0, y1 - y0, hexcolor(YELLOW)),
        '</svg>',
        ''])


big = icon(False)
small = icon(True)
smallest = icon(True, cursor=False)
sizes = [256, 128, 64, 48, 32, 24, 16]
frames = []
for size in sizes:
    source = smallest if size <= 16 else small if size <= 32 else big
    frames.append(source.resize((size, size), Image.LANCZOS))
ico = os.path.join(OUT, 'Blyant.ico') if OUT else os.path.join(ROOT, 'Blyant', 'Blyant.ico')
frames[0].save(ico, sizes=[(s, s) for s in sizes], append_images=frames[1:])
print('Written:', os.path.normpath(ico))
vector = os.path.join(os.path.dirname(ico), 'Blyant.svg')
with open(vector, 'w', encoding='utf-8', newline='\n') as file:
    file.write(svg())
print('Written:', os.path.normpath(vector))
if not OUT:
    sys.exit(0)
big.save(os.path.join(OUT, 'blyant-1024.png'))
for f in frames:
    f.save(os.path.join(OUT, 'blyant-%d.png' % f.size[0]))

# A preview of all the sizes side by side, on a light and a dark background, at their real size and enlarged
sheet = Image.new('RGBA', (1400, 760), (255, 255, 255, 255))
d = ImageDraw.Draw(sheet)
d.rectangle([0, 380, 1400, 760], fill=(32, 32, 32, 255))
for row, y in ((0, 40), (1, 420)):
    x = 20
    for f in frames:
        sheet.alpha_composite(f, (x, y))
        x += f.size[0] + 16
    # The small ones enlarged 4 times, as pixels
    x = 900
    for f in frames[-3:]:
        e = f.resize((f.size[0] * 4, f.size[1] * 4), Image.NEAREST)
        sheet.alpha_composite(e, (x, y + 160))
        x += e.size[0] + 16
sheet.save(os.path.join(OUT, 'preview.png'))
