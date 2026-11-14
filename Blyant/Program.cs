namespace Blyant;

class Program
{
    private static void Main(string[] args)
    {
        // A path given (relative to the directory Blyant is started in, which is read before anything else)
        var currentDirectory = Directory.GetCurrentDirectory();
        var arguments = args.ToList();
        // "--shell": Blyant on lines (and "--yes" with it)
        var asked = StartArgument.TakeShellOptions(arguments, currentDirectory, out var yes, out var run, out var onScreen);
        // An option (--help, --version) is answered before anything is shown (also to a pipe, as "blyant --help |
        // less"), wherever it is given ("blyant +12 --help"): it is never taken for the name of a file to make
        if (StartArgument.FirstOption(arguments, currentDirectory) is { } option)
        {
            Environment.Exit(StartArgument.AnswerOption(option, Console.Out));
        }

        // The shell: asked for, or set (Configuration: General) when nothing else is given. The setting is read before
        // anything is shown, so the shell starts without the screen of the full screen
        var shell = StartArgument.StartsAsShell(asked, onScreen, arguments, FileIo.StartsInShell);

        // Keys are read one at a time from the terminal: with its input from a file or a pipe Blyant cannot run (the
        // shell can: it reads commands from it)
        if (!shell && Console.IsInputRedirected)
        {
            // English: written before the settings (and the language) are read
            Console.Error.WriteLine("Blyant runs in a terminal: its input cannot come from a file or pipe.");
            Environment.Exit(1);
        }

        Init.RunInitialization(shell);
        if (shell)
        {
            var code = RunShell(arguments, currentDirectory, yes, run);
            // "screen": on to the full screen
            if (Shell.TakeScreenRequest() is { } screen)
            {
                ToScreen(screen);
                MainLoop();
            }

            Environment.Exit(code);
        }
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

        // Without a directory given: the one of the setting shelldir (not for --run: its commands are of the directory
        // Blyant is started in)
        string? note = null;
        if (start.Path == null && run.Count == 0) directory = Shell.StartIn(currentDirectory, out note);
        if (run.Count == 0) return Shell.Run(directory, yes, null, note);
        var session = new ShellSession(new StringReader(string.Join("\n", run) + "\n"), Console.Out, Console.Error,
            directory, false, yes);
        return Shell.Run(session);
    }

    // ":shell" on the full screen: the places of the file browser kept (as when Blyant is left), the terminal as it
    // was, and the shell in the directory of the page (the file edited: the last list). "exit" there leaves Blyant,
    // "screen" comes back to the full screen
    private static void ToShell(Shell.Switch request)
    {
        FileExplorer.RememberOnExit();
        Init.LeaveScreen();
        // (a page without a directory of its own, as the start page: that of the setting shelldir)
        string? note = null;
        var directory = request.Directory is { Length: > 0 } asked && Directory.Exists(asked)
            ? asked
            : Shell.StartIn(Directory.GetCurrentDirectory(), out note);
        var code = Shell.Run(directory, false, request.File, note);
        if (Shell.TakeScreenRequest() is { } screen) ToScreen(screen);
        else Environment.Exit(code);
    }

    // "screen" in the shell: the full screen, in the file browser of the directory (closing it shows the start page)
    private static void ToScreen(string directory)
    {
        Init.EnterScreen();
        Jump.OpenAtStart(new JumpTarget(JumpKind.Directory, directory));
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
                if (Shell.Requested is { } request)
                {
                    // ":shell": Blyant goes on as the shell (and back to the full screen with "screen")
                    Shell.Requested = null;
                    ToShell(request);
                }
                else if (Jump.Pending != null)
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
