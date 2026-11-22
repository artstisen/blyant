using Blyant.Format.Core;

namespace Blyant;

/// <summary>
/// The commands of the shell that work on the text of a file without opening it (format, trim), tell about a file
/// (info), and show or change the settings (set). format and trim are the commands of the editor, run on the text of
/// the file by an editor without a screen, so they do the same as ":format" and ":trim" there
/// </summary>
internal sealed partial class ShellSession
{
    // "format page.html", "trim notes.md", "trim! notes.md": the command of the editor on the text of the file, which
    // is saved when it changed (its line endings and byte order mark kept). Only a file in UTF-8 (the editor saves
    // in UTF-8: it is opened there to be changed to it)
    private void ChangeText(string argument, string command)
    {
        var words = Words(argument);
        if (words.Count != 1)
        {
            Error(Lang.F("Give one file: {0} 3", command));
            return;
        }

        if (EditableFile(words[0]) is not { } editable) return;
        var (item, file) = editable;

        // An editor without a screen, of the kind of the file (Markdown keeps its line breaks when trimmed)
        var name = Path.GetFileName(item.Path);
        var editor = new EditorEngine(file.Text, 80, 24, false)
        {
            CompletionSyntax = SyntaxHighlighter.TypeOfFile(name, file.Text),
            FormatKind = SourceDocument.DetectKind(name)
        };
        editor.RunCommandLine(command);
        var text = editor.GetText();
        if (editor.MessageIsError)
        {
            Error(Lang.F("{0}: {1}", item.Path, editor.Message));
            return;
        }

        if (text == file.Text)
        {
            LastList = new List<Item> { item };
            Line(command == "format"
                ? Lang.F("{0}: Already formatted", item.Path)
                : Lang.F("{0}: {1}", item.Path, editor.Message));
            return;
        }

        if (!SavedEdit(file, text, command == "format" ? "Format" : "Trim", item.Path)) return;

        LastList = new List<Item> { item };
        Line(command == "format" ? Lang.F("{0}: Formatted", item.Path) : Lang.F("{0}: {1}", item.Path, editor.Message));
    }

    // "insert notes.md top Buy milk", "insert 3 bottom - one more", "insert notes.md +12 A new line": a line of text put
    // in a file without opening it: before the first line, after the last, or as line 12 (before the line that was
    // there; past the end: after the last). The text is all that follows the place, as typed. The file is saved with
    // its line endings and byte order mark; only a file in UTF-8, and not one Blyant uses. The file is then the last
    // list, at the line put in ("open 1")
    private void Insert(string argument)
    {
        // The file (a word, or in quotes), the place, and the rest of the line as the text
        var words = Words(argument);
        if (words.Count < 2)
        {
            Error(Lang.T("Give a file, where, and the text: insert notes.md bottom Buy milk"));
            return;
        }

        var rest = AfterWords(argument, 2);
        var at = InsertPlace(words[1]);
        if (at is null)
        {
            Error(Lang.F("Not a place in a file: {0} (top, bottom or +12)", words[1]));
            return;
        }

        if (EditableFile(words[0]) is not { } editable) return;
        var (item, file) = editable;

        // (more than one line: $clip of lines copied)
        var added = LinesPutIn(rest);
        var count = added.Count;
        var (text, number) = WithLinesAt(file.Text, added, at.Value - 1);
        // (the path, never the text)
        if (!SavedEdit(file, text, "Insert", item.Path + ":" + number)) return;

        LastList = new List<Item> { new(item.Path, false, (number, 1)) };
        // ("open 1" at the first line put in; @ at the last, so the next "insert @" goes after them)
        _line = number + Math.Max(count, 1) - 1;
        TellPutIn(item.Path, number, count);
    }

    // What was put in told: "notes.md: line 5 put in", "notes.md: lines 5-7 put in"
    private void TellPutIn(string path, int first, int count) =>
        Line(count <= 1
            ? Lang.F("{0}: line {1} put in", path, first)
            : Lang.F("{0}: lines {1}-{2} put in", path, first, first + count - 1));

    // The place insert and input take: "top" (line 1), "bottom" (after the last), "+12" (as line 12); null for another
    private static int? InsertPlace(string word)
    {
        var where = word.ToLowerInvariant();
        return where switch
        {
            "top" => 1,
            "bottom" => int.MaxValue,
            _ => IsPlusLine(where, out var line) ? line : null
        };
    }

    // A number written with digits only ("12"; not "+12", " 12" or "1,2", which int.Parse takes too)
    internal static bool IsNumber(string text, out int number) =>
        int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
            out number);

    // "+12": line 12 (from 1); false for another word ("++12", "+ 12", "+0")
    internal static bool IsPlusLine(string word, out int line)
    {
        line = 0;
        return word.Length > 1 && word[0] == '+' && IsNumber(word[1..], out line) && line > 0;
    }

    // The line that ends input: only "." (blanks after it too; one before it is text: " ." is put in)
    internal static bool IsInputEnd(string line) => line.TrimEnd() == ".";

    // "input", "input @", "input notes.md bottom", "input notes.md +12": the lines typed put in the file one after
    // another, as in the line editor ed: alone (or "@", or "a" as in ed), after $line, as insert @ puts a line; "input
    // before" (or "i" as in ed) before $line, which goes down (see WithAt); a line with only "." ends
    // it, and so does the end of the input (Ctrl+D). Each line is written when it is typed, so none is lost; all are one
    // step of undo. The file is $file from the start, and the prompt is "$file" and the number the next line gets
    // ("$file:14> ": short, whatever the name of the file; "echo $file" tells the path). The lines are put in as
    // typed: no $name, and kept nowhere else (not in the commands typed, nor in the log). In a run, the lines after
    // "input" in the script; from a file or a pipe, the lines after it. As insert: the line endings and byte order mark
    // of the file kept, only a file in UTF-8, and not one Blyant uses; the file is then the last list, at the first
    // line put in, and $line is the last
    private void Input(string argument)
    {
        // ("input" alone and "input @" are $file after $line, put in before: see WithAt)
        var words = Words(argument);
        var place = words.Count == 2 ? InsertPlace(words[1]) : null;
        if (place is null)
        {
            Error(Lang.T("Give a file and where, or nothing: input, input notes.md bottom, input notes.md +12"));
            return;
        }

        if (EditableFile(words[0]) is not { } editable) return;
        var (item, file) = editable;
        var next = (int)Math.Min(place.Value, LinesOfText(file.Text).Count + 1L);
        // (the file the lines go in is $file, at the line before them, from the start: the prompt names $file)
        _file = Path.GetFullPath(item.Path);
        _line = next - 1;
        var first = next;
        var count = 0;
        // English: "$file" is the name of the shell, the same in all languages
        while (ReadText("$file:" + next.ToString(System.Globalization.CultureInfo.InvariantCulture) + "> ") is { } typed)
        {
            if (IsInputEnd(typed)) break;
            // (the file as it was read, or as input wrote it last: changed meanwhile, in the editor or by another
            // program, or gone, it is not written over)
            if (Changed(item.Path, file))
            {
                Error(Lang.F("{0} was changed meanwhile: the line is not put in, and input stops", item.Path));
                break;
            }

            var (text, number) = WithLinesAt(file.Text, new List<string> { typed }, next - 1);
            // (the path and the line, never the text)
            if (!SavedEdit(file, text, "Input", item.Path + ":" + number)) break;
            if (count == 0) first = number;
            count++;
            next = number + 1;
        }

        if (count == 0)
        {
            Line(Lang.T("Nothing put in"));
            return;
        }

        LastList = new List<Item> { new(item.Path, false, (first, 1)) };
        // (@ at the last line put in, so the next "input" goes on after it)
        _line = first + count - 1;
        TellPutIn(item.Path, first, count);
    }

    // "change notes.md +12 A new line", "change notes.md 12-15 One line instead": the lines become the text (one line;
    // more with $clip of lines copied);
    // "change notes.md 12-15" without a text: the lines are deleted, after they are shown and the question is
    // answered yes (No selected; from a file only with --yes). As insert: the line endings and byte order mark of the
    // file are kept, only a file in UTF-8 and not one Blyant uses, and the file is then the last list at the place
    private void ChangeLines(string argument)
    {
        var words = Words(argument);
        if (words.Count < 2)
        {
            Error(Lang.T("Give a file, the lines, and the text: change notes.md 12-15 New line"));
            return;
        }

        if (!LinesToChange(words[1], out var first, out var last))
        {
            Error(Lang.F("Not lines: {0} (as +12, or 12-15)", words[1]));
            return;
        }

        var text = AfterWords(argument, 2);
        if (EditableFile(words[0]) is not { } editable) return;
        var (item, file) = editable;

        var lines = LinesOfText(file.Text);
        var count = lines.Count;
        if (first > count)
        {
            Error(Lang.F("{0} has {1} lines", item.Path, count));
            return;
        }

        last = Math.Min(last, count);
        var delete = text.Length == 0;
        if (delete)
        {
            // The lines that would go, and the question (they are back only with undo, while Blyant runs). Between the
            // markers of peek (the setting shellmarkers)
            var width = last.ToString().Length;
            if (Shell.Markers) Line((first > 1 ? AboveMarker : TopMarker) + Path.GetFileName(item.Path));
            for (var i = first; i <= last; i++) Row(i, width, lines[i - 1]);
            if (Shell.Markers) Line((last < count ? BelowMarker : EndMarker) + Lang.F("lines {0}-{1} of {2}", first, last, count));
            var question = first == last
                ? Lang.F("Delete line {0} of {1}?", first, Path.GetFileName(item.Path))
                : Lang.F("Delete lines {0}-{1} of {2}?", first, last, Path.GetFileName(item.Path));
            if (!ChoiceBox.Confirm(Lang.T("Delete lines"), question, string.Empty, true))
            {
                Line(Lang.T("Nothing was deleted"));
                return;
            }

            // (changed by another program while the question was asked: the lines shown may not be those now)
            if (Changed(item.Path, file))
            {
                Error(Lang.F("{0} was changed meanwhile: nothing was deleted", item.Path));
                return;
            }
        }

        var (changed, at) = WithLinesChanged(file.Text, first, last, delete ? null : text);
        // (the path and the lines, never the text)
        if (!SavedEdit(file, changed, delete ? "Delete lines" : "Change lines", item.Path + ":" + first + "-" + last)) return;

        LastList = new List<Item> { new(item.Path, false, (at, 1)) };
        if (delete)
        {
            Line(first == last
                ? Lang.F("{0}: line {1} deleted", item.Path, first)
                : Lang.F("{0}: lines {1}-{2} deleted", item.Path, first, last));
        }
        else
        {
            Line(first == last
                ? Lang.F("{0}: line {1} changed", item.Path, first)
                : Lang.F("{0}: lines {1}-{2} changed", item.Path, first, last));
            // ("change notes.md 4 replace old new": lines was meant, most likely, as the names are near. Done, as a
            // line can start with such a word, but told)
            if (LinesMeantByChange(text)) Line(Lang.T("change puts the text in place of the whole line: lines changes a text in it (undo takes it back)"));
        }
    }

    // The words of lines that take a text after them
    private static readonly string[] LinesTextEdits = { "start", "end", "unstart", "unend", "replace", "remove", "at" };

    /// <summary>
    /// True when the text of change starts with a word of lines that takes a text, in lower case as lines is typed,
    /// with more after it ("replace old new", "end (by Friday)"): lines was meant, most likely
    /// </summary>
    internal static bool LinesMeantByChange(string text)
    {
        var trimmed = text.TrimStart();
        var blank = trimmed.IndexOfAny([' ', '\t']);
        if (blank <= 0 || !LinesTextEdits.Contains(trimmed[..blank], StringComparer.Ordinal)) return false;
        var rest = Words(trimmed[blank..]);
        // ("at 10 text" and "replace old new" only in the form lines takes, so "at last, peace" or "replace the
        // window" are lines of text; start, end, unstart, unend and remove take any text)
        return trimmed[..blank] switch
        {
            "at" => rest.Count >= 2 && rest[0].Length > 0 && rest[0].All(char.IsAsciiDigit),
            "replace" => rest.Count == 2,
            _ => rest.Count > 0
        };
    }

    // The text of a file found (not a directory), as it is on the disk: null, and told, when it cannot be read
    private TextFile? ReadableFile(Item item)
    {
        if (item.IsDirectory)
        {
            Error(Lang.F("Not a file: {0}", item.Path));
            return null;
        }

        try
        {
            return TextFile.Load(item.Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
            return null;
        }
    }

    // The text file of a word (a row of the last list, or a path) to change without opening it: there, in UTF-8 (the
    // editor saves in UTF-8: it is opened there to be changed to it), with one kind of line endings, and not one
    // Blyant uses. Null (and told) if not
    private (Item Item, TextFile File)? EditableFile(string word)
    {
        if (Existing(word) is not { } item || Protected(item) || ReadableFile(item) is not { } file) return null;
        if (!file.IsUtf8)
        {
            Error(Lang.F("{0} is not in UTF-8 ({1}): the editor can open it and save in UTF-8", item.Path,
                file.EncodingName));
            return null;
        }

        // (saved, every line would get the one kind most lines have: the file is not as it was, also after undo)
        if (file.MixedLineEndings)
        {
            Error(Lang.F("{0} has mixed line endings: the editor can open it and set one kind",
                item.Path));
            return null;
        }

        return (item, file);
    }

    // The file is not as it was read (or cannot be read now)
    private static bool Changed(string path, TextFile file)
    {
        try
        {
            return TextFile.Load(path).Text != file.Text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return true;
        }
    }

    /// <summary>
    /// The lines change works on: "+12" (line 12) or "12-15" (also "+12-15")
    /// </summary>
    internal static bool LinesToChange(string text, out int first, out int last)
    {
        first = last = 0;
        if (text.StartsWith('+')) text = text[1..];
        var dash = text.IndexOf('-');
        if (dash < 0) return IsNumber(text, out first) && first > 0 && (last = first) > 0;
        return IsNumber(text[..dash], out first) && IsNumber(text[(dash + 1)..], out last) && first > 0 &&
               last >= first;
    }

    // The lines of a text (lines end in "\n"; the last line break does not start another line)
    internal static List<string> LinesOfText(string text)
    {
        text = TextBuffer.Normalize(text);
        if (text.Length == 0) return new List<string>();
        return (text.EndsWith('\n') ? text[..^1] : text).Split('\n').ToList();
    }

    /// <summary>
    /// A text with the lines first to last (from 1; there) changed into a line, or taken out (null), and the line the
    /// change is at now. A text that ends with a line break keeps it
    /// </summary>
    internal static (string Text, int Line) WithLinesChanged(string text, int first, int last, string? line)
    {
        text = TextBuffer.Normalize(text);
        var endsWithBreak = text.EndsWith('\n');
        var lines = LinesOfText(text);
        lines.RemoveRange(first - 1, last - first + 1);
        if (line is not null) lines.InsertRange(first - 1, LinesPutIn(line));
        if (lines.Count == 0) return (string.Empty, 1);
        return (JoinedLines(lines, endsWithBreak), Math.Min(first, lines.Count));
    }

    /// <summary>
    /// Lines joined into a text with "\n", with a line break at the end when the text had one, and always when the
    /// last line is blank: without it, the blank line would only be the break the line before it ends with, and lost
    /// </summary>
    internal static string JoinedLines(IReadOnlyList<string> lines, bool endsWithBreak) =>
        string.Join("\n", lines) + (endsWithBreak || (lines.Count > 0 && lines[^1].Length == 0) ? "\n" : string.Empty);

    /// <summary>
    /// A text with a line put in as line "at" (1: before the first; past the end: after the last), and the number it
    /// got. The lines end in "\n"; a text that ends with a line break keeps it, and one that does not gets none after
    /// a line put in at the end. A text of more than one line ($clip) is put in as its lines, from "at"
    /// </summary>
    internal static (string Text, int Line) WithLine(string text, string line, int at) =>
        WithLinesAt(text, LinesPutIn(line), at - 1);

    /// <summary>
    /// The lines a text put in a file becomes: one, or the lines of a text of more than one line ($clip), each line
    /// break starting a line, so blank lines at its end are lines too ($clip of a whole file has no line break at its
    /// end: clip leaves it out)
    /// </summary>
    internal static List<string> LinesPutIn(string text) => TextBuffer.Normalize(text).Split('\n').ToList();

    // What follows the first words of a text (words in quotes as one), as typed (one blank after them left out)
    private static string AfterWords(string text, int count)
    {
        var i = 0;
        for (var word = 0; word < count; word++)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            var quoted = false;
            while (i < text.Length && (quoted || !char.IsWhiteSpace(text[i])))
            {
                if (text[i] == '"') quoted = !quoted;
                i++;
            }
        }

        if (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        return i < text.Length ? text[i..] : string.Empty;
    }

    // "info 3": the name, kind, size, dates and path of a file or directory, as ":info" in the file browser, and for a
    // text file its characters, words and lines, its encoding and line endings
    private void Info(string argument)
    {
        var words = Words(argument);
        var item = words.Count == 0 ? new Item(Directory, true) : words.Count == 1 ? Existing(words[0]) : null;
        if (words.Count > 1) Error(Lang.T("Give one file or directory: info 3"));
        if (item is null) return;
        List<(string Label, string Value)> rows;
        try
        {
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(item.Path));
            rows = FileExplorer.InfoRows(name.Length > 0 ? name : item.Path, item.Path, item.IsDirectory,
                item.IsDirectory ? 0 : new FileInfo(item.Path).Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
            return;
        }

        if (!item.IsDirectory && SyntaxHighlighter.IsTextFile(Path.GetFileName(item.Path)))
        {
            try
            {
                var file = TextFile.Load(item.Path);
                // (before the path, the last row)
                rows.InsertRange(Math.Max(0, rows.Count - 1), new[]
                {
                    (Lang.T("Characters:"), Tools.CharacterCount(file.Text).ToString()),
                    (Lang.T("Words:"), Tools.WordCount(file.Text).ToString()),
                    (Lang.T("Lines:"), Tools.LineCount(file.Text).ToString()),
                    (Lang.T("Encoding:"), file.EncodingName),
                    (Lang.T("Line endings:"), TextFile.LabelOf(file.LineEnding))
                });
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                // Not read as text (too large, or not text): the rest is shown
            }
        }

        // (the last list is kept: "fav", "info 3", "info 4", then "open 4")
        var width = rows.Max(row => row.Label.Length) + 2;
        foreach (var (label, value) in rows) Line("  " + label.PadRight(width) + value);
    }

    // "reset": all the settings back to their defaults, as "Reset all settings" on the System page of Configuration
    // (the lists, favorites, bookmarks and files are kept). Asked first, No selected; from a file only with --yes
    private void Reset()
    {
        if (!ChoiceBox.Confirm(DialogText.CaptionsSettings, DialogText.ResetSettingsQuestion, string.Empty, true))
        {
            Line(Lang.T("Settings unchanged"));
            return;
        }

        Actions.ApplyDefaultSettings();
        // Completing words is on again: the file is made if there is none (one that is there is kept)
        Completions.EnsureFile();
        // English: the log
        EventLog.Add(DialogText.LogShell + ": settings reset");
        Line(Lang.T("Settings reset to the defaults"));
    }

    // "reg": the registers; "reg docs": the value of one (alone on its line, as a script reads it); "reg note Buy ink":
    // a text; "reg here .": the directory the shell is in; "reg plot #3": the full path of row 3 of the last list;
    // "reg -d note", "reg -d all": taken away. Kept in the data directory. Only the names are logged, never the values
    private void Register(string argument)
    {
        var words = Words(argument);
        if (words.Count == 0)
        {
            if (ShellRegisters.All.Count == 0) Line(Lang.T("No registers (reg name text keeps one)"));
            var width = ShellRegisters.All.Keys.Concat(ShellRegisters.Reserved).Max(key => key.Length) + 1;
            foreach (var (key, value) in ShellRegisters.All) Line("  $" + key.PadRight(width) + RegisterShown(value));
            SkippedRegisters();
            // The names set by Blyant, with their values now ($clip and $file when they have one)
            Line(Lang.T("Set by Blyant:"));
            ClipRow(width);
            PeekRow(width);
            // (the others than $clip and $peek: one line each)
            foreach (var reserved in ShellRegisters.Reserved.Where(reserved =>
                         !ShellRegisters.IsClip(reserved) && reserved != ShellRegisters.PeekName))
            {
                if (ShellRegisters.Get(reserved) is { } value) Line("  $" + reserved.PadRight(width) + Printable(value));
            }

            return;
        }

        // "reg -t storm": the registers with a text in the name or the value
        if (words[0] == "-t")
        {
            var sought = AfterWords(argument, 1).Trim();
            sought = Unquoted(sought) ?? sought;

            if (sought.Length == 0)
            {
                Error(Lang.T("Give a text: reg -t storm (or chap*)"));
                return;
            }

            SearchRegisters(sought);
            return;
        }

        if (words[0] == "-d")
        {
            if (words.Count != 2)
            {
                Error(Lang.T("Give the register to take away: reg -d note (or all)"));
                return;
            }

            if (ShellRegisters.IsReserved(words[1]))
            {
                Error(ReservedRefused(words[1]));
                return;
            }

            if (words[1].Equals("all", StringComparison.OrdinalIgnoreCase)) ShellRegisters.Clear();
            else if (!ShellRegisters.Remove(words[1]))
            {
                Error(Lang.F("No register named {0} (reg lists them)", words[1]));
                return;
            }

            // English: the log (the name only)
            EventLog.Add(DialogText.LogShell + ": register taken away: " + words[1].ToLowerInvariant());
            SaveRegisters(words[1].Equals("all", StringComparison.OrdinalIgnoreCase)
                ? Lang.T("All registers taken away")
                : Lang.F("Register {0} taken away", "$" + words[1].ToLowerInvariant()));
            return;
        }

        var name = words[0];
        if (!ShellRegisters.IsName(name))
        {
            Error(Lang.F("Not a name for a register: {0} (a letter first, then letters, digits or _)", name));
            return;
        }

        // "reg notes = text" (as "reg notes text"), "reg notes += text": added at its end, as lines
        var adds = words.Count > 1 && words[1] == "+=";
        var equals = adds || (words.Count > 1 && words[1] == "=");
        var text = AfterWords(argument, equals ? 2 : 1);
        if ((text.Length > 0 || equals) && ShellRegisters.IsReserved(name))
        {
            Error(ReservedRefused(name));
            return;
        }

        // (nothing typed after "=" or "+="; an empty $peek or $clip is a blank line, and goes in)
        if (equals && AfterWords(_typedArgument ?? argument, 2).Trim().Length == 0)
        {
            Error(Lang.F("Give the text: reg {0} {1} Buy ink (or $peek, $clip ...)", name, words[1]));
            return;
        }

        if (text.Length == 0 && !equals)
        {
            // The value alone on its line ("reg clip": the text clip copied last, all its lines)
            // (a line at a time: a long value comes in pages)
            if (ShellRegisters.Get(name) is { } value)
            {
                foreach (var line in value.Split('\n')) Line(line);
            }
            else Error(Lang.F("No register named {0} (reg lists them)", name));
            return;
        }

        string kept;
        if (text.Trim() == ".")
        {
            kept = Directory;
        }
        else if (text.Trim() is ['#', .. var row] && row.Length > 0 && row.All(char.IsAsciiDigit))
        {
            if (!int.TryParse(row, out var number) || number < 1 || number > LastList.Count ||
                LastList[number - 1].Path.Length == 0)
            {
                Error(NoRow(row));
                return;
            }

            kept = Path.GetFullPath(LastList[number - 1].Path);
        }
        else
        {
            // (a text typed in quotes, as a path with blanks is: without them, so $docs is one path again)
            var trimmed = text.Trim();
            kept = Unquoted(trimmed) ?? text;
        }

        if (adds) ShellRegisters.Add(name, kept);
        else ShellRegisters.Set(name, kept);
        // English: the log (the name only, never the value)
        EventLog.Add(DialogText.LogShell + (adds ? ": register added to: " : ": register set: ") + name.ToLowerInvariant());
        SaveRegisters("  $" + name.ToLowerInvariant() + " = " + RegisterShown(ShellRegisters.Get(name) ?? string.Empty));
    }

    // "reg -t storm", "reg -t chap*": the registers, and the names set by Blyant, with the text in the name or in a
    // line of the value (as find -t takes a text: letter case alike, * and ?), as "reg" lists them; when it is in a
    // later line than the first, that line is shown under the row. Shows only: nothing is changed
    private void SearchRegisters(string text)
    {
        var pattern = FileSearch.WildcardText(text);
        var width = ShellRegisters.All.Keys.Concat(ShellRegisters.Reserved).Max(key => key.Length) + 1;
        var found = 0;
        foreach (var (key, value) in ShellRegisters.All)
        {
            if (RegisterFound(key, value, pattern, width)) found++;
        }

        var setByBlyant = false;
        foreach (var reserved in ShellRegisters.Reserved)
        {
            if (ShellRegisters.Get(reserved) is not { } value || !RegisterFits(reserved, value, pattern)) continue;
            if (!setByBlyant) Line(Lang.T("Set by Blyant:"));
            setByBlyant = true;
            RegisterFound(reserved, value, pattern, width);
            found++;
        }

        if (found == 0) Line(Lang.F("No register with {0} in the name or the value", text));
    }

    // The text is in the name of a register or in a line of the value
    private static bool RegisterFits(string name, string value, System.Text.RegularExpressions.Regex pattern) =>
        FileSearch.Matches(pattern, name) || LinesPutIn(value).Any(line => FileSearch.Matches(pattern, line));

    // A register the text is in, as "reg" lists it, and the line found under it when that is not the first (true:
    // shown)
    private bool RegisterFound(string name, string value, System.Text.RegularExpressions.Regex pattern, int width)
    {
        if (!RegisterFits(name, value, pattern)) return false;
        Line("  $" + name.PadRight(width) + RegisterShown(value));
        var lines = LinesPutIn(value);
        if (FileSearch.Matches(pattern, name) || FileSearch.Matches(pattern, lines[0])) return true;
        var at = lines.FindIndex(line => FileSearch.Matches(pattern, line));
        if (at > 0)
        {
            Line(new string(' ', width + 3) +
                 Lang.F("line {0}: {1}", at + 1, Cut(Printable(lines[at]).Replace('\t', ' ').Trim(), 60)));
        }

        return true;
    }

    // A value as the list of the registers shows it: as it is, or the first line of lines (cut) and how many lines
    private static string RegisterShown(string value)
    {
        if (!value.Contains('\n')) return value;
        var (shown, lines) = FirstLine(value);
        return Lang.F("{0}  ({1} lines)", shown, lines);
    }

    // The first line of a value, as a row shows it (control characters as "?", tabs as blanks, cut so the row fits
    // the screen: "..." where it is cut, " ..." after a whole first line when more lines follow), and how many lines
    private static (string Shown, int Lines) FirstLine(string value)
    {
        var lines = LinesPutIn(value);
        var first = Printable(lines[0]).Replace('\t', ' ');
        var shown = Cut(first, 40);
        if (shown.Length < first.Length) shown += "...";
        else if (lines.Count > 1) shown += " ...";
        return (shown, lines.Count);
    }

    // Why a name set by Blyant is not set or taken away by "reg"
    private static string ReservedRefused(string name) => ShellRegisters.IsClip(name)
        ? Lang.T("$clip is the text clip copied last: it cannot be set or taken away")
        : Lang.F("${0} is set by Blyant: it is not a register to set or take away", name.ToLowerInvariant());

    // $clip in the list of the registers: the first line of the text clip copied last, and how many lines it has
    private void ClipRow(int width)
    {
        if (ShellRegisters.Clip is not { } clip) return;
        var (shown, lines) = FirstLine(clip);
        Line("  $" + ShellRegisters.ClipName.PadRight(width) + (lines == 1
            ? Lang.F("{0}  (the last clip)", shown)
            : Lang.F("{0}  ({1} lines, the last clip)", shown, lines)));
    }

    // $peek in the list of the registers: its first line and how many lines it has, as $clip
    private void PeekRow(int width)
    {
        if (ShellRegisters.Peek is not { } peek) return;
        var (shown, lines) = FirstLine(peek);
        Line("  $" + ShellRegisters.PeekName.PadRight(width) + (lines == 1
            ? Lang.F("{0}  (the last peek)", shown)
            : Lang.F("{0}  ({1} lines, the last peek)", shown, lines)));
    }

    // Lines of the file of the registers that are not registers (edited by hand): told, so a register mistyped there is
    // not taken for gone
    private void SkippedRegisters()
    {
        if (ShellRegisters.Skipped == 0) return;
        Line(ShellRegisters.Skipped == 1
            ? Lang.F("(1 line of {0} is not a register: a name, a tab, and the value)", ShellRegisters.FileName)
            : Lang.F("({0} lines of {1} are not registers: a name, a tab, and the value)", ShellRegisters.Skipped,
                ShellRegisters.FileName));
        // (a register named as one Blyant sets now, as "calc" from before $calc)
        foreach (var name in ShellRegisters.ReservedInFile)
        {
            Line(Lang.F("(${0} in {1} is set by Blyant now: give that register another name)", name,
                ShellRegisters.FileName));
        }
    }

    // "reload": the settings file read again, and applied as when Blyant starts (after editing it by hand), and the
    // registers. A value a setting cannot have is told: the setting has its default, as when Blyant starts
    private void Reload(string argument)
    {
        if (argument.Trim().Length > 0)
        {
            Error(Lang.T("reload takes nothing after it"));
            return;
        }

        var content = FileIo.ReadSettingsFile();
        if (content == DialogText.ErrorReadingFile || content == DialogText.FileNotFound)
        {
            Error(Lang.F("{0}: {1}", content, FileIo.SettingsFileName));
            return;
        }

        // (the text read once: what is used is what is compared below)
        if (!FileIo.ApplySettingsText(content))
        {
            Error(Lang.F("{0} has no settings: settings unchanged", FileIo.SettingsFileName));
            return;
        }

        ShellRegisters.Load();
        // English: the log
        EventLog.Add(DialogText.LogShell + ": settings and registers read again");
        // The values in the file that were not taken (as Blyant writes them now)
        var now = FileIo.ParseSettings(FileIo.SettingsText());
        foreach (var (name, value) in FileIo.ParseSettings(content))
        {
            if (!now.TryGetValue(name, out var taken) || SameSetting(taken, value)) continue;
            Error(Lang.F("{0} cannot be {1}: it is {2}", name, value.Trim(), taken));
        }

        Line(Lang.T("Settings and registers read again"));
        SkippedRegisters();
    }

    // A value of a setting read is the one written, as Blyant writes it (letter case alike, "02" is 2)
    private static bool SameSetting(string now, string wanted) =>
        string.Equals(now.Trim(), wanted.Trim(), StringComparison.OrdinalIgnoreCase) ||
        (int.TryParse(now.Trim(), out var a) && int.TryParse(wanted.Trim(), out var b) && a == b);

    // The registers written to their file: what was done is told, or why they could not be kept
    private void SaveRegisters(string done)
    {
        var result = ShellRegisters.Save();
        if (result == DialogText.Success) Line(done);
        else Error(result);
    }

    // "set": the settings, as in the settings file; "set indent": one of them; "set indent 2": a setting changed (as on
    // the pages of Configuration). A value the setting cannot have is told, and the setting stays as it was
    private void Set(string argument)
    {
        var content = FileIo.ReadSettingsFile();
        if (content == DialogText.ErrorReadingFile || content == DialogText.FileNotFound)
        {
            Error(content + ": " + FileIo.SettingsFileName);
            return;
        }

        // A file written by an earlier version can lack settings of this one: it is written whole first (as when a
        // setting is changed in Configuration), so every setting can be shown and changed
        if (FileIo.ParseSettings(FileIo.SettingsText()).Keys.Any(key => !FileIo.ParseSettings(content).ContainsKey(key)))
        {
            var written = FileIo.WriteSettingsFile(FileIo.SettingsText());
            if (Failed(written)) return;

            content = FileIo.ReadSettingsFile();
        }

        var settings = FileIo.ParseSettings(content);
        var trimmed = argument.Trim();
        if (trimmed.Length == 0)
        {
            var width = settings.Keys.Max(key => key.Length) + 1;
            foreach (var (key, value) in settings) Line("  " + key.PadRight(width) + "= " + value);
            return;
        }

        // ("set language=de" as in the settings file, also "set language = de")
        var equals = trimmed.IndexOf('=');
        if (equals > 0 && !trimmed[..equals].Trim().Contains(' '))
        {
            trimmed = trimmed[..equals].Trim() + " " + trimmed[(equals + 1)..].Trim();
        }

        var parts = trimmed.Split(' ', 2);
        var name = parts[0].ToLowerInvariant();
        if (!settings.TryGetValue(name, out var before))
        {
            Error(Lang.F("No setting named {0} (set lists them)", parts[0]));
            return;
        }

        if (parts.Length == 1)
        {
            Line("  " + name + " = " + before);
            return;
        }

        // The line of the setting changed, the settings read again (and written as Blyant writes them)
        var wanted = parts[1].Trim();
        // (in quotes: without them; set shelldir "" empties the setting)
        wanted = Unquoted(wanted) ?? wanted;
        var lines = content.Split('\n').Select(line =>
        {
            var separator = line.IndexOf('=');
            return separator > 0 && string.Equals(line[..separator].Trim(), name, StringComparison.OrdinalIgnoreCase)
                ? name + "=" + wanted
                : line;
        });
        var result = FileIo.WriteSettingsFile(string.Join("\n", lines) + "\n");
        if (Failed(result)) return;

        FileIo.LoadSettings();
        FileIo.SaveSettings();
        var now = FileIo.ParseSettings(FileIo.ReadSettingsFile()).TryGetValue(name, out var found) ? found : string.Empty;
        // English: the log (the name only, never the value)
        EventLog.Add(DialogText.LogShell + ": setting " + name);
        if (SameSetting(now, wanted))
        {
            Line("  " + name + " = " + now);
            return;
        }

        // Not a value it can have: the settings as they were
        var restored = FileIo.WriteSettingsFile(content.EndsWith('\n') ? content : content + "\n");
        FileIo.LoadSettings();
        Error(restored == DialogText.Success
            ? Lang.F("{0} cannot be {1}: it stays {2}", name, wanted, before)
            : Lang.F("{0} cannot be {1}, and the settings file could not be written back: {2}", name, wanted, restored));
    }
}
