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

    /// <summary>
    /// The file a word typed stands for, as the commands take it (a row of the last list, $file ...): for the headings
    /// of "shift" and "lines". Without it, the word is a path
    /// </summary>
    internal Func<string, string>? FileOf { get; init; }

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
        PathThenWords,
        QuotedWords,
        DirectoryOrOptions, // A directory; a word that starts with "-": the options, then the directories that fit
        PathOrOptions, // A file or directory; a word that starts with "-": the options, then the names that fit
        Names // Words, or those a pattern fits ("chapter-*" to "chapter-1.md"), in quotes with a blank in them
    }

    // What "clip" copies
    private static readonly string[] ClipKinds = { "path", "name", "text", "lines", "words" };

    // What can follow a tag: a search in the names of its files, or in their lines
    private static readonly string[] TagSearchWords = { "filter", "-t" };

    // Where "peek" puts its window
    private static readonly string[] PeekPlaces = { "top", "middle", "bottom", "all" };

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
        // "!3", "!!", "again 3": the command it runs, put on the line to be changed before Enter
        // ("again x": no number, completed as words: "again filter")
        if (ShellSession.AgainNumber(text) is { } number && number != 0)
        {
            var kept = CommandHistory.Of(CommandContext.Shell);
            if (number < 1 || number > kept.Count) return false;
            _before = string.Empty;
            _typed = text;
            _matches = new List<string> { kept[number - 1] };
            return true;
        }

        var (before, word) = LastWord(text);
        _before = before;
        _typed = word;
        // $name: the names of the registers, anywhere on the line
        if (word.StartsWith('$') && !word.Contains('"'))
        {
            // (the file edited by hand since the last command: its names)
            ShellRegisters.Refresh();
            // ($clip when clip has copied a text)
            // (and those set by Blyant that have a value now: $clip when something is copied, $file ...)
            var names = ShellRegisters.All.Keys.Concat(ShellRegisters.Reserved.Where(name => ShellRegisters.Get(name) != null));
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
        // ("word @ 2 " and Tab: "@" is the file and the line, so it counts as two words, as "word notes.md +12 2 ")
        if (ShellSession.TakesAt.Contains(command))
        {
            var file = _words.FindIndex(w => w is not ("-p" or "-s"));
            if (file >= 0 && _words[file] == "@") _words.Insert(file + 1, "+0");
        }

        var (kind, choices) = After(command, _words.Select(w => w.ToLowerInvariant()).ToList());
        _matches = kind switch
        {
            Kind.Path => PathsFitting(word, false),
            Kind.Directory => PathsFitting(word, true),
            Kind.PathOrOptions => word.StartsWith('-')
                ? Fitting(choices, word).Concat(PathsFitting(word, false)).ToList()
                : PathsFitting(word, false),
            Kind.DirectoryOrOptions => word.StartsWith('-')
                ? Fitting(choices, word).Concat(PathsFitting(word, true)).ToList()
                : PathsFitting(word, true),
            Kind.Words => Fitting(choices, word),
            Kind.PathOrWords => Fitting(choices, word).Concat(PathsFitting(word, false)).ToList(),
            Kind.PathThenWords => PathsFitting(word, false).Concat(Fitting(choices, word)).ToList(),
            // (a word with a blank in it in quotes: fitting what is typed after a quote)
            Kind.QuotedWords => Fitting(choices, word.TrimStart('"'))
                .Select(w => w.Any(char.IsWhiteSpace) ? "\"" + w + "\"" : w).ToList(),
            Kind.Names => NamesFitting(choices, word.TrimStart('"'))
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
                // ("ls -a -l notes": the directory after the options; "ls -" and Tab: the options not given yet. The
                // words as typed: "-D" is not an option, as ls takes them)
                return _words.All(word => word is "-a" or "-l" or "-d" or "-s")
                    ? (Kind.DirectoryOrOptions, new[] { "-a", "-l", "-d", "-s" }.Except(_words))
                    : (Kind.None, none);
            case "open":
            case "edit":
                return (position == 0 ? Kind.Path : Kind.None, none);
            case "peek":
                // ("peek bottom": the file peeked last; "peek plot.md middle"; the files first, as with nothing typed
                // the files opened last come first)
                // ("peek back intro": the names of the peeks saved)
                if (position == 1 && after[0].Equals("back", StringComparison.OrdinalIgnoreCase))
                {
                    PeekMarks.Load();
                    return (Kind.Words, PeekMarks.Saved.Select(entry => entry.Name).Where(name => name.Length > 0));
                }

                if (position == 1 && after[0].Equals("save", StringComparison.OrdinalIgnoreCase))
                    return (Kind.Words, new[] { "-d" });
                if (position == 1 && after[0].Equals("saved", StringComparison.OrdinalIgnoreCase))
                    return (Kind.Words, new[] { "filter", "-t" });
                return position switch
                {
                    0 => (Kind.PathThenWords, PeekPlaces.Concat(ShellSession.PeekMarkWords)),
                    1 => (Kind.Words, PeekPlaces),
                    _ => (Kind.None, none)
                };
            case "file":
                // ("file plot.md > $other": ">" after the file; $ and Tab the registers; "file plot.md bottom": at
                // the last line)
                if (position == 1) return (Kind.Words, new[] { ">", "top", "bottom" });
                return (position == 0 ? Kind.Path : Kind.None, none);
            case "line":
                return (position == 0 ? Kind.Words : Kind.None, new[] { "top", "bottom" });
            case "path":
            case "name":
            case "info":
            case "delete":
            case "del":
            case "format":
            case "trim":
            case "trim!":
            case "change":
                return (position == 0 ? Kind.Path : Kind.None, none);
            case "rename":
                // The new name: the name it has now, to change ("rename file1.md " and Tab: "file1.md")
                if (position == 1) return (Kind.QuotedWords, NameOf(_words[0]));
                return (position == 0 ? Kind.Path : Kind.None, none);
            case "words":
            case "word":
            {
                // ("-p" first: words between blanks) The file, the line and the words (typed), then what is done
                var p = position > 0 && _words[0] == "-p" ? 1 : 0;
                if (position == 0) return (Kind.PathOrWords, new[] { "-p" });
                if (command == "word" && position == p + 3)
                    return (Kind.Words, new[] { "replace", "before", "after", "delete", "move", "upper", "lower", "title" });
                // ("move before 6", "move after 11")
                if (command == "word" && position == p + 4 && _words.Count > p + 3 &&
                    _words[p + 3].Equals("move", StringComparison.OrdinalIgnoreCase))
                    return (Kind.Words, new[] { "before", "after" });
                return (position == p ? Kind.Path : Kind.None, none);
            }
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
            {
                // ("-s" first: a section) The file, the lines (typed) or the heading of the section, and the place:
                // a heading of the file, top, bottom, up or down
                var section = position > 0 && _words[0] == "-s" ? 1 : 0;
                if (position == 0) return (Kind.PathOrWords, new[] { "-s" });
                if (section == 1 && position == 2) return (Kind.Names, HeadingsOf(_words[1]));
                if (position == section) return (Kind.Path, none);
                return position - section == 2
                    ? (Kind.Names, HeadingsOf(_words[section]).Concat(new[] { "top", "bottom", "up", "down" }))
                    : (Kind.None, none);
            }
            case "lines":
            {
                // The file, the lines (typed), ">" (typed), the other file, the place in it, and copy or cut
                var arrow = after.ToList().IndexOf(">");
                if (position == 0) return (Kind.Path, none);
                // After the lines (or "+10 5"): ">" for a copy, or what is done to the text in them
                if (arrow < 0)
                {
                    // ("sort -a", "sort -d": after "sort" as the word of what is done, not in a text)
                    var edit = ShellSession.LinesForm(after).Edit;
                    if (edit >= 0 && after[edit] == "sort" && position == edit + 1)
                        return (Kind.Words, new[] { "-a", "-d" });
                    var what = position == 2 || (position == 3 && after[1].StartsWith('+'));
                    return (what ? Kind.Words : Kind.None,
                        new[] { ">", "start", "end", "unstart", "unend", "replace", "remove", "at", "sort", "upper", "lower", "title" });
                }

                if (position == arrow + 1) return (Kind.Path, none);
                if (position == arrow + 2)
                    return (Kind.Names, new[] { "top", "bottom" }.Concat(HeadingsOf(_words[arrow + 1])));
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
                    return (Kind.Words, new[]
                        {
                            "add", "remove", "lists", "new", "rename", "delete", "use", "folder", "subfolder", "move",
                            "undo", "filter", "-l"
                        }
                        .Concat(FileFavorites.ListWords()));
                }

                if (position == 1 && after[0] == "-l") return (Kind.Words, FileFavorites.ListWords().Prepend("filter"));
                if (position == 1 && after[0] == "use") return (Kind.Words, FileFavorites.ListWords());
                if (position == 1 && after[0] == "delete") return (Kind.Words, FileFavorites.ListNames());
                // (a list, or a folder in brackets)
                if (position == 1 && after[0] == "rename")
                    return (Kind.QuotedWords, FileFavorites.ListNames().Concat(FavoriteFolderWords()));
                if (position == 1 && after[0] == "subfolder") return (Kind.QuotedWords, FavoriteFolderWords());
                // (a favorite by its name, a pattern of names, or a folder)
                if (position == 1 && after[0] == "move") return (Kind.Names, FavoriteFolderWords().Concat(FavoriteNames()));
                // ("fav add notes.md top": where it goes)
                if (position == 2 && after[0] == "add")
                    return (Kind.QuotedWords, new[] { "top", "bottom" }.Concat(FavoriteFolderWords()));
                if (position == 2 && after[0] == "move")
                {
                    return (Kind.QuotedWords,
                        new[] { "top", "bottom", "up", "down", "out" }.Concat(FavoriteFolderWords()));
                }

                if (position == 1 && after[0] == "remove") return (Kind.PathOrWords, FavoriteFolderWords());
                return (position == 1 && after[0] == "add" ? Kind.Path : Kind.None, none);
            case "history":
            case "hist":
            case "recent":
            case "rec":
            {
                // The options not given yet, after the options only ("recent -s -n", "hist -l filter draft")
                if (after.Take(position).Any(word => !word.StartsWith('-'))) return (Kind.None, none);
                var options = new[] { "-l", "-s", "-f", "-n" }.Except(after.Take(position));
                return (Kind.Words, command is "recent" or "rec" ? options : options.Append("filter"));
            }
            case "tag":
            {
                // "tag " and Tab: what tag does, then the tags; after add and remove a file, then the tags; after
                // rename and clear the tags
                FileTags.Load();
                var tags = FileTags.All().Select(pair => pair.Tag);
                if (position == 0)
                {
                    return (Kind.Words, new[] { "add", "remove", "rename", "clear", "-l", "filter", "-t" }.Concat(tags));
                }

                // ("tag -l " and Tab: the tags; after a tag, "filter" and "-t")
                var first = after[0] == "-l" ? 1 : 0;
                if (position == first) return (Kind.Words, tags);
                if (position == first + 1 && !TagSearchWords.Contains(after[first]) &&
                    !ShellSession.TagActions.Contains(after[first]))
                {
                    return (Kind.Words, TagSearchWords);
                }

                var action = after[0];
                if (action is "add" or "remove") return position == 1 ? (Kind.Path, none) : position == 2 ? (Kind.Words, tags) : (Kind.None, none);
                return action is "rename" or "clear" && position == 1 ? (Kind.Words, tags) : (Kind.None, none);
            }
            case "replace":
                // ("replace -n -i notes.md ...": the file after the options; "replace -" and Tab: the options)
                return _words.All(word => word is "-n" or "-i")
                    ? (Kind.PathOrOptions, new[] { "-n", "-i" }.Except(_words))
                    : (Kind.None, none);
            case "tags":
                return (position == 0 ? Kind.Words : Kind.None, new[] { "-f", "clear", "filter", "-t" });
            case "bookmarks":
            case "book":
                return (position == 0 ? Kind.Words : Kind.None, new[] { "-l" });
            case "hotlist":
            case "hot":
                if (position == 0) return (Kind.Words, new[] { "add", "remove", "filter" });
                return (position == 1 && after[0] is "add" or "remove" ? Kind.Directory : Kind.None, none);
            case "project":
                return (position == 0 ? Kind.Words : Kind.None, Projects.DisplayNames(Projects.All()));
            case "set":
                return (position == 0 ? Kind.Words : Kind.None, FileIo.ParseSettings(FileIo.SettingsText()).Keys);
            case "clip":
                // The kind, or a path ("clip 3" copies the path)
                if (position == 0) return (Kind.PathOrWords, ClipKinds);
                // ("clip words -p notes.md": the file after -p)
                if (after[0] == "words" && position == 1) return (Kind.PathOrWords, new[] { "-p" });
                if (after[0] == "words" && position == 2 && after[1] == "-p") return (Kind.Path, none);
                return (position == 1 && ClipKinds.Contains(after[0]) ? Kind.Path : Kind.None, none);
            case "reg":
                if (position == 0) return (Kind.Words, new[] { "-d", "-t" }.Concat(ShellRegisters.All.Keys));
                // ("reg notes = ...", "reg notes += ...")
                if (position == 1 && after[0] != "-d") return (Kind.Words, new[] { "=", "+=" });
                return (position == 1 && after[0] == "-d" ? Kind.Words : Kind.None,
                    new[] { "all" }.Concat(ShellRegisters.All.Keys));
            case "again":
                return (position == 0 ? Kind.Words : Kind.None, new[] { "filter", "edit" });
            case "find":
            {
                // The options not given yet, before the name ("find -f -t storm"); after -t the text
                if (after.Take(position).Any(word => !word.StartsWith('-') || word == "-t")) return (Kind.None, none);
                return (Kind.Words, new[] { "-l", "-f", "-b", "-d", "-r", "-t" }.Except(after.Take(position)));
            }
            case "run":
                // (a file of commands, after -y; $ and Tab the registers; "run -" and Tab: -y)
                return _words.All(word => word == "-y")
                    ? (Kind.PathOrOptions, new[] { "-y" }.Except(_words))
                    : (Kind.None, none);
            case "about":
                return (position == 0 ? Kind.Words : Kind.None, new[] { "license" });
            case "help":
            case "h":
                // ("help -" and Tab: -t, to search the rows of help)
                return (position == 0 ? Kind.Words : Kind.None, Commands.Names(CommandContext.Shell).Prepend("-t"));
            default:
                return (Kind.None, none);
        }
    }

    // The folders of the open list of favorites as the shell names them: "[Drafts]", "[Writing/Drafts]"
    private static IEnumerable<string> FavoriteFolderWords()
    {
        FileFavorites.Load();
        var items = FileFavorites.Items;
        return Enumerable.Range(0, items.Count).Where(i => items[i].IsFolder).Select(i =>
            "[" + (items[i].Level > 0 ? items[FileFavorites.Parent(i)].Name + "/" : string.Empty) + items[i].Name + "]")
            .ToList();
    }

    // The names of the favorites of the open list, as "fav" shows them (a directory with "/")
    private static IEnumerable<string> FavoriteNames()
    {
        FileFavorites.Load();
        return FileFavorites.Items.Where(item => !item.IsFolder).Select(item =>
            ShellSession.FavoriteName(item) + (System.IO.Directory.Exists(item.Path) ? "/" : string.Empty)).ToList();
    }

    // The words that start with what is typed, or that a pattern typed fits ("*ch*"), as they are written; a pattern
    // that fits none is a start of a word, as for files
    private static List<string> NamesFitting(IEnumerable<string> words, string typed)
    {
        var list = words.ToList();
        if (!IsPattern(typed)) return Fitting(list, typed);
        var fitting = list.Where(word => PatternFits(typed, word.TrimEnd('/'))).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return fitting.Count > 0 ? fitting : Fitting(list, typed);
    }

    // The name of a file or directory as typed (a row of the last list, $file, a path), for "rename"; none for a
    // path with characters a path cannot have
    private IEnumerable<string> NameOf(string typed)
    {
        try
        {
            var path = FileOf?.Invoke(typed) ?? _fullPath(typed.Trim('"'));
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            return name.Length > 0 ? new[] { name } : Enumerable.Empty<string>();
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Enumerable.Empty<string>();
        }
    }

    // The names of the headings of a file (for "shift"); none if it cannot be read
    private IEnumerable<string> HeadingsOf(string typed)
    {
        try
        {
            var path = FileOf?.Invoke(typed) ?? _fullPath(typed.Trim('"'));
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
                ? PatternFits(start, entry.Name)
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

    /// <summary>
    /// A pattern ("*" any characters, "?" one) fits a whole text, letter case alike. A "\" in it is the character
    /// itself (as in a path on Windows, or a name on Linux), not the escape character of .NET's patterns
    /// </summary>
    internal static bool PatternFits(string pattern, string text) =>
        System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern.Replace("\\", "\\\\"), text);

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
                // (in quotes, "" is a quote: the word goes on)
                if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                {
                    i++;
                    continue;
                }

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
