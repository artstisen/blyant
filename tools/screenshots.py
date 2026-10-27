"""Screenshots of Blyant for the README and the guides, as SVG images of a terminal window.

    python tools/screenshots.py [--exe PATH] [scene ...]

Blyant is run in a hidden terminal (a pseudo console) of 100x30, with a demo project in C:\\Writing and a data
directory of its own in the temp folder, so nothing of the user's is shown or changed. Each scene gets the keys that
set it up, and what the terminal then shows (each cell with its colors, and the cursor) is drawn as an SVG in
docs/screenshots. Run it again after a change of the screens.

Needs Windows, and the Python packages pyte and pywinpty. The texts of the demo project are made up for it.
The program is the release build (publish/win-x64/Blyant.exe) unless --exe gives another.
"""
import argparse
import ctypes
import html
import os
import queue
import re
import shutil
import sys
import tempfile
import threading
import time

import pyte
import winpty

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'docs', 'screenshots')
WRITING = r'C:\Writing'
PROJECT = os.path.join(WRITING, 'Harbour Lights')
MARKER = os.path.join(WRITING, '.blyant-demo')
COLS, ROWS = 100, 30

# ---------------------------------------------------------------------------------------------------------------
# The demo project (made up for the screenshots)

CHAPTER_1 = """# Chapter 1: The Lamp Room

The ferry was late, as it always was in October, and Maren Holt stood at the
rail with her collar up and watched the island come out of the rain.

It was smaller than she remembered. The lighthouse sat on its rock like a
pencil someone had forgotten on a table, and the keeper's cottage beside it
had lost half its whitewash to the wind.

> "Nobody keeps a light any more," the ferryman said. "The lamp keeps itself."

She did not answer. Her grandfather had kept that light for forty-one years,
and he had never once let it keep itself.

## The key

The key was where the letter said it would be: under the third stone of the
wall, wrapped in oilcloth, with a label in his square handwriting.

*For Maren. Wind the clock first.*

She wound the clock first.
"""

CHAPTER_2 = """# Chapter 2: Low Water

The tide tables were pinned to the kitchen wall, a year out of date. Maren
copied the new times into the margin in pencil, the way he used to.

At low water the causeway showed itself, a grey spine of stones running to the
mainland. At high water it was gone, and the island was an island again.

- Low water: 06:12 and 18:40
- High water: 12:25
- Sunset: 17:58

She walked out as far as the third marker and turned back.
"""

CHAPTER_3_LINES = [
    "# Chapter 3: The Logbook",
    "",
    "The logbook was in the drawer of the chart table, under a tin of matches and",
    "a pair of reading glasses with one arm mended in wire.",
    "",
    "Maren opened it at the last page. The entries stopped in **March**, in the",
    "middle of a sentence, and the harbour lights were listed one by one down the",
    "margin, each with a tick beside it.",
    "",
    "## The entries",
    "",
    "- *Harbour mouth:* steady green, all night",
    "- *North pier:* red, flashing every four seconds",
    "- *The Teeth:* white, two flashes, then dark",
    "",
    "She read them twice. The Teeth had been dark for years; the harbour board had",
    "said so in a letter she still had somewhere. But her grandfather had ticked",
    "it every night until March.",
    "",
    "> Saw the Teeth again tonight. Two flashes. Nobody on the rocks.",
    "",
    "She closed the book and listened to the clock.",
    "",
]
for n in range(1, 9):
    CHAPTER_3_LINES += [
        "## Night %d" % n if n > 1 else "## The first night",
        "",
        "The wind came round to the east after midnight and the windows of the lamp",
        "room hummed like a glass rubbed with a wet finger. Maren sat on the iron",
        "steps with the logbook on her knees and watched the harbour lights.",
        "",
        "Steady green. Red, every four seconds. And out past the north pier, where the",
        "chart showed nothing but broken water, two white flashes, then dark.",
        "",
        "She wrote the time in the margin: **%02d:%02d**." % (1 + n % 4, (n * 13) % 60),
        "",
    ]
CHAPTER_3_LINES += [
    "## Morning",
    "",
    "In the morning she rang the harbour office. See the [notes on the call]",
    "(../research/interview-notes.txt) for what the clerk said.",
    "",
]
CHAPTER_3 = "\n".join(CHAPTER_3_LINES)

FILES = {
    'chapter-01.md': CHAPTER_1,
    'chapter-02.md': CHAPTER_2,
    'chapter-03.md': CHAPTER_3,
    'chapter-04.md': "# Chapter 4: The Teeth\n\n(To be written: Maren rows out at low water.)\n",
    'outline.md': """# Harbour Lights: outline

1. The Lamp Room: Maren arrives, finds the key
2. Low Water: the causeway, the tide tables
3. The Logbook: the lights he ticked every night
4. The Teeth: the light that should be dark
5. The Harbour Board: what the letter did not say
""",
    'characters.md': """# Characters

**Maren Holt**: thirty-four, a surveyor, the keeper's granddaughter.
**Elias Holt**: the keeper, forty-one years on the island.
**Jon Arvik**: the ferryman, who knows more than he says.
""",
    'todo.md': """# To do

- [ ] Check the tide times in chapter 2 against the tables
- [ ] Give the ferryman a reason to stay on the island
- [x] Move the logbook scene to chapter 3
""",
    os.path.join('research', 'tides.md'): """# Tides

Spring tides near the full and the new moon; neap tides between.
The causeway is dry for about two hours either side of low water.
Use the times for the second week of October.
""",
    os.path.join('research', 'lighthouse-history.md'): """# The lighthouse

Built of granite, first lit with an oil lamp, later converted to electric.
Keepers lived on the rock until the light was automated.
The clock drove the lens; it had to be wound every four hours.
""",
    os.path.join('research', 'interview-notes.txt'): """Notes from a call with the harbour office (made up for the story)

- The light on the Teeth was taken out of service years ago
- No one has asked about it since
- "If you see it flashing, it is not ours"
""",
    os.path.join('drafts', 'chapter-03-first.md'): "# Chapter 3 (first draft)\n\nThe logbook was in the drawer.\n",
    os.path.join('drafts', 'opening-lines.md'): "# Openings to try\n\n- The ferry was late.\n- The lamp keeps itself.\n",
}

OTHER_PROJECTS = {
    os.path.join(WRITING, 'Short Stories', 'the-salt-road.md'):
        "# The Salt Road\n\nThey carried salt over the hills in the dry months, two donkeys and a boy.\n",
    os.path.join(WRITING, 'Short Stories', 'winter-orchard.md'):
        "# Winter Orchard\n\nThe apples that nobody picked hung on the bare trees like lanterns.\n",
    os.path.join(WRITING, 'Blog', 'on-revising.md'):
        "# On revising\n\nThe second draft is where the story finds out what it is about.\n",
}


def hide(path):
    # Hidden on Windows: the marker is not shown in the file browser
    ctypes.windll.kernel32.SetFileAttributesW(path, 0x02)


def make_demo():
    """The demo project in C:\\Writing (made again each time; a C:\\Writing that is not the demo is left alone)"""
    if os.path.exists(WRITING) and not os.path.exists(MARKER):
        sys.exit(WRITING + ' is there and is not the demo of this script: nothing was changed')
    if os.path.exists(WRITING):
        shutil.rmtree(WRITING)
    os.makedirs(WRITING)
    with open(MARKER, 'w', encoding='utf-8') as f:
        f.write('Made by tools/screenshots.py of Blyant. It is made again each time the script runs.\n')
    hide(MARKER)
    for name, text in FILES.items():
        path = os.path.join(PROJECT, name)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
    for path, text in OTHER_PROJECTS.items():
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
    # A picture (only its name and size are shown)
    with open(os.path.join(PROJECT, 'research', 'harbour-map.png'), 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' + bytes(4096))


# ---------------------------------------------------------------------------------------------------------------
# The data directory of the demo: settings and lists

def p(*parts):
    return os.path.join(PROJECT, *parts)


SETTINGS = {
    'linewidth': 99, 'linecount': 29, 'autosize': 'true', 'startmode': 'select', 'filetype': 'md',
    'newlineendings': 'system', 'startfavorites': 'false', 'deletetotrash': 'true', 'favoritepanes': 'true',
    'favoritepeek': 'true', 'favoritenumbers': 'false', 'bookmarkpanes': 'true', 'bookmarkpeek': 'true',
    'bookmarknumbers': 'true', 'filenumbers': 'true', 'historynumbers': 'false', 'hotlistnumbers': 'false',
    'searchnumbers': 'false', 'commandlist': 'true', 'hints': 'true', 'hintindex': '1', 'osc52': 'true',
    'pastebin': 'false', 'clipboardyank': 'all', 'pastebinappend': 'false', 'pastebinfile': 'paste-bin.txt',
    'snipsort': 'file', 'asciistyle': '0', 'logoanim': 'off', 'logo': 'Blyant', 'colorscheme': 'Default',
    'monochrome': 'false', 'browsedir1': PROJECT, 'browsedir2': p('research'),
    'browsesel1': 'chapter-03.md', 'browsesel2': 'tides.md', 'browsepane': '1', 'pickdir': '',
    'browsedual': 'true', 'recovery': 'true', 'autocomplete': 'false', 'language': 'en', 'syntaxcolors': 'true',
    'linenumbers': 'true', 'wrap': 'true', 'whitespace': 'false', 'scrolloff': 'true', 'scrolllines': '12',
    'spelllang': 'en',
}

FAVORITES = """Cyan\t[Harbour Lights]
#Second draft by the end of March. Keep the chapters under 4,000 words.
 Yellow\t[Chapters]
  \t1:1\t{c1}
  \t1:1\t{c2}
  \t24:1\t{c3}
#Night scenes: check that the times in the margin go forward.\\nRead it aloud before the next draft.
  \t{c4}
 Green\t[Research]
#Facts to check before the second draft
  \t{tides}
  \t{history}
  \t{notes}
 \t{outline}
 \t{characters}
Magenta\t[Short Stories]
 \t{salt}
 \t{orchard}
Red\t{todo}
""".format(c1=p('chapter-01.md'), c2=p('chapter-02.md'), c3=p('chapter-03.md'), c4=p('chapter-04.md'),
           tides=p('research', 'tides.md'), history=p('research', 'lighthouse-history.md'),
           notes=p('research', 'interview-notes.txt'), outline=p('outline.md'), characters=p('characters.md'),
           salt=os.path.join(WRITING, 'Short Stories', 'the-salt-road.md'),
           orchard=os.path.join(WRITING, 'Short Stories', 'winter-orchard.md'), todo=p('todo.md'))

BOOKMARKS = """[Harbour Lights]\tCyan
 file\t{c3}\t\t\t24:1
#The first night in the lamp room
 file\t{c1}\t\t\t16:1
 file\t{outline}
 file\t{characters}
[Research]\tGreen
 dir\t{research}
 file\t{tides}\t\t\t3:1
dir\t{blog}
page\tschemes
""".format(c3=p('chapter-03.md'), c1=p('chapter-01.md'), outline=p('outline.md'), characters=p('characters.md'),
           research=p('research'), tides=p('research', 'tides.md'), blog=os.path.join(WRITING, 'Blog'))

HISTORY = """2026-10-27 21:40\t14:1\t{c3}
2026-10-27 20:12\t1:1\t{outline}
2026-10-27 19:55\t5:1\t{c2}
2026-10-26 22:03\t1:1\t{c1}
""".format(c3=p('chapter-03.md'), outline=p('outline.md'), c2=p('chapter-02.md'), c1=p('chapter-01.md'))

PROJECTS = "\n".join([PROJECT, os.path.join(WRITING, 'Short Stories'), os.path.join(WRITING, 'Blog')]) + "\n"


def make_data(work, settings):
    data = os.path.join(work, 'data')
    if os.path.exists(data):
        shutil.rmtree(data)
    os.makedirs(data)
    values = dict(SETTINGS, **settings)
    lines = ['; Blyant screenshots', '[SETTINGS]'] + ['%s=%s' % item for item in values.items()]
    files = {'settings.ini': '\n'.join(lines) + '\n', 'favorites.txt': FAVORITES, 'bookmarks.txt': BOOKMARKS,
             'history.txt': HISTORY, 'projects.txt': PROJECTS,
             'hotlist.txt': p('research') + '\n' + PROJECT + '\n'}
    for name, text in files.items():
        with open(os.path.join(data, name), 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)


# ---------------------------------------------------------------------------------------------------------------
# Running Blyant in a pseudo console

KEYS = {'<Esc>': '\x1b', '<Enter>': '\r', '<Tab>': '\t', '<Down>': '\x1b[B', '<Up>': '\x1b[A',
        '<Left>': '\x1b[D', '<Right>': '\x1b[C', '<C-o>': '\x0f', '<BS>': '\x7f'}


def run(exe, work, args, steps):
    """Start Blyant, send the steps of keys, and return the screen when all is drawn"""
    proc = winpty.PtyProcess.spawn([exe] + args, cwd=work, dimensions=(ROWS, COLS))
    screen = pyte.Screen(COLS, ROWS)
    stream = pyte.Stream(screen)
    chunks = queue.Queue()

    def reader():
        while True:
            try:
                data = proc.read(65536)
            except Exception:
                return
            if data:
                chunks.put(data)

    threading.Thread(target=reader, daemon=True).start()

    def pump(seconds):
        end = time.time() + seconds
        while time.time() < end:
            try:
                stream.feed(chunks.get(timeout=0.05))
            except queue.Empty:
                pass

    pump(4)
    for step in steps:
        for part in re.split(r'(<[^>]+>)', step):
            if part == '<wait>':
                pump(1)
            elif part:
                proc.write(KEYS.get(part, part))
                pump(0.3)
        pump(1.2)
    pump(1.5)
    proc.terminate(force=True)
    return screen


# ---------------------------------------------------------------------------------------------------------------
# The screen as an SVG of a terminal window

# The 16 colors of the console (the palette of Windows Terminal, Campbell)
PALETTE = {
    'black': '#0C0C0C', 'red': '#C50F1F', 'green': '#13A10E', 'brown': '#C19C00', 'blue': '#0037DA',
    'magenta': '#881798', 'cyan': '#3A96DD', 'white': '#CCCCCC', 'brightblack': '#767676', 'brightred': '#E74856',
    'brightgreen': '#16C60C', 'brightbrown': '#F9F1A5', 'brightblue': '#3B78FF', 'brightmagenta': '#B4009E',
    'brightcyan': '#61D6D6', 'brightwhite': '#F2F2F2',
}
DEFAULT_FG, DEFAULT_BG = '#CCCCCC', '#0C0C0C'
CELL_W, CELL_H, FONT_SIZE = 8.4, 17, 14
PAD, BAR = 14, 34


def color(name, default):
    if name == 'default':
        return default
    if name in PALETTE:
        return PALETTE[name]
    if re.fullmatch(r'[0-9a-fA-F]{6}', name):
        return '#' + name
    return default


def svg(screen, title, show_cursor):
    width = PAD * 2 + COLS * CELL_W
    height = BAR + PAD * 2 + ROWS * CELL_H
    out = ['<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d">'
           % (width, height, width, height),
           '<title>%s</title>' % html.escape(title),
           '<style>text{font-family:"Cascadia Mono","Cascadia Code",Consolas,"DejaVu Sans Mono",Menlo,'
           'monospace;font-size:%dpx;white-space:pre}</style>' % FONT_SIZE,
           '<rect width="100%%" height="100%%" rx="10" fill="%s"/>' % DEFAULT_BG,
           '<rect width="100%%" height="%d" rx="10" fill="#2B2B2B"/>' % BAR,
           '<rect y="%d" width="100%%" height="10" fill="#2B2B2B"/>' % (BAR - 10)]
    for i, dot in enumerate(['#FF5F57', '#FEBC2E', '#28C840']):
        out.append('<circle cx="%d" cy="%d" r="6" fill="%s"/>' % (20 + i * 20, BAR // 2, dot))
    out.append('<text x="%.1f" y="%d" fill="#A0A0A0" text-anchor="middle">%s</text>'
               % (width / 2, BAR // 2 + 5, html.escape(title)))
    top = BAR + PAD
    cursor = None if screen.cursor.hidden or not show_cursor else (screen.cursor.y, screen.cursor.x)
    for y in range(ROWS):
        row = screen.buffer[y]
        cells = []
        for x in range(COLS):
            char = row[x]
            fg, bg = color(char.fg, DEFAULT_FG), color(char.bg, DEFAULT_BG)
            if char.reverse:
                fg, bg = bg, fg
            if cursor == (y, x):
                fg, bg = DEFAULT_BG, DEFAULT_FG
            cells.append((char.data or ' ', fg, bg, char.bold))
        # The backgrounds, in runs of the same color
        x = 0
        while x < COLS:
            bg = cells[x][2]
            end = x
            while end < COLS and cells[end][2] == bg:
                end += 1
            if bg != DEFAULT_BG:
                out.append('<rect x="%.1f" y="%d" width="%.1f" height="%d" fill="%s"/>'
                           % (PAD + x * CELL_W, top + y * CELL_H, (end - x) * CELL_W, CELL_H, bg))
            x = end
        # The text, in runs of the same color (each run placed at its column, so any font lines up)
        x = 0
        while x < COLS:
            fg, bold = cells[x][1], cells[x][3]
            end = x
            while end < COLS and cells[end][1] == fg and cells[end][3] == bold:
                end += 1
            text = ''.join(cell[0] for cell in cells[x:end])
            if text.strip():
                stripped = text.rstrip()
                out.append('<text x="%.1f" y="%.1f" fill="%s"%s textLength="%.1f" lengthAdjust="spacingAndGlyphs"'
                           ' xml:space="preserve">%s</text>'
                           % (PAD + x * CELL_W, top + y * CELL_H + CELL_H * 0.75, fg,
                              ' font-weight="bold"' if bold else '', len(stripped) * CELL_W,
                              html.escape(stripped)))
            x = end
    out.append('</svg>')
    return '\n'.join(out) + '\n'


# ---------------------------------------------------------------------------------------------------------------
# The scenes: a name, a title, the settings, the arguments of Blyant, the keys, and if the cursor is drawn

SCENES = [
    ('start-page', 'Blyant: start page', {}, [], ['<wait>'], False),
    ('file-browser', 'Blyant: file browser, two panes with line numbers', {}, [], ['f', '<wait>'], False),
    ('editor', 'Blyant: editing Markdown, a search with its count', {'colorscheme': 'Deep Blue'},
     [p('chapter-03.md')], ['<wait>', 'gg', '/harbour<Enter>', 'n'], True),
    ('split-view', 'Blyant: split view, and the commands while typing', {},
     [p('chapter-03.md')], ['<wait>', 'gg:split<Enter>', '<Tab>60Gzz', ':sp'], True),
    ('favorites', 'Blyant: favorites in colored folders, with a note and a peek', {'colorscheme': 'Tasty Bytes'},
     [], ['V', '5gg'], False),
    ('jump-menu', 'Blyant: the Jump menu, bookmarks of places in files', {},
     [], [':book<Enter>', '2gg'], False),
    ('color-schemes', 'Blyant: color schemes, the colors of the command line', {'colorscheme': 'Deep Blue'},
     [], [':scheme<Enter>', '<Tab>', 'll'], False),
    ('compare', 'Blyant: :diff, the changes against the saved file', {'colorscheme': 'Tasty Bytes'},
     [p('chapter-01.md')], ['<wait>', '3Gcc<Esc>', 'iThe ferry came late that October.<Esc>',
                            '13Go', 'He had written it on the first page of every logbook.<Esc>',
                            ':diff<Enter>'], False),
]


def main():
    parser = argparse.ArgumentParser(description='Screenshots of Blyant as SVG images')
    parser.add_argument('--exe', default=os.path.join(ROOT, 'publish', 'win-x64', 'Blyant.exe'))
    parser.add_argument('scenes', nargs='*', help='the scenes to make (all when none is given)')
    options = parser.parse_args()
    if not os.path.exists(options.exe):
        sys.exit('Blyant is not there: ' + options.exe + ' (run publish.ps1 win-x64, or give --exe)')
    make_demo()
    os.makedirs(OUT, exist_ok=True)
    work = os.path.join(tempfile.gettempdir(), 'blyant-screenshots')
    os.makedirs(work, exist_ok=True)
    for name, title, settings, args, steps, show_cursor in SCENES:
        if options.scenes and name not in options.scenes:
            continue
        make_data(work, settings)
        screen = run(options.exe, work, args, steps)
        with open(os.path.join(OUT, name + '.svg'), 'w', encoding='utf-8', newline='\n') as f:
            f.write(svg(screen, title, show_cursor))
        print(name)


if __name__ == '__main__':
    main()
