namespace Blyant;

/// <summary>
/// "clip": a path, a name, the text of a file, some of its lines or words of a line, or the value of a register, to
/// the clipboard
/// (OSC 52) and/or the paste bin, as set on the clipboard page of Configuration, and to the string memory, so "p" in
/// the editor puts it in (after "open" or "screen", which run in the same Blyant), and to $clip, which the commands
/// of the shell take as a register. What is copied is never logged
/// </summary>
internal sealed partial class ShellSession
{
    // "clip 3", "clip path 3": the full path of row 3 of the last list (or of a file or directory typed; "." the one
    // the shell is in); "clip name 3": its name; "clip text 3": the text of the file; "clip lines 3 4-6": lines of it,
    // as "lines" and "shift" take them ("4", "4-6", "4,7,9-10", "+100 20", a text in the line); "clip words 3 12
    // 6-9": words of line 12, by the numbers words shows ("-p": all between blanks); "clip $note": the value of the
    // register, as it is
    private void Clip(string argument)
    {
        var words = Words(argument);
        if (words.Count == 0)
        {
            Error(Lang.T("Give what to copy: clip 3, clip name 3, clip text 3, clip lines 3 +10 5"));
            return;
        }

        var kind = words[0].ToLowerInvariant();
        string text;
        string shown;
        if (words.Count == 1 && IsClipOfRegister("clip " + argument))
        {
            kind = "register";
            text = ShellRegisters.Get(words[0][1..]) ?? string.Empty;
            shown = "$" + words[0][1..].ToLowerInvariant();
        }
        else
        {
            if (kind is not ("path" or "name" or "text" or "lines" or "words")) kind = "path";
            else words.RemoveAt(0);
            if (kind == "words" ? !ClipWords(words, out text, out shown) : !ClipText(kind, words, out text, out shown))
                return;
        }

        CopyClip(kind, text, shown);
    }

    // "path", "name" (as :path and :name in the editor): the full path, or the name, of the file worked on last ($file)
    // copied; "path 3", "name plot.md": of that file or directory, as "clip path 3" and "clip name 3"
    private void ClipPathOrName(string kind, string argument)
    {
        var words = Words(argument);
        if (words.Count == 0)
        {
            if (_file == null)
            {
                Error(Lang.T("No file yet: open, peek, insert, new ... give $file"));
                return;
            }

            // (the path and name are known: copied also when the file was moved or deleted since, as $file shows it)
            var known = kind == "path" ? _file : Path.GetFileName(_file);
            CopyClip(kind, known, known);
            return;
        }

        if (words.Count > 1)
        {
            Error(Lang.F("Give one file or directory: {0} 3", kind));
            return;
        }

        if (!ClipText(kind, words, out var text, out var shown)) return;
        CopyClip(kind, text, shown);
    }

    // A text copied: to $clip, the clipboard (through the terminal, and/or the paste bin, as set) and the string memory
    // of the editor, and told
    private void CopyClip(string kind, string text, string shown)
    {
        if (text.Length == 0)
        {
            Error(Lang.T("Nothing to copy: it is empty"));
            return;
        }

        // $clip: kept in Blyant, also without a terminal (a script: "clip lines notes.md 4-6", then "insert plan.md
        // bottom $clip")
        // (the text of a file without the line break that ends its last line: one line is one line, and a blank
        // line at the end is a line)
        ShellRegisters.Clip = kind == "text" && text.EndsWith('\n') ? text[..^1] : text;
        // English: the log (what kind, never the text)
        EventLog.Add(DialogText.LogShell + ": clip: " + kind);
        // Without a terminal (the output to a file or a program) the clipboard sequence would end up in the output:
        // $clip only, and the paste bin if it is turned on
        if (!Clipboard && !SystemClipboard.PasteBin)
        {
            Line(Lang.T("Kept for $clip (the clipboard needs a terminal)"));
            Line("  " + shown);
            return;
        }

        var (message, isError) = SystemClipboard.CopyWithMemory(text, Clipboard);
        if (isError)
        {
            Error(message);
            return;
        }

        Line(message);
        Line("  " + shown);
    }

    // The text of the kind asked for, and what is told of it (the path or name copied, or the lines). False when it
    // cannot be had (told)
    private bool ClipText(string kind, List<string> words, out string text, out string shown)
    {
        text = shown = string.Empty;
        var lines = kind == "lines";
        if (words.Count == 0 || (!lines && words.Count > 1) || (lines && (words.Count is < 2 or > 3 ||
                                                                         (words.Count == 3 && !words[1].StartsWith('+')))))
        {
            Error(lines
                ? Lang.T("Give a file and the lines: clip lines 3 40, clip lines 3 4-6, clip lines 3 +100 30")
                : Lang.F("Give one file or directory: clip {0} 3", kind));
            return false;
        }

        if (Existing(words[0]) is not { } item) return false;
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(item.Path));
        if (kind is "path" or "name")
        {
            text = kind == "path" ? path : Path.GetFileName(path);
            // (a root, as "C:\" or "/", has no name: its path)
            if (text.Length == 0) text = path;
            shown = text;
            return true;
        }

        if (ReadableFile(item) is not { } file) return false;
        var all = TextBuffer.Normalize(file.Text);
        if (!lines)
        {
            text = all;
            shown = Lang.F("{0} ({1} characters)", path, all.Length);
            return true;
        }

        // The lines as "lines" and "shift" take them: "40" is line 40 (peek shows the first 40, as it only shows them).
        // (the last line break does not start a line)
        var rows = LinesOfText(all);
        if (PickedLines(words[1], words.Count == 3 ? words[2] : null, rows, item.Path) is not { } picked) return false;
        text = string.Join("\n", picked.Select(number => rows[number - 1]));
        shown = picked.Count == 1
            ? Lang.F("Line {0} of {1}", picked[0], path)
            : picked[^1] - picked[0] == picked.Count - 1
                ? Lang.F("Lines {0}-{1} of {2}", picked[0], picked[^1], path)
                : Lang.F("Lines {0} of {1}", Numbers(picked), path);
        return true;
    }

    // "clip words notes.md 12 6-9" ("-p" first: a word is all between blanks): the words of the line by their numbers,
    // as words shows them ("#6", "#6:19", "6-9"), from the start of the first to the end of the last, as they are in
    // the line, what is between them too ("text, that I want"). The file is only read: $file, $line and the last list
    // stay as they were. False when the words cannot be had (told)
    private bool ClipWords(List<string> words, out string text, out string shown)
    {
        text = shown = string.Empty;
        if (BlanksOption(words) is not { } blanks) return false;
        var usage = Lang.T("Give a file, a line and the words: clip words notes.md 12 6-9");
        if (words.Count != 3)
        {
            Error(usage);
            return false;
        }

        if (!LinesToChange(WithoutHashes(words[2]), out var first, out var last))
        {
            Error(Lang.F("Not words: {0} (as 9, or 5-7)", words[2]));
            return false;
        }

        if (!LinesToChange(words[1], out var number, out var lastLine) || lastLine != number)
        {
            Error(usage);
            return false;
        }

        if (Existing(words[0]) is not { } item || ReadableFile(item) is not { } file) return false;
        var lines = LinesOfText(TextBuffer.Normalize(file.Text));
        if (number > lines.Count)
        {
            Error(Lang.F("{0} has {1} lines", item.Path, lines.Count));
            return false;
        }

        var line = lines[number - 1];
        var found = LineWords.Find(line, blanks);
        if (found.Count == 0)
        {
            Error(Lang.F("Line {0} has no words", number));
            return false;
        }

        if (last > found.Count)
        {
            Error(found.Count == 1
                ? Lang.F("Line {0} has 1 word: {1} is not one", number, last)
                : Lang.F("Line {0} has {1} words: {2} is not one", number, found.Count, last));
            return false;
        }

        var start = found[first - 1].Start;
        text = line[start..(found[last - 1].Start + found[last - 1].Length)];
        var path = Path.GetFullPath(item.Path);
        shown = first == last
            ? Lang.F("Word {0} of line {1} of {2}", first, number, path)
            : Lang.F("Words {0}-{1} of line {2} of {3}", first, last, number, path);
        return true;
    }

    // "clip $note": the line is "clip" and one $name of a register (its value is copied as it is)
    private static bool IsClipOfRegister(string line)
    {
        var words = Words(line);
        return words.Count == 2 && words[0].Equals("clip", StringComparison.OrdinalIgnoreCase) &&
               words[1] is ['$', .. var name] && ShellRegisters.IsName(name) && ShellRegisters.Get(name) != null;
    }
}
