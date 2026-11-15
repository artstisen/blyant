namespace Blyant;

/// <summary>
/// "run $tidy", "run tidy.txt", "run $pwd/weekly.txt": the commands of a register (a register of several lines: one a line) or of a file run
/// one after another, each written out before it runs, as "again" does. It stops at the first that fails, and
/// questions are asked as for any command. A $name in a command is put in when that command runs ("$$date" in the
/// register keeps "$date" for then). A line starting with "#" is a comment
/// </summary>
internal sealed partial class ShellSession
{
    // How many runs can be in one another: a register that runs itself is stopped
    private const int MaxRunDepth = 10;

    private int _runDepth;


    // "run $tidy": its value (put in already: the commands); "run tidy.txt": the lines of the file
    private void RunCommands(string argument)
    {
        var typed = (_typedArgument ?? argument).Trim();
        if (typed.Length == 0)
        {
            Error(Lang.T("Give a register or a file: run $tidy, run tidy.txt"));
            return;
        }

        string text;
        string what;
        // "$tidy" alone: the commands in the register ($clip and $peek too); "$file", "$pwd/weekly.txt", "notes.txt":
        // a file, with the $names put in
        if (typed is ['$', .. var name] && ShellRegisters.IsName(name) &&
            (!ShellRegisters.IsReserved(name) || ShellRegisters.IsClip(name) ||
             name.Equals(ShellRegisters.PeekName, StringComparison.OrdinalIgnoreCase)))
        {
            if (ShellRegisters.Get(name) == null)
            {
                Error(Lang.F("No register named {0} (reg lists them)", name));
                return;
            }

            text = argument;
            what = "$" + name.ToLowerInvariant();
        }
        else
        {
            // (one path: in quotes, or with blanks from a $name put in)
            var path = argument.Trim();
            if (path.Length >= 2 && path[0] == '"' && path[^1] == '"') path = path[1..^1];
            if (Existing(path) is not { } item) return;
            if (item.IsDirectory)
            {
                Error(Lang.F("Not a file: {0}", item.Path));
                return;
            }

            try
            {
                text = TextFile.Load(item.Path).Text;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
                return;
            }

            what = item.Path;
        }

        if (_runDepth >= MaxRunDepth)
        {
            Error(Lang.F("run is {0} deep: a register or file that runs itself is stopped", MaxRunDepth));
            return;
        }

        // English: the log (the register or the file, never its commands: each is logged by its name as it runs)
        EventLog.Add(DialogText.LogShell + ": run " + (what.StartsWith('$') ? what : "file " + what));
        var lines = LinesOfText(text);
        var typedBefore = _typed;
        _runDepth++;
        // (the commands run are not kept for again: "run $tidy" is, so !! runs them all again)
        _typed = false;
        try
        {
            for (var i = 0; i < lines.Count; i++)
            {
                var command = lines[i].Trim();
                if (command.Length == 0 || command.StartsWith('#')) continue;
                Line(Printable(command));
                var failed = Failures;
                // ("exit" in a run ends the run, not Blyant; "screen" is refused in a run)
                if (!RunLine(command)) return;
                if (Failures > failed)
                {
                    Error(Lang.F("run stopped at line {0} of {1}: {2}", i + 1, what, command));
                    return;
                }
            }
        }
        finally
        {
            _runDepth--;
            _typed = typedBefore;
        }
    }
}
