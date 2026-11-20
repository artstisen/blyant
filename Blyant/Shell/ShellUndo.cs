namespace Blyant;

/// <summary>
/// Undo and redo of the changes the commands of the shell make to the text of files ("insert", "change", "shift",
/// "lines", "format", "trim"): before a command writes a file, its content is kept, and after it the content written.
/// "undo" puts the content back, "redo" the change again, and "changes" lists them. One command is one step ("lines ...
/// cut" changes two files: undone together), the last 50 steps are kept (setting shellundosteps, 10 to 200; at most
/// UndoMaxBytes in all), while Blyant runs only (never in a file: no copy of the text stays on the disk). A step is
/// undone only while its files are as it left them: a change made since (in the editor, by another program) is never
/// written over. A command whose files cannot all be kept (one larger than UndoMaxSize, or not read) is no step: it
/// cannot be undone, and it is told
/// </summary>
internal sealed partial class ShellSession
{
    // The largest file kept for undo (its content before and after)
    internal const long UndoMaxSize = 2 * 1024 * 1024;

    // The most bytes all the steps keep: the oldest are let go past it
    internal const long UndoMaxBytes = 32 * 1024 * 1024;

    // A change: the command, and the files it changed, each with its content before and after
    // (and where $file and $line were before the command and after it: undo and redo put "@" back there)
    private sealed record UndoStep(string Command, List<(string Path, byte[] Before, byte[] After)> Files,
        (string? File, int? Line) Was, (string? File, int? Line) Now)
    {
        internal long Bytes => Files.Sum(file => (long)file.Before.Length + file.After.Length);
    }

    private readonly List<UndoStep> _undo = new(); // The newest last
    private readonly List<UndoStep> _redo = new(); // The step undone last, last

    // The files the command running now has changed (null: none yet), and whether one of them could not be kept
    private List<(string Path, byte[] Before, byte[] After)>? _changing;
    private bool _changingLost;

    // A change that cannot be undone has been made in this shell (told when there is nothing to undo)
    private bool _changeLost;

    // A file of a command saved with the text, its content before and after kept for undo. A file larger than
    // UndoMaxSize, or one that cannot be read first, makes the command one that cannot be undone (told)
    private string SaveEdit(TextFile file, string text)
    {
        byte[]? before = null;
        var tooLarge = false;
        try
        {
            if (new FileInfo(file.Path).Length <= UndoMaxSize) before = File.ReadAllBytes(file.Path);
            else tooLarge = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            before = null;
        }

        var result = file.Save(text);
        if (result != DialogText.Success) return result;
        _changedFiles.Add(Path.GetFullPath(file.Path));
        var after = file.GetBytes(text);
        if (before == null || after.Length > UndoMaxSize)
        {
            Line(tooLarge || after.Length > UndoMaxSize
                ? Lang.F("(this cannot be undone: {0} is larger than 2 MB)", Path.GetFileName(file.Path))
                : Lang.F("(this cannot be undone: {0} could not be read first)", Path.GetFileName(file.Path)));
            _changingLost = true;
            return result;
        }

        // (written as it was: nothing to undo)
        if (before.AsSpan().SequenceEqual(after)) return result;
        (_changing ??= new()).Add((Path.GetFullPath(file.Path), before, after));
        return result;
    }

    // The files a command has changed are the newest of the latest files ("recent", "hist"), as files opened in the
    // editor: to find them again. $file last, so it is the newest ("lines ... cut" changes two files)
    private void LatestChanged()
    {
        if (_changedFiles.Count == 0) return;
        // (not the file of "again edit": not a file worked on)
        var files = _changedFiles.Where(path => !IsCommandsFile(path)).Distinct(FileOperations.NameComparer)
            .OrderBy(path => _file != null && FileOperations.NameComparer.Equals(path, _file)).ToList();
        if (files.Count > 0) FileExplorer.AddToHistory(files);
    }

    // A command starts: the files it changes are one step (the step of a command it runs in, "run", is kept aside)
    private (List<(string Path, byte[] Before, byte[] After)>? Files, bool Lost) StartStep()
    {
        var outer = (_changing, _changingLost);
        _changing = null;
        _changingLost = false;
        return outer;
    }

    // A command has run: what it changed is the newest step, and the steps undone can no longer be redone (also after
    // a change that cannot be undone). The step of a command it ran in goes on
    private void EndStep(string command, (List<(string Path, byte[] Before, byte[] After)>? Files, bool Lost) outer,
        (string? File, int? Line) was)
    {
        if (_changingLost)
        {
            _redo.Clear();
            _changeLost = true;
        }
        else if (_changing is { Count: > 0 } files)
        {
            _undo.Add(new UndoStep(command, files, was, (_file, _line)));
            _redo.Clear();
            LetGo();
        }

        (_changing, _changingLost) = outer;
    }

    // The oldest steps let go: past the setting (also made smaller meanwhile), or past UndoMaxBytes in all (the
    // newest step is kept)
    private void LetGo()
    {
        while (_undo.Count > Shell.UndoSteps) _undo.RemoveAt(0);
        while (_undo.Count > 1 && _undo.Sum(step => step.Bytes) + _redo.Sum(step => step.Bytes) > UndoMaxBytes)
        {
            _undo.RemoveAt(0);
        }

        while (_redo.Count > Shell.UndoSteps) _redo.RemoveAt(0);
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

        LetGo();
        var from = redo ? _redo : _undo;
        if (from.Count == 0)
        {
            Line(redo
                ? Lang.T("Nothing to redo")
                : _redo.Count > 0
                    ? Lang.T("Nothing more to undo (redo does it again)")
                    : _changeLost
                        ? Lang.T("Nothing to undo: the changes made here cannot be undone")
                        : Lang.T("Nothing to undo: no file has been changed in this shell"));
            return;
        }

        var changed = new List<Item>();
        UndoStep? done = null;
        for (var i = 0; i < steps && from.Count > 0; i++)
        {
            var step = from[^1];
            // Each file as the step left it (undo) or found it (redo): else nothing of the step is written
            foreach (var (path, before, after) in step.Files)
            {
                if (IsNow(path, redo ? before : after)) continue;
                Error(!File.Exists(path)
                    ? redo
                        ? Lang.F("{0} is no longer there: {1} is not redone", path, step.Command)
                        : Lang.F("{0} is no longer there: {1} is not undone", path, step.Command)
                    : redo
                        ? Lang.F("{0} has changed since it was undone: {1} is not redone", path, step.Command)
                        : Lang.F("{0} has changed since: {1} is not undone", path, step.Command));
                Done(changed);
                return;
            }

            // (the last file first: "lines ... cut" put the lines in, then took them out). A file that cannot be
            // written: the files of the step written already are put back as they were, so the step stays whole
            var written = new List<(string Path, byte[] Was)>();
            foreach (var (path, before, after) in Enumerable.Reverse(step.Files))
            {
                try
                {
                    FileIo.WriteAllBytesSafe(path, redo ? after : before);
                    written.Add((path, redo ? before : after));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Error(Lang.F("{0} cannot be written: {1}", path, e.Message));
                    foreach (var (back, was) in written)
                    {
                        try
                        {
                            FileIo.WriteAllBytesSafe(back, was);
                        }
                        catch (Exception again) when (again is IOException or UnauthorizedAccessException)
                        {
                            Error(Lang.F("{0} cannot be written: {1}", back, again.Message));
                        }
                    }

                    Done(changed);
                    return;
                }
            }

            changed.AddRange(written.Select(file => new Item(file.Path, false)));
            _changedFiles.AddRange(written.Select(file => Path.GetFullPath(file.Path)));
            done = step;
            from.RemoveAt(from.Count - 1);
            (redo ? _undo : _redo).Add(step);
            // English: the log (the command by name only)
            EventLog.Add(DialogText.LogShell + (redo ? ": redo: " : ": undo: ") + EventLog.CommandName(step.Command));
            Line(redo ? Lang.F("Redone: {0}", step.Command) : Lang.F("Undone: {0}", step.Command));
        }

        Done(changed);

        // The files written are the last list ("open 1"), the first of them $file. $file and $line ("@") go back
        // where they were before the step undone last, or where the step redone last left them
        void Done(List<Item> files)
        {
            if (files.Count > 0) LastList = files.DistinctBy(item => item.Path).ToList();
            if (done == null) return;
            // (no $file before the command: the line it left is no line of the file as it is now)
            if ((redo ? done.Now : done.Was) is { File: { } file } place)
            {
                _file = file;
                _line = place.Line;
            }
            else _line = null;
        }
    }

    // The file has this content now (its length first, so a file that grew is not read)
    private static bool IsNow(string path, byte[] content)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length == content.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(content);
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
        LetGo();
        if (_undo.Count == 0 && _redo.Count == 0)
        {
            Line(_changeLost
                ? Lang.T("Nothing to undo: the changes made here cannot be undone")
                : Lang.T("No changes to undo: no file has been changed in this shell"));
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
