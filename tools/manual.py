"""Make the user manual of Blyant from the guides (Guide-to-*.md): a Markdown file, and with --pdf a PDF made with
Typst (https://typst.app, the typst program on the PATH or in the folder of WinGet, or set in TYPST).

    python tools/manual.py [--pdf] [--out FOLDER]

The manual is written to FOLDER (publish/manual by default): Blyant-User-Manual.md, and with --pdf
Blyant-User-Manual.typ and Blyant-User-Manual.pdf. The guides are the only source: the manual takes their parts in the
order of CHAPTERS below, without the pictures, and turns the links between them into links in the manual. Every part
of every guide must be in the manual (a heading of a guide that is not taken stops the script), so a part added to a
guide is not left out by mistake, and a part without text stops it too.

The manual speaks of chapters where the guides speak of guides: "the guide to the editor" in a link of a guide is
"the chapter on the editor" in the manual, and "this guide" is "this chapter" (as_manual, each time the manual is
made; the guides themselves are not changed).
"""
import glob
import os
import re
import shutil
import subprocess
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
WEBSITE = 'https://blyant-editor.com'
NAME = 'Blyant-User-Manual'

# The chapters, in the order of the manual. A part is (guide, heading, how, title):
#   how 'all'    the heading with its text and the parts below it
#       'intro'  the heading with its text only (the parts below it are taken one by one)
#       'body'   the text and the parts below it, without the heading (the text of the chapter itself)
#       'lead'   the text only, without the heading (the parts below it are taken one by one)
#   title        the heading in the manual (None: as in the guide)
INSTALL, START, EDITOR = 'Guide-to-installation.md', 'Guide-to-start-page.md', 'Guide-to-editor.md'
JUMP, BOARD, TYPST = 'Guide-to-jump-menu.md', 'Guide-to-corkboard.md', 'Guide-to-typst.md'
CHAPTERS = [
    ('Getting started', [
        (INSTALL, 'Installing and running Blyant', 'intro', None),
        (INSTALL, 'Download and run Blyant', 'all', None),
        (INSTALL, 'Install .NET', 'all', None),
        (INSTALL, 'Good to know', 'all', None),
        (INSTALL, 'Starting Blyant with a file', 'all', None),
        (JUMP, 'Guide to the Jump menu, color schemes and logos', 'intro', 'Keys used everywhere'),
        (START, 'Start page', 'intro', 'The start page'),
        (START, 'Start', 'all', 'The Start tab'),
        (START, 'Commands', 'all', 'Commands of the start page'),
        (START, 'Help', 'all', 'The Help tab'),
    ]),
    ('Configuration', [
        (START, 'Configuration', 'body', None),
    ]),
    ('The file browser', [
        (START, 'File browser', 'lead', None),
        (START, 'Managing files', 'all', None),
        (START, 'The trash', 'all', None),
        (START, 'Searching for files', 'all', None),
        (START, 'Where the cursor was', 'all', None),
    ]),
    ('Favorites, history and hotlist', [
        (START, 'Favorites', 'all', None),
        (START, 'File history', 'all', None),
        (START, 'Directory hotlist', 'all', None),
    ]),
    ('The editor', [
        (EDITOR, 'Guide to the editor', 'intro', 'The modes of the editor'),
        (EDITOR, 'Save and close', 'all', None),
        (EDITOR, 'Entering Insert mode', 'all', None),
        (EDITOR, 'Moving the cursor (Normal and Visual mode)', 'all', None),
        (EDITOR, 'Editing (Normal mode)', 'all', None),
        (EDITOR, 'Visual mode', 'all', None),
        (EDITOR, 'Search', 'all', None),
        (EDITOR, 'Replace', 'all', None),
        (EDITOR, 'Comparing', 'all', None),
        (EDITOR, 'Marks', 'all', None),
        (EDITOR, 'Edit history', 'all', None),
        (EDITOR, 'Line numbers', 'all', None),
        (EDITOR, 'Long lines', 'all', None),
        (EDITOR, 'Scrolling', 'all', None),
        (EDITOR, 'Zen mode', 'all', None),
        (EDITOR, 'Emoji and wide characters', 'all', None),
        (EDITOR, 'Colors', 'all', 'Colors of the text'),
        (EDITOR, 'Line endings', 'all', None),
        (EDITOR, 'Byte order mark (BOM)', 'all', None),
        (EDITOR, 'Other commands', 'all', 'Other commands of the editor'),
        (EDITOR, 'Title and other single line fields', 'all', None),
    ]),
    ('Writing tools', [
        (EDITOR, 'Completing words', 'all', None),
        (EDITOR, 'Snippets', 'all', None),
        (EDITOR, 'Macros', 'all', None),
        (EDITOR, 'Spell checking', 'all', None),
        (EDITOR, 'String memory', 'all', None),
        (EDITOR, 'System clipboard and paste bin', 'all', None),
    ]),
    ('The Jump menu and bookmarks', [
        (JUMP, 'Jump menu', 'body', None),
    ]),
    ('Projects', [
        (START, 'Projects', 'body', None),
    ]),
    ('The corkboard', [
        (BOARD, 'Guide to the corkboard', 'body', None),
    ]),
    ('Color schemes and logos', [
        (JUMP, 'Color schemes', 'all', None),
        (JUMP, 'Logos', 'all', None),
        (START, 'Logo animation', 'all', None),
    ]),
    ('Writing with Typst', [
        (TYPST, 'Guide to writing with Typst', 'body', None),
    ]),
    ('Appendix', [
        (START, 'The log', 'all', None),
        (START, 'Directories', 'all', 'The data directory'),
        (INSTALL, 'Language files', 'all', None),
        (INSTALL, 'Building from source', 'all', None),
    ]),
]

def read_logo():
    """The logo of Blyant as the program has it (LogoArt.BuiltIn), without the blank columns in front of it"""
    source = open(os.path.join(ROOT, 'Blyant', 'LogoArt.cs'), encoding='utf-8-sig').read()
    start = source.index('internal static LogoArt BuiltIn')
    part = source[start:source.index('});', start)]
    # The lines are verbatim strings: @"...", where "" is one quote
    lines = [m.group(1).replace('""', '"') for m in re.finditer(r'@"((?:[^"]|"")*)"', part)]
    if len(lines) != 4:
        sys.exit('manual.py: the logo was not found in LogoArt.cs')
    indent = min(len(line) - len(line.lstrip(' ')) for line in lines)
    return [line[indent:] for line in lines]


LOGO = read_logo()


def as_manual(line):
    """A line of a guide as the manual says it: the other guides are chapters of the manual"""
    line = re.sub(r'\b([Tt]he \[?)guide to\b', r'\1chapter on', line)
    line = re.sub(r'\[guide to\b', '[chapter on', line)
    return re.sub(r'\b([Tt])his guide\b', r'\1his chapter', line)


# ---- Reading the guides

class Heading:
    def __init__(self, guide, level, title, line):
        self.guide, self.level, self.title, self.line = guide, level, title, line
        self.anchor = None  # As GitHub makes it, for the links of the guides
        self.taken = False


def slug(title):
    """The anchor GitHub gives a heading"""
    text = title.strip().lower()
    text = re.sub(r'[^\w\- ]', '', text)
    return text.replace(' ', '-')


def read_guides():
    guides = {}
    for path in sorted(glob.glob(os.path.join(ROOT, 'Guide-to-*.md'))):
        name = os.path.basename(path)
        lines = open(path, encoding='utf-8').read().split('\n')
        headings, fenced, used = [], False, {}
        for i, line in enumerate(lines):
            if line.startswith('```'):
                fenced = not fenced
                continue
            m = re.match(r'^(#{1,6}) (.+?)\s*$', line)
            if m and not fenced:
                h = Heading(name, len(m.group(1)), m.group(2), i)
                base = slug(h.title)
                h.anchor = base if base not in used else base + '-' + str(used[base])
                used[base] = used.get(base, 0) + 1
                headings.append(h)
        guides[name] = (lines, headings)
    return guides


def find(guides, guide, title):
    lines, headings = guides[guide]
    found = [h for h in headings if h.title == title]
    if len(found) != 1:
        sys.exit('manual.py: %s has %d headings "%s" (the guide has changed: CHAPTERS must follow it)'
                 % (guide, len(found), title))
    return found[0]


def end_of(guides, h, intro):
    """The line after the text of a heading: at the next heading (intro), or the next of its level or above"""
    lines, headings = guides[h.guide]
    for other in headings:
        if other.line > h.line and (intro or other.level <= h.level):
            return other.line
    return len(lines)


# ---- The manual: sections with their Markdown text

class Section:
    def __init__(self, level, title, sources, body):
        self.level, self.title, self.sources, self.body = level, title, sources, body
        self.number = ''
        self.id = ''


def build(guides):
    sections = []
    for chapter_title, parts in CHAPTERS:
        chapter = Section(1, chapter_title, [], [])
        sections.append(chapter)
        for guide, title, how, new_title in parts:
            h = find(guides, guide, title)
            lines, headings = guides[guide]
            if h.taken:
                sys.exit('manual.py: %s "%s" is taken twice' % (guide, title))
            stop = end_of(guides, h, how in ('intro', 'lead'))
            inside = [o for o in headings if h.line < o.line < stop]
            if how in ('body', 'lead'):
                # The text under the heading is the text of the chapter, and its parts are the sections
                h.taken = True
                chapter.sources.append((guide, h.anchor))
                first = inside[0].line if inside else stop
                chapter.body += lines[h.line + 1:first]
                shift = 2 - (inside[0].level if inside else h.level + 1)
            else:
                h.taken = True
                sections.append(Section(2, new_title or h.title, [(guide, h.anchor)],
                                        lines[h.line + 1:(inside[0].line if inside else stop)]))
                shift = 2 - h.level
            for i, o in enumerate(inside):
                if o.taken:
                    sys.exit('manual.py: %s "%s" is taken twice' % (guide, o.title))
                o.taken = True
                end = inside[i + 1].line if i + 1 < len(inside) else stop
                sections.append(Section(max(2, min(6, o.level + shift)), o.title, [(o.guide, o.anchor)],
                                        lines[o.line + 1:end]))
    # Every part of every guide is in the manual (not the titles of the guides that are not taken)
    missing = [h for _, hs in guides.values() for h in hs if not h.taken and h.level > 1]
    if missing:
        sys.exit('manual.py: not in the manual: ' + ', '.join('%s "%s"' % (h.guide, h.title) for h in missing))
    # Numbers: 1, 1.1, 1.1.1 ...
    counters = [0] * 7
    for s in sections:
        counters[s.level] += 1
        for deeper in range(s.level + 1, 7):
            counters[deeper] = 0
        s.number = '.'.join(str(counters[i]) for i in range(1, s.level + 1))
        s.id = 'sec-' + s.number.replace('.', '-')
    for s in sections:
        s.body = clean(s.body)
        # A heading right on top of another, as the title of a guide without text of its own (a chapter may be
        # only its sections)
        if s.level > 1 and not s.body:
            sys.exit('manual.py: %s "%s" has no text (take its parts instead, in CHAPTERS)' % (s.number, s.title))
    return sections


def clean(lines):
    """Without the pictures, and without blank lines at the start and the end. The text outside the blocks of code
    speaks of chapters, not guides"""
    kept, fenced = [], False
    for line in lines:
        if line.startswith('```'):
            fenced = not fenced
        elif not fenced:
            if re.match(r'^\s*!\[[^\]]*\]\([^)]*\)\s*$', line):
                continue
            line = as_manual(line)
        kept.append(line)
    while kept and not kept[0].strip():
        kept.pop(0)
    while kept and not kept[-1].strip():
        kept.pop()
    return kept


def link_targets(guides, sections):
    """(guide, anchor of a heading) -> the section of the manual it is in"""
    targets = {}
    for s in sections:
        for guide, anchor in s.sources:
            targets[(guide, anchor)] = s
    return targets


LINK = re.compile(r'\[((?:[^\[\]]|\[[^\]]*\])*)\]\(([^)\s]*)\)')


def resolve(target, guide, targets):
    """A link of a guide: ('web', url), ('section', section), or None (to a file not in the manual)"""
    if re.match(r'https?://', target):
        return ('web', target)
    file, _, anchor = target.partition('#')
    file = file or guide
    if not file.startswith('Guide-to-'):
        return None
    if not anchor:
        # A whole guide: its first section in the manual
        first = [s for (g, _), s in targets.items() if g == file]
        return ('section', min(first, key=lambda s: [int(n) for n in s.number.split('.')])) if first else None
    s = targets.get((file, anchor))
    if s is None:
        sys.exit('manual.py: a link to "%s" in %s goes to no part of the manual' % (target, guide))
    return ('section', s)


# ---- Markdown

def md_anchor(s):
    return slug(s.number + ' ' + s.title)


def to_markdown(sections, targets, version):
    out = ['```text'] + LOGO + ['```', '', '# User Manual', '',
                                '**Blyant %s**  ' % version, WEBSITE, '', '## Contents', '']
    for s in sections:
        if s.level <= 2:
            indent = '' if s.level == 1 else '   '
            out.append('%s- [%s %s](#%s)' % (indent, s.number, s.title, md_anchor(s)))
    out.append('')
    for s in sections:
        out += ['', '#' * s.level + ' ' + s.number + ' ' + s.title, '']
        guide = s.sources[0][0] if s.sources else None
        fenced = False
        for line in s.body:
            if line.startswith('```'):
                fenced = not fenced
            if not fenced:
                line = LINK.sub(lambda m: md_link(m, guide, targets), line)
            out.append(line)
    return '\n'.join(out).strip() + '\n'


def md_link(m, guide, targets):
    text, target = m.group(1), m.group(2)
    kind = resolve(target, guide, targets)
    if kind is None:
        return text
    if kind[0] == 'web':
        return m.group(0)
    return '[%s](#%s)' % (text, md_anchor(kind[1]))


# ---- Typst

SPECIAL = set('\\#$*_`<>@[]~/=+-"\'')


def escape(text):
    """Plain text in Typst markup: every character with a meaning there escaped"""
    out = []
    for c in text:
        out.append('\\' + c if c in SPECIAL else c)
    return ''.join(out)


def typst_string(text):
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'


INLINE = re.compile(
    r'(?P<code>(`+)(?P<codetext>.+?)\2(?!`))'
    r'|(?P<link>\[(?P<linktext>(?:[^\[\]]|\[[^\]]*\])*)\]\((?P<linktarget>[^)\s]*)\))'
    r'|(?P<url>https?://[^\s)\]>,]+[^\s)\]>,.;:])'
    r'|(?P<bold>\*\*(?P<boldtext>.+?)\*\*)'
    r'|(?P<em1>(?<![\w*])\*(?P<em1text>[^*\s](?:[^*]*[^*\s])?)\*(?![\w*]))'
    r'|(?P<em2>(?<![\w])_(?P<em2text>[^_\s](?:[^_]*[^_\s])?)_(?!\w))'
    r'|(?P<esc>\\(?P<esctext>[\\`*_{}\[\]()#+\-.!|<>]))')


def inline(text, guide, targets):
    """A line of Markdown text as Typst markup"""
    out, pos = [], 0
    for m in INLINE.finditer(text):
        out.append(escape(text[pos:m.start()]))
        pos = m.end()
        if m.group('code') is not None:
            code = m.group('codetext')
            if len(m.group(2)) > 1:
                code = code.strip()
            out.append('#raw(' + typst_string(code) + ');')
        elif m.group('link') is not None:
            label, target = m.group('linktext'), m.group('linktarget')
            kind = resolve(target, guide, targets)
            body = inline(label, guide, targets)
            if kind is None:
                out.append(body)
            elif kind[0] == 'web':
                out.append('#link(' + typst_string(kind[1]) + ')[' + body + ']')
            else:
                out.append('#see(<' + kind[1].id + '>)[' + body + ']')
        elif m.group('url') is not None:
            out.append('#link(' + typst_string(m.group('url')) + ')')
        elif m.group('bold') is not None:
            out.append('#strong[' + inline(m.group('boldtext'), guide, targets) + ']')
        elif m.group('em1') is not None:
            out.append('#emph[' + inline(m.group('em1text'), guide, targets) + ']')
        elif m.group('em2') is not None:
            out.append('#emph[' + inline(m.group('em2text'), guide, targets) + ']')
        else:
            out.append(escape(m.group('esctext')))
    out.append(escape(text[pos:]))
    return ''.join(out)


def table_cells(line):
    line = line.strip()
    if line.startswith('|'):
        line = line[1:]
    if line.endswith('|') and not line.endswith('\\|'):
        line = line[:-1]
    cells, cell, i = [], '', 0
    while i < len(line):
        if line[i] == '\\' and i + 1 < len(line) and line[i + 1] == '|':
            cell += '|'
            i += 2
            continue
        if line[i] == '`':
            # A pipe in code is not a column
            end = line.find('`', i + 1)
            if end > 0:
                cell += line[i:end + 1]
                i = end + 1
                continue
        if line[i] == '|':
            cells.append(cell.strip())
            cell = ''
        else:
            cell += line[i]
        i += 1
    cells.append(cell.strip())
    return cells


def block(lines, guide, targets):
    """The text of a section (Markdown) as Typst markup"""
    out, i = [], 0
    while i < len(lines):
        line = lines[i]
        if line.startswith('```'):
            lang = line[3:].strip()
            j = i + 1
            while j < len(lines) and not lines[j].startswith('```'):
                j += 1
            code = '\n'.join(lines[i + 1:j])
            out.append('#raw(block: true, lang: ' + (typst_string(lang) if lang else 'none') + ', ' +
                       typst_string(code) + ')')
            i = j + 1
            continue
        if line.lstrip().startswith('|') and i + 1 < len(lines) and re.match(r'^\s*\|?\s*:?-{3,}', lines[i + 1]):
            header = table_cells(line)
            rows = []
            j = i + 2
            while j < len(lines) and lines[j].lstrip().startswith('|'):
                rows.append(table_cells(lines[j]))
                j += 1
            cells = ['[#strong[' + inline(c, guide, targets) + ']]' for c in header]
            for row in rows:
                row = (row + [''] * len(header))[:len(header)]
                cells += ['[' + inline(c, guide, targets) + ']' for c in row]
            out.append('#table(columns: %d, %s)' % (len(header), ', '.join(cells)))
            i = j
            continue
        m = re.match(r'^(\s*)([-*]|\d+\.) (.*)$', line)
        if m:
            indent = len(m.group(1).expandtabs(4)) // 2 * 2
            marker = '-' if m.group(2) in '-*' else '+'
            out.append(' ' * indent + marker + ' ' + inline(m.group(3), guide, targets))
            i += 1
            continue
        if not line.strip():
            out.append('')
        else:
            # A paragraph, or the lines that go on a list item (indented as they are)
            indent = len(line) - len(line.lstrip())
            out.append(' ' * indent + inline(line.strip(), guide, targets))
        i += 1
    return '\n'.join(out)


TYPST_HEAD = r'''#set document(title: "Blyant User Manual")
#set page(paper: "a4", margin: (x: 2.2cm, top: 2.4cm, bottom: 2.6cm))
#set text(size: 10.5pt, lang: "en")
#set par(justify: true, leading: 0.62em)
#set heading(numbering: "1.1")
#show raw: set text(size: 9pt)
#show raw.where(block: true): it => block(fill: luma(245), inset: 7pt, radius: 3pt, width: 100%, it)
#show heading.where(level: 1): it => { pagebreak(weak: true); v(2.2cm); text(size: 22pt, it); v(0.8cm) }
#show heading.where(level: 2): set text(size: 14pt)
#show link: set text(fill: rgb("#1d4f91"))
#set table(stroke: 0.5pt + luma(180), inset: 5pt)
// A link to a part of the manual, with the page it is on
#let see(target, body) = [#link(target, body) (p.~#context counter(page).at(target).first())]

// The cover
#page(numbering: none, align(center + horizon)[
  #box(align(left, stack(spacing: 7pt, ..LOGO.map(line => raw(line)))))
  #v(1.5cm)
  #text(size: 34pt, weight: "bold")[User Manual]
  #v(0.6cm)
  #text(size: 16pt)[Blyant VERSION]
  #v(0.3cm)
  #link("WEBSITE")[WEBSITE_TEXT]
])

// The contents, on pages numbered i, ii ...
#set page(numbering: "i")
#counter(page).update(1)
#outline(title: [Contents], depth: 2, indent: auto)

// The chapters, from page 1
#pagebreak()
#set page(numbering: "1")
#counter(page).update(1)
'''


def to_typst(sections, targets, version):
    head = TYPST_HEAD.replace('LOGO', '(' + ', '.join(typst_string(line) for line in LOGO) + ',)')
    head = head.replace('VERSION', escape(version))
    head = head.replace('WEBSITE_TEXT', escape(WEBSITE.split('//')[1])).replace('WEBSITE', WEBSITE)
    out = [head]
    for s in sections:
        guide = s.sources[0][0] if s.sources else None
        out += ['', '=' * s.level + ' ' + escape(s.title) + ' <' + s.id + '>', '']
        out.append(block(s.body, guide, targets))
    return '\n'.join(out) + '\n'


# ---- Running

def version_of():
    project = open(os.path.join(ROOT, 'Blyant', 'Blyant.csproj'), encoding='utf-8').read()
    m = re.search(r'<Version>([^<]+)</Version>', project)
    return m.group(1) if m else '?'


def typst_program():
    if os.environ.get('TYPST'):
        return os.environ['TYPST']
    found = shutil.which('typst')
    if found:
        return found
    winget = os.path.join(os.environ.get('LOCALAPPDATA', ''), 'Microsoft', 'WinGet', 'Packages')
    for path in glob.glob(os.path.join(winget, 'Typst.Typst*', '*', 'typst.exe')):
        return path
    return None


def main():
    args = sys.argv[1:]
    pdf = '--pdf' in args
    out_dir = os.path.join(ROOT, 'publish', 'manual')
    if '--out' in args:
        out_dir = os.path.abspath(args[args.index('--out') + 1])
    os.makedirs(out_dir, exist_ok=True)
    version = version_of()
    guides = read_guides()
    sections = build(guides)
    targets = link_targets(guides, sections)
    md_path = os.path.join(out_dir, NAME + '.md')
    with open(md_path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(to_markdown(sections, targets, version))
    print('Written:', md_path)
    if not pdf:
        return
    typ_path = os.path.join(out_dir, NAME + '.typ')
    with open(typ_path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(to_typst(sections, targets, version))
    program = typst_program()
    if program is None:
        sys.exit('manual.py: typst was not found (install it, or set TYPST to the program)')
    pdf_path = os.path.join(out_dir, NAME + '.pdf')
    result = subprocess.run([program, 'compile', typ_path, pdf_path], capture_output=True, text=True)
    if result.returncode != 0:
        sys.exit('manual.py: typst failed:\n' + result.stderr)
    print('Written:', pdf_path)


if __name__ == '__main__':
    main()
