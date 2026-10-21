namespace Blyant;

/// <summary>
/// Thrown when a new screen has been drawn and is waiting for input. It ends whatever led to the screen
/// and is caught by the main loop (see Program), which reads the next key for the screen.
/// </summary>
internal sealed class NavigationException : Exception
{
}

/// <summary>
/// The places of the application lead to each other: a jump from the Jump menu leaves the page it was made on
/// for a file, a directory or a page of Configuration. Instead of the pages calling each other ever deeper, the
/// page that is left calls Navigation.Wait. This returns control to the main loop, which then goes to the place
/// (Jump.Pending), or shows the start page.
/// </summary>
internal static class Navigation
{
    /// <summary>
    /// The screen of the current mode is drawn and ready: return to the main loop to wait for the next key.
    /// Nothing after a call to this method is executed.
    /// </summary>
    internal static void Wait()
    {
        throw new NavigationException();
    }
}
