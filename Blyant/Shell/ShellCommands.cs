using System.Globalization;

namespace Blyant;

/// <summary>
/// The commands of the shell (CommandContext.Shell in Commands.cs). Each takes what it works on as its argument: a
/// path, a name, or the number of a row of the last list shown
/// </summary>
internal sealed partial class ShellSession
{
    /// <summary>
    /// The commands, as "help" lists them (the names in Commands.cs)
    /// </summary>
    private static IEnumerable<(string Command, string Text)> HelpRows() => new[]
    {
        ("pwd", Lang.T("Show the directory you are in")),
        ("ls [-a] [-l] [dir|n]", Lang.T("List a directory (-a: hidden ones too, -l: full paths)")),
        ("cd [dir|file|n]", Lang.T("Go to a directory, or that of a file (alone: home)")),
        ("up", Lang.T("Go to the directory above")),
        ("home", Lang.T("Go to your home directory")),
        ("data", Lang.T("Go to Blyant's data directory (settings, lists, log)")),
        ("open file|n [+line] (edit)", Lang.T("Edit a file on the full screen (closing it returns here)")),
        ("resume [+line]", Lang.T("Edit $file again, where you worked last")),
        ("peek file|n [lines]", Lang.T("Show the lines of a file (the first 20, 40, 100-120, or +100 30)")),
        ("peek [file|n] +|++|-|--", Shell.PeekStep == 1
            ? Lang.F("Show 1 line further down (++: {0}), or up (-, --)", Shell.PeekBigStep)
            : Lang.F("Show {0} lines further down (++: {1}), or up (-, --)", Shell.PeekStep, Shell.PeekBigStep)),
        ("peek [f] top|middle|bottom|n%", Lang.T("Show the top, middle, end, or from n% down")),
        ("peek +line", Lang.T("Peek the last file again from a line (peek +120)")),
        ("+ n, - n, ++ n, -- n", Lang.T("Move the window n steps (+ 20)")),
        ("peek (or .)", Lang.T("Show the last peek again, as the file is now")),
        ("peek [file|n] /text", Lang.T("Show the first line with the text")),
        ("/, ?", Lang.T("Next match (/), or the one before (?)")),
        ("/text", Lang.T("Search the file peeked last (as peek /text)")),
        ("find [-l|-f|-b] [name] [-t text]", Lang.T("Find files here and below, by name (*.md) and by text")),
        ("find -f, find -b", Lang.T("Show the folder and name, or the name only")),
        ("find [-l|-f|-b]", Lang.T("Show the last search again, as it was found")),
        ("find -r", Lang.T("Search again for the same, where it was searched")),
        ("find|recent|hist|fav ... > $name", Lang.T("Keep the paths of the rows shown in a register")),
        ("new name", Lang.T("Make a new, empty file here (.md if no extension)")),
        ("mkdir name", Lang.T("Make a new directory here")),
        ("rename file|n name", Lang.T("Give a file or directory a new name")),
        ("copy file|n dir|n", Lang.T("Copy a file or directory into a directory")),
        ("move file|n dir|n", Lang.T("Move a file or directory into a directory")),
        ("delete file|n", Lang.T("Delete a file or directory, to the trash (del)")),
        ("info [file|n]", Lang.T("Describe a file or directory (alone: this one)")),
        ("insert file|n top|bottom|+line text", Lang.T("Put a line of text in a file, without opening it")),
        ("change file|n +line|from-to [text]", Lang.T("Replace lines of a file with text, or delete them")),
        ("shift file|n lines place", Lang.T("Move lines in a file: under a heading, +line, top or bottom")),
        ("shift! file|n lines place", Lang.T("Move lines, and tick or untick any task boxes")),
        ("lines file|n lines > file|n place", Lang.T("Copy lines to another file (cut last: move them)")),
        ("lines file|n lines >|>> $name", Lang.T("Keep lines in a register (>>: add them to it)")),
        ("lines file|n lines start|end text", Lang.T("Put a text at the start or the end of lines")),
        ("lines file lines unstart|unend text", Lang.T("Take a text off the start or the end of lines")),
        ("lines file|n lines replace old new", Lang.T("Replace a text in lines, the rest kept")),
        ("lines file|n lines sort [-a|-d]", Lang.T("Sort lines: -a A to Z, capitals first (default), -d Z to A")),
        ("lines file|n lines upper|lower|title", Lang.T("Make lines UPPERCASE, lowercase or Title Case")),
        ("words file|n line", Lang.T("Show the words of a line, numbered, for word")),
        ("word file|n line n|n-m action [text]", Lang.T("Change words by number: replace, before, after, delete")),
        ("word ... upper|lower|title", Lang.T("Make words UPPERCASE, lowercase or Title Case")),
        ("format file|n", Lang.T("Format an HTML, CSS or XML file, as :format does")),
        ("trim file|n", Lang.T("Remove blank space at line ends, as :trim does (trim!)")),
        ("set [name [value]]", Lang.T("Show the settings, or change one")),
        ("reset", Lang.T("Reset all settings to the defaults (asks first)")),
        ("reload", Lang.T("Read the settings file and the registers again, after editing them")),
        ("forget", Lang.T("Forget commands typed, $clip, $peek, $calc, last search, undo (asks)")),
        ("clip [path|name] file|dir|n|.", Lang.T("Copy a path, or a name, to the clipboard")),
        ("clip text file|n", Lang.T("Copy the text of a file to the clipboard")),
        ("clip lines file|n lines", Lang.T("Copy lines of a file (4, 4-6, 4,7, +100 20)")),
        ("clip $name", Lang.T("Copy the value of a register to the clipboard")),
        ("clip ... $clip", Lang.T("The text copied last, in a command")),
        ("path [file|n], name [file|n]", Lang.T("Copy the path, or name, of $file (or of a file)")),
        ("reg [name [text|.|#n]]", Lang.T("Keep a text or a path in a register: $name puts it in a command")),
        ("reg name += text", Lang.T("Add a text to a register, as lines (reg notes += $peek)")),
        ("reg -d name|all", Lang.T("Take away one register, or all")),
        ("echo text", Lang.T("Write a text, with $names put in (echo $file)")),
        ("calc expression", Lang.T("Calculate: + - * / % ^ ( ), the result kept in $calc")),
        ("calc expression > $name", Lang.T("Also keep the result in a register (>>: add it)")),
        ("file [file|n]", Lang.T("Set $file to a file or row (file 3), or show $file")),
        ("file file|n > $name", Lang.T("Keep the full path of a file in a register")),
        ("favorites [-l] [list]", Lang.T("List the favorites (fav), or another list")),
        ("favorites -l", Lang.T("Lists with full paths (also hist, recent, book)")),
        ("favorites lists", Lang.T("List the lists of favorites (*: the open one)")),
        ("favorites main", Lang.T("Open the main list of favorites again")),
        ("favorites new|delete name", Lang.T("Make a list and open it, or delete one")),
        ("favorites rename name new", Lang.T("Give a list of favorites a new name")),
        ("favorites use name", Lang.T("Open a list named like a command (new ...)")),
        ("favorites folder name", Lang.T("Make a folder of favorites, last in the list")),
        ("favorites subfolder [f] name", Lang.T("Make a subfolder, first in the folder [f]")),
        ("favorites rename [f] name", Lang.T("Give a folder of favorites a new name")),
        ("favorites remove [f]", Lang.T("Remove a folder with its favorites (asks first)")),
        ("favorites move rows|[f] where", Lang.T("Move: top, bottom, up, down, out, 5, [folder]")),
        ("favorites move name where", Lang.T("Move by name, or all matching (ch*.md)")),
        ("favorites filter text", Lang.T("Show the favorites with a text in them (or *.md)")),
        ("favorites undo", Lang.T("Undo the last fav remove")),
        ("favorites add file|n [where]", Lang.T("Add to the favorites: top, bottom, 5, [folder]")),
        ("favorites remove file|n", Lang.T("Take off the favorites")),
        ("history [-l] [-s] [-f] [-n]", Lang.T("List the files opened (hist), options as for recent")),
        ("history filter text", Lang.T("Show the files opened with a text in them (or *.md)")),
        ("recent [-l] [-s] [-f] [-n]", Lang.T("List the latest files (-s sizes, -f folder/name, -n by name)")),
        ("recent|hist|fav ... -t text", Lang.T("Only the files with a text in a line, and the line (-t *: line 1)")),
        ("hotlist", Lang.T("List the directory hotlist (hot)")),
        ("hotlist filter text", Lang.T("Show the directories with a text in them")),
        ("hotlist add|remove [dir|n]", Lang.T("Add a directory to the hotlist (this one), or take it off")),
        ("bookmarks [-l]", Lang.T("List the bookmarks (book)")),
        ("projects", Lang.T("List the projects (proj)")),
        ("project n|name", Lang.T("Go to the directory of a project")),
        ("log [n]", Lang.T("Show the last lines of the log (20)")),
        ("clear", Lang.T("Clear the screen (also cls)")),
        ("screen", Lang.T("Go to the file browser here, on the full screen")),
        ("version", Lang.T("Show the version of Blyant")),
        ("about [license]", Lang.T("Show the version and most used commands, or the license")),
        ("again [n]", Lang.T("List the commands typed, or run one again (!n, !!)")),
        ("again filter text", Lang.T("Show the commands typed with a text in them")),
        ("again n|n-m > $name", Lang.T("Keep commands typed in a register, for run (>>: add)")),
        ("undo [n]", Lang.T("Undo the last change of a file (insert, change ...)")),
        ("redo [n]", Lang.T("Undo the last undo")),
        ("changes", Lang.T("List the changes undo goes back through")),
        ("run $name|file", Lang.T("Run commands from a register or file, one per line")),
        ("help [command]", Lang.T("List the commands, or tell what one does")),
        ("exit", Lang.T("Leave Blyant (also quit, q)"))
    };

    /// <summary>
    /// Run a line typed (a command and what follows it)
    /// </summary>
    /// <returns>False when the shell is to be left</returns>
    internal bool Execute(string line)
    {
        line = line.Trim();
        // (a line starting with "#": a comment, in a file of commands and a register run)
        if (line.Length == 0 || line.StartsWith('#')) return true;
        // "!!", "!3", "again 3": a command typed before, written out and run again (as typed then)
        if (AgainNumber(line) is { } number)
        {
            var kept = CommandHistory.Of(CommandContext.Shell);
            // (in a run, the last command kept is the run itself: it would run again and again)
            var refused = _runDepth > 0
                ? Lang.T("again, !n and !! are refused in a run: it would repeat itself")
                : number >= 1 && number <= kept.Count
                    ? null
                    : kept.Count == 0
                        ? Terminal
                            ? Lang.T("No commands typed yet (they are kept while Blyant runs)")
                            : Lang.T("No commands typed: from a file or a pipe they are not kept")
                        : kept.Count == 1
                            ? Lang.F("No command {0}: 1 is kept (again lists it)", number)
                            : Lang.F("No command {0}: {1} are kept (again lists them)", number, kept.Count);
            if (refused != null)
            {
                // English: the log (by name, as other commands)
                EventLog.AddCommand(DialogText.LogShell, "again");
                NewPage();
                Error(refused);
                return true;
            }

            line = kept[number - 1];
            NewPage();
            Line(line);
        }

        // Typed at the prompt: kept for Up and Down and for "again" (the command an "again" ran, not the "again")
        if (_typed && !CommandLine.Name(line).Equals("again", StringComparison.OrdinalIgnoreCase))
        {
            CommandHistory.Add(CommandContext.Shell, line);
        }

        // By name only (never what follows it: a path, a name, the value of a register)
        EventLog.AddCommand(DialogText.LogShell, line);
        // (the file of the registers edited by hand since: read again, so $name and reg use what is in it)
        ShellRegisters.Refresh();
        // $name: the value of the register, or of a name set by Blyant ($clip, $peek, $pwd, $file, $date ...): in the
        // paths a value with blanks is one word; the text of insert, change, reg and set, and what find, project and
        // run take, as it is. ("help $clip" tells of it: nothing is put in after help)
        var name = CommandLine.Name(line).ToLowerInvariant();
        var argument = CommandLine.Argument(line);
        // (what was typed, before the $names are put in: "run $tidy" is a register, "run $pwd/t.txt" a file; "reg
        // notes += $peek" of an empty $peek is not "reg notes +=" with nothing)
        _typedArgument = argument;
        var help = name is "help" or "h";
        // ("lines a.md 4-6 > $notes": the register the lines go to, not its value: kept as the name)
        if (name == "lines") argument = ProtectRegisterTarget(argument);
        // ("file plot.md > $other", "calc 2 * 3 > $x", "again 3 > $tidy", "find -t TODO >> $open": the register the
        // result goes to, kept as the name)
        if (TakesTarget.Contains(name)) argument = ProtectTargetAtEnd(argument);
        // ("calc $calc ^ 2": each value one number, in parentheses)
        if (name == "calc") argument = ShellRegisters.Parenthesized(argument);
        // (in a run, "screen" would leave the run for the full screen, and leave the shell at the next exit)
        var problem = _runDepth > 0 && name == "screen"
            ? Lang.T("screen is refused in a run: run it after")
            : help ? null : ExpansionProblem(name, line, argument);
        if (problem != null)
        {
            NewPage();
            Error(problem);
            return true;
        }

        // Put in after the line is split into the command and what follows it, so a value is not trimmed: the blank
        // lines and blanks at the end of $clip stay. ("clip $note" alone copies the value as it is: not put in first,
        // where it would be taken as a path)
        if (!help && !IsClipOfRegister(line))
        {
            argument = ShellRegisters.Expand(argument, name switch
            {
                "insert" or "change" => 2,
                // (the text of "word notes.md 12 9 replace ..." as it is)
                "word" => 4,
                "reg" or "set" => 1,
                // ("run $tidy": its lines as they are)
                // ("echo $file and $date": the values as they are)
                // ("calc $calc * 2": the value as it is)
                "find" or "project" or "run" or "echo" or "calc" => 0,
                // (the text of "lines todo.md 4 start ..." as it is)
                "lines" => LinesWords(argument),
                _ => int.MaxValue
            });
        }

        // (the line of the command typed is the first row of its page)
        NewPage();
        // (the files the command changes: one step for undo, named by the line as typed)
        var outer = StartStep();
        bool going;
        try
        {
            going = RunCommand(name, argument);
        }
        finally
        {
            EndStep(line, outer);
        }

        // (setting shellautopeek: the window of the last peek again, when the command changed its file)
        if (going) AutoPeek(name);
        return going;
    }

    /// <summary>
    /// The number of the command a line runs again: "!!" (1, the last), "!3", "again 3"; 0 for "again" with a word
    /// that is not a number (told as no command). Null for any other line ("again" alone lists them)
    /// </summary>
    internal static int? AgainNumber(string line)
    {
        line = line.Trim();
        if (line == "!!") return 1;
        if (line.Length > 1 && line[0] == '!' && line[1..].All(char.IsAsciiDigit))
        {
            return int.TryParse(line[1..], out var bang) ? bang : 0;
        }

        if (!CommandLine.Name(line).Equals("again", StringComparison.OrdinalIgnoreCase)) return null;
        var argument = CommandLine.Argument(line);
        // ("again filter lines": a list, as "again" alone; "again 3-5 > $tidy": kept, not run)
        if (argument.Length == 0 || FilterOf(argument) != null || TargetAtEnd(argument) != null) return null;
        return argument.All(char.IsAsciiDigit) && int.TryParse(argument, out var number) ? number : 0;
    }

    // "again": the commands typed at the prompt, the newest first, numbered for "again 3" and "!3" (not a last list:
    // "open 3" is still a row of the list shown before)
    // "again filter lines": those with a text in them, or that a pattern fits ("lines*"), with their numbers, so "!7"
    // runs the one numbered 7
    private void ListAgain(string argument)
    {
        if (TargetAtEnd(argument) is { } target)
        {
            AgainInto(target.Before, target.Name, target.Adds);
            return;
        }

        string? filter = null;
        if (FilterOf(argument) is { } asked)
        {
            filter = asked.Text;
            if (filter.Length == 0)
            {
                Error(Lang.T("Give a text: again filter lines (or lines*)"));
                return;
            }
        }

        var kept = CommandHistory.Of(CommandContext.Shell);
        if (kept.Count == 0)
        {
            Line(Lang.T("No commands typed yet (they are kept while Blyant runs)"));
            return;
        }

        var width = kept.Count.ToString().Length;
        var found = 0;
        for (var i = 0; i < kept.Count; i++)
        {
            if (filter != null && !(ShellCompletion.IsPattern(filter)
                    ? ShellCompletion.PatternFits(filter, kept[i])
                    : kept[i].Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            Line((i + 1).ToString().PadLeft(width + 2) + "  " + kept[i]);
            found++;
        }

        if (found == 0) Line(Lang.F("No commands with {0} in them", filter ?? string.Empty));
    }

    // The commands that take a value of more than one line: insert and change put in its lines, reg keeps them, run
    // runs them, and echo writes them
    private static readonly string[] TakesLines = { "insert", "change", "reg", "run", "echo" };

    // Why the $names of a command line cannot be put in (null: they can): $clip or $peek that is not there yet, or
    // empty where change would delete the lines; a value of more than one line where one line is taken; $file before
    // a command has worked on a file. ("clip $note" alone copies a value as it is)
    private string? ExpansionProblem(string name, string line, string argument)
    {
        var oneLine = !TakesLines.Contains(name) && !IsClipOfRegister(line);
        foreach (var (reserved, value) in new[] { (ShellRegisters.ClipName, ShellRegisters.Clip), (ShellRegisters.PeekName, ShellRegisters.Peek) })
        {
            if (!ShellRegisters.Mentions(argument, reserved)) continue;
            if (value == null)
            {
                return reserved == ShellRegisters.ClipName
                    ? Lang.T("Nothing copied yet: clip copies a text for $clip")
                    : Lang.T("Nothing peeked yet: peek shows lines for $peek");
            }

            if (value.Length == 0 && name == "change")
            {
                return reserved == ShellRegisters.ClipName
                    ? Lang.T("$clip is empty: change would delete the lines")
                    : Lang.T("$peek is empty: change would delete the lines");
            }

            if (oneLine && value.Contains('\n')) return MoreThanOneLine(reserved);
        }

        if (oneLine && ShellRegisters.All.FirstOrDefault(pair => pair.Value.Contains('\n') &&
                ShellRegisters.Mentions(argument, pair.Key)) is { Key: { } several })
        {
            return MoreThanOneLine(several);
        }

        if (ShellRegisters.Calc == null && ShellRegisters.Mentions(argument, ShellRegisters.CalcName))
        {
            return Lang.T("Nothing calculated yet: calc 2 + 3 gives $calc");
        }

        return _file == null && ShellRegisters.Mentions(argument, "file")
            ? Lang.T("No file yet: open, peek, insert, new ... give $file")
            : null;

        static string MoreThanOneLine(string name) =>
            Lang.F("${0} has more than one line: only insert, change, reg, run and echo take it", name);
    }

    // What followed the command, as typed: before the $names were put in
    private string? _typedArgument;

    // The commands that put their result in a register named at the end ("> $name", ">> $name": added to it)
    private static readonly HashSet<string> TakesTarget =
        new() { "file", "calc", "again", "find", "recent", "history", "hist", "favorites", "fav", "favs" };

    /// <summary>
    /// The words after a command with the "$" of the register after a ">" or ">>" at the end made "$$", so that it
    /// stays the name ("calc 2 * 3 > $price") and is not put in as its value
    /// </summary>
    internal static string ProtectTargetAtEnd(string argument)
    {
        var match = System.Text.RegularExpressions.Regex.Match(argument,
            @"(^|\s)>>?\s+\$(?=[A-Za-z][A-Za-z0-9_]*\s*$)");
        return match.Success
            ? argument[..(match.Index + match.Length - 1)] + "$" + argument[(match.Index + match.Length - 1)..]
            : argument;
    }

    /// <summary>
    /// A register named at the end of what follows a command: "> $name" (set) or ">> $name" (added to it as lines).
    /// What comes before it, the name (lowercase), and if it is added to; null when there is none
    /// </summary>
    internal static (string Before, string Name, bool Adds)? TargetAtEnd(string argument)
    {
        var match = System.Text.RegularExpressions.Regex.Match(argument,
            @"(^|\s)(>>?)\s+\$\$?([A-Za-z][A-Za-z0-9_]*)\s*$");
        if (!match.Success) return null;
        return (argument[..match.Index].Trim(), match.Groups[3].Value.ToLowerInvariant(), match.Groups[2].Value == ">>");
    }

    // A value kept in a register named after ">" or ">>" (added to it as lines), saved, and shown as "reg" shows it.
    // The log has the name only
    private void KeepInRegister(string name, string value, bool adds)
    {
        if (adds) ShellRegisters.Add(name, value);
        else ShellRegisters.Set(name, value);
        // English: the log (the name only, never the value)
        EventLog.Add(DialogText.LogShell + (adds ? ": register added to: " : ": register set: ") + name);
        SaveRegisters("  $" + name + " = " + RegisterShown(ShellRegisters.Get(name) ?? string.Empty));
    }

    // A name set by Blyant after ">" is not a register to keep a result in: told (true)
    private bool TargetRefused(string name)
    {
        if (!ShellRegisters.IsReserved(name)) return false;
        Error(ReservedRefused(name));
        return true;
    }

    // The paths of the rows a list shows while a list goes to a register ("find -t TODO > $open"); null: none is
    private List<string>? _shownPaths;

    // A row shown by a list: its full path kept when the list goes to a register
    private void RowShown(Item item)
    {
        if (_shownPaths != null && item.Path.Length > 0) _shownPaths.Add(Path.GetFullPath(item.Path));
    }

    // "find -t TODO > $open", "recent >> $files", "fav filter draft > $drafts": the list shown as always, and the full
    // paths of its rows (only those shown, after a filter) kept in the register, one on each line, whatever the
    // options show (-b, -f). An empty list, or one that fails, leaves the register as it was
    private void ListInto(string argument, Action<string> list)
    {
        if (TargetAtEnd(argument) is not { } target)
        {
            list(argument);
            return;
        }

        if (TargetRefused(target.Name)) return;
        var failed = Failures;
        _shownPaths = new List<string>();
        List<string> paths;
        try
        {
            list(target.Before);
        }
        finally
        {
            paths = _shownPaths;
            _shownPaths = null;
        }

        if (Failures > failed) return;
        if (paths.Count == 0)
        {
            Line(Lang.F("${0} is not changed: the list is empty", target.Name));
            return;
        }

        KeepInRegister(target.Name, string.Join("\n", paths), target.Adds);
    }

    // "file 3", "file plot.md": the file of a row of the last list, or one named, made $file, without peeking or
    // opening it; the last list stays as it was ("file 4", "open 3" go on in it). "file" alone: $file shown.
    // "file plot.md > $other": the full path kept in a register instead (see FileToRegister)
    private void FileCommand(string argument)
    {
        var words = Words(argument);
        if (words.Count == 3 && words[1] == ">")
        {
            FileToRegister(words[0], words[2]);
            return;
        }

        if (words.Count == 3 && words[1] == ">>")
        {
            Error(Lang.T("file keeps one path: file plot.md > $other (lines ... >> $name adds lines)"));
            return;
        }

        if (words.Contains(">"))
        {
            Error(Lang.T("Give a file and a register: file plot.md > $other"));
            return;
        }

        if (words.Count == 0)
        {
            if (_file == null) Error(Lang.T("No file yet: open, peek, insert, new ... give $file"));
            else Line(Lang.F("$file: {0}", _file));
            return;
        }

        if (words.Count > 1)
        {
            Error(Lang.T("Give one file: file 3, file plot.md"));
            return;
        }

        if (Existing(words[0]) is not { } item) return;
        if (item.IsDirectory)
        {
            Error(Lang.F("Not a file: {0} (cd goes to a directory)", item.Path));
            return;
        }

        _file = Path.GetFullPath(item.Path);
        Line(Lang.F("$file: {0}", _file));
    }

    // "calc 1250 * 1.25 + 80": the result written, and kept in $calc ("insert budget.md bottom Total: $calc", "calc
    // $calc / 12"); "calc" alone shows it. A calculation that fails leaves $calc as it was. See Calculator
    private void Calc(string expression)
    {
        // "calc 22 * 3500 > $words": also kept in a register ($calc too); ">> $results" adds it as a line
        var target = TargetAtEnd(expression);
        if (target is { } given)
        {
            if (TargetRefused(given.Name)) return;
            expression = given.Before;
            if (expression.Length == 0)
            {
                Error(Lang.T("Give a calculation: calc 2 + 3 * 4"));
                return;
            }
        }

        if (expression.Trim().Length == 0)
        {
            if (ShellRegisters.Calc is { } last) Line(Lang.F("$calc: {0}", last));
            else Error(Lang.T("Give a calculation: calc 2 + 3 * 4"));
            return;
        }

        var (result, error) = Calculator.Calculate(expression);
        if (result == null)
        {
            Error(error ?? string.Empty);
            return;
        }

        ShellRegisters.Calc = result;
        // (with "> $name" the result is in the line of the register; ">>" shows the register as a whole)
        if (target is not { Adds: false }) Line(result);
        if (target is { } keep) KeepInRegister(keep.Name, result, keep.Adds);
    }

    // "file plot.md > $other", "file 3 > $other", "file $file > $other": the full path of a file kept in a register,
    // so it works from any directory and after a restart; the file must be there. $file and the last list stay as
    // they were. A directory is refused, as "file" refuses it ("reg name ." keeps the directory the shell is in)
    private void FileToRegister(string word, string target)
    {
        if (target is not ['$', .. var name] || !ShellRegisters.IsName(name))
        {
            Error(Lang.T("Give a file and a register: file plot.md > $other"));
            return;
        }

        if (ShellRegisters.IsReserved(name))
        {
            Error(ReservedRefused(name));
            return;
        }

        if (Existing(word) is not { } item) return;
        if (item.IsDirectory)
        {
            Error(Lang.F("Not a file: {0} (reg name . keeps the directory you are in)", item.Path));
            return;
        }

        var path = Path.GetFullPath(item.Path);
        name = name.ToLowerInvariant();
        ShellRegisters.Set(name, path);
        // English: the log (the name only, never the value)
        EventLog.Add(DialogText.LogShell + ": register set: " + name);
        SaveRegisters("  $" + name + " = " + path);
    }

    // "again 3 > $tidy", "again 3-5 > $tidy", "again 6 >> $tidy": commands typed (numbered as "again" lists them,
    // the newest first) kept in a register as they were typed, the oldest first, one on each line, so "run $tidy"
    // runs them in the order they were typed; a $name in them is put in when the register runs
    private void AgainInto(string numbers, string name, bool adds)
    {
        if (TargetRefused(name)) return;
        var parts = numbers.Split('-');
        if (parts.Length is < 1 or > 2 || parts.Any(part => part.Length == 0 || !part.All(char.IsAsciiDigit)) ||
            !int.TryParse(parts[0], out var first) || !int.TryParse(parts[^1], out var last))
        {
            Error(Lang.T("Give the numbers of the commands: again 3 > $tidy, again 3-5 > $tidy"));
            return;
        }

        var kept = CommandHistory.Of(CommandContext.Shell);
        var (low, high) = (Math.Min(first, last), Math.Max(first, last));
        if (low < 1 || high > kept.Count)
        {
            var number = low < 1 ? low : high;
            Error(kept.Count == 1
                ? Lang.F("No command {0}: 1 is kept (again lists it)", number)
                : Lang.F("No command {0}: {1} are kept (again lists them)", number, kept.Count));
            return;
        }

        // (the oldest first: the numbers count from the newest)
        var commands = Enumerable.Range(low, high - low + 1).Reverse().Select(n => kept[n - 1]);
        KeepInRegister(name, string.Join("\n", commands), adds);
    }

    // "forget": what the shell keeps only while Blyant runs, forgotten after asking (in a script or with --run, the
    // answer is no unless --yes is given): the commands typed (again, Up and !n), $clip, $peek and the window of the
    // last peek, $calc, the last search (find alone, find -r), and the undo and redo steps. Saved things stay: the
    // registers, the settings, $file, the directory and the last list
    private void Forget(string argument)
    {
        if (argument.Trim().Length > 0)
        {
            Error(Lang.T("forget takes nothing after it"));
            return;
        }

        // (this command is the newest of the commands typed: not counted)
        var commands = Math.Max(0, CommandHistory.Of(CommandContext.Shell).Count - (_typed ? 1 : 0));
        var note = Lang.F("Commands typed: {0}. Undo steps: {1}. And $clip, $peek, $calc and the last search. " +
                          "Registers, settings and $file stay.", commands, _undo.Count + _redo.Count);
        if (!ChoiceBox.Confirm(Lang.T("Shell memory"), Lang.T("Forget what the shell keeps while Blyant runs?"), note, true))
        {
            Line(Lang.T("Nothing forgotten"));
            return;
        }

        CommandHistory.Clear(CommandContext.Shell);
        ShellRegisters.Clip = null;
        ShellRegisters.Peek = null;
        ShellRegisters.Calc = null;
        ForgetFind();
        _lastPeek = null;
        _lastSearch = null;
        _undo.Clear();
        _redo.Clear();
        Line(Lang.T("Forgotten: the commands typed, $clip, $peek, $calc, the last search and the undo steps"));
    }

    // "echo $file", "echo Tidied on $date": the text written as typed, with the $names put in ("$$": a "$"), a value
    // of several lines with all its lines. A $name that is no register and no name set by Blyant is told: nothing is
    // written. Changes nothing (no file, no last list, not $file)
    private void Echo(string text)
    {
        var unknown = ShellRegisters.NamesIn(_typedArgument ?? string.Empty)
            .FirstOrDefault(name => ShellRegisters.Get(name) == null && !ShellRegisters.IsReserved(name));
        if (unknown != null)
        {
            Error(Lang.F("No register named {0} (reg lists them)", unknown));
            return;
        }

        // (a line at a time: a long value comes in pages)
        foreach (var line in text.Split('\n')) Line(line);
    }

    private bool RunCommand(string name, string argument)
    {
        switch (name)
        {
            case "exit":
            case "quit":
            case "q":
                return false;
            case "pwd":
                Line(Directory);
                break;
            case "ls":
                ListDirectory(argument);
                break;
            case "cd":
                ChangeDirectory(argument);
                break;
            case "up":
                ChangeDirectory("..");
                break;
            case "home":
                ChangeDirectory(string.Empty);
                break;
            case "data":
                // (where it is: next to the program, or in the data of the user)
                ChangeDirectory(Path.GetFullPath(FileIo.GetDataDirectoryName()));
                break;
            case "open":
            case "edit":
                Open(argument);
                break;
            case "resume":
                if (ResumeTarget(argument) is { } target) OpenFile(new Item(target.Path, false), target.Line);
                break;
            case "favorites":
            case "fav":
            case "favs":
                // "fav add 3", "fav remove 3", the lists ("fav lists", "fav new", "fav rename", "fav delete"), else the
                // list ("fav work", "fav use work": another list; "fav main": the main list). "fav > $files": the
                // paths of the list in a register
                if (TargetAtEnd(argument) is { } favTarget && !ListsFavorites(favTarget.Before))
                {
                    Error(Lang.T("Only a list of favorites goes to a register: fav > $name, fav filter draft > $name"));
                    break;
                }

                ListInto(argument, Favorites);
                break;
            case "history":
            case "hist":
                ListInto(argument, rest => ListHistory(int.MaxValue, rest));
                break;
            case "recent":
                ListInto(argument, rest => ListHistory(RecentBox.MaxFiles, rest));
                break;
            case "hotlist":
            case "hot":
                if (Change(argument) is { } addHot) ChangeHotlist(Rest(argument), addHot);
                else ListHotlist(argument);
                break;
            case "peek":
                Peek(argument);
                break;
            // "+", "++", "-", "--" alone: "peek +" ... (the window of the last peek moved; nothing else they could be).
            // With a number, as many steps: "+ 20" 20 lines (as set), "++ 3" 3 big steps
            case "+":
            case "++":
            case "-":
            case "--":
            {
                var steps = 1;
                if (argument.Trim().Length > 0 && !(int.TryParse(argument.Trim(), NumberStyles.None,
                        CultureInfo.InvariantCulture, out steps) && steps >= 1))
                {
                    Error(Lang.F("{0} takes a number of steps, or nothing: {0} 20", name));
                }
                else
                {
                    Peek(name, steps);
                }

                break;
            }
            // "/" and "?" alone: the next line with the text peek searched last, or the one before
            case "/":
            case "?":
                if (argument.Trim().Length > 0) Error(Lang.F("{0} takes nothing after it: peek notes.md /storm, then {0}", name));
                else SearchAgain(name == "/" ? 1 : -1);
                break;
            // "/storm" (a word starting with "/"): a search in the file peeked last, as "peek /storm"
            case var _ when name.Length > 1 && name[0] == '/':
                SearchLastPeek((name + " " + argument).Trim()[1..]);
                break;
            // "." alone: the last peek again ("peek" alone), as the file is now
            case ".":
                if (argument.Trim().Length > 0) Error(Lang.T(". takes nothing after it: . alone shows the last peek again"));
                else PeekAgain();
                break;
            case "find":
                ListInto(argument, Find);
                break;
            case "new":
                NewFile(argument);
                break;
            case "mkdir":
                MakeDirectory(argument);
                break;
            case "rename":
                Rename(argument);
                break;
            case "copy":
                CopyOrMove(argument, false);
                break;
            case "move":
                CopyOrMove(argument, true);
                break;
            case "delete":
            case "del":
                Delete(argument);
                break;
            case "info":
                Info(argument);
                break;
            case "insert":
                Insert(argument);
                break;
            case "shift":
                Shift(argument, false);
                break;
            case "shift!":
                Shift(argument, true);
                break;
            case "lines":
                Lines(argument);
                break;
            case "change":
                ChangeLines(argument);
                break;
            case "format":
                ChangeText(argument, "format");
                break;
            case "trim":
                ChangeText(argument, "trim");
                break;
            case "trim!":
                ChangeText(argument, "trim!");
                break;
            case "set":
                Set(argument);
                break;
            case "reset":
                Reset();
                break;
            case "reload":
                Reload(argument);
                break;
            case "reg":
                Register(argument);
                break;
            case "clip":
                Clip(argument);
                break;
            case "file":
                FileCommand(argument);
                break;
            // As :path and :name in the editor: of $file, or of a file or row given
            case "path":
            case "name":
                ClipPathOrName(name, argument);
                break;
            case "bookmarks":
            case "book":
                ListBookmarks(argument);
                break;
            case "projects":
            case "proj":
                ListProjects();
                break;
            case "project":
                GoToProject(argument);
                break;
            case "log":
                ShowLog(argument);
                break;
            case "version":
                Line(DialogText.ApplicationName + " " + DialogText.ApplicationVersion);
                break;
            case "about":
                About(argument);
                break;
            case "screen":
                // The full screen, in the file browser of this directory (from a file or --run: no screen)
                if (!Terminal)
                {
                    Error(Lang.T("The full screen needs a terminal"));
                    break;
                }

                ScreenDirectory = Directory;
                return false;
            case "clear":
            case "cls":
                // (in a terminal: else there is no screen, and nothing is done)
                ClearScreen?.Invoke();
                break;
            case "run":
                RunCommands(argument);
                break;
            case "echo":
                Echo(argument);
                break;
            case "calc":
                Calc(argument);
                break;
            case "words":
                WordsCommand(argument);
                break;
            case "word":
                WordCommand(argument);
                break;
            case "forget":
                Forget(argument);
                break;
            case "again":
                ListAgain(argument);
                break;
            case "undo":
                Undo(argument, false);
                break;
            case "redo":
                Undo(argument, true);
                break;
            case "changes":
                if (argument.Trim().Length > 0) Error(Lang.T("changes takes nothing after it"));
                else ListChanges();
                break;
            case "help":
            case "h":
                Help(argument);
                break;
            default:
                Error(Lang.F("Unknown command: {0} (help lists the commands)", name));
                break;
        }

        return true;
    }

    // "add ..." (true) or "remove ..." (false) after "fav" or "hot", or null for none of them
    private static bool? Change(string argument) =>
        argument.TrimStart().Split(' ', 2)[0].ToLowerInvariant() switch
        {
            "add" => true,
            "remove" => false,
            _ => null
        };

    // What follows the first word of what follows a command

    private static string Rest(string argument)
    {
        var parts = argument.TrimStart().Split(' ', 2);
        return parts.Length > 1 ? parts[1] : string.Empty;
    }

    #region Places

    // A row that is not in the last list: told with how many rows it has (a list of one row: the file a command worked on)
    private string NoRow(string row) =>
        !_listShown ? Lang.F("No row {0}: no list has been shown", row)
        : LastList.Count == 1 ? Lang.F("No row {0}: the last list has 1 row", row)
        : Lang.F("No row {0}: the last list has {1} rows", row, LastList.Count);

    /// <summary>
    /// The path a command is given: the number of a row of the last list, or a path (not full: from the directory
    /// the shell is in; "~": the home directory). Null (and told) for a number that is not a row
    /// </summary>
    internal Item? Resolve(string argument, bool wantDirectory)
    {
        argument = argument.Trim().Trim('"');
        if (argument.Length > 0 && argument.All(char.IsAsciiDigit) && !Exists(PathOf(argument)))
        {
            if (int.TryParse(argument, out var number) && number >= 1 && number <= LastList.Count)
            {
                var item = LastList[number - 1];
                if (item.Path.Length > 0) return item;
            }

            Error(NoRow(argument));
            return null;
        }

        var path = PathOf(argument);
        return new Item(path, wantDirectory || System.IO.Directory.Exists(path));

        static bool Exists(string path) => File.Exists(path) || System.IO.Directory.Exists(path);
    }

    /// <summary>
    /// The file a word typed stands for, as the commands take it, for Tab (the headings of "shift 3 5 Do"): $names put
    /// in, a row of the last list ("3"), or a path. Nothing is told
    /// </summary>
    internal string CompletionFile(string typed)
    {
        var word = ShellRegisters.Expand(typed, int.MaxValue).Trim().Trim('"');
        return RowOf(word)?.Path ?? PathOf(word);
    }

    // A path typed, as a full path: "~" is the home directory, and a path that is not full is taken from the
    // directory the shell is in (on Windows "/docs" from its drive, not from the drive Blyant was started on)
    internal string PathOf(string typed)
    {
        if (typed.Length == 0) return Directory;
        typed = Shell.FromHome(typed);
        try
        {
            // (with a base path, a path from the root without a drive, "/docs" on Windows, is on the drive of the
            // base path: not on the drive Blyant was started on)
            return Path.GetFullPath(typed, Directory);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return typed;
        }
    }

    // "cd notes", "cd 2": a directory; "cd plot.md", "cd 3" on a file (of the favorites, the history, a search ...): the
    // directory of the file, which is then the last list ("open 1"), and where it went is told
    private void ChangeDirectory(string argument)
    {
        var item = argument.Trim().Length == 0 ? new Item(PathOf("~"), true) : Resolve(argument, true);
        if (item == null) return;
        if (!System.IO.Directory.Exists(item.Path) && File.Exists(item.Path) &&
            Path.GetDirectoryName(Path.GetFullPath(item.Path)) is { Length: > 0 } folder)
        {
            GoTo(folder);
            LastList = new List<Item> { new(Path.GetFullPath(item.Path), false, item.At) };
            Line(Lang.F("In {0} (the directory of {1})", Directory, Path.GetFileName(item.Path)));
            return;
        }

        if (!System.IO.Directory.Exists(item.Path))
        {
            Error(Lang.F("Not found: {0}", item.Path));
            return;
        }

        GoTo(item.Path);
    }

    // The directory the shell is in from now on
    private void GoTo(string path)
    {
        Directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (Directory.Length == 0 || !Path.IsPathRooted(Directory)) Directory = Path.GetFullPath(path);
    }

    // "ls", "ls notes", "ls 2": the folders and files of a directory, as the file browser shows them (hidden and
    // system ones left out: on Linux and macOS the names that start with "."); "ls -a": those too
    private void ListDirectory(string argument)
    {
        // "-a": hidden ones too; "-l": the full paths, without the mark of a favorite (for scripts); before the
        // directory, in any order ("ls -a -l notes")
        var (all, full) = (false, false);
        while (argument.TrimStart() is ['-', 'a' or 'l', ..] trimmed &&
               (trimmed.Length == 2 || char.IsWhiteSpace(trimmed[2])))
        {
            if (trimmed[1] == 'a') all = true;
            else full = true;
            argument = trimmed[2..];
        }

        var item = Resolve(argument, true);
        if (item == null) return;
        if (!System.IO.Directory.Exists(item.Path))
        {
            Error(Lang.F("Not found: {0}", item.Path));
            return;
        }

        try
        {
            var info = new DirectoryInfo(item.Path);
            // The folders first, then the files, each by name (letter case alike)
            // (a favorite marked " *", as in the file browser)
            FileFavorites.Load();
            var rows = info.EnumerateDirectories().Where(Listed).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => (new Item(d.FullName, true),
                    full ? Path.TrimEndingDirectorySeparator(d.FullName) + "/" : d.Name + "/" + FavoriteMark(d.FullName)))
                .Concat(info.EnumerateFiles().Where(Listed).OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(f => (new Item(f.FullName, false), full ? f.FullName : f.Name + FavoriteMark(f.FullName))))
                .ToList();
            Line(item.Path);
            List(rows, Lang.T("(empty)"));
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
        }

        bool Listed(FileSystemInfo entry) => all || (entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;
    }

    #endregion

    #region Lists

    // "fav", "fav work": the favorites of the open list, or of another. "fav -l", "fav -l work": the full paths.
    // "options": false for "fav use -l" (a list named "-l")
    private void ListFavorites(string argument, bool options = true, string? search = null)
    {
        var full = false;
        if (options) (full, argument) = LongList(argument);
        FileFavorites.Load();
        if (argument.Trim().Length > 0)
        {
            // Another list: used from now on, as ":list name" in the file browser
            if (FavoriteList(argument) is not { } list) return;

            FileFavorites.UseList(list);
            // (kept, as ":list" keeps it: the full screen shows the same list)
            FileIo.SaveSettings();
        }

        // "fav -t storm": the favorites with the text in a line, as a filter shows them
        if (search != null)
        {
            FilterFavorites(null, full, search);
            return;
        }

        Line(Lang.F("List: {0}", ListWord(FileFavorites.CurrentList)));
        // The folders of the list are shown as headings (not numbered)
        var items = new List<Item>();
        var levels = new List<int>();
        // (the folders before each row: a folder with a subfolder first in it, or empty folders, are several)
        var headings = new Dictionary<int, List<string>>();
        foreach (var favorite in FileFavorites.Items)
        {
            if (favorite.IsFolder)
            {
                if (!headings.TryGetValue(items.Count, out var before)) headings[items.Count] = before = new List<string>();
                before.Add(new string(' ', 2 * favorite.Level) + "[" + favorite.Name + "]");
                continue;
            }

            var isDirectory = System.IO.Directory.Exists(favorite.Path);
            var at = favorite.Line > 0 ? (favorite.Line, Math.Max(1, favorite.Column)) : ((int, int)?)null;
            items.Add(new Item(favorite.Path, isDirectory, at));
            levels.Add(favorite.Level);
        }

        // (no mark: they are all favorites)
        var names = Names(items.Select(item => (item.Path, item.IsDirectory)).ToList(), full, false);
        var rows = items.Select((item, i) => (item, names[i])).ToList();

        if (headings.Count == 0)
        {
            List(rows, Lang.T("No favorites"));
            return;
        }

        // With folders: the rows with the folder names between them (the numbers are those of the rows)
        ShowRows(rows.Select(row => row.Item1).ToList());
        var width = rows.Count.ToString().Length;
        for (var i = 0; i <= rows.Count; i++)
        {
            if (headings.TryGetValue(i, out var before)) foreach (var heading in before) Line(heading);
            // (in as far as its folder: a favorite of a folder after one of its subfolders is not taken for one of the
            // subfolder's)
            if (i < rows.Count)
            {
                Line(new string(' ', 2 * levels[i]) + (i + 1).ToString().PadLeft(width + 2) + "  " + rows[i].Item2);
                RowShown(rows[i].Item1);
            }
        }
    }

    // "hist", "recent": the files by name and when they were opened. Options before anything else, in any order:
    // "-l" the full paths, "-s" the size of each file, "-f" the folder and the name ("Book/chapter-3.md"), "-n" sorted
    // by name (the rows are numbered as shown, so "open 3" is row 3 of the sorted list)
    private void ListHistory(int most, string argument)
    {
        // "recent -t storm": the files with the text in a line, with the line found (as find -t)
        var (before, search) = TextOption(argument);
        if (search == string.Empty)
        {
            Error(Lang.T("Give a text after -t: recent -t storm, recent -t *"));
            return;
        }

        argument = before;
        var (options, rest) = LeadingOptions(argument);
        // "hist filter draft" ("hist -l -s filter draft": with options)
        string? filter = null;
        if (most == int.MaxValue && rest.Split(' ', 2)[0].Equals("filter", StringComparison.OrdinalIgnoreCase))
        {
            filter = Rest(rest).Trim().Trim('"');
            if (filter.Length == 0)
            {
                Error(Lang.T("Give a text: hist filter draft (or *.md)"));
                return;
            }

            rest = string.Empty;
        }

        var unknown = options.Where(option => option is not ("-l" or "-s" or "-f" or "-n")).Concat(Words(rest)).ToList();
        if (unknown.Count > 0)
        {
            Error(Lang.F("Not taken: {0} (-l full paths, -s sizes, -f folder/name, -n by name)", string.Join(' ', unknown)));
            return;
        }

        var (full, sizes, folder, byName) =
            (options.Contains("-l"), options.Contains("-s"), options.Contains("-f"), options.Contains("-n"));
        FileHistory.Load();
        var entries = FileHistory.Items.Take(most).ToList();
        // (with the full paths the folder is in them already)
        var names = Names(entries.Select(entry => (entry.Path, false)).ToList(), full, withFolder: folder && !full);
        // Sorted by what is shown (letter case alike), and the same names by path, so the order is always the same
        var order = Enumerable.Range(0, entries.Count).ToList();
        if (byName)
        {
            order = order.OrderBy(i => names[i], StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => entries[i].Path, StringComparer.Ordinal).ToList();
        }

        entries = order.Select(i => entries[i]).ToList();
        names = order.Select(i => names[i]).ToList();
        // (the dates lined up after the names; after full paths not: the rows would be wider than the window)
        var width = full || names.Count == 0 ? 0 : names.Max(TextMetrics.WidthOf);
        var sizeTexts = sizes ? entries.Select(entry => SizeOf(entry.Path)).ToList() : null;
        var sizeWidth = sizeTexts is { Count: > 0 } ? sizeTexts.Max(TextMetrics.WidthOf) : 0;
        var rows = entries.Select((entry, i) => (new Item(entry.Path, false),
            Padded(names[i], width) + "  " + entry.Opened.ToString("yyyy-MM-dd HH:mm") +
            (sizeTexts == null ? string.Empty : "  " + sizeTexts[i].PadLeft(sizeWidth)))).ToList();
        if (search != null)
        {
            // The rows with the text: the line found instead of the date ("02-storm.md:3  The storm came"), numbered
            // as in the whole list, so "open 4" opens row 4 at that line
            var pattern = FileSearch.WildcardText(search);
            var found = entries.Select(entry => FileSearch.FirstLineWith(entry.Path, pattern)).ToList();
            rows = entries.Select((entry, i) => found[i] is { } line
                ? (new Item(entry.Path, false, (line.Line + 1, 1), search),
                    names[i] + ":" + (line.Line + 1) + "  " + line.Text)
                : rows[i]).ToList();
            Filtered(rows, i => found[i] != null && (filter == null || Fits(filter, entries[i].Path, null)),
                Lang.F("No file has {0} in a line", search));
            return;
        }

        if (filter != null)
        {
            Filtered(rows, i => Fits(filter, entries[i].Path, null), Lang.F("No files with {0} in them", filter));
            return;
        }

        List(rows, Lang.T("No files opened"));
    }

    // The rows of a list that a filter fits, numbered as in the whole list, which is the last list ("open 7" is row 7
    // of the list without the filter); what is told when none does
    private void Filtered(IReadOnlyList<(Item Item, string Text)> rows, Func<int, bool> fits, string none)
    {
        ShowRows(rows.Select(row => row.Item).ToList());
        var width = rows.Count.ToString().Length;
        var found = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            if (!fits(i)) continue;
            Line((i + 1).ToString().PadLeft(width + 2) + "  " + rows[i].Text);
            RowShown(rows[i].Item);
            found++;
        }

        if (found == 0) Line(none);
    }

    // A filter fits a path (or a name it has: of a directory of the hotlist): the text is in it (letter case alike),
    // or a pattern ("*.md") fits the name of the file or directory, or the name
    private static bool Fits(string filter, string path, string? name)
    {
        if (ShellCompletion.IsPattern(filter))
        {
            return ShellCompletion.PatternFits(filter, Path.GetFileName(Path.TrimEndingDirectorySeparator(path))) ||
                   (name is { Length: > 0 } && ShellCompletion.PatternFits(filter, name));
        }

        return path.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               (name != null && name.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    // "hot": the directories of the hotlist; "hot filter notes" those with a text in their name or path
    private void ListHotlist(string argument)
    {
        DirectoryHotlist.Load();
        var entries = DirectoryHotlist.Items.ToList();
        var rows = entries.Select(entry =>
            (new Item(entry.Path, true),
                (entry.Name.Length > 0 ? entry.Name + "  " : string.Empty) + Shown(entry.Path, true))).ToList();
        if (FilterOf(argument) is { } asked)
        {
            var filter = asked.Text;
            if (filter.Length == 0)
            {
                Error(Lang.T("Give a text: hot filter notes"));
                return;
            }

            Filtered(rows, i => Fits(filter, entries[i].Path, entries[i].Name),
                Lang.F("No directories with {0} in them", filter));
            return;
        }

        List(rows, Lang.T("No directories in the hotlist"));
    }

    // "book": the bookmarks, by their name and the name of the file or directory ("-l": its full path)
    private void ListBookmarks(string argument)
    {
        if (OnlyLong(argument) is not { } full) return;
        Bookmarks.Load();
        var places = Bookmarks.Places.ToList();
        var names = Names(places.Select(entry =>
            (entry.Target.Kind is JumpKind.File or JumpKind.Directory ? entry.Target.FullPath : "?",
                entry.Target.Kind == JumpKind.Directory)).ToList(), full);
        var rows = places.Select((entry, i) =>
        {
            var target = entry.Target;
            var name = entry.Name.Length > 0 ? entry.Name + "  " : string.Empty;
            return target.Kind switch
            {
                JumpKind.File => (new Item(target.FullPath, false, target.At),
                    name + names[i] + (target.At is { } at ? ":" + at.Line : string.Empty)),
                JumpKind.Directory => (new Item(target.FullPath, true), name + names[i]),
                // A page of Blyant: on the full screen only
                _ => (new Item(string.Empty, false), name + Lang.F("page {0} (full screen only)", target.Target))
            };
        }).ToList();
        List(rows, Lang.T("No bookmarks"));
    }

    private void ListProjects()
    {
        var projects = Projects.All();
        var names = Projects.DisplayNames(projects);
        var rows = projects.Select((project, i) =>
            (new Item(project.Path, true), names[i] + "  " + Shown(project.Path, true))).ToList();
        List(rows, Lang.T("No projects"));
    }

    // "project 2" (the number in the list of projects) or "project novel" (its name): go to its directory
    private void GoToProject(string argument)
    {
        argument = argument.Trim();
        var projects = Projects.All();
        var names = Projects.DisplayNames(projects);
        var index = int.TryParse(argument, out var number) && number >= 1 && number <= projects.Count
            ? number - 1
            : names.FindIndex(name => string.Equals(name, argument, StringComparison.CurrentCultureIgnoreCase));
        if (argument.Length == 0 || index < 0)
        {
            Error(Lang.F("No project {0} (projects lists them)", argument));
            return;
        }

        ChangeDirectory(projects[index].Path);
    }

    private void ShowLog(string argument)
    {
        var count = int.TryParse(argument.Trim(), out var n) && n > 0 ? n : 20;
        foreach (var line in EventLog.All.Skip(Math.Max(0, EventLog.All.Count - count))) Line(line);
    }

    // "about": Blyant, its version and author, and the commands of the shell used the most (the about page of the full
    // screen tells of its keys); "about license": the license, as on the second page of About
    private void About(string argument)
    {
        var what = argument.Trim().ToLowerInvariant();
        if (what.Length > 0 && what != "license")
        {
            Error(Lang.T("Give nothing, or license: about license"));
            return;
        }

        // The columns of the window (0: not a terminal, or one that does not tell its size)
        var width = Terminal && WindowSize?.Invoke() is { Width: > 20 } size ? size.Width - 1 : 0;
        if (what == "license")
        {
            // In a terminal a line wider than the window is broken between words (the paragraphs of the license are
            // one line each); to a file or a program, as it is
            foreach (var line in TextBuffer.Normalize(SoftwareLicense.GetLicenseText()).TrimEnd('\n').Split('\n'))
            {
                if (width == 0 || TextMetrics.WidthOf(line) <= width) Line(line);
                else foreach (var part in ChoiceBox.Wrap(line, width)) Line(part);
            }

            return;
        }

        Line(DialogText.ApplicationName + " " + DialogText.ApplicationVersion + " " + DialogText.ApplicationBy);
        Line();
        Line(Lang.T("The commands used the most:"));
        var rows = new[]
        {
            ("help [command]", Lang.T("List the commands, or tell what one does")),
            ("ls, cd 2, up", Lang.T("List a directory, go into row 2, or up")),
            ("open 3", Lang.T("Edit a file on the full screen (closing it returns here)")),
            ("peek 3, find plot", Lang.T("Show lines of a file; find files by name (-t: by text)")),
            ("recent, fav, hot", Lang.T("The latest files, the favorites, the directory hotlist")),
            ("set language de", Lang.T("Language: en, de, da, nb, sv, es, fr, it, pl, nl or fi")),
            ("screen", Lang.T("Go to the file browser here, on the full screen")),
            ("exit", Lang.T("Leave Blyant (also quit, q)"))
        };
        CommandRows(rows, "  ");

        Line();
        Line(Lang.T("about license shows the license."));
        Line(Lang.T("The shell tutorial in the user manual shows more."));
    }

    // Rows of a command and what it does: the texts in a column, a text wider than the window (79 columns when it is
    // not known, as to a file) broken between words under its column
    private void CommandRows(IReadOnlyList<(string Command, string Text)> rows, string indent)
    {
        var width = Terminal && WindowSize?.Invoke() is { Width: > 20 } size ? size.Width - 1 : 79;
        var widest = rows.Max(row => row.Command.Length) + 2;
        var room = width - widest - indent.Length;
        foreach (var (command, text) in rows)
        {
            var parts = room >= 20 ? ChoiceBox.Wrap(text, room) : [text];
            Line(indent + command.PadRight(widest) + parts[0]);
            foreach (var part in parts.Skip(1)) Line(new string(' ', indent.Length + widest) + part);
        }
    }

    private void Help(string argument)
    {
        var rows = HelpRows().ToList();
        var name = argument.Trim().ToLowerInvariant();
        if (name.Length > 0)
        {
            // The command, or the long name of a short one ("fav": favorites)
            var full = name switch
            {
                "fav" or "favs" => "favorites",
                "hist" => "history",
                "hot" => "hotlist",
                "book" => "bookmarks",
                "proj" => "projects",
                "h" => "help",
                "q" or "quit" => "exit",
                "del" => "delete",
                "cls" => "clear",
                // (one row tells both)
                "name" => "path",
                "edit" => "open",
                "+" or "++" or "-" or "--" or "." or "/" or "?" or "$peek" => "peek",
                _ when name.Length > 1 && name[0] == '/' => "peek",
                "$clip" => "clip",
                "$calc" => "calc",
                _ when name.StartsWith('!') => "again",
                _ => name
            };
            // (all the rows of the command: "help clip" tells each of its forms)
            // ("trim!", "shift!": with the command)
            // (the steps "+ n" ... with peek)
            var found = rows.Where(r => r.Command.Split(' ')[0].TrimEnd('!') == full.TrimEnd('!') ||
                                        (full == "peek" && r.Command[0] is '+' or '/')).ToList();
            if (found.Count == 0)
            {
                Error(Lang.F("Unknown command: {0} (help lists the commands)", name));
                return;
            }

            CommandRows(found, string.Empty);
            return;
        }

        CommandRows(rows, "  ");
        Line();
        Line(Lang.T("A number is a row of the last list shown: open 3, cd 2"));
    }

    #endregion

    #region Open

    // "open notes.md", "open 3", "open notes.md +12": the editor of the full screen, and back at the prompt when it is
    // closed. A file that is not there (in a directory that is) is made if you want that. A directory: gone to
    private void Open(string argument)
    {
        // (a name in quotes as it is: "a  b.md" keeps its blanks)
        var words = Words(argument);
        var line = words.Select(word => IsLine(word)
            ? int.TryParse(word[1..], out var n) ? Math.Max(1, n) : int.MaxValue
            : (int?)null).LastOrDefault(n => n != null);
        var name = string.Join(' ', words.Where(word => !IsLine(word)));
        if (name.Length == 0)
        {
            Error(Lang.T("Open what? open notes.md, or open 3 (a row of the last list)"));
            return;
        }

        if (Resolve(name, false) is { } item) OpenFile(item, line);
    }

    // A file in the editor ("open", "resume"), at a line or the line of the item; a directory: gone to
    private void OpenFile(Item item, int? line)
    {
        if (item.IsDirectory)
        {
            ChangeDirectory(item.Path);
            return;
        }

        var result = StartArgument.Resolve(item.Path, Directory, line);
        if (result.Message.Length > 0)
        {
            Error(result.Message);
            return;
        }

        // (before a new file is made: without the editor it would stay empty)
        if (!Terminal)
        {
            Error(Lang.F("{0} is not opened: the editor needs a terminal", result.Path));
            return;
        }

        if (result.AskToCreate)
        {
            if (!ChoiceBox.Confirm(DialogText.NewFile,
                    Lang.F("{0} is not there. Make it?", Path.GetFileName(result.Path)), result.Path, false))
            {
                return;
            }

            try
            {
                // Only a new file: one that has come since it was looked for is not emptied
                FileIo.CreateNewFile(result.Path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Error(e.Message);
                return;
            }
        }

        var at = line is { } l ? (l, 1) : item.At;
        // (the file opened: $file; a directory went to it above)
        _file = Path.GetFullPath(result.Path);
        var stayed = EditOnScreen(result.Path, at);
        EditorClosed();
        if (stayed) Line(Lang.F("Back from the editor: {0}", result.Path));
        else Line(Lang.F("Back from the editor: {0} (the place jumped to is not shown here)", result.Path));
    }

    // The last list when the editor of the full screen was closed last: a line in it is from before the editing, and
    // not where you worked last any more ("resume" then opens where the cursor of the editor was)
    private List<Item>? _listAtEditor;

    /// <summary>
    /// The editor was closed (open, resume): the line of the last list is from before it
    /// </summary>
    internal void EditorClosed() => _listAtEditor = LastList;

    /// <summary>
    /// What "resume" opens: $file (kept when the shell is left, so "resume" goes on after a restart), at the line
    /// given ("resume +120"), else at the line of the last list when that is $file at a line made after the editor
    /// was closed last (insert, lines, change, a search of peek), else (null) where the cursor of the editor was in
    /// it. Null (and told) when there is no $file, it is gone, or more than a line is given
    /// </summary>
    internal (string Path, int? Line)? ResumeTarget(string argument)
    {
        var words = Words(argument);
        if (words.Count > 1 || (words.Count == 1 && !IsLine(words[0])))
        {
            Error(Lang.T("Give only a line, or nothing: resume +120"));
            return null;
        }

        if (_file == null)
        {
            Error(Lang.T("No file yet: open, peek, insert, new ... give $file"));
            return null;
        }

        // (not made again, as open would offer for a file that is not there)
        if (!FileOperations.IsFileThere(_file))
        {
            Error(Lang.F("Not found: {0}", _file));
            return null;
        }

        if (words.Count == 1) return (_file, int.TryParse(words[0][1..], out var given) ? Math.Max(1, given) : int.MaxValue);
        if (!ReferenceEquals(LastList, _listAtEditor) && LastList is [{ At: { } at } only] &&
            FileOperations.NameComparer.Equals(Path.GetFullPath(only.Path), _file))
        {
            return (_file, at.Line);
        }

        return (_file, null);
    }

    // "+12": a line to open the file at
    private static bool IsLine(string word) => word.Length > 1 && word[0] == '+' && word[1..].All(char.IsAsciiDigit);

    // The editor of the full screen, with the file browser of its directory behind it, which closes with it (as when
    // a file is opened from the corkboard). A jump to another place leaves the editor (asking about changes first)
    // and is not made: the shell goes on
    // Returns false if the editor was left for a jump
    private static bool EditOnScreen(string path, (int Line, int Column)? at)
    {
        Shell.OnScreen = true;
        try
        {
            Init.EnterScreen();
            FileExplorer.RunAtFile(path, at, true);
            return true;
        }
        catch (NavigationException)
        {
            Jump.DropPending();
            return false;
        }
        finally
        {
            Shell.OnScreen = false;
            Init.LeaveScreen();
        }
    }

    #endregion
}
