using System.Text;

namespace Blyant;

/// <summary>
/// The commands typed in the shell, kept in "shellhistory.txt" in the data directory, so the next shell has them for
/// Up, Down and "again": a line for each command, the oldest first. The setting "shellhistory" tells how many are kept
/// (50, at most 200; 0: none in the file, only the last 50 while Blyant runs, as before 1.6.1). A command is added to
/// the end of the file when it is typed, so nothing is lost when the terminal is closed, and the file is written again
/// with the newest commands when Blyant reads it and has more, or has grown to twice as many lines. Not kept: commands
/// from a file, a pipe or --run, and a command typed with a blank in front. A command can hold a file name or a text:
/// "forget" deletes the file, and so does the setting 0, and the log has the path, never the commands.
/// Several Blyants can add to the file at once (each line is appended whole). When one writes it again with the newest
/// commands, a line another adds at that moment can be lost, which is fine for a list of commands
/// </summary>
internal static class ShellHistory
{
    internal const string FileName = "shellhistory.txt";

    /// <summary>
    /// The file of the commands. It can be set to another file than the one in the data directory (for testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    internal static string FilePath =>
        FilePathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    // The file read into the commands of the shell (null: none yet). It is read once while Blyant runs, also when the
    // shell is left for the full screen and started again
    private static string? _loaded;

    // The lines in the file: counted when it is read, and as commands are added
    private static int _lines;

    // The file could not be read: it is not written over, only added to
    private static bool _unreadable;

    /// <summary>
    /// True when the commands typed are kept in the file (the setting shellhistory is not 0)
    /// </summary>
    internal static bool Kept => Shell.HistoryCount > 0;

    /// <summary>
    /// Read the file into the commands of the shell, once: its commands come before any typed already. A file with
    /// more lines than the setting keeps is written again with the newest
    /// </summary>
    internal static void Load()
    {
        if (!Kept)
        {
            RemoveWhenOff();
            return;
        }

        var path = Path.GetFullPath(FilePath);
        if (_loaded is not null && FileOperations.SamePath(_loaded, path)) return;
        _loaded = path;
        _lines = 0;
        _unreadable = false;
        if (ReadLines(path) is not { } lines)
        {
            // (read again with the next command: the file may only have been locked for a moment)
            _loaded = null;
            return;
        }

        var typed = CommandHistory.Of(CommandContext.Shell);
        CommandHistory.Clear(CommandContext.Shell);
        foreach (var command in lines) CommandHistory.Add(CommandContext.Shell, command);
        foreach (var command in typed.Reverse()) CommandHistory.Add(CommandContext.Shell, command);
        _lines = lines.Count;
        if (_lines > Shell.HistoryCount) Compact();
    }

    /// <summary>
    /// A command typed: added to the end of the file (read first, when the setting was turned on since the shell
    /// started). A file of twice as many lines as kept is written again with the newest
    /// </summary>
    internal static void Add(string command)
    {
        command = command.Trim();
        if (!Kept)
        {
            RemoveWhenOff();
            return;
        }

        if (command.Length == 0 || command.IndexOfAny(['\n', '\r']) >= 0) return;
        Load();
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            if (directory is not null && !Directory.Exists(directory)) FileIo.CreateDirectory(directory);
            FileIo.AppendText(FilePath, command + "\n");
            _lines++;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // (logged by FileIo: the command is still kept while Blyant runs)
            return;
        }

        if (_lines >= Shell.HistoryCount * 2) Compact();
    }

    /// <summary>
    /// "forget": the file deleted (a new one is made with the next command typed)
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Clear()
    {
        try
        {
            FileIo.DeleteFile(FilePath);
            // (a new file, which is read and written again as any)
            _lines = 0;
            _unreadable = false;
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// The commands of a file, oldest first, each once (where it was given last), the newest "count" of them
    /// </summary>
    internal static List<string> Newest(IEnumerable<string> lines, int count)
    {
        var commands = new List<string>();
        foreach (var line in lines)
        {
            var command = line.Trim();
            if (command.Length == 0) continue;
            commands.Remove(command);
            commands.Add(command);
        }

        return commands.Count > count ? commands.GetRange(commands.Count - count, count) : commands;
    }

    // The lines of the file that are commands (none when it is not there); null, and logged, when it cannot be read
    private static List<string>? ReadLines(string path)
    {
        try
        {
            if (!File.Exists(path)) return new List<string>();
            return File.ReadAllText(path, Encoding.UTF8).Split('\n').Select(line => line.Trim())
                .Where(line => line.Length > 0).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _unreadable = true;
            // English: the log (the path, never the commands)
            EventLog.Add(DialogText.LogFile + "Shell history not read: " + path + ": " + e.Message);
            return null;
        }
    }

    // The file written again with the newest commands, read from it again (another Blyant may have added to it)
    private static void Compact()
    {
        if (_unreadable || ReadLines(Path.GetFullPath(FilePath)) is not { } lines) return;
        var newest = Newest(lines, Shell.HistoryCount);
        try
        {
            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(string.Concat(newest.Select(c => c + "\n"))));
            _lines = newest.Count;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // (logged by FileIo: the file stays as it was, and is written again later)
        }
    }

    // The setting 0: no commands kept in the file, so a file kept before is deleted (logged by FileIo; one that cannot
    // be deleted now is tried again with the next command)
    private static void RemoveWhenOff()
    {
        _loaded = null;
        _lines = 0;
        if (!File.Exists(FilePath)) return;
        try
        {
            FileIo.DeleteFile(FilePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // (logged by FileIo)
        }
    }

    /// <summary>
    /// Read the file again the next time (for the tests: Load reads it again by itself when the data directory changed)
    /// </summary>
    internal static void Reset()
    {
        _loaded = null;
        _lines = 0;
        _unreadable = false;
    }
}
