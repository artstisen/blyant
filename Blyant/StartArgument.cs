namespace Blyant;

/// <summary>
/// What Blyant opens when it is started with a file or directory, as "blyant notes.md" in a terminal (a path that
/// is not a full path is taken from the directory Blyant is started in), and "+12" to open the file at line 12 (as
/// Vim and other editors are started by other programs: "blyant +12 notes.md"):
///   a text file      opened in the editor, at the line if one is given, with the file browser in its directory behind it
///   a directory      shown in the file browser
///   a file that is not there, in a directory that is: Blyant asks to make it
/// The place is opened as a jump of the Jump menu (Jump.Pending). This class does not touch the console.
/// </summary>
internal static class StartArgument
{
    /// <summary>
    /// What a path given at startup leads to: a place to open, or a file to make first (when the user wants that),
    /// or a message (the path cannot be used)
    /// </summary>
    internal sealed record Result(JumpTarget? Target, string Path, bool AskToCreate = false, string Message = "");

    /// <summary>
    /// The arguments Blyant is started with: the file or directory (null: none), the line to open a file at (null:
    /// where it was left), or a message when they cannot be used
    /// </summary>
    internal sealed record Arguments(string? Path, int? Line, string Message = "");

    /// <summary>
    /// Read the arguments (after the options are answered): one file or directory, and "+12", the line to open the
    /// file at, before it or after it. A file or directory named "+12" that is there is that file or directory
    /// </summary>
    internal static Arguments Parse(IReadOnlyList<string> arguments, string currentDirectory)
    {
        string? path = null;
        int? line = null;
        foreach (var argument in arguments)
        {
            if (LineOf(argument, currentDirectory) is { } number)
            {
                line = number;
                continue;
            }

            if (path != null) return new Arguments(null, null, Lang.F("One file or directory at a time: {0}", argument));
            path = argument;
        }

        // A line without a file to open at it: told (Blyant starts as it is set to)
        if (path == null && line is { } at)
        {
            return new Arguments(null, null, Lang.F("No file to open at line {0}", at));
        }

        return new Arguments(path, line);
    }

    /// <summary>
    /// The first of the arguments that is an option ("--help", "-v" ...), wherever it is given ("blyant +12 --help",
    /// "blyant notes.md -v"), or null
    /// </summary>
    internal static string? FirstOption(IReadOnlyList<string> arguments, string currentDirectory) =>
        arguments.FirstOrDefault(argument => IsOption(argument, currentDirectory));

    /// <summary>
    /// "--shell" (Blyant on lines, see Shell), and "--yes" with it: taken out of the arguments (a file of that name
    /// that is there is a file)
    /// </summary>
    /// <returns>True for the shell</returns>
    internal static bool TakeShellOptions(List<string> arguments, string currentDirectory, out bool yes) =>
        TakeShellOptions(arguments, currentDirectory, out yes, out _);

    /// <summary>
    /// "--shell", "--run command" (a command run by the shell, as many as are given: Blyant ends after them) and
    /// "--yes" with them: taken out of the arguments (a file of that name that is there is a file)
    /// </summary>
    /// <returns>True for the shell (also with --run)</returns>
    internal static bool TakeShellOptions(List<string> arguments, string currentDirectory, out bool yes,
        out List<string> run)
    {
        run = new List<string>();
        for (var i = 0; i < arguments.Count - 1; i++)
        {
            if (arguments[i] != "--run" || !IsOption(arguments[i], currentDirectory)) continue;
            run.Add(arguments[i + 1]);
            arguments.RemoveRange(i, 2);
            i--;
        }

        var shell = arguments.RemoveAll(argument => argument == "--shell" && IsOption(argument, currentDirectory)) > 0 ||
                    run.Count > 0;
        yes = shell && arguments.RemoveAll(argument => argument == "--yes" && IsOption(argument, currentDirectory)) > 0;
        return shell;
    }

    // "+12": the line 12 (a line before the first is the first), or null for another argument
    private static int? LineOf(string argument, string currentDirectory)
    {
        if (argument.Length < 2 || argument[0] != '+' || !argument[1..].All(char.IsAsciiDigit)) return null;
        try
        {
            var path = System.IO.Path.GetFullPath(argument, currentDirectory);
            if (File.Exists(path) || Directory.Exists(path)) return null;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException)
        {
            // (not a path: a line)
        }

        return int.TryParse(argument[1..], out var line) ? Math.Max(1, line) : int.MaxValue;
    }

    /// <summary>
    /// True if the argument is an option ("--help", "-v" ...): it starts with "-" and is not a file or directory
    /// that is there (a file can be named "-notes.md")
    /// </summary>
    internal static bool IsOption(string argument, string currentDirectory)
    {
        if (!argument.StartsWith('-') || argument == "-") return false;
        try
        {
            var path = System.IO.Path.GetFullPath(argument, currentDirectory);
            return !File.Exists(path) && !Directory.Exists(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException)
        {
            return true;
        }
    }

    /// <summary>
    /// Answer an option: the usage or the version, written to the terminal. Returns the exit code (1 for an option
    /// that is not known). English: written before the settings (and the language) are read
    /// </summary>
    internal static int AnswerOption(string option, TextWriter output)
    {
        switch (option.ToLowerInvariant())
        {
            case "--version" or "-v" or "-version":
                output.WriteLine(DialogText.ApplicationName + " " + DialogText.ApplicationVersion);
                return 0;
            case "--help" or "-h" or "-?" or "-help":
                output.WriteLine(Usage);
                return 0;
            default:
                output.WriteLine("Unknown option: " + option);
                output.WriteLine(Usage);
                return 1;
        }
    }

    // English: see AnswerOption
    private const string Usage =
        "Usage: blyant [+line] [file or directory]\n" +
        "  A file is opened in the editor, a directory in the file browser. A file that\n" +
        "  is not there (in a directory that is) can be made. Without a path, Blyant\n" +
        "  starts as it is set to.\n" +
        "  +12           Open the file at line 12\n" +
        "  --shell [dir] Use Blyant on lines: type commands at a prompt (help lists\n" +
        "                them). Commands can also be read from a file\n" +
        "  --run \"cmd\"   Run a command of the shell, and end (--run can be given more\n" +
        "                than once): blyant --run \"format page.html\"\n" +
        "  --yes         With --shell or --run, for commands that are not typed: answer\n" +
        "                yes to questions (else nothing is done that asks first)\n" +
        "  --help, -h    Show this text\n" +
        "  --version, -v Show the version";

    /// <summary>
    /// Find what a path given at startup leads to
    /// </summary>
    /// <param name="argument">The path, as given</param>
    /// <param name="currentDirectory">The directory Blyant is started in</param>
    /// <param name="line">The line to open a file at ("+12"), or null</param>
    internal static Result Resolve(string argument, string currentDirectory, int? line = null)
    {
        var result = ResolvePath(argument, currentDirectory);
        // A file opened at the line (a directory has none)
        return line is { } at && result.Target is { Kind: JumpKind.File } target
            ? result with { Target = target with { At = (at, 1) } }
            : result;
    }

    private static Result ResolvePath(string argument, string currentDirectory)
    {
        string path;
        try
        {
            path = System.IO.Path.GetFullPath(argument.Trim().Trim('"'), currentDirectory);
        }
        catch (Exception e)
        {
            return new Result(null, argument, Message: e.Message);
        }

        if (Directory.Exists(path)) return new Result(new JumpTarget(JumpKind.Directory, path), path);
        if (File.Exists(path)) return new Result(new JumpTarget(JumpKind.File, path), path);

        // Not there: a text file can be made (in a directory that is there)
        var parent = System.IO.Path.GetDirectoryName(path);
        if (parent != null && Directory.Exists(parent))
        {
            return new Result(new JumpTarget(JumpKind.File, path), path, AskToCreate: true);
        }

        return new Result(null, path, Message: Lang.F("Not found: {0}", path));
    }
}
