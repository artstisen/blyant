namespace Blyant;

class Program
{
    private static void Main(string[] args)
    {
        // A path given (relative to the directory Blyant is started in, which is read before anything else)
        var currentDirectory = Directory.GetCurrentDirectory();
        // An option (--help, --version) is answered before anything is shown (also to a pipe, as "blyant --help |
        // less"): it is never taken for the name of a file to make
        if (args.Length > 0 && StartArgument.IsOption(args[0], currentDirectory))
        {
            Environment.Exit(StartArgument.AnswerOption(args[0], Console.Out));
        }

        // Keys are read one at a time from the terminal: with its input from a file or a pipe Blyant cannot run
        if (Console.IsInputRedirected)
        {
            // English: written before the settings (and the language) are read
            Console.Error.WriteLine("Blyant runs in a terminal: its input cannot come from a file or pipe.");
            Environment.Exit(1);
        }

        Init.RunInitialization();
        if (Lang.Missing != null)
        {
            // The language set has no file (a language file in a folder "lang" that is gone): English is used
            Tools.ClearScreen();
            ChoiceBox.Message(DialogText.CaptionsSettings,
                Lang.F("The language file {0} was not found: English is used.", Lang.Missing + ".txt"),
                Lang.T("Put it in the folder lang next to Blyant (or in the data directory)."));
        }

        // Started with a file or directory ("blyant notes.md"): it is opened as a jump, without the start page
        if (args.Length > 0 && OpenArgument(args[0], currentDirectory) is { } target)
        {
            Jump.OpenAtStart(target);
        }
        else
        {
            StartMenu.Start();
        }

        MainLoop();
    }

    // The place a path given at startup leads to, or null (a message tells why, or the user did not want a file
    // that is not there to be made)
    private static JumpTarget? OpenArgument(string argument, string currentDirectory)
    {
        var result = StartArgument.Resolve(argument, currentDirectory);
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
