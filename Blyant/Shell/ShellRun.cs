namespace Blyant;

/// <summary>
/// "run $tidy", "run tidy.txt", "run $pwd/weekly.txt": the commands of a register (a register of several lines: one a line) or of a file run
/// one after another, each written out before it runs, as "again" does. It stops at the first that fails, and
/// questions are asked as for any command. A $name in a command is put in when that command runs ("$$date" in the
/// register keeps "$date" for then). A line starting with "#" is a comment. Words after the register or file are
/// put in where a command has $1, $2 ... ($* all of them): "run $check $file 12"; a script of one line without them
/// gets them at its end ("reg test open", "run $test $file"). "run $cmd" (the file of "again edit") shows the
/// commands and asks first, at a terminal: run, edit or cancel; "run -y $cmd" runs them at once
/// </summary>
internal sealed partial class ShellSession
{
    // How many runs can be in one another: a register that runs itself is stopped
    private const int MaxRunDepth = 10;

    private int _runDepth;


    // "run $tidy": its value (put in already: the commands); "run tidy.txt": the lines of the file; "run $check
    // $file 12": with words for $1 and $2 in its commands
    private void RunCommands(string argument)
    {
        var typed = (_typedArgument ?? argument).Trim();
        // "-y": $cmd runs without asking first
        var yes = false;
        if (Words(typed) is ["-y", ..])
        {
            yes = true;
            typed = AfterWords(typed, 1).Trim();
            argument = AfterWords(argument.Trim(), 1).Trim();
        }

        if (typed.Length == 0)
        {
            Error(Lang.T("Give a register or a file: run $tidy, run tidy.txt"));
            return;
        }

        // The words after the register or file: for $1, $2 ... (a file named with blanks, "run my script.txt",
        // is the file as before: the whole line is the path when that file is there)
        var arguments = new List<string>();
        var parts = Words(typed);
        if (parts.Count > 1 && !File.Exists(PathOf(argument.Trim().Trim('"'))))
        {
            var rest = AfterWords(typed, 1);
            // (a value with blanks and quotes cannot be one word: it would be split wrongly)
            if (ShellRegisters.NamesIn(rest).Select(ShellRegisters.Get)
                    .FirstOrDefault(value => value != null && value.Any(char.IsWhiteSpace) && value.Contains('"')) is
                { } quoted)
            {
                Error(Lang.F("A word with blanks and quotes cannot be given to a script: {0}", quoted));
                return;
            }

            // ($names put in, a value with blanks as one word)
            arguments = Words(ShellRegisters.Expand(rest, int.MaxValue));
            typed = typed[..^rest.Length].Trim();
            argument = ShellRegisters.Expand(typed, 0);
        }

        string text;
        string what;
        var commandsFile = false;
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
            if (Existing(path) is not { } item || ReadableFile(item) is not { } file) return;
            text = file.Text;
            what = item.Path;
            commandsFile = IsCommandsFile(item.Path);
        }

        if (_runDepth >= MaxRunDepth)
        {
            Error(Lang.F("run is {0} deep: a register or file that runs itself is stopped", MaxRunDepth));
            return;
        }

        var lines = LinesOfText(text);
        if (WithArguments(lines, arguments) is not { } given) return;
        lines = given;
        // The file of "again edit" (run $cmd): asked first, at a terminal
        if (commandsFile && !yes && !RunCommandsAsked(what, lines)) return;
        // English: the log (the register or the file, never its commands: each is logged by its name as it runs)
        EventLog.Add(DialogText.LogShell + ": run " + (what.StartsWith('$') ? what : "file " + what));
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

    // $1 to $9 and $* in the commands of a script ("$$1" is "$1" as it is)
    [System.Text.RegularExpressions.GeneratedRegex(@"\$\$|\$([1-9*])")]
    private static partial System.Text.RegularExpressions.Regex ArgumentPattern();

    /// <summary>
    /// The commands of a script with the words given after it put in: $1 the first, $2 the second ..., $* all of them.
    /// A script of one command without any of them gets the words at its end. Null (and told) when the words do not
    /// fit: fewer than the highest $n, more than it without $*, or words for a script of several commands without $n.
    /// Without words the script is as it is
    /// </summary>
    internal List<string>? WithArguments(List<string> lines, IReadOnlyList<string> arguments)
    {
        var commands = lines.Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#')).ToList();
        var (highest, all) = ArgumentsUsed(commands);
        if (arguments.Count == 0)
        {
            // Without words: a command that takes $1 ... is refused, as it would get "$1" itself ("new $1" would make
            // a file named so); a "reg" line keeps $1 as text on purpose, for a script it makes. $* is nothing
            var (needed, _) = ArgumentsUsed(commands.Where(command =>
                !CommandLine.Name(command).Equals("reg", StringComparison.OrdinalIgnoreCase)));
            if (needed > 0)
            {
                Error(Lang.F("The script takes {0} words ($1 to ${0}): {1} given", needed, 0));
                return null;
            }

            return all ? Substituted(lines, Array.Empty<string>()) : lines;
        }

        // (a value with blanks put in as one word, in quotes; a "$" in a word stays a "$": the words were put in
        // already, and are not put in again when the command runs)
        if (arguments.FirstOrDefault(word => word.Any(char.IsWhiteSpace) && word.Contains('"')) is { } quoted)
        {
            Error(Lang.F("A word with blanks and quotes cannot be given to a script: {0}", quoted));
            return null;
        }

        var words = arguments.Select(word => word.Replace("$", "$$"))
            .Select(word => word.Any(char.IsWhiteSpace) ? "\"" + word + "\"" : word).ToList();
        if (highest == 0 && !all)
        {
            if (commands.Count == 1)
            {
                return lines.Select(line => ReferenceEquals(line, commands[0]) || line == commands[0]
                    ? line.TrimEnd() + " " + string.Join(' ', words)
                    : line).ToList();
            }

            Error(Lang.T("The script has no $1: write $1, $2 ... where the words go (reg tidy = trim $1)"));
            return null;
        }

        if (words.Count < highest || (!all && words.Count > highest))
        {
            Error(all
                ? Lang.F("The script takes at least {0} words ($1 to ${0}): {1} given", highest, words.Count)
                : Lang.F("The script takes {0} words ($1 to ${0}): {1} given", highest, words.Count));
            return null;
        }

        return Substituted(lines, words);
    }

    // The highest $n in commands, and whether $* is in one
    private static (int Highest, bool All) ArgumentsUsed(IEnumerable<string> commands)
    {
        var highest = 0;
        var all = false;
        foreach (var command in commands)
        {
            foreach (System.Text.RegularExpressions.Match match in ArgumentPattern().Matches(command))
            {
                if (!match.Groups[1].Success) continue;
                if (match.Groups[1].Value == "*") all = true;
                else highest = Math.Max(highest, match.Groups[1].Value[0] - '0');
            }
        }

        return (highest, all);
    }

    // The words put in for $1 ... and $* in the commands; a comment ("# $2 is the line") as it is
    private static List<string> Substituted(List<string> lines, IReadOnlyList<string> words) =>
        lines.Select(line => line.TrimStart().StartsWith('#') ? line : ArgumentPattern().Replace(line, match =>
            !match.Groups[1].Success ? match.Value
            : match.Groups[1].Value == "*" ? string.Join(' ', words)
            : match.Groups[1].Value[0] - '1' < words.Count ? words[match.Groups[1].Value[0] - '1'] : match.Value))
            .ToList();
}
