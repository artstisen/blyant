namespace Blyant;

/// <summary>
/// What Tab completes to on the line of the shell: the name of a command, the words that can follow some of them (the
/// names of the settings after "set", the lists of favorites after "fav", the projects after "project" ...), and the
/// names of files and directories after the commands that take a path ("open pl" to "open plot.md"; "cd" and the
/// directory of "copy" and "move" take directories only). Tab again gives the next name that fits, and after the last
/// of them what was typed; Shift+Tab goes back. A name that is the only one that fits is taken: Tab then goes on from
/// it (into a directory: "Draft/"). A name with "*" or "?" in it is a pattern ("*151*" to "Blyant-v151.md"), and with
/// nothing typed the files opened last come first (setting "shellrecentfirst"). This class does not touch the console
/// </summary>
internal sealed class ShellCompletion
{
    private readonly Func<string> _directory;
    private readonly Func<string, string> _fullPath;
    private string _shown = string.Empty; // What the last completion gave: another text starts over
    private string _before = string.Empty; // The text in front of the word completed
    private string _typed = string.Empty; // The word as it was typed
    private List<string> _matches = new();
    private int _index = -1; // The match shown (-1: what was typed)
    private bool _active;
    private List<string> _words = new(); // The words after the command, as typed

    /// <param name="directory">The directory the shell is in</param>
    /// <param name="fullPath">A path typed as a full path (as the shell takes it: from its directory, "~")</param>
    internal ShellCompletion(Func<string> directory, Func<string, string> fullPath)
    {
        _directory = directory;
        _fullPath = fullPath;
    }

    // What follows a command: a path (a file or a directory), a directory, or words
    private enum Kind
    {
        None,
        Path,
        Directory,
        Words,
        PathOrWords,
        QuotedWords
    }

    // What "clip" copies
    private static readonly string[] ClipKinds = { "path", "name", "text", "lines" };

    /// <summary>
    /// The line after Tab (forward) or Shift+Tab: the word at its end completed, or the line as it was
    /// </summary>
    internal string Next(string text, bool forward)
    {
        if (!_active || !string.Equals(text, _shown, StringComparison.Ordinal))
        {
            _active = false;
            _index = -1;
            if (!Start(text)) return text;
            _active = true;
        }

        var count = _matches.Count + 1;
        _index = (_index + 1 + (forward ? 1 : count - 1)) % count - 1;
        _shown = _before + (_index < 0 ? _typed : _matches[_index]);
        // The only name that fits is taken: Tab goes on from it
        if (_matches.Count == 1) _active = false;
        return _shown;
    }

    // Find the words that fit the word at the end of the line (false: none)
    private bool Start(string text)
    {
        var (before, word) = LastWord(text);
        _before = before;
        _typed = word;
        // $name: the names of the registers, anywhere on the line
        if (word.StartsWith('$') && !word.Contains('"'))
        {
            // (the file edited by hand since the last command: its names)
            ShellRegisters.Refresh();
            // ($clip when clip has copied a text)
            var names = ShellRegisters.All.Keys.Concat(ShellRegisters.Clip != null ? [ShellRegisters.ClipName] : []);
            _matches = Fitting(names.Order(StringComparer.Ordinal).Select(name => "$" + name), word);
            return _matches.Count > 0;
        }

        var words = ShellSession.Words(before);
        if (words.Count == 0)
        {
            // The name of a command (not a word in quotes)
            if (word.StartsWith('"')) return false;
            _matches = Fitting(Commands.Names(CommandContext.Shell), word);
            return _matches.Count > 0;
        }

        var command = words[0].ToLowerInvariant();
        // (the words as typed: the file of shift is read by its name, in its letter case)
        _words = words.Skip(1).ToList();
        var (kind, choices) = After(command, _words.Select(w => w.ToLowerInvariant()).ToList());
        _matches = kind switch
        {
            Kind.Path => PathsFitting(word, false),
            Kind.Directory => PathsFitting(word, true),
            Kind.Words => Fitting(choices, word),
            Kind.PathOrWords => Fitting(choices, word).Concat(PathsFitting(word, false)).ToList(),
            // (a word with a blank in it in quotes: fitting what is typed after a quote)
            Kind.QuotedWords => Fitting(choices, word.TrimStart('"'))
                .Select(w => w.Any(char.IsWhiteSpace) ? "\"" + w + "\"" : w).ToList(),
            _ => new List<string>()
        };
        return _matches.Count > 0;
    }

    // What the word after a command and the words after it (lowercase) can be
    private (Kind Kind, IEnumerable<string> Words) After(string command, IReadOnlyList<string> after)
    {
        var none = Enumerable.Empty<string>();
        var position = after.Count;
        switch (command)
        {
            case "cd":
                return (position == 0 ? Kind.Directory : Kind.None, none);
            case "ls":
                // ("ls -a notes": the directory after -a)
                return (position == 0 || (position == 1 && after[0] == "-a") ? Kind.Directory : Kind.None, none);
            case "open":
            case "peek":
            case "info":
            case "delete":
            case "del":
            case "format":
            case "trim":
            case "trim!":
            case "change":
            case "rename":
                return (position == 0 ? Kind.Path : Kind.None, none);
            case "copy":
            case "move":
                return position switch
                {
                    0 => (Kind.Path, none),
                    1 => (Kind.Directory, none),
                    _ => (Kind.None, none)
                };
            case "shift":
            case "shift!":
                // The file, the lines (typed), and the place: top, bottom or a heading of the file
                return position switch
                {
                    0 => (Kind.Path, none),
                    2 => (Kind.QuotedWords, new[] { "top", "bottom" }.Concat(HeadingsOf(_words[0]))),
                    _ => (Kind.None, none)
                };
            case "lines":
            {
                // The file, the lines (typed), ">" (typed), the other file, the place in it, and copy or cut
                var arrow = after.ToList().IndexOf(">");
                if (position == 0) return (Kind.Path, none);
                if (arrow < 0) return (Kind.None, none);
                if (position == arrow + 1) return (Kind.Path, none);
                if (position == arrow + 2)
                    return (Kind.QuotedWords, new[] { "top", "bottom" }.Concat(HeadingsOf(_words[arrow + 1])));
                return (position == arrow + 3 ? Kind.Words : Kind.None, new[] { "copy", "cut" });
            }
            case "insert":
                return position switch
                {
                    0 => (Kind.Path, none),
                    1 => (Kind.Words, new[] { "top", "bottom" }),
                    _ => (Kind.None, none)
                };
            case "favorites":
            case "fav":
            case "favs":
                if (position == 0)
                {
                    return (Kind.Words, new[] { "add", "remove", "lists", "new", "rename", "delete", "use", "-l" }
                        .Concat(FileFavorites.ListWords()));
                }

                if (position == 1 && after[0] is "-l" or "use") return (Kind.Words, FileFavorites.ListWords());
                if (position == 1 && after[0] is "rename" or "delete") return (Kind.Words, FileFavorites.ListNames());
                return (position == 1 && after[0] is "add" or "remove" ? Kind.Path : Kind.None, none);
            case "history":
            case "hist":
            case "recent":
            case "bookmarks":
            case "book":
                return (position == 0 ? Kind.Words : Kind.None, new[] { "-l" });
            case "hotlist":
            case "hot":
                if (position == 0) return (Kind.Words, new[] { "add", "remove" });
                return (position == 1 && after[0] is "add" or "remove" ? Kind.Directory : Kind.None, none);
            case "project":
                return (position == 0 ? Kind.Words : Kind.None, Projects.DisplayNames(Projects.All()));
            case "set":
                return (position == 0 ? Kind.Words : Kind.None, FileIo.ParseSettings(FileIo.SettingsText()).Keys);
            case "clip":
                // The kind, or a path ("clip 3" copies the path)
                if (position == 0) return (Kind.PathOrWords, ClipKinds);
                return (position == 1 && ClipKinds.Contains(after[0]) ? Kind.Path : Kind.None, none);
            case "reg":
                if (position == 0) return (Kind.Words, new[] { "-d" }.Concat(ShellRegisters.All.Keys));
                return (position == 1 && after[0] == "-d" ? Kind.Words : Kind.None,
                    new[] { "all" }.Concat(ShellRegisters.All.Keys));
            case "about":
                return (position == 0 ? Kind.Words : Kind.None, new[] { "license" });
            case "help":
            case "h":
                return (position == 0 ? Kind.Words : Kind.None, Commands.Names(CommandContext.Shell));
            default:
                return (Kind.None, none);
        }
    }

    // The names of the headings of a file (for "shift"); none if it cannot be read
    private IEnumerable<string> HeadingsOf(string typed)
    {
        try
        {
            var path = _fullPath(typed.Trim('"'));
            if (!File.Exists(path) || new FileInfo(path).Length > TextFile.MaxSize) return Enumerable.Empty<string>();
            return ShellSession.Headings(TextBuffer.Normalize(File.ReadAllText(path)), ShellSession.IsTypst(path))
                .Select(h => h.Name).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Enumerable.Empty<string>();
        }
    }

    // The words that start with what is typed (not the word itself), as they are written
    private static List<string> Fitting(IEnumerable<string> words, string typed) =>
        words.Where(word => word.StartsWith(typed, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(word, typed, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // The files and directories (or directories only) whose names start with the end of the word typed, in the
    // directory of the word ("Draft/no" in "Draft"), by name (letter case alike). A directory gets "/" after it, and a
    // name with a blank in it is put in quotes. Hidden names only when the word starts with ".". A name with "*" or "?"
    // is a pattern ("*151*", "plot?.md"; in the name only, not in the directories before it). With nothing typed, the
    // files opened last that are in the directory come first, the last opened first (directories: those the files are
    // in), then the others by name (setting "shellrecentfirst")
    private List<string> PathsFitting(string word, bool directoriesOnly)
    {
        var typed = word.Trim('"');
        var cut = Math.Max(typed.LastIndexOf('/'), typed.LastIndexOf('\\'));
        var folder = cut >= 0 ? typed[..(cut + 1)] : string.Empty;
        var start = typed[(cut + 1)..];
        var pattern = IsPattern(start);
        string directory;
        try
        {
            directory = folder.Length == 0 ? _directory() : _fullPath(folder);
            if (!System.IO.Directory.Exists(directory)) return new List<string>();
            var info = new DirectoryInfo(directory);
            var recent = start.Length == 0 && Shell.RecentFirst ? RecentIn(directory, directoriesOnly) : null;
            var entries = info.EnumerateDirectories().Select(d => (Name: d.Name, IsDirectory: true))
                .Concat(directoriesOnly
                    ? Enumerable.Empty<(string Name, bool IsDirectory)>()
                    : info.EnumerateFiles().Select(f => (Name: f.Name, IsDirectory: false)))
                .Where(entry => start.StartsWith('.') || !entry.Name.StartsWith('.')).ToList();
            var fitting = entries.Where(entry => pattern
                ? System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(start, entry.Name)
                : entry.Name.StartsWith(start, StringComparison.OrdinalIgnoreCase)).ToList();
            // (a pattern that fits nothing is a name typed: on Linux and macOS "notes?.md" can be one)
            if (pattern && fitting.Count == 0)
            {
                fitting = entries.Where(entry => entry.Name.StartsWith(start, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var names = fitting
                .OrderBy(entry => recent == null ? 0 : Rank(recent, entry.Name))
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select(entry => folder + entry.Name + (entry.IsDirectory ? "/" : string.Empty))
                .Where(path => !string.Equals(path, typed, StringComparison.Ordinal))
                .Select(path => path.Contains(' ') ? "\"" + path + "\"" : path)
                .ToList();
            return names;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or
                                       System.Security.SecurityException)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// True for a name with "*" (any characters) or "?" (one character) in it: a pattern for Tab
    /// </summary>
    internal static bool IsPattern(string name) => name.IndexOfAny(['*', '?']) >= 0;

    // Where a name is in the files opened last (0: the last opened), after them all when it is not one of them
    private static int Rank(List<string> recent, string name)
    {
        var index = recent.FindIndex(r => FileOperations.NameComparer.Equals(r, name));
        return index < 0 ? int.MaxValue : index;
    }

    // The names in a directory of the files opened last (the last opened first): the files that are in it, or, for a
    // directory to complete, its directories that have them (in them or below)
    private static List<string> RecentIn(string directory, bool directoriesOnly)
    {
        FileHistory.Load();
        var names = new List<string>();
        foreach (var entry in FileHistory.Items)
        {
            string? name;
            if (directoriesOnly)
            {
                // ("~/novel" for "~/novel/chapters/02.md": "chapters")
                if (!FileOperations.IsInside(entry.Path, directory)) continue;
                var relative = Path.GetRelativePath(directory, entry.Path);
                var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                name = first.Length < relative.Length ? first : null;
            }
            else
            {
                name = FileOperations.SamePath(Path.GetDirectoryName(entry.Path) ?? string.Empty, directory)
                    ? Path.GetFileName(entry.Path)
                    : null;
            }

            // (letter case as the file system has it: "Notes.md" is not "notes.md" on Linux)
            if (name != null && !names.Contains(name, FileOperations.NameComparer)) names.Add(name);
        }

        return names;
    }

    /// <summary>
    /// The text in front of the last word of a line, and that word: after the last blank, or from a double quote that
    /// is not closed ("open \"my no" gives "\"my no")
    /// </summary>
    internal static (string Before, string Word) LastWord(string text)
    {
        var quoted = false;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                if (!quoted) start = i;
                quoted = !quoted;
            }
            else if (char.IsWhiteSpace(text[i]) && !quoted)
            {
                start = i + 1;
            }
        }

        return (text[..start], text[start..]);
    }
}
