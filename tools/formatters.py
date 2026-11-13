"""Copy the formatters of Blyant Validator into Blyant (Blyant/Format), for ":format" in the editor.

Blyant Validator (https://github.com/artstisen/blyant-validator) formats HTML, CSS and XML. Its formatters, and the
parts of it they need (the CSS tokenizer and parser, the HTML reader, the XML validator), are copied as they are: only the namespaces
change (Blyant.Validator.Css becomes Blyant.Format.Css ...), and each file gets a line telling where it came from.
Their tests are copied too (Blyant.Tests/Format). The files are not changed by hand in Blyant: change them in
Blyant Validator, and run this again.

Usage:  python tools/formatters.py [path of Blyant Validator]   (default: ../_WEBCODER-VALIDATOR next to Blyant)
        python tools/formatters.py --check [path]               (tells whether the copies are the same, writes nothing)
"""
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
TARGET = os.path.join(ROOT, 'Blyant', 'Format')

# The files copied, from src/ of Blyant Validator, and where they go in Blyant/Format
FILES = [
    ('Blyant.Validator.Core/Abstractions.cs', 'Core/Abstractions.cs'),
    ('Blyant.Validator.Core/Diagnostic.cs', 'Core/Diagnostic.cs'),
    ('Blyant.Validator.Core/LineMap.cs', 'Core/LineMap.cs'),
    ('Blyant.Validator.Css/CssFormatter.cs', 'Css/CssFormatter.cs'),
    ('Blyant.Validator.Css/Syntax/CssParser.cs', 'Css/Syntax/CssParser.cs'),
    ('Blyant.Validator.Css/Syntax/CssToken.cs', 'Css/Syntax/CssToken.cs'),
    ('Blyant.Validator.Css/Syntax/CssTokenizer.cs', 'Css/Syntax/CssTokenizer.cs'),
    ('Blyant.Validator.Html/HtmlFormatter.cs', 'Html/HtmlFormatter.cs'),
    ('Blyant.Validator.Html/Parsing/HtmlReader.cs', 'Html/Parsing/HtmlReader.cs'),
    ('Blyant.Validator.Html/Parsing/HtmlTree.cs', 'Html/Parsing/HtmlTree.cs'),
    ('Blyant.Validator.Xml/XmlFormatter.cs', 'Xml/XmlFormatter.cs'),
    # (the XML formatter first checks that the file is well-formed, with the validator of XML, which also knows
    # sitemaps and feeds)
    ('Blyant.Validator.Xml/XmlValidator.cs', 'Xml/XmlValidator.cs'),
    ('Blyant.Validator.Xml/Sitemap.cs', 'Xml/Sitemap.cs'),
    ('Blyant.Validator.Xml/Feeds.cs', 'Xml/Feeds.cs'),
]

# Their tests, from tests/Blyant.Validator.Tests, and where they go in Blyant.Tests/Format: the formatters are tested
# in Blyant with the same examples
TESTS = [
    ('CssFormatterTests.cs', 'CssFormatterTests.cs'),
    ('HtmlFormatterTests.cs', 'HtmlFormatterTests.cs'),
    ('XmlFormatterTests.cs', 'XmlFormatterTests.cs'),
]
TESTS_TARGET = os.path.join(ROOT, 'Blyant.Tests', 'Format')


def version_of(validator):
    with open(os.path.join(validator, 'Directory.Build.props'), encoding='utf-8-sig') as f:
        m = re.search(r'<Version>([^<]+)</Version>', f.read())
    if not m:
        sys.exit('formatters.py: the version of Blyant Validator was not found')
    return m.group(1)


def converted(text, source, version):
    """The file as it is in Blyant: other namespaces, a line telling where it came from, UTF-8 with BOM and CRLF"""
    text = text.lstrip('﻿').replace('\r\n', '\n')
    text = re.sub(r'\bBlyant\.Validator\.', 'Blyant.Format.', text)
    head = ('// Copied from Blyant Validator %s (%s) by tools/formatters.py: not changed by hand in Blyant.\n'
            '// Change it in Blyant Validator, and copy it again. English: its messages are not translated\n'
            % (version, source))
    return ('﻿' + head + text).replace('\n', '\r\n').encode('utf-8')


def main():
    args = sys.argv[1:]
    check = '--check' in args
    args = [a for a in args if a != '--check']
    validator = os.path.normpath(args[0] if args else os.path.join(ROOT, '..', '_WEBCODER-VALIDATOR'))
    if not os.path.isdir(os.path.join(validator, 'src')):
        sys.exit('formatters.py: Blyant Validator is not in %s' % validator)
    version = version_of(validator)
    differ = []
    files = ([(os.path.join('src', source), os.path.join(TARGET, target)) for source, target in FILES] +
             [(os.path.join('tests', 'Blyant.Validator.Tests', source), os.path.join(TESTS_TARGET, target))
              for source, target in TESTS])
    for source, path in files:
        with open(os.path.join(validator, source), encoding='utf-8') as f:
            data = converted(f.read(), source.replace(os.sep, '/'), version)
        old = open(path, 'rb').read() if os.path.exists(path) else None
        if old == data:
            continue
        differ.append(os.path.relpath(path, ROOT))
        if not check:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, 'wb') as f:
                f.write(data)
    if check:
        print('Blyant Validator %s: %s' % (version, ('differs: ' + ', '.join(differ)) if differ else 'the same'))
        sys.exit(1 if differ else 0)
    print('Blyant Validator %s: %d of %d files copied' % (version, len(differ), len(files)))


if __name__ == '__main__':
    main()
