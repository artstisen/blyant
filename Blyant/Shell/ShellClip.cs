namespace Blyant;

/// <summary>
/// "clip": a path, a name, the text of a file or some of its lines, or the value of a register, to the clipboard
/// (OSC 52) and/or the paste bin, as set on the clipboard page of Configuration, and to the string memory, so "p" in
/// the editor puts it in (after "open" or "screen", which run in the same Blyant). What is copied is never logged
/// </summary>
internal sealed partial class ShellSession
{
    // "clip 3", "clip path 3": the full path of row 3 of the last list (or of a file or directory typed; "." the one
    // the shell is in); "clip name 3": its name; "clip text 3": the text of the file; "clip lines 3 +100 20": lines of
    // it, as peek takes them; "clip $note": the value of the register, as it is
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
            if (kind is not ("path" or "name" or "text" or "lines")) kind = "path";
            else words.RemoveAt(0);
            if (!ClipText(kind, words, out text, out shown)) return;
        }

        if (text.Length == 0)
        {
            Error(Lang.T("Nothing to copy: it is empty"));
            return;
        }

        // Without a terminal (the output to a file or a program) the paste bin only: the string memory would end
        // with Blyant
        if (!Clipboard && !SystemClipboard.PasteBin)
        {
            Error(Lang.T("The clipboard needs a terminal (the output goes to a file or a program): turn on the paste bin"));
            return;
        }

        var (message, isError) = SystemClipboard.CopyWithMemory(text, Clipboard);
        // English: the log (what kind, never the text)
        EventLog.Add(DialogText.LogShell + ": clip: " + kind);
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
                ? Lang.T("Give a file and the lines: clip lines 3 40, clip lines 3 100-120, clip lines 3 +100 30")
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

        if (item.IsDirectory)
        {
            Error(Lang.F("Not a file: {0}", item.Path));
            return false;
        }

        var (first, last) = (1, int.MaxValue);
        if (lines && !LinesOf(words[1], words.Count == 3 ? words[2] : null, out first, out last))
        {
            Error(Lang.F("Not lines: {0} (as 40, 100-120, or +100 30)", string.Join(' ', words.Skip(1))));
            return false;
        }

        string all;
        try
        {
            all = TextBuffer.Normalize(TextFile.Load(item.Path).Text);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
            return false;
        }

        if (!lines)
        {
            text = all;
            shown = Lang.F("{0} ({1} characters)", path, all.Length);
            return true;
        }

        // (the last line break does not start a line)
        var rows = LinesOfText(all);
        var count = rows.Count;
        last = Math.Min(last, count);
        if (first > last)
        {
            Error(Lang.F("{0} has {1} lines", item.Path, count));
            return false;
        }

        text = string.Join("\n", rows.GetRange(first - 1, last - first + 1));
        shown = Lang.F("Lines {0}-{1} of {2}", first, last, path);
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
