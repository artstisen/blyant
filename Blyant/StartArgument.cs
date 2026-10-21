namespace Blyant;

/// <summary>
/// What Blyant opens when it is started with a file or directory, as "blyant notes.md" in a terminal (a path that
/// is not a full path is taken from the directory Blyant is started in):
///   a text file      opened in the editor, with the file browser in its directory behind it
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
    /// Find what a path given at startup leads to
    /// </summary>
    /// <param name="argument">The path, as given</param>
    /// <param name="currentDirectory">The directory Blyant is started in</param>
    internal static Result Resolve(string argument, string currentDirectory)
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
