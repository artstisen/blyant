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

        if (Existing(words[0]) is not { } item) return;
        if (item.IsDirectory)
        {
            Error(Lang.F("Not a file: {0}", item.Path));
            return;
        }

        TextFile file;
        try
        {
            file = TextFile.Load(item.Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
            return;
        }

        if (!file.IsUtf8)
        {
            Error(Lang.F("{0} is not in UTF-8 ({1}): open it in the editor, which saves it in UTF-8", item.Path,
                file.EncodingName));
            return;
        }

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
            Error(item.Path + ": " + editor.Message);
            return;
        }

        if (text == file.Text)
        {
            Line(item.Path + ": " + (command == "format" ? Lang.T("Already formatted") : editor.Message));
            return;
        }

        var result = file.Save(text);
        FileExplorer.LogFileOperation(command == "format" ? "Format" : "Trim", item.Path, result);
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

        LastList = new List<Item> { item };
        Line(item.Path + ": " + (command == "format" ? Lang.T("Formatted") : editor.Message));
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
        var where = words[1].ToLowerInvariant();
        int? at = where switch
        {
            "top" => 1,
            "bottom" => int.MaxValue,
            _ => where.Length > 1 && where[0] == '+' && int.TryParse(where[1..], out var line) && line > 0 ? line : null
        };
        if (at == null)
        {
            Error(Lang.F("Not a place in a file: {0} (top, bottom or +12)", words[1]));
            return;
        }

        if (Existing(words[0]) is not { } item || Protected(item)) return;
        if (item.IsDirectory)
        {
            Error(Lang.F("Not a file: {0}", item.Path));
            return;
        }

        TextFile file;
        try
        {
            file = TextFile.Load(item.Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
            return;
        }

        if (!file.IsUtf8)
        {
            Error(Lang.F("{0} is not in UTF-8 ({1}): open it in the editor, which saves it in UTF-8", item.Path,
                file.EncodingName));
            return;
        }

        var (text, number) = WithLine(file.Text, rest, at.Value);
        var result = file.Save(text);
        // (the path, never the text)
        FileExplorer.LogFileOperation("Insert", item.Path + ":" + number, result);
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

        LastList = new List<Item> { new(item.Path, false, (number, 1)) };
        Line(Lang.F("{0}: line {1} put in", item.Path, number));
    }

    // "change notes.md +12 A new line", "change notes.md 12-15 One line instead": the lines become the text (one line);
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
        if (Existing(words[0]) is not { } item || Protected(item)) return;
        if (item.IsDirectory)
        {
            Error(Lang.F("Not a file: {0}", item.Path));
            return;
        }

        TextFile file;
        try
        {
            file = TextFile.Load(item.Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
            return;
        }

        if (!file.IsUtf8)
        {
            Error(Lang.F("{0} is not in UTF-8 ({1}): open it in the editor, which saves it in UTF-8", item.Path,
                file.EncodingName));
            return;
        }

        var count = LinesOfText(file.Text).Count;
        if (first > count)
        {
            Error(Lang.F("{0} has {1} lines", item.Path, count));
            return;
        }

        last = Math.Min(last, count);
        var delete = text.Length == 0;
        if (delete)
        {
            // The lines that would go, and the question (no undo)
            var lines = LinesOfText(file.Text);
            var width = last.ToString().Length;
            for (var i = first; i <= last; i++) Line(i.ToString().PadLeft(width + 2) + "  " + lines[i - 1]);
            var question = first == last
                ? Lang.F("Delete line {0} of {1}?", first, Path.GetFileName(item.Path))
                : Lang.F("Delete lines {0}-{1} of {2}?", first, last, Path.GetFileName(item.Path));
            if (!ChoiceBox.Confirm(Lang.T("Delete lines"), question, string.Empty, true))
            {
                Line(Lang.T("Nothing was deleted"));
                return;
            }
        }

        var (changed, at) = WithLinesChanged(file.Text, first, last, delete ? null : text);
        var result = file.Save(changed);
        // (the path and the lines, never the text)
        FileExplorer.LogFileOperation(delete ? "Delete lines" : "Change lines", item.Path + ":" + first + "-" + last, result);
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

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
        if (dash < 0) return int.TryParse(text, out first) && first > 0 && (last = first) > 0;
        return int.TryParse(text[..dash], out first) && int.TryParse(text[(dash + 1)..], out last) && first > 0 &&
               last >= first;
    }

    // The lines of a text (lines end in "\n"; the last line break does not start another line)
    private static List<string> LinesOfText(string text)
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
        if (line != null) lines.Insert(first - 1, line.Replace("\r", string.Empty).Replace("\n", " "));
        if (lines.Count == 0) return (string.Empty, 1);
        return (string.Join("\n", lines) + (endsWithBreak ? "\n" : string.Empty), Math.Min(first, lines.Count));
    }

    /// <summary>
    /// A text with a line put in as line "at" (1: before the first; past the end: after the last), and the number it
    /// got. The lines end in "\n"; a text that ends with a line break keeps it, and one that does not gets none after
    /// a line put in at the end
    /// </summary>
    internal static (string Text, int Line) WithLine(string text, string line, int at)
    {
        text = TextBuffer.Normalize(text);
        line = line.Replace("\r", string.Empty).Replace("\n", " ");
        if (text.Length == 0) return (line + "\n", 1);
        var endsWithBreak = text.EndsWith('\n');
        var lines = (endsWithBreak ? text[..^1] : text).Split('\n').ToList();
        var index = Math.Clamp(at - 1, 0, lines.Count);
        lines.Insert(index, line);
        return (string.Join("\n", lines) + (endsWithBreak ? "\n" : string.Empty), index + 1);
    }

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
        if (item == null) return;
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

        LastList = new List<Item> { item };
        var width = rows.Max(row => row.Label.Length) + 2;
        foreach (var (label, value) in rows) Line("  " + label.PadRight(width) + value);
    }

    // "reset": all the settings back to their defaults, as "Reset all settings" on the System page of Configuration
    // (the lists, favorites, bookmarks and files are kept). Asked first, No selected; from a file only with --yes
    private void Reset()
    {
        if (!ChoiceBox.Confirm(DialogText.CaptionsSettings, DialogText.ResetSettingsQuestion, string.Empty, true))
        {
            Line(Lang.T("The settings are as they were"));
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
            if (ShellRegisters.All.Count == 0)
            {
                Line(Lang.T("No registers (reg name text keeps one)"));
                return;
            }

            var width = ShellRegisters.All.Keys.Max(key => key.Length) + 1;
            foreach (var (key, value) in ShellRegisters.All) Line("  $" + key.PadRight(width) + value);
            return;
        }

        if (words[0] == "-d")
        {
            if (words.Count != 2)
            {
                Error(Lang.T("Give the register to take away: reg -d note (or all)"));
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

        var text = AfterWords(argument, 1);
        if (text.Length == 0)
        {
            // The value alone on its line
            if (ShellRegisters.Get(name) is { } value) Line(value);
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
                Error(!_listShown
                    ? Lang.F("No row {0}: no list has been shown", row)
                    : Lang.F("No row {0}: the last list has {1} rows", row, LastList.Count));
                return;
            }

            kept = Path.GetFullPath(LastList[number - 1].Path);
        }
        else
        {
            kept = text;
        }

        ShellRegisters.Set(name, kept);
        // English: the log (the name only, never the value)
        EventLog.Add(DialogText.LogShell + ": register set: " + name.ToLowerInvariant());
        SaveRegisters("  $" + name.ToLowerInvariant() + " = " + ShellRegisters.Get(name));
    }

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
            if (written != DialogText.Success)
            {
                Error(written);
                return;
            }

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
        var lines = content.Split('\n').Select(line =>
        {
            var separator = line.IndexOf('=');
            return separator > 0 && string.Equals(line[..separator].Trim(), name, StringComparison.OrdinalIgnoreCase)
                ? name + "=" + wanted
                : line;
        });
        var result = FileIo.WriteSettingsFile(string.Join("\n", lines) + "\n");
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

        FileIo.LoadSettings();
        FileIo.SaveSettings();
        var now = FileIo.ParseSettings(FileIo.ReadSettingsFile()).TryGetValue(name, out var found) ? found : string.Empty;
        // English: the log (the name only, never the value)
        EventLog.Add(DialogText.LogShell + ": setting " + name);
        if (string.Equals(now.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
        {
            Line("  " + name + " = " + now);
            return;
        }

        // Not a value it can have: the settings as they were
        FileIo.WriteSettingsFile(content.EndsWith('\n') ? content : content + "\n");
        FileIo.LoadSettings();
        Error(Lang.F("{0} cannot be {1}: it stays {2}", name, wanted, before));
    }
}
