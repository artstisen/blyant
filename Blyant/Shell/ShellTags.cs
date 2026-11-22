namespace Blyant;

/// <summary>
/// "tag" and "tags": files given tags, to find them again by the tag ("tag add notes.md draft", then "tag draft": the
/// files with it, as a numbered list). The tags are kept in tags.txt (FileTags). $tag is the tag worked with: set by
/// "tag add" and "tag draft", and by the file worked on when that file has tags but not $tag (the tag given to it last)
/// </summary>
internal sealed partial class ShellSession
{
    // The tag worked with ($tag; null: none yet). Kept while Blyant runs, also when the shell is left for the full screen
    private static string? _tag;

    /// <summary>
    /// No tag worked with (a new Blyant, or a test)
    /// </summary>
    internal static void ForgetTag() => _tag = null;

    /// <summary>
    /// The words after "tag" that are not tags (Tab completes tags after any other word)
    /// </summary>
    internal static readonly HashSet<string> TagActions = new(StringComparer.OrdinalIgnoreCase) { "add", "remove", "rename", "clear" };

    // "tag draft": the files with the tag (it is then $tag); "tag" alone: those of $tag; "tag -l draft": the full paths.
    // "tag add", "tag remove", "tag rename" and "tag clear" change the tags
    private void TagCommand(string argument)
    {
        var words = Words(argument);
        // ("tag -l draft": the full paths; only first, so a file named "-l" can be given after add)
        var full = words.Count > 0 && words[0] == "-l";
        if (full) words.RemoveAt(0);
        // "tag draft filter chapter", "tag draft -t storm": the files of the tag with a text in the name, or in a line
        // ("tag -t storm", "tag filter chapter": those of $tag)
        if (words.Count == 0 || !TagActions.Contains(words[0]))
        {
            // ("tag notes.md draft": a file and a tag, add left out)
            if (words.Count == 2 && FileTags.IsName(words[1]) && !IsFilterWord(words[1]) && words[1] != "-t" &&
                File.Exists(PathOf(words[0])))
            {
                Error(Lang.F("To give a file a tag: tag add {0} {1}", words[0], words[1]));
                return;
            }

            if (TagSearch(full ? AfterWords(argument, 1) : argument, true) is not { } search) return;
            if (search.Tag is null && _tag is null)
            {
                Error(Lang.T("No tag yet: tag add notes.md draft, then tag draft"));
                return;
            }

            ListTag(search.Tag ?? _tag!, full, search.Filter, search.Text);
            return;
        }

        switch (words[0].ToLowerInvariant())
        {
            case "add":
                TagAdd(words);
                return;
            case "remove":
                TagRemove(words);
                return;
            case "rename":
                TagRename(words);
                return;
            case "clear":
                TagClear(words);
                return;
        }
    }

    // The files with a tag, numbered (the last list: "open 2"); the tag is then $tag. "filter": only those with a text
    // in the path, or whose name fits a pattern; "text": only those with it in a line, and the line found
    private void ListTag(string tag, bool full, string? filter = null, string? text = null)
    {
        FileTags.Load();
        var files = FileTags.FilesWith(tag);
        if (files.Count == 0)
        {
            Error(Lang.F("No file has the tag {0} (tags lists them)", tag));
            return;
        }

        _tag = tag;
        var names = Names(files.Select(path => (path, false)).ToList(), full);
        var rows = files.Select((path, i) => (new Item(path, false), names[i])).ToList();
        if (filter is null && text is null) List(rows, string.Empty);
        else SearchTagged(files, rows, names, filter, text, !full);
    }

    // What follows "tag" or "tags" when it is a search: a tag (null: none given), the text of "filter" (null: none),
    // and the text after "-t" (null: none). Null, and told, when it is not one ("filter" or "-t" without a text)
    // "filterCanBeTag": "tag filter" alone is a tag of that name when there is one (made before "filter" was a word
    // of tag); after "tags" it never is
    private (string? Tag, string? Filter, string? Text)? TagSearch(string argument, bool filterCanBeTag)
    {
        var (before, text) = TextOption(argument);
        if (text == string.Empty)
        {
            Error(Lang.T("Give a text after -t: tags -t storm, tag draft -t storm"));
            return null;
        }

        var words = Words(before);
        string? tag = null;
        // ("filter" alone is a tag of that name when there is one: made before "filter" was a word of tag)
        if (words.Count > 0 &&
            (!IsFilterWord(words[0]) || (filterCanBeTag && words.Count == 1 && text is null && FilterIsATag())))
        {
            if (TagName(words[0]) is not { } named) return null;
            tag = named;
            words.RemoveAt(0);
            before = AfterWords(before, 1);
        }

        if (words.Count == 0) return (tag, null, text);
        if (!IsFilterWord(words[0]))
        {
            Error(Lang.T("Give one tag: tag draft (tag add notes.md draft gives a file a tag)"));
            return null;
        }

        var typedFilter = Rest(before.Trim()).Trim();
        var filter = Unquoted(typedFilter) ?? typedFilter;
        if (filter.Length == 0)
        {
            Error(Lang.T("Give a text: tags filter chapter, tag draft filter chapter (or *.md)"));
            return null;
        }

        return (tag, filter, text);
    }

    // The rows of tagged files that fit a filter (a text in the path, or a pattern for the name) and have a text in a
    // line (the line found instead of the rest of the row, so "open 3" opens the file there), numbered as in the whole
    // list, as recent -t and hist filter show them. Only text files are read, and only those the filter takes.
    // "lineUp": the lines found start in one column (not after full paths)
    private void SearchTagged(IReadOnlyList<string> paths, List<(Item Item, string Text)> rows, IReadOnlyList<string> names,
        string? filter, string? text, bool lineUp)
    {
        var fits = paths.Select(path => filter is null || Fits(filter, path, null)).ToList();
        if (text is null)
        {
            Filtered(rows, i => fits[i], Lang.F("No files with {0} in them", filter!));
            return;
        }

        var (shown, found) = FoundRows(paths, rows, names, text, lineUp, i => fits[i]);
        Filtered(shown, i => found[i], Lang.F("No file has {0} in a line", text));
    }

    // "tag add notes.md draft", "tag add 2,4,7 draft", "tag add 2-5 draft", "tag add @ draft": the files get the tag,
    // and it is $tag. The last list stays, so more rows of it can be given the tag
    private void TagAdd(List<string> words)
    {
        if (words.Count != 3)
        {
            Error(Lang.T("Give the files and the tag: tag add notes.md draft (or rows: 2,4 or 2-5)"));
            return;
        }

        if (TagName(words[2], true) is not { } tag || FilesOf(words[1], true) is not { } files) return;
        // (a tab or a line break in the name would break the line of tags.txt)
        if (files.FirstOrDefault(file => !FileTags.CanHaveTags(file.Path)) is { } odd)
        {
            Error(Lang.F("{0} has a tab or a line break in its name: it cannot have tags", odd.Path));
            return;
        }

        FileTags.Load();
        foreach (var file in files) FileTags.Add(file.Path, tag);
        var result = FileTags.Save();
        if (Failed(result)) return;

        // (the paths, never the tag: a name the user gave)
        foreach (var file in files) EventLog.Add(DialogText.LogFile + "Tag add: " + file.Path);
        _tag = tag;
        Line(files.Count == 1 ? Lang.F("Tagged {0}: {1}", tag, files[0].Path) : Lang.F("Tagged {0}: {1} files", tag, files.Count));
    }

    // "tag remove notes.md draft": the tag taken off the files; "tag remove notes.md": all tags of the files. A file
    // that is not there any more can be given
    private void TagRemove(List<string> words)
    {
        if (words.Count is < 2 or > 3)
        {
            Error(Lang.T("Give the files, and a tag to take off only that one: tag remove notes.md draft"));
            return;
        }

        string? tag = null;
        if (words.Count == 3 && TagName(words[2]) is null) return;
        if (words.Count == 3) tag = words[2].ToLowerInvariant();
        if (FilesOf(words[1], false) is not { } files) return;
        FileTags.Load();
        var removed = files.Where(file => FileTags.Remove(file.Path, tag)).ToList();
        if (removed.Count == 0)
        {
            Line(tag is null ? Lang.T("Nothing taken off: no tags there") : Lang.F("Nothing taken off: no tag {0} there", tag));
            return;
        }

        var result = FileTags.Save();
        if (Failed(result)) return;

        foreach (var file in removed) EventLog.Add(DialogText.LogFile + "Tag remove: " + file.Path);
        if (removed.Count == 1)
        {
            Line(tag is null
                ? Lang.F("Tags taken off: {0}", removed[0].Path)
                : Lang.F("Tag {0} taken off: {1}", tag, removed[0].Path));
        }
        else
        {
            Line(tag is null
                ? Lang.F("Tags taken off {0} files", removed.Count)
                : Lang.F("Tag {0} taken off {1} files", tag, removed.Count));
        }
    }

    // "tag rename draft final": the files with draft have final instead
    private void TagRename(List<string> words)
    {
        if (words.Count != 3)
        {
            Error(Lang.T("Give the tag and the new name: tag rename draft final"));
            return;
        }

        if (TagName(words[1]) is not { } tag || TagName(words[2], true) is not { } name) return;
        // (the same name: renamed, the files would lose it)
        if (tag == name)
        {
            Error(Lang.F("The tag is called {0} already", tag));
            return;
        }

        FileTags.Load();
        if (FileTags.Rename(tag, name) == 0)
        {
            Error(Lang.F("No file has the tag {0} (tags lists them)", tag));
            return;
        }

        var result = FileTags.Save();
        if (Failed(result)) return;

        EventLog.Add(DialogText.LogFile + "Tag rename");
        if (_tag == tag) _tag = name;
        Line(Lang.F("Tag {0} is now {1}", tag, name));
    }

    // "tag clear draft": the tag taken off all files, asked first (it cannot be undone)
    private void TagClear(List<string> words)
    {
        if (words.Count != 2)
        {
            Error(Lang.T("Give the tag: tag clear draft"));
            return;
        }

        if (TagName(words[1]) is not { } tag) return;
        FileTags.Load();
        var count = FileTags.FilesWith(tag).Count;
        if (count == 0)
        {
            Error(Lang.F("No file has the tag {0} (tags lists them)", tag));
            return;
        }

        if (!ChoiceBox.ConfirmGuarded(Lang.T("Clear tag"), Lang.F("Take the tag {0} off all files that have it ({1})?", tag, count),
                Lang.T("The files stay as they are.")))
        {
            Line(Lang.T("Nothing was changed"));
            return;
        }

        FileTags.ClearTag(tag);
        var result = FileTags.Save();
        if (Failed(result)) return;

        EventLog.Add(DialogText.LogFile + "Tag clear");
        if (_tag == tag) _tag = null;
        Line(Lang.F("Tag {0} taken off all files", tag));
    }

    // "tags": the tags, and how many files have each; "tags -f": the files with tags, and their tags (numbered: "open
    // 2"); "tags clear": all tags taken off all files, asked first
    private void TagsCommand(string argument)
    {
        var words = Words(argument);
        FileTags.Load();
        // "tags filter chapter", "tags -t storm": the files with any tag, with a text in the name, or in a line
        if (words.Count > 0 && (words[0].Equals("filter", StringComparison.OrdinalIgnoreCase) || words.Contains("-t")))
        {
            if (TagSearch(argument, false) is not { } search) return;
            if (search.Tag is not null)
            {
                Error(Lang.T("tags lists the tags, tags -f the files with tags, and tags clear takes them all off"));
                return;
            }

            var tagged = FileTags.Tagged();
            if (tagged.Count == 0)
            {
                Line(Lang.T("No tags yet: tag add notes.md draft"));
                return;
            }

            var (rows, names) = TaggedRows(tagged);
            SearchTagged(tagged.Select(entry => entry.Path).ToList(), rows, names, search.Filter, search.Text, true);
            return;
        }

        if (words.Count == 1 && words[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            ClearAllTags();
            return;
        }

        if (words.Count > 1 || (words.Count == 1 && words[0] != "-f"))
        {
            // ("tags draft": tag was meant, a letter away)
            if (words.Count == 1 && FileTags.IsName(words[0]))
                Error(Lang.F("tag {0} lists the files with the tag (tags lists the tags)", words[0].ToLowerInvariant()));
            else Error(Lang.T("tags lists the tags, tags -f the files with tags, and tags clear takes them all off"));
            return;
        }

        if (words.Count == 1)
        {
            List(TaggedRows(FileTags.Tagged()).Rows, Lang.T("No tags yet: tag add notes.md draft"));
            return;
        }

        var all = FileTags.All();
        if (all.Count == 0)
        {
            Line(Lang.T("No tags yet: tag add notes.md draft"));
            return;
        }

        var tagWidth = all.Max(pair => TextMetrics.WidthOf(pair.Tag));
        foreach (var (tag, count) in all)
        {
            Line("  " + Padded(tag, tagWidth) + "  " + (count == 1 ? Lang.T("1 file") : Lang.F("{0} files", count)));
        }
    }

    // "tags clear": all tags taken off all files, asked first
    private void ClearAllTags()
    {
        var count = FileTags.Tagged().Count;
        if (count == 0)
        {
            Line(Lang.T("No tags yet: tag add notes.md draft"));
            return;
        }

        if (!ChoiceBox.ConfirmGuarded(Lang.T("Clear tags"), Lang.F("Take all tags off all files ({0})?", count),
                Lang.T("The files stay as they are.")))
        {
            Line(Lang.T("Nothing was changed"));
            return;
        }

        FileTags.ClearTag(null);
        var result = FileTags.Save();
        if (Failed(result)) return;

        EventLog.Add(DialogText.LogFile + "Tags clear");
        _tag = null;
        Line(Lang.T("All tags taken off"));
    }

    // A name for a tag, in small letters (null, and told: not one; "add" and the other words of tag are not names)
    // "isNew": a name given to a tag (add, the new name of rename), which cannot be "filter" either: "tag filter"
    // would search. A tag of that name made before can still be listed, renamed, removed and cleared
    private string? TagName(string word, bool isNew = false)
    {
        if (!FileTags.IsName(word))
        {
            Error(Lang.F("Not a name for a tag: {0} (letters, digits, - and _)", word));
            return null;
        }

        if (TagActions.Contains(word) || (isNew && IsFilterWord(word)))
        {
            Error(Lang.F("{0} is a word of the tag command: give the tag another name", word.ToLowerInvariant()));
            return null;
        }

        return word.ToLowerInvariant();
    }

    // The rows of the files with tags (tags -f): the name (with the folder where names are alike) and the tags, lined
    // up; and the names
    private (List<(Item Item, string Text)> Rows, List<string> Names) TaggedRows(
        IReadOnlyList<(string Path, IReadOnlyList<string> Tags)> tagged)
    {
        var names = Names(tagged.Select(entry => (entry.Path, false)).ToList(), false);
        var width = names.Count == 0 ? 0 : names.Max(TextMetrics.WidthOf);
        return (tagged.Select((entry, i) => (new Item(entry.Path, false),
            Padded(names[i], width) + "  " + string.Join(", ", entry.Tags))).ToList(), names);
    }

    // "filter": the word of a search by the path, after "tag" and "tags"
    private static bool IsFilterWord(string word) => word.Equals("filter", StringComparison.OrdinalIgnoreCase);

    // A tag named "filter" is there (given before it was a word of tag)
    private static bool FilterIsATag()
    {
        FileTags.Load();
        return FileTags.FilesWith("filter").Count > 0;
    }

    // The files a word gives: rows of the last list ("2,4,7", "2-5"), "@" (the file worked on), or a file (a name, a
    // row, $file put in). "there": they must be there (tag add); tag remove takes one that is gone. Null, and told:
    // not files
    private List<Item>? FilesOf(string word, bool there)
    {
        if (word == "@")
        {
            if (_file is null)
            {
                Error(Lang.T("@ is the file and the line of the last command: none yet (words notes.md 12)"));
                return null;
            }

            word = _file;
        }

        var words = new List<string> { word };
        if (word.Length > 1 && word.All(c => char.IsAsciiDigit(c) || c is ',' or '-') && word.Any(char.IsAsciiDigit) &&
            !File.Exists(PathOf(word)))
        {
            if (RowsOf(word) is not { } rows)
            {
                Error(Lang.F("Not rows: {0} (as 2,4,7 or 2-5)", word));
                return null;
            }

            words = rows.Select(row => row.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        }

        var files = new List<Item>();
        foreach (var each in words)
        {
            if ((there ? Existing(each) : Resolve(each, false)) is not { } item) return null;
            if (item.IsDirectory || System.IO.Directory.Exists(item.Path))
            {
                Error(Lang.F("Not a file: {0}", item.Path));
                return null;
            }

            if (!files.Any(file => FileOperations.NameComparer.Equals(file.Path, item.Path))) files.Add(item);
        }

        return files;
    }

    // "2,4,7", "2-5", "2,5-7": the rows (null: not rows, or one the last list does not have)
    private List<int>? RowsOf(string word)
    {
        var rows = new List<int>();
        foreach (var part in word.Split(','))
        {
            var dash = part.IndexOf('-');
            var (from, to) = dash < 0 ? (part, part) : (part[..dash], part[(dash + 1)..]);
            if (!int.TryParse(from, out var first) || !int.TryParse(to, out var last) || first < 1 || last < first ||
                last > LastList.Count)
            {
                return null;
            }

            for (var row = first; row <= last; row++) rows.Add(row);
        }

        return rows;
    }

    // $tag follows the file worked on: when the command made another file $file, and that file has tags but not $tag,
    // the tag given to it last is $tag (told). A file with $tag, or without tags, leaves it. Not after "tag" and
    // "tags", which set it themselves
    private void TagFollowsFile(string? before, string command)
    {
        if (command is "tag" or "tags" || _file is null ||
            (before is not null && FileOperations.NameComparer.Equals(before, _file)))
        {
            return;
        }

        FileTags.Load();
        var tags = FileTags.TagsOf(_file);
        if (tags.Count == 0 || (_tag is not null && tags.Contains(_tag))) return;
        _tag = tags[^1];
        Line("  $tag = " + _tag); // English: a name set, as reg shows a register
    }
}
