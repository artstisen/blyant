using Blyant.Format.Core;
using Blyant.Format.Css;
using Blyant.Format.Html;
using Blyant.Format.Xml;

namespace Blyant;

/// <summary>
/// ":format": the text of an HTML, CSS or XML file formatted as Blyant Validator formats it (its formatters are copied
/// into Blyant/Format by tools/formatters.py). Only blank space changes: the indents, and where the lines break. The
/// kind of text is that of the file, told by its name as Blyant Validator tells it (FormatKind), and the whole text is
/// formatted, as one change that "u" undoes (the lines that differ are replaced). A text
/// that cannot be formatted safely (a block that is never closed, XML that is not well-formed) is left as it is, and
/// the formatter tells why, with the line
/// </summary>
internal sealed partial class EditorEngine
{
    /// <summary>
    /// The kind of the file edited, as Blyant Validator tells it by the name (.html .htm: HTML, .css: CSS, .xml .svg
    /// .rss .atom .xhtml: XML), for ":format". Unknown: a text that is not formatted (a field, a peek, a Markdown file)
    /// </summary>
    internal DocumentKind FormatKind { get; set; }

    private void FormatText()
    {
        if (SingleLine) return;
        var kind = FormatKind;
        IFormatter? formatter = kind switch
        {
            DocumentKind.Html => new HtmlFormatter(),
            DocumentKind.Css => new CssFormatter(),
            DocumentKind.Xml => new XmlFormatter(),
            _ => null
        };
        if (formatter == null)
        {
            SetError(Lang.T("Only HTML, CSS and XML files can be formatted"));
            return;
        }

        if (Buffer.RefuseIfReadOnly()) return;
        var text = Buffer.GetText();
        string formatted;
        try
        {
            // The lines of the editor end in "\n" (the line endings of the file are kept when it is saved), and the
            // indent is that of Blyant Validator, so a file is formatted alike in both
            formatted = TextBuffer.Normalize(formatter.Format(new SourceDocument(string.Empty, text, kind),
                new FormatOptions { NewLine = "\n" }));
        }
        catch (Format.Core.FormatException e)
        {
            // (the message of the formatter, in English, with the line: ":42" goes there)
            SetError(Lang.F("Not formatted: {0}", e.Message));
            return;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A fault of the formatter: the text is left as it is, and the fault is logged (never the text)
            EventLog.Add(DialogText.LogEditorCommand + ": format failed: " + e.GetType().Name + ": " + e.Message);
            SetError(Lang.F("Not formatted: {0}", e.Message));
            return;
        }

        if (formatted == text)
        {
            Message = Lang.T("Already formatted");
            return;
        }

        var line = Cursor.Line;
        ReplaceChanged(text, formatted);
        // The cursor stays on its line (or the last), at its first character
        line = Math.Min(line, Buffer.LineCount - 1);
        Cursor = new TextPos(line, FirstNonBlank(line));
        ClampCursor();
        Message = Lang.T("Formatted (u undoes it)");
    }

    /// <summary>
    /// Change the text into another by replacing only the lines that differ (the lines that are the same at the start
    /// and the end are left, so marks and bookmarks there stay where they are). Whole lines, so no character is cut in
    /// two. One change: one step of undo
    /// </summary>
    private void ReplaceChanged(string text, string replacement)
    {
        var start = 0;
        var most = Math.Min(text.Length, replacement.Length);
        while (start < most && text[start] == replacement[start]) start++;
        var end = 0;
        while (end < most - start && text[^(end + 1)] == replacement[^(end + 1)]) end++;
        // From the start of the line of the first difference, to the start of a line after the last (the same in both)
        start = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        while (end > 0 && text[^(end + 1)] != '\n') end--;
        // (through the step of the command: the part taken out and the part put in are undone together)
        var from = PositionOf(text, start);
        Delete(from, PositionOf(text, text.Length - end));
        Insert(from, replacement.Substring(start, replacement.Length - start - end));
    }

    // The line and column of an offset in the text of the buffer (lines end in "\n")
    private static TextPos PositionOf(string text, int offset)
    {
        var line = 0;
        var lineStart = 0;
        for (var i = 0; i < offset; i++)
        {
            if (text[i] != '\n') continue;
            line++;
            lineStart = i + 1;
        }

        return new TextPos(line, offset - lineStart);
    }
}
