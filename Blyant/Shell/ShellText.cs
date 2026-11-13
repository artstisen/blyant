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
