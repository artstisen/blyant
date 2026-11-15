namespace Blyant;

/// <summary>
/// Undo and redo of the changes the commands of the shell make to the text of files ("insert", "change", "shift",
/// "lines", "format", "trim"): before a command writes a file, its content is kept, and after it the content written.
/// "undo" puts the content back, "redo" the change again, and "changes" lists them. One command is one step ("lines ...
/// cut" changes two files: undone together), the last 10 steps are kept (setting shellundosteps, 1 to 50), while
/// Blyant runs only (never in a file: no
/// copy of the text stays on the disk). A step is undone only while its files are as it left them: a change made since
/// (in the editor, by another program) is never written over
/// </summary>
internal sealed partial class ShellSession
{
    // The largest file kept for undo (its content before and after)
    internal const long UndoMaxSize = 2 * 1024 * 1024;

    // A change: the command, and the files it changed, each with its content before and after
    private sealed record UndoStep(string Command, List<(string Path, byte[] Before, byte[] After)> Files);

    private readonly List<UndoStep> _undo = new(); // The newest last
    private readonly List<UndoStep> _redo = new(); // The step undone last, last

    // The files the command running now has changed (null: none yet)
    private List<(string Path, byte[] Before, byte[] After)>? _changing;

    // A file of a command saved with the text, its content before and after kept for undo (not a file larger than
    // UndoMaxSize: told)
    private string SaveEdit(TextFile file, string text)
    {
        byte[]? before = null;
        try
        {
            if (new FileInfo(file.Path).Length <= UndoMaxSize) before = File.ReadAllBytes(file.Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            before = null;
        }

        var result = file.Save(text);
        if (result != DialogText.Success) return result;
        var after = file.GetBytes(text);
        if (before == null || after.Length > UndoMaxSize)
        {
            Line(Lang.T("(not kept for undo: the file is larger than 2 MB)"));
            return result;
        }

        (_changing ??= new()).Add((Path.GetFullPath(file.Path), before, after));
        return result;
    }

    // A command starts: the files it changes are one step
    private List<(string Path, byte[] Before, byte[] After)>? StartStep()
    {
        var outer = _changing;
        _changing = null;
        return outer;
    }

    // A command has run: what it changed is the newest step (the steps undone can no longer be redone), and the step
    // of a command it ran in ("run") goes on
    private void EndStep(string command, List<(string Path, byte[] Before, byte[] After)>? outer)
    {
        if (_changing is { Count: > 0 } files)
        {
            _undo.Add(new UndoStep(command, files));
            // (the oldest let go: also when the setting was made smaller meanwhile)
            while (_undo.Count > Shell.UndoSteps) _undo.RemoveAt(0);
            _redo.Clear();
        }

        _changing = outer;
    }

    // "undo", "undo 3": the newest step put back, or the newest 3, one after another (stops at one that cannot be)
    private void Undo(string argument, bool redo)
    {
        var steps = 1;
        if (argument.Trim().Length > 0 && !(int.TryParse(argument.Trim(), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out steps) && steps >= 1))
        {
            Error(redo ? Lang.T("Give how many: redo, redo 2") : Lang.T("Give how many: undo, undo 3 (changes lists them)"));
            return;
        }

        var from = redo ? _redo : _undo;
        if (from.Count == 0)
        {
            Line(redo ? Lang.T("Nothing to redo") : Lang.T("Nothing to undo: no file has been changed in this shell"));
            return;
        }

        var changed = new List<Item>();
        for (var i = 0; i < steps && from.Count > 0; i++)
        {
            var step = from[^1];
            // Each file as the step left it (undo) or found it (redo): else nothing of the step is written
            foreach (var (path, before, after) in step.Files)
            {
                if (!IsNow(path, redo ? before : after))
                {
                    Error(redo
                        ? Lang.F("{0} has changed since it was undone: {1} is not done again", path, step.Command)
                        : Lang.F("{0} has changed since: {1} is not undone", path, step.Command));
                    Done(changed);
                    return;
                }
            }

            // (the last file first: "lines ... cut" put the lines in, then took them out)
            foreach (var (path, before, after) in Enumerable.Reverse(step.Files))
            {
                try
                {
                    FileIo.WriteAllBytesSafe(path, redo ? after : before);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Error(Lang.F("{0} cannot be written: {1}", path, e.Message));
                    Done(changed);
                    return;
                }

                changed.Add(new Item(path, false));
            }

            from.RemoveAt(from.Count - 1);
            (redo ? _undo : _redo).Add(step);
            // English: the log (the command by name only)
            EventLog.Add(DialogText.LogShell + (redo ? ": redo: " : ": undo: ") + EventLog.CommandName(step.Command));
            Line(redo ? Lang.F("Done again: {0}", step.Command) : Lang.F("Undone: {0}", step.Command));
        }

        Done(changed);

        // The files written are the last list ("open 1"), the first of them $file
        void Done(List<Item> written)
        {
            if (written.Count > 0) LastList = written.DistinctBy(item => item.Path).ToList();
        }
    }

    // The file has this content now
    private static bool IsNow(string path, byte[] content)
    {
        try
        {
            return File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(content);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // "changes": the steps undo goes back through, the newest first and numbered ("undo 3" undoes the 3 newest), and
    // those redo does again
    private void ListChanges()
    {
        if (_undo.Count == 0 && _redo.Count == 0)
        {
            Line(Lang.T("No changes to undo: no file has been changed in this shell"));
            return;
        }

        var width = Math.Max(_undo.Count, 1).ToString().Length;
        for (var i = 0; i < _undo.Count; i++)
        {
            Line((i + 1).ToString().PadLeft(width + 2) + "  " + _undo[^(i + 1)].Command);
        }

        if (_redo.Count == 0) return;
        Line(Lang.T("Undone (redo does them again):"));
        for (var i = 0; i < _redo.Count; i++) Line("    " + _redo[^(i + 1)].Command);
    }
}
