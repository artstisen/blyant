using System.Globalization;

namespace Blyant;

/// <summary>
/// The commands of the shell that work on files: new, mkdir, rename, copy, move, delete, peek and find, and adding to
/// and taking off the favorites and the hotlist. They keep the rules and the lists of the file browser
/// (FileExplorer: ProtectionOf, ConfirmDeleteOf, PathMoved ...), and every file operation is logged as there
/// </summary>
internal sealed partial class ShellSession
{
    /// <summary>
    /// The words of what follows a command: separated by blanks, or between double quotes ("my notes.md")
    /// </summary>
    internal static List<string> Words(string text)
    {
        var words = new List<string>();
        var word = new System.Text.StringBuilder();
        var quoted = false;
        var started = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
                started = true;
                continue;
            }

            if (char.IsWhiteSpace(c) && !quoted)
            {
                if (started) words.Add(word.ToString());
                word.Clear();
                started = false;
                continue;
            }

            word.Append(c);
            started = true;
        }

        if (started) words.Add(word.ToString());
        return words;
    }

    // The file or directory of a word (a row of the last list, or a path), which must be there: null (and told) if not
    private Item? Existing(string word)
    {
        var item = Resolve(word, false);
        if (item == null) return null;
        var isDirectory = System.IO.Directory.Exists(item.Path);
        if (isDirectory || File.Exists(item.Path)) return item with { IsDirectory = isDirectory };
        Error(Lang.F("Not found: {0}", item.Path));
        return null;
    }

    // Told and false when Blyant uses the file or directory (its settings, its lists)
    private bool Protected(Item item)
    {
        if (FileExplorer.ProtectionOf(item.Path, item.IsDirectory) is not { } why) return false;
        Error(Lang.F("{0}: {1}", why, item.Path));
        return true;
    }

    // What a file operation came to: shown when it was done (the new place as the last list: "open 1"), else told
    private void Done(string result, string message, string? path)
    {
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

        Line(message);
        if (path != null) LastList = new List<Item> { new(path, System.IO.Directory.Exists(path)) };
    }

    // "new notes": a new, empty file in the directory you are in (without an extension: that of new files, .md)
    private void NewFile(string argument)
    {
        var words = Words(argument);
        if (words.Count != 1)
        {
            Error(Lang.T("Give one name: new notes.md"));
            return;
        }

        var name = FileOperations.WithDefaultExtension(words[0].Trim());
        if (Tools.FileNameProblem(name) is { } problem)
        {
            Error(problem);
            return;
        }

        var path = Path.Combine(Directory, name);
        if (File.Exists(path) || System.IO.Directory.Exists(path))
        {
            Error(Lang.F("{0}: {1}", DialogText.NameExists, path));
            return;
        }

        string result;
        try
        {
            // Only a new file: one that has come since it was looked for is not emptied
            FileIo.CreateNewFile(path);
            result = DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            result = e.Message;
        }

        FileExplorer.LogFileOperation("New file", path, result);
        // (in the latest files at once, as one opened: "recent" and Tab with nothing typed offer it)
        if (result == DialogText.Success) FileExplorer.AddToHistory(path);
        Done(result, Lang.F("Made: {0}", path), path);
        // (@ at the top of the empty file: "insert @ text" writes its first line, and @ goes on from there)
        if (result == DialogText.Success) _line = 0;
    }

    // "mkdir drafts": a new directory in the directory you are in
    private void MakeDirectory(string argument)
    {
        var words = Words(argument);
        if (words.Count != 1)
        {
            Error(Lang.T("Give one name: mkdir drafts"));
            return;
        }

        var result = FileOperations.CreateDirectory(Directory, words[0], out var path);
        FileExplorer.LogFileOperation("New directory", Path.Combine(Directory, words[0]), result);
        Done(result, Lang.F("Made: {0}", path), result == DialogText.Success ? path : null);
    }

    // "rename 3 ending.md": a new name, in the same directory. The lists follow it
    private void Rename(string argument)
    {
        var words = Words(argument);
        if (words.Count != 2)
        {
            Error(Lang.T("Give what and its new name: rename 3 ending.md"));
            return;
        }

        if (Existing(words[0]) is not { } item || Protected(item)) return;
        var result = FileOperations.Rename(item.Path, words[1], out var path);
        FileExplorer.LogFileOperation("Rename", item.Path + " -> " + words[1], result);
        if (result == DialogText.Success) Moved(item, path);
        if (result == DialogText.Success) LatestFile(item, path);
        Done(result, Lang.F("Renamed: {0}", path), result == DialogText.Success ? path : null);
    }

    // "copy 3 drafts", "move 3 ../old": into a directory (a copy in the same directory gets a new name). A file of
    // that name in the directory is never overwritten. What is moved is followed by the lists
    private void CopyOrMove(string argument, bool move)
    {
        var words = Words(argument);
        if (words.Count != 2)
        {
            Error(move ? Lang.T("Give what and where to: move 3 drafts") : Lang.T("Give what and where to: copy 3 drafts"));
            return;
        }

        if (Existing(words[0]) is not { } item || (move && Protected(item))) return;
        if (Resolve(words[1], true) is not { } target) return;
        if (!System.IO.Directory.Exists(target.Path))
        {
            Error(Lang.F("Not a directory: {0}", target.Path));
            return;
        }

        var result = move
            ? FileOperations.Move(item.Path, target.Path, out var path)
            : FileOperations.Copy(item.Path, target.Path, out path);
        FileExplorer.LogFileOperation(move ? "Move" : "Copy", item.Path + " -> " + target.Path, result);
        if (move && result == DialogText.Success) Moved(item, path);
        if (result == DialogText.Success) LatestFile(item, path);
        Done(result, move ? Lang.F("Moved to {0}", path) : Lang.F("Copied to {0}", path),
            result == DialogText.Success ? path : null);
    }

    // "delete 3": to the trash (unless deleting to the trash is turned off), after asking: the name is typed to go
    // ahead. One that cannot be moved to the trash is deleted for good only if you say so
    private void Delete(string argument)
    {
        var words = Words(argument);
        if (words.Count != 1)
        {
            Error(Lang.T("Give one file or directory: delete 3"));
            return;
        }

        if (Existing(words[0]) is not { } item || Protected(item)) return;
        var trash = FileOperations.TrashDirectory;
        var toTrash = FileOperations.DeleteToTrash && FileOperations.CanTrash(item.Path, trash);
        if (!FileExplorer.ConfirmDeleteOf(item.Path, item.IsDirectory, toTrash))
        {
            Line(Lang.T("Nothing was deleted"));
            return;
        }

        var kind = item.IsDirectory ? "Delete directory" : "Delete";
        if (toTrash)
        {
            var result = FileOperations.MoveToTrash(item.Path, trash, out var trashedPath);
            FileExplorer.LogFileOperation(kind + " (to the trash)",
                item.Path + (trashedPath.Length > 0 ? " -> " + trashedPath : string.Empty), result);
            if (result == DialogText.Success)
            {
                Deleted(item);
                Line(Lang.F("Moved to the trash: {0}", item.Path));
                return;
            }

            if (result == DialogText.FileNotFound ||
                !ChoiceBox.ConfirmGuarded(item.IsDirectory ? DialogText.DeleteDirectory : DialogText.DeleteFile,
                    Lang.F("\"{0}\" could not be moved to the trash. Delete it for good?",
                        Tools.TruncateString(Path.GetFileName(item.Path), 40, true)), result))
            {
                Error(result);
                return;
            }
        }

        var deleted = FileOperations.Delete(item.Path);
        FileExplorer.LogFileOperation(kind, item.Path, deleted);
        if (deleted == DialogText.Success) Deleted(item);
        Done(deleted, Lang.F("Deleted: {0}", item.Path), null);
    }

    // A file or directory renamed or moved: the lists follow it, and so does the shell when it is in it ("rename . d2")
    private void Moved(Item item, string path)
    {
        FileExplorer.PathMoved(item.Path, path, item.IsDirectory);
        PeekFollows(item.Path, path);
        if (item.IsDirectory && In(item.Path))
        {
            Directory = Path.GetFullPath(Path.Combine(path, Path.GetRelativePath(item.Path, Directory)));
        }
    }

    // A file copied, renamed or moved is the newest of the latest files, as one made with "new": to work on it next
    // ("recent", and Tab with nothing typed, offer it first). A directory is not put there
    private static void LatestFile(Item item, string path)
    {
        if (!item.IsDirectory) FileExplorer.AddToHistory(path);
    }

    // The shell is in the directory, or in one below it
    private bool In(string directory) =>
        FileOperations.SamePath(Directory, directory) || FileOperations.IsInside(Directory, directory);

    // A file or directory deleted: off the lists, and the shell in it ("delete .") goes to the directory above it
    private void Deleted(Item item)
    {
        FileExplorer.PathDeleted(item.Path);
        if (!item.IsDirectory || !In(item.Path)) return;
        var above = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(item.Path));
        while (above != null && !System.IO.Directory.Exists(above)) above = Path.GetDirectoryName(above);
        if (above == null) return;
        Directory = above;
        Line(Lang.F("In {0}", Directory));
    }

    // "peek 3", "peek notes.md 40", "peek notes.md 100-120", "peek notes.md +100", "peek notes.md +100 30": the lines of
    // a text file, numbered (the first 15 if no lines are given, setting shellpeeklines; from a line: as many, or as
    // many as given), between the markers "->" and "<-" (setting shellmarkers). The file is
    // then the last list, at the first line shown: "open 1" opens it there. Only read, never written. A place after
    // the file (or alone: the file peeked last) puts a window as large as the last one there: "+", "++", "-", "--"
    // (steps from it; "+ 20": steps, given as "count"), "top", "middle", "bottom", "50%"; "peek +120" alone is the
    // file peeked last from line 120. A word alone that is the name of a file here is that file
    private void Peek(string argument, int count = 1)
    {
        var words = Words(argument);
        // "peek" alone (and "."): the window of the last peek again, as the file is now
        if (words.Count == 0)
        {
            PeekAgain();
            return;
        }

        // "peek notes.md /storm", "peek /storm" (the file peeked last): the window at the first line with the text.
        // A word that starts with "/" alone is a path when it is there, or has another "/" in it (a path mistyped is
        // told as not found, not searched)
        var searchAt = words[0].Length > 1 && words[0][0] == '/' && words[0].IndexOfAny(['/', '\\'], 1) < 0 &&
                       !File.Exists(PathOf(words[0])) && !System.IO.Directory.Exists(PathOf(words[0]))
            ? 0
            : words.Count >= 2 && words[1].Length > 0 && words[1][0] == '/' ? 1 : -1;
        if (searchAt >= 0)
        {
            var text = AfterWords(argument, searchAt).Trim().Trim('"')[1..];
            if (searchAt == 0)
            {
                SearchLastPeek(text);
                return;
            }

            PeekSearch(words[0], text, 0);
            return;
        }

        // A row of "find -t": the window around the line found, marked (looked up without telling: a number that is
        // not a row is told by the peek below)
        if (words.Count == 1 && RowOf(words[0]) is { Found: { } searched, At: { } found } row)
        {
            PeekFound(row.Path, searched, found.Line);
            return;
        }

        var place = words.Count is 1 or 2 ? PlaceOf(words[^1]) : null;
        var alone = words.Count == 1 && !File.Exists(PathOf(words[0])) && !System.IO.Directory.Exists(PathOf(words[0]));
        // "peek +120": the file peeked last, from line 120, its window as large
        var fromLine = alone && place == null && words[0].Length > 1 && words[0][0] == '+' &&
                       words[0][1..].All(char.IsAsciiDigit);
        if (alone && (place != null || fromLine))
        {
            if (_lastPeek is not { } previous)
            {
                Error(Lang.T("No file peeked yet: peek a file first (peek plot.md)"));
                return;
            }

            words = fromLine
                ? new List<string> { previous.Path, words[0], previous.Count.ToString(CultureInfo.InvariantCulture) }
                : new List<string> { previous.Path, words[0] };
        }
        else if (place != null && words.Count == 1)
        {
            // (a file named "top", "+" ... here: that file)
            place = null;
        }

        if (place == null && (words.Count > 3 || (words.Count == 3 && !words[1].StartsWith('+'))))
        {
            Error(Lang.T("Give a file, and the lines if you want: peek 3 10-30, peek 3 +100 30, peek 3 bottom"));
            return;
        }

        PeekWindow(words, place, count, false, false);
    }

    // The window of a peek shown: the file (words[0]) and its lines or place. "again": the window of the last peek
    // (moved to the last lines when the file got shorter); "auto": shown again by the setting shellautopeek, which
    // leaves the last list, $file and $peek as the command before it left them, and tells nothing that goes wrong
    // "mark": a line marked with ">" (a line found), and "open 1" opens the file there; "match": which line found of
    // how many, told under the lines; "loaded": the text of the file, read already
    private void PeekWindow(List<string> words, (PeekPlace Place, int Value)? place, int count, bool again, bool auto,
        int mark = 0, (int Number, int Count)? match = null, string? loaded = null)
    {
        Item item;
        if (auto)
        {
            item = new Item(Path.GetFullPath(words[0]), false);
        }
        else
        {
            if (Existing(words[0]) is not { } found) return;
            item = found;
        }

        if (item.IsDirectory)
        {
            Error(Lang.F("Not a file: {0}", item.Path));
            return;
        }

        // The window it is moved from: the last peek of this file, as large; for another file the first lines (15)
        // (a step from its top: "peek other.md +" from line 2)
        var path = Path.GetFullPath(item.Path);
        var same = _lastPeek is { } last1 && FileOperations.SamePath(last1.Path, path);
        var size = same ? _lastPeek!.Value.Count : Shell.PeekLines;
        var (first, last) = (1, Shell.PeekLines);
        if (place is { Place: PeekPlace.Step } step)
        {
            var from = (same ? _lastPeek!.Value.First : 1) + (long)step.Value * count;
            // (as large at the end of int too)
            first = (int)Math.Clamp(from, 1, int.MaxValue - size + 1);
            last = first + size - 1;
        }
        else if (place == null && words.Count >= 2 && !LinesOf(words[1], words.Count == 3 ? words[2] : null, out first, out last))
        {
            Error(Lang.F("Not lines: {0} (as 40, 100-120, or +100 30)", string.Join(' ', words.Skip(1))));
            return;
        }

        string text;
        try
        {
            text = loaded ?? TextFile.Load(item.Path).Text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (!auto) Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
            return;
        }

        // (counted as change, shift and clip count them: the last line break does not start a line)
        var lines = LinesOfText(text);
        // (a line no longer there is not marked)
        if (mark > lines.Count) mark = 0;
        // The top, the middle, the bottom, or a part of the file ("50%": from halfway)
        if (place is { Place: not PeekPlace.Step } at)
        {
            long start = at.Place switch
            {
                PeekPlace.Middle => (lines.Count - size) / 2 + 1,
                PeekPlace.Bottom => lines.Count - size + 1,
                PeekPlace.Percent => 1 + (long)Math.Max(0, lines.Count - 1) * at.Value / 100,
                _ => 1
            };
            first = (int)Math.Clamp(start, 1, int.MaxValue - size + 1);
            last = first + size - 1;
        }

        // The size of the window asked for: kept, also where the file has fewer lines
        var asked = (int)Math.Min((long)last - first + 1, int.MaxValue);
        // Moved past the end (or the file got shorter): the last lines, as many as the window
        var moved = (place != null || again) && last > lines.Count && lines.Count > 0;
        if (moved)
        {
            last = lines.Count;
            first = Math.Max(1, last - asked + 1);
        }

        // (kept for "peek +" and ".": the file, the window asked for, and the file as it was, for the setting
        // shellautopeek; also when it shows no lines, so the next one goes on from it)
        _lastPeek = (path, first, asked, StampOf(item.Path), text.GetHashCode());
        // (from a line: "open 1" opens the file there)
        if (!auto)
        {
            var opened = mark > 0 ? mark : first;
            // (@ is the line worked on: a window of the same file is a look and keeps it, a line found is the line,
            // and another file is at the first line shown)
            // (a window past the end of the file: no line there to take; an empty file is at its top)
            var kept = _file != null && FileOperations.NameComparer.Equals(_file, Path.GetFullPath(item.Path)) ? FileLine : null;
            LastList = new List<Item> { opened > 1 ? item with { At = (opened, 1) } : item };
            _line = mark == 0 && kept != null ? kept
                : opened <= lines.Count ? opened
                : lines.Count == 0 ? 0
                : kept;
        }
        last = Math.Min(last, lines.Count);
        if (first > last)
        {
            Line(Lang.F("{0} has {1} lines", item.Path, lines.Count));
            return;
        }

        var width = last.ToString().Length;
        // (the setting shellmarkers: the name of the file above the lines, and where they are below them, so a peek is
        // told apart from the lines around it. Lines of the file always start with a blank)
        if (Shell.Markers)
        {
            Line((first > 1 ? AboveMarker : TopMarker) +
                 (auto ? Lang.F("{0} (peek again)", Path.GetFileName(item.Path)) : Path.GetFileName(item.Path)));
        }

        // (the line found marked with ">" in the margin)
        for (var i = first; i <= last; i++)
        {
            var number = i.ToString().PadLeft(width + 2);
            Line((i == mark ? ">" + number[1..] : number) + "  " + lines[i - 1]);
        }
        // $peek: the lines shown, as they are now (to put them in another file: insert plan.md bottom $peek)
        if (!auto) ShellRegisters.Peek = string.Join("\n", lines.GetRange(first - 1, last - first + 1));
        // (where the window is: also at the end, when it was put or moved there; with the markers always)
        if (Shell.Markers)
        {
            Line((last < lines.Count ? BelowMarker : EndMarker) + (match is { } found
                ? Lang.F("match {0} of {1}, lines {2}-{3} of {4}", found.Number, found.Count, first, last, lines.Count)
                : Lang.F("lines {0}-{1} of {2}", first, last, lines.Count)));
        }
        else if (match is { } told)
        {
            Line(Lang.F("(match {0} of {1}, lines {2}-{3} of {4})", told.Number, told.Count, first, last, lines.Count));
        }
        else if (last < lines.Count || place != null || moved)
        {
            Line(Lang.F("(lines {0}-{1} of {2})", first, last, lines.Count));
        }
    }

    // The markers of peek and words (the setting shellmarkers), the same in all languages: above the lines, the top of
    // the file shown ("->"), or lines of the file above them ("-^"); below them, the end of the file shown ("<-"), or
    // lines of the file below them ("-v")
    internal const string TopMarker = "-> ";
    internal const string AboveMarker = "-^ ";
    internal const string EndMarker = "<- ";
    internal const string BelowMarker = "-v ";

    // Where a window of peek is put: a step from the last window, the top, the middle, the bottom, or a part of the
    // file
    private enum PeekPlace
    {
        Step,
        Top,
        Middle,
        Bottom,
        Percent
    }

    // The place a word after peek is ("+": a step of 1 line, as set; "50%": 50), or null
    private static (PeekPlace Place, int Value)? PlaceOf(string word)
    {
        if (PeekStep(word) is var step && step != 0) return (PeekPlace.Step, step);
        switch (word.ToLowerInvariant())
        {
            case "top":
                return (PeekPlace.Top, 0);
            case "middle":
                return (PeekPlace.Middle, 0);
            case "bottom":
                return (PeekPlace.Bottom, 0);
        }

        return word.Length is > 1 and <= 4 && word[^1] == '%' &&
               int.TryParse(word[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var percent) && percent <= 100
            ? (PeekPlace.Percent, percent)
            : null;
    }

    // The file searched last by peek, the text and the line found ("/" and "?" go on from it, in the file peeked last)
    private (string Path, string Text, int Line)? _lastSearch;

    // "peek /storm", "/storm" alone: a search in the file peeked last
    private void SearchLastPeek(string text)
    {
        if (_lastPeek is not { } peeked)
        {
            Error(Lang.T("No file peeked yet: peek a file first (peek plot.md)"));
            return;
        }

        PeekSearch(peeked.Path, text, 0);
    }

    // A row of the last list that a word is (a number), or null: nothing told
    private Item? RowOf(string word) =>
        word.Length > 0 && word.All(char.IsAsciiDigit) && !File.Exists(PathOf(word)) &&
        !System.IO.Directory.Exists(PathOf(word)) && int.TryParse(word, out var row) && row >= 1 &&
        row <= LastList.Count && LastList[row - 1].Path.Length > 0
            ? LastList[row - 1]
            : null;

    // A line found by find -t: the window around it, marked. When the file changed since, the line with the text
    // nearest to it is marked; when none has it, the window is at the line, with no mark
    private void PeekFound(string path, string searched, int line)
    {
        string text;
        try
        {
            text = TextFile.Load(path).Text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Error(Lang.F("{0} cannot be read: {1}", path, e.Message));
            return;
        }

        var lines = LinesOfText(text);
        var pattern = FileSearch.WildcardText(searched);
        var found = Enumerable.Range(1, lines.Count).Where(i => FileSearch.Matches(pattern, lines[i - 1])).ToList();
        var target = found.Count == 0 ? 0 : found.MinBy(i => Math.Abs(i - line));
        PeekAtLine(path, target > 0 ? target : line, target > 0, null, text);
    }

    // The window of a peek around a line (two lines of it above; the line at the top of a window of 1 or 2 lines),
    // as large as the last window of the file (15 lines for another, setting shellpeeklines), the line marked
    private void PeekAtLine(string path, int line, bool mark, (int Number, int Count)? match, string text)
    {
        var size = _lastPeek is { } before && FileOperations.SamePath(before.Path, path) ? before.Count : Shell.PeekLines;
        var first = size < 3 ? line : Math.Max(1, line - 2);
        PeekWindow(new List<string>
        {
            path, "+" + first.ToString(CultureInfo.InvariantCulture), size.ToString(CultureInfo.InvariantCulture)
        }, null, 1, true, false, mark ? line : 0, match, text);
    }

    // "peek notes.md /storm": the window at the first line with the text in it (letter case alike; "*" and "?" as
    // find -t takes them: "storm*night"), two lines of it above, the line marked. "direction" 1 ("/"): the next line
    // with it after the one found last, -1 ("?"): the one before; at the end the search goes round, and it is told
    private void PeekSearch(string fileWord, string text, int direction)
    {
        if (text.Trim().Length == 0)
        {
            Error(Lang.T("Give a text: peek notes.md /storm"));
            return;
        }

        if (Existing(fileWord) is not { } item) return;
        if (item.IsDirectory)
        {
            Error(Lang.F("Not a file: {0}", item.Path));
            return;
        }

        string content;
        try
        {
            content = TextFile.Load(item.Path).Text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
            return;
        }

        var lines = LinesOfText(content);
        var pattern = FileSearch.WildcardText(text);
        var found = new List<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (FileSearch.Matches(pattern, lines[i])) found.Add(i + 1);
        }

        var path = Path.GetFullPath(item.Path);
        if (found.Count == 0)
        {
            Error(Lang.F("No line with {0} in {1}", text, Path.GetFileName(path)));
            return;
        }

        // From the line found last (in this file, the same text), or from the top
        var from = direction != 0 && _lastSearch is { } last && last.Text == text && FileOperations.SamePath(last.Path, path)
            ? last.Line
            : 0;
        int target;
        if (direction > 0)
        {
            target = found.FirstOrDefault(line => line > from);
            if (target == 0)
            {
                target = found[0];
                Line(Lang.T("(from the top again)"));
            }
        }
        else if (direction < 0)
        {
            target = found.LastOrDefault(line => line < from);
            if (target == 0)
            {
                target = found[^1];
                Line(Lang.T("(from the end again)"));
            }
        }
        else
        {
            target = found[0];
        }

        _lastSearch = (path, text, target);
        PeekAtLine(path, target, true, (found.IndexOf(target) + 1, found.Count), content);
    }

    // "/" and "?" alone: the next line with the text searched last, or the one before, in the file peeked last
    private void SearchAgain(int direction)
    {
        if (_lastSearch is not { } searched || _lastPeek is not { } peeked)
        {
            Error(Lang.T("Nothing searched yet: peek notes.md /storm"));
            return;
        }

        PeekSearch(peeked.Path, searched.Text, direction);
    }

    // The last peek of this shell: the file, its first line and how many lines (for "peek +" and "."), and when the
    // file was last written, how long it was and a hash of its text then (for the setting shellautopeek)
    private (string Path, int First, int Count, (DateTime, long)? Stamp, int Hash)? _lastPeek;

    // The largest file whose text shellautopeek reads after a command to tell a change its time and length miss
    private const long AutoPeekReadLimit = 4 * 1024 * 1024;

    // "." and "peek" alone: the window of the last peek shown again, the same lines of the same file, read as it is
    // now (after a change made to it: insert, change, lines, the editor ...); at its end when it got shorter
    private void PeekAgain(bool auto = false)
    {
        if (_lastPeek is not { } previous)
        {
            Error(Lang.T("No file peeked yet: peek a file first (peek plot.md)"));
            return;
        }

        // (the words as they would be typed, given as words: a path with a quote in it is one word)
        PeekWindow(new List<string>
        {
            previous.Path, "+" + previous.First.ToString(CultureInfo.InvariantCulture),
            previous.Count.ToString(CultureInfo.InvariantCulture)
        }, null, 1, true, auto);
    }

    // When a file was last written, and how long it is (null: it is not there, or cannot be read)
    private static (DateTime, long)? StampOf(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? (info.LastWriteTimeUtc, info.Length) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or
                                       NotSupportedException)
        {
            return null;
        }
    }

    // The setting shellautopeek: after a command that changed the file of the last peek (insert, change, lines,
    // shift, format, trim, the editor opened with open, or another program), its window is shown again, with its
    // name above it. A change is told by the time the file was written and its length, or else by its text (a change
    // of the same length within the time the file system tells apart)
    private void AutoPeek(string command)
    {
        if (!Shell.AutoPeek || _lastPeek is not { } previous ||
            command is "peek" or "." or "+" or "++" or "-" or "--" or "/" or "?" || command.StartsWith('/'))
        {
            return;
        }

        var now = StampOf(previous.Path);
        if (now == null) return;
        if (now == previous.Stamp)
        {
            if (now.Value.Item2 > AutoPeekReadLimit) return;
            try
            {
                if (TextFile.Load(previous.Path).Text.GetHashCode() == previous.Hash) return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return;
            }
        }

        // (with the markers, the first of them names the file)
        if (!Shell.Markers) Line(Lang.F("(peek again: {0})", Path.GetFileName(previous.Path)));
        PeekAgain(true);
    }

    // The last peek follows its file when it is renamed or moved in the shell (or the directory it is in), so "." and
    // "+" go on in it
    private void PeekFollows(string from, string to)
    {
        if (_lastPeek is not { } previous) return;
        if (FileOperations.SamePath(previous.Path, from))
        {
            _lastPeek = previous with { Path = Path.GetFullPath(to) };
        }
        else if (FileOperations.IsInside(previous.Path, from))
        {
            _lastPeek = previous with { Path = Path.GetFullPath(Path.Combine(to, Path.GetRelativePath(from, previous.Path))) };
        }
    }

    // How far "+", "++", "-" and "--" move the window of peek (settings shellpeekstep and shellpeekbigstep; 0:
    // another word)
    private static int PeekStep(string word) => word switch
    {
        "+" => Shell.PeekStep,
        "++" => Shell.PeekBigStep,
        "-" => -Shell.PeekStep,
        "--" => -Shell.PeekBigStep,
        _ => 0
    };

    // "40": the first 40 lines; "100-120": those lines; "+100": 15 lines from line 100 (setting shellpeeklines), and
    // "+100" "30": 30 of them
    internal static bool LinesOf(string text, string? count, out int first, out int last)
    {
        first = 1;
        last = 0;
        if (text.StartsWith('+'))
        {
            var many = Shell.PeekLines;
            if (!int.TryParse(text[1..], out first) || first < 1 ||
                (count != null && (!int.TryParse(count, out many) || many < 1)))
            {
                return false;
            }

            last = (int)Math.Min((long)first + many - 1, int.MaxValue);
            return true;
        }

        if (count != null) return false;
        var dash = text.IndexOf('-');
        if (dash < 0) return int.TryParse(text, out last) && last > 0;
        return int.TryParse(text[..dash], out first) && int.TryParse(text[(dash + 1)..], out last) &&
               first > 0 && last >= first;
    }

    // The last search of this Blyant ("find" alone shows it again, "find -r" makes it again): where, what was looked
    // for, when, and what was found. Kept while Blyant runs, as $peek is
    private sealed record FindSearch(string Directory, FileSearch.Options Options, DateTime When, FileSearch.Outcome Outcome);

    private static FindSearch? _lastFind;

    /// <summary>
    /// Forget the last search (a new Blyant, or a test)
    /// </summary>
    internal static void ForgetFind() => _lastFind = null;

    // "find plot", "find *.md", "find -t storm", "find *.md -t the storm": files in the directory you are in and the
    // directories in it, by name (a part of it, or with * and ?), and by a text in them (after -t). As the search of
    // the file browser: hidden files are left out, and only text files are searched for text. The rows show the path
    // from the directory searched; options before the name, in any order: "-l" the full paths (without the mark of
    // a favorite, for scripts), "-f" the folder and the name ("drafts/storm.md"), "-b" the name only ("storm.md"),
    // with the folder after a name two rows have. The rows are the same in each: "open 3", "info 3" take row 3.
    // "find" alone (or with -l, -f, -b only) shows the last search again, as it was found, from wherever you are;
    // "find -r" searches again for the same in the directory of the last search
    private void Find(string argument)
    {
        var (full, folder, bare, repeat) = (false, false, false, false);
        argument = argument.TrimStart();
        while (argument.StartsWith('-'))
        {
            var word = argument.Split((char[]?)null, 2)[0];
            // ("-t": the text, read below)
            if (word == "-t") break;
            if (word is not ("-l" or "-f" or "-b" or "-r"))
            {
                Error(Lang.F("Not taken: {0} (-l full paths, -f folder/name, -b names only, -r again, -t text)", word));
                return;
            }

            if (word == "-l") full = true;
            else if (word == "-f") folder = true;
            else if (word == "-b") bare = true;
            else repeat = true;
            argument = argument[word.Length..].TrimStart();
        }

        // "find", "find -b": the last search shown again; "find -r": made again where it was made
        if (argument.Trim().Length == 0 && (_lastFind != null || repeat))
        {
            if (_lastFind is not { } last)
            {
                Error(Lang.T("No search yet: find plot, find -t storm"));
                return;
            }

            if (repeat)
            {
                if (!System.IO.Directory.Exists(last.Directory))
                {
                    Error(Lang.F("{0} is not there any more: the search is not made again", last.Directory));
                    return;
                }

                Line(Lang.F("Search again: {0} in {1}", Searched(last.Options), last.Directory));
                MakeSearch(last.Directory, last.Options, full, folder, bare);
                return;
            }

            Line(Lang.F("Last search: {0} in {1} ({2})", Searched(last.Options), last.Directory,
                last.When.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)));
            ShowSearch(last, full, folder, bare);
            return;
        }

        if (repeat)
        {
            Error(Lang.T("find -r makes the last search again: give no name or text after it"));
            return;
        }

        var text = string.Empty;
        var at = argument.IndexOf("-t ", StringComparison.Ordinal);
        if (argument.TrimEnd().EndsWith("-t", StringComparison.Ordinal) && at < 0) at = argument.TrimEnd().Length - 2;
        if (at >= 0 && (at == 0 || char.IsWhiteSpace(argument[at - 1])))
        {
            text = argument.Length > at + 3 ? argument[(at + 3)..].Trim() : string.Empty;
            // (in quotes: without them, "find -t "# *"", as the lists take -t)
            if (text.Length >= 2 && text[0] == '"' && text[^1] == '"' && text.Count(c => c == '"') == 2) text = text[1..^1];
            argument = argument[..at];
        }

        // ("*" and "?" in the name, as in the file browser, and in the text: "find -t storm*night")
        var options = new FileSearch.Options
        {
            Name = argument.Trim().Trim('"'), Text = text, Subfolders = true, TextWildcards = true
        };
        var problem = FileSearch.Validate(options);
        if (problem != DialogText.Success)
        {
            Error(Lang.F("{0} (find plot, find *.md, find -t storm)", problem));
            return;
        }

        MakeSearch(Directory, options, full, folder, bare);
    }

    // What a search looked for, as typed: "storm", "-t storm", "*.md -t attic"
    private static string Searched(FileSearch.Options options) =>
        options.Text.Length == 0 ? options.Name
        : options.Name.Length == 0 ? "-t " + options.Text
        : options.Name + " -t " + options.Text;

    // A search made in a directory and its directories, kept as the last search, and shown
    private void MakeSearch(string directory, FileSearch.Options options, bool full, bool folder, bool bare)
    {
        FileExplorer.LogFileOperation("Search", directory + " (with subfolders)", DialogText.Success);
        var search = new FindSearch(directory, options, DateTime.Now, FileSearch.Search(directory, options));
        _lastFind = search;
        ShowSearch(search, full, folder, bare);
    }

    // The rows of a search, numbered, as the last list: the path from the directory searched (or as the options
    // say), the line found after a search by text, and "(missing)" after a file gone since
    private void ShowSearch(FindSearch search, bool full, bool folder, bool bare)
    {
        var outcome = search.Outcome;
        var text = search.Options.Text;
        // (a favorite marked " *" after its name, as in the search results of the full screen; not a line found in a
        // file, which is not the file)
        FileFavorites.Load();
        // "-f" and "-b": as recent and fav show them (with "-l" the full paths win)
        var names = !full && (folder || bare)
            ? Names(outcome.Results.Select(result => (result.Path, result.IsDirectory)).ToList(), false, false, folder)
            : null;
        var missing = " " + DialogText.FavoriteMissing;
        var rows = outcome.Results.Select((result, i) =>
        {
            // The name or path, then the mark of a favorite (not after a line found), then "(missing)" (not with -l:
            // the full paths for scripts), as recent and fav show them
            var name = names != null
                ? names[i].EndsWith(missing, StringComparison.Ordinal) ? names[i][..^missing.Length] : names[i]
                : (full ? result.Path : Path.GetRelativePath(search.Directory, result.Path)) +
                  (result.IsDirectory ? "/" : string.Empty);
            var shown = name + (!full && result.Line < 0 ? FavoriteMark(result.Path) : string.Empty) +
                        (!full && Gone(result) ? missing : string.Empty);
            return result.Line >= 0
                ? (new Item(result.Path, result.IsDirectory, (result.Line + 1, 1), text),
                    shown + ":" + (result.Line + 1) + "  " + result.LineText.Trim())
                : (new Item(result.Path, result.IsDirectory), shown);
        }).ToList();
        List(rows, Lang.T("Nothing found"));
        if (outcome.Capped) Line(Lang.F("(the first {0}: the search stopped there)", FileSearch.MaxResults));
        if (outcome.Unreadable > 0) Line(Lang.F("(directories that could not be read: {0})", outcome.Unreadable));

        static bool Gone(FileSearch.Result result) =>
            result.IsDirectory ? !System.IO.Directory.Exists(result.Path) : !FileOperations.IsFileThere(result.Path);
    }

    // "fav add 3", "fav remove notes.md": a file or directory added to the favorites in use, or taken off them. A
    // place after the file of "fav add" puts it there: "top", "bottom", the place of a row ("5") or a folder
    // ("[Drafts]": at the top of it), as "fav move" moves it; without one it goes at the top (row 1), and the list is shown
    // again, as its rows have moved
    private void ChangeFavorites(string argument, bool add)
    {
        var words = Words(argument);
        if (add && words.Count > 1)
        {
            AddFavoriteAt(words[0], AfterWords(argument, 1).Trim());
            return;
        }

        if (words.Count != 1)
        {
            Error(add ? Lang.T("Give what to add: fav add 3") : Lang.T("Give what to take off: fav remove 3"));
            return;
        }

        // (one that is gone can be taken off)
        if ((add ? Existing(words[0]) : Resolve(words[0], false)) is not { } item) return;
        FileFavorites.Load();
        if (FileFavorites.Contains(item.Path) == add)
        {
            Line(add ? Lang.F("Already a favorite: {0}", item.Path) : Lang.F("Not a favorite: {0}", item.Path));
            return;
        }

        // (taken off: "fav undo" puts it back)
        var before = FileFavorites.Serialize();
        FileFavorites.Toggle(item.Path);
        var result = FileFavorites.Save();
        if (!add && result == DialogText.Success) KeepForUndo(before);
        EventLog.Add(DialogText.LogFile + (add ? "Favorite add: " : "Favorite remove: ") + item.Path);
        Done(result, add ? Lang.F("Added to the favorites: {0}", item.Path)
            : Lang.F("Taken off the favorites: {0}", item.Path), null);
        // (added at the top: the rows of the list shown before have moved down, so it is shown again)
        if (add && result == DialogText.Success) ListFavorites(string.Empty);
    }

    // "fav add notes.md [Drafts]": a favorite added at a place (top, bottom, a row, a folder). Nothing is added when
    // the place is not there; the list is shown again, as after "fav move"
    private void AddFavoriteAt(string what, string where)
    {
        if (where.Trim('"').ToLowerInvariant() is "up" or "down" or "out")
        {
            Error(Lang.T("A new favorite goes to top, bottom, a row (5) or a [folder]: fav add notes.md [Drafts]"));
            return;
        }

        if (Existing(what) is not { } item) return;
        FileFavorites.Load();
        if (FileFavorites.Contains(item.Path))
        {
            Line(Lang.F("Already a favorite: {0} (fav move puts it elsewhere)", item.Path));
            return;
        }

        // The place of a row, counted as the list is before the favorite is added: the row after the last is the
        // bottom
        if (int.TryParse(where.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var row))
        {
            var count = FavoriteRows().Count;
            if (row == count + 1)
            {
                where = "bottom";
            }
            else if (row < 1 || row > count)
            {
                Error(count == 1
                    ? Lang.F("No row {0}: the favorites have 1 row", row)
                    : Lang.F("No row {0}: the favorites have {1} rows", row, count));
                return;
            }
        }

        // (added at the end, then moved as "fav move" moves it; put back as it was when the place is not there)
        var before = FileFavorites.Serialize();
        if (!FileFavorites.Toggle(item.Path, int.MaxValue))
        {
            Error(Lang.F("Not added to the favorites: {0}", item.Path));
            return;
        }

        var added = FileFavorites.Items[^1];
        var error = MoveRows(new List<FileFavorites.Favorite> { added }, where);
        if (error != null)
        {
            FileFavorites.Parse(before);
            // (a place fav move does not know: told as for fav add)
            if (error == MoveUsage)
            {
                error = Lang.T("A new favorite goes to top, bottom, a row (5) or a [folder]: fav add notes.md [Drafts]");
            }

            if (error.Length > 0) Error(error);
            return;
        }

        // (one line in the log: the change and the path)
        FavoritesChanged(Lang.F("Added to the favorites: {0}", item.Path), "add at a place: " + item.Path);
    }

    // "hot add" (the directory you are in), "hot add 2", "hot remove 3": a directory added to the hotlist, or taken
    // off it
    private void ChangeHotlist(string argument, bool add)
    {
        var words = Words(argument);
        if (words.Count > 1 || (!add && words.Count == 0))
        {
            Error(add ? Lang.T("Give a directory, or none for this one: hot add 2") : Lang.T("Give what to take off: hot remove 3"));
            return;
        }

        // (one that is gone can be taken off)
        var item = words.Count == 0 ? new Item(Directory, true) : add ? Existing(words[0]) : Resolve(words[0], true);
        if (item == null) return;
        if (!item.IsDirectory || (!add && File.Exists(item.Path)))
        {
            Error(Lang.F("Not a directory: {0}", item.Path));
            return;
        }

        DirectoryHotlist.Load();
        if (add)
        {
            var added = DirectoryHotlist.Add(item.Path);
            if (added == DirectoryHotlist.AddResult.AlreadyThere)
            {
                Line(Lang.F("Already in the hotlist: {0}", item.Path));
                return;
            }

            if (added == DirectoryHotlist.AddResult.Full)
            {
                Error(Lang.T("The hotlist is full"));
                return;
            }

            EventLog.Add(DialogText.LogFile + "Hotlist add: " + item.Path);
            Done(DirectoryHotlist.Save(), Lang.F("Added to the hotlist: {0}", item.Path), null);
            return;
        }

        var index = DirectoryHotlist.Items.ToList().FindIndex(entry => FileOperations.SamePath(entry.Path, item.Path));
        if (index < 0)
        {
            Line(Lang.F("Not in the hotlist: {0}", item.Path));
            return;
        }

        DirectoryHotlist.RemoveMany(new[] { index });
        EventLog.Add(DialogText.LogFile + "Hotlist remove: " + item.Path);
        Done(DirectoryHotlist.Save(), Lang.F("Taken off the hotlist: {0}", item.Path), null);
    }
}
