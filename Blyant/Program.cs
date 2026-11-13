namespace Blyant;

class Program
{
    private static void Main(string[] args)
    {
        // A path given (relative to the directory Blyant is started in, which is read before anything else)
        var currentDirectory = Directory.GetCurrentDirectory();
        var arguments = args.ToList();
        // "--shell": Blyant on lines (and "--yes" with it)
        var shell = StartArgument.TakeShellOptions(arguments, currentDirectory, out var yes, out var run);
        // An option (--help, --version) is answered before anything is shown (also to a pipe, as "blyant --help |
        // less"), wherever it is given ("blyant +12 --help"): it is never taken for the name of a file to make
        if (StartArgument.FirstOption(arguments, currentDirectory) is { } option)
        {
            Environment.Exit(StartArgument.AnswerOption(option, Console.Out));
        }

        // Keys are read one at a time from the terminal: with its input from a file or a pipe Blyant cannot run (the
        // shell can: it reads commands from it)
        if (!shell && Console.IsInputRedirected)
        {
            // English: written before the settings (and the language) are read
            Console.Error.WriteLine("Blyant runs in a terminal: its input cannot come from a file or pipe.");
            Environment.Exit(1);
        }

        Init.RunInitialization(shell);
        if (shell) Environment.Exit(RunShell(arguments, currentDirectory, yes, run));
        if (Lang.Missing != null)
        {
            // The language set has no file (a language file in a folder "lang" that is gone): English is used
            Tools.ClearScreen();
            ChoiceBox.Message(DialogText.CaptionsSettings,
                Lang.F("The language file {0} was not found: English is used.", Lang.Missing + ".txt"),
                Lang.T("Put it in the folder lang next to Blyant (or in the data directory)."));
        }

        // Started with a file or directory ("blyant notes.md", "blyant +12 notes.md" at line 12): it is opened as a
        // jump, without the start page
        var start = StartArgument.Parse(arguments, currentDirectory);
        if (start.Message.Length > 0)
        {
            Tools.ClearScreen();
            ChoiceBox.Message(DialogText.ApplicationName, start.Message, string.Empty);
            StartMenu.Start();
        }
        else if (start.Path != null && OpenArgument(start.Path, start.Line, currentDirectory) is { } target)
        {
            Jump.OpenAtStart(target);
        }
        else
        {
            StartMenu.Start();
        }

        MainLoop();
    }

    // The shell, in the directory given (or of the file given), or the one Blyant is started in. Returns the exit code
    // With "--run": the commands given, as read from a file (no prompt, questions not asked), and Blyant ends
    private static int RunShell(IReadOnlyList<string> arguments, string currentDirectory, bool yes,
        IReadOnlyList<string> run)
    {
        if (Lang.Missing != null)
        {
            Console.Error.WriteLine(Lang.F("The language file {0} was not found: English is used.",
                Lang.Missing + ".txt"));
        }

        var start = StartArgument.Parse(arguments, currentDirectory);
        if (start.Message.Length > 0)
        {
            Console.Error.WriteLine(start.Message);
            return 1;
        }

        var directory = currentDirectory;
        if (start.Path != null)
        {
            var result = StartArgument.Resolve(start.Path, currentDirectory);
            if (result.Target is { Kind: JumpKind.Directory } folder && !result.AskToCreate)
            {
                directory = folder.FullPath;
            }
            else if (result.Target is { Kind: JumpKind.File } file && !result.AskToCreate)
            {
                directory = Path.GetDirectoryName(file.FullPath) ?? currentDirectory;
            }
            else
            {
                Console.Error.WriteLine(result.Message.Length > 0 ? result.Message : Lang.F("Not found: {0}", result.Path));
                return 1;
            }
        }

        if (run.Count == 0) return Shell.Run(directory, yes);
        var session = new ShellSession(new StringReader(string.Join("\n", run) + "\n"), Console.Out, Console.Error,
            directory, false, yes);
        return Shell.Run(session);
    }

    // The place a path given at startup leads to, or null (a message tells why, or the user did not want a file
    // that is not there to be made)
    private static JumpTarget? OpenArgument(string argument, int? line, string currentDirectory)
    {
        var result = StartArgument.Resolve(argument, currentDirectory, line);
        if (result.Message.Length > 0)
        {
            Tools.ClearScreen();
            ChoiceBox.Message(DialogText.ApplicationName, result.Message, string.Empty);
            return null;
        }

        if (!result.AskToCreate) return result.Target;
        Tools.ClearScreen();
        if (!ChoiceBox.Confirm(DialogText.NewFile, Lang.F("{0} is not there. Make it?", Path.GetFileName(result.Path)),
                result.Path, false))
        {
            return null;
        }

        try
        {
            // Only a new file: one that has come since it was looked for is not emptied
            FileIo.CreateNewFile(result.Path);
            return result.Target;
        }
        catch (Exception e)
        {
            ChoiceBox.Message(DialogText.NewFile, e.Message, result.Path);
            return null;
        }
    }

    // Each round shows the start page, or goes to the place picked in the Jump menu. A page that leads to
    // another place ends with Navigation.Wait, which brings control back here (see Navigation)
    private static void MainLoop()
    {
        // Tab after ":book " completes the names of the bookmarks, on every page
        CommandCompletion.Arguments["book"] = Bookmarks.NamesToComplete;
        // ... after ":list " the lists of favorites, and after ":jump " the names of the pages
        CommandCompletion.Arguments["list"] = FileFavorites.ListWords;
        CommandCompletion.Arguments["board"] = Corkboard.BoardWords;
        CommandCompletion.Arguments["corkboard"] = Corkboard.BoardWords;
        foreach (var jump in new[] { "jump", "ju", "jmp" }) CommandCompletion.Arguments[jump] = () => JumpPages.Names;
        // ... and after ":colorscheme " the names of the color schemes (which can have spaces in them)
        foreach (var scheme in CommandCompletion.SchemeCommands) CommandCompletion.Arguments[scheme] = ColorSchemes.AllNames;
        while (true)
        {
            try
            {
                if (Jump.Pending != null)
                {
                    // A place was picked in the Jump menu (or given at startup): go there
                    Jump.RunPending();
                }
                else
                {
                    StartMenu.Run();
                }
            }
            catch (NavigationException)
            {
                // Another place is to be shown: continue with it
            }
            catch (Exception ex)
            {
                try
                {
                    Tools.FatalError(ex);
                }
                catch (NavigationException)
                {
                    // Restarted
                }
            }
        }
    }
}
