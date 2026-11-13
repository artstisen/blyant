namespace Blyant;

/// <summary>
/// The shell ("blyant --shell"): Blyant used on lines, in any terminal, with a screen reader or from a script. A
/// command is typed at a prompt, and what it shows is written as lines of text: no colors, and no cursor moved. The
/// lists are numbered, and the number of a row of the last list can be given to the next command ("open 3"). Files are
/// edited in the editor of the full screen, and closing it is back at the prompt. See ShellSession
/// </summary>
internal static class Shell
{
    /// <summary>
    /// The shell that runs (null: Blyant runs on the full screen)
    /// </summary>
    internal static ShellSession? Current { get; private set; }

    /// <summary>
    /// The full screen is shown from the shell (the editor of a file opened with "open"): its boxes ask the questions
    /// </summary>
    internal static bool OnScreen { get; set; }

    /// <summary>
    /// The shell that asks the questions of Blyant on lines (ChoiceBox), or null: while the shell runs, and the full
    /// screen is not shown from it
    /// </summary>
    internal static ShellSession? Prompts => OnScreen ? null : Current;

    /// <summary>
    /// Run the shell on the terminal until it is left ("exit", or the end of the commands read from a file)
    /// </summary>
    /// <param name="directory">The directory it starts in</param>
    /// <param name="yes">Commands read from a file (not a terminal): their questions are answered yes</param>
    /// <returns>The exit code: 1 if a command read from a file failed, else 0</returns>
    internal static int Run(string directory, bool yes)
    {
        var terminal = !Console.IsInputRedirected && !Console.IsOutputRedirected;
        var session = new ShellSession(Console.In, Console.Out, Console.Error, directory, terminal, yes);
        return Run(session);
    }

    /// <summary>
    /// Run a shell (of the terminal, or of a test) until it is left
    /// </summary>
    internal static int Run(ShellSession session)
    {
        Current = session;
        try
        {
            return session.Run();
        }
        finally
        {
            Current = null;
            OnScreen = false;
        }
    }
}
