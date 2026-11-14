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
        Done(result, Lang.F("Made: {0}", path), path);
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
        if (item.IsDirectory && In(item.Path))
        {
            Directory = Path.GetFullPath(Path.Combine(path, Path.GetRelativePath(item.Path, Directory)));
        }
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
    // a text file, numbered (the first 20 if no lines are given; from a line: 20, or as many as given). The file is
    // then the last list, at the first line shown: "open 1" opens it there. Only read, never written
    private void Peek(string argument)
    {
        var words = Words(argument);
        if (words.Count is < 1 or > 3 || (words.Count == 3 && !words[1].StartsWith('+')))
        {
            Error(Lang.T("Give a file, and the lines if you want: peek 3 10-30, peek 3 +100 30"));
            return;
        }

        if (Existing(words[0]) is not { } item) return;
        if (item.IsDirectory)
        {
            Error(Lang.F("Not a file: {0}", item.Path));
            return;
        }

        var (first, last) = (1, PeekLines);
        if (words.Count >= 2 && !LinesOf(words[1], words.Count == 3 ? words[2] : null, out first, out last))
        {
            Error(Lang.F("Not lines: {0} (as 40, 100-120, or +100 30)", string.Join(' ', words.Skip(1))));
            return;
        }

        List<string> lines;
        try
        {
            // (counted as change, shift and clip count them: the last line break does not start a line)
            lines = LinesOfText(TextFile.Load(item.Path).Text);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
            return;
        }

        // (from a line: "open 1" opens the file there)
        LastList = new List<Item> { first > 1 ? item with { At = (first, 1) } : item };
        last = Math.Min(last, lines.Count);
        if (first > last)
        {
            Line(Lang.F("{0} has {1} lines", item.Path, lines.Count));
            return;
        }

        var width = last.ToString().Length;
        for (var i = first; i <= last; i++) Line(i.ToString().PadLeft(width + 2) + "  " + lines[i - 1]);
        if (last < lines.Count) Line(Lang.F("(lines {0}-{1} of {2})", first, last, lines.Count));
    }

    // The lines peek shows when none are given
    private const int PeekLines = 20;

    // "40": the first 40 lines; "100-120": those lines; "+100": 20 lines from line 100, and "+100" "30": 30 of them
    internal static bool LinesOf(string text, string? count, out int first, out int last)
    {
        first = 1;
        last = 0;
        if (text.StartsWith('+'))
        {
            var many = PeekLines;
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

    // "find plot", "find *.md", "find -t storm", "find *.md -t the storm": files in the directory you are in and the
    // directories in it, by name (a part of it, or with * and ?), and by a text in them (after -t). As the search of
    // the file browser: hidden files are left out, and only text files are searched for text
    private void Find(string argument)
    {
        var text = string.Empty;
        var at = argument.IndexOf("-t ", StringComparison.Ordinal);
        if (argument.TrimEnd().EndsWith("-t", StringComparison.Ordinal) && at < 0) at = argument.TrimEnd().Length - 2;
        if (at >= 0 && (at == 0 || char.IsWhiteSpace(argument[at - 1])))
        {
            text = argument.Length > at + 3 ? argument[(at + 3)..].Trim() : string.Empty;
            argument = argument[..at];
        }

        var options = new FileSearch.Options { Name = argument.Trim().Trim('"'), Text = text, Subfolders = true };
        var problem = FileSearch.Validate(options);
        if (problem != DialogText.Success)
        {
            Error(Lang.F("{0} (find plot, find *.md, find -t storm)", problem));
            return;
        }

        FileExplorer.LogFileOperation("Search", Directory + " (with subfolders)", DialogText.Success);
        var outcome = FileSearch.Search(Directory, options);
        var rows = outcome.Results.Select(result =>
        {
            var shown = Path.GetRelativePath(Directory, result.Path) + (result.IsDirectory ? "/" : string.Empty);
            return result.Line >= 0
                ? (new Item(result.Path, result.IsDirectory, (result.Line + 1, 1)),
                    shown + ":" + (result.Line + 1) + "  " + result.LineText.Trim())
                : (new Item(result.Path, result.IsDirectory), shown);
        }).ToList();
        List(rows, Lang.T("Nothing found"));
        if (outcome.Capped) Line(Lang.F("(the first {0}: the search stopped there)", FileSearch.MaxResults));
        if (outcome.Unreadable > 0) Line(Lang.F("(directories that could not be read: {0})", outcome.Unreadable));
    }

    // "fav add 3", "fav remove notes.md": a file or directory added to the favorites in use, or taken off them
    private void ChangeFavorites(string argument, bool add)
    {
        var words = Words(argument);
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

        FileFavorites.Toggle(item.Path);
        var result = FileFavorites.Save();
        EventLog.Add(DialogText.LogFile + (add ? "Favorite add: " : "Favorite remove: ") + item.Path);
        Done(result, add ? Lang.F("Added to the favorites: {0}", item.Path)
            : Lang.F("Taken off the favorites: {0}", item.Path), null);
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
