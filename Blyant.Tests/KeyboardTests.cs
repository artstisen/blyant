namespace Blyant.Tests;

/// <summary>
/// The key of a page: a key handed back by a page that closed before it could use it is read first (a peek closed
/// by Esc, with the key typed right after it), the key that has a page drawn again, and the log of a partial copy
/// taken out of the trash
/// </summary>
[Collection("Static state")]
public class KeyboardTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "blyant-keys-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Keyboard.TakePending();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public void AKeyHandedBack_IsReadNext_Once()
    {
        var j = new ConsoleKeyInfo('j', ConsoleKey.J, false, false, false);
        Keyboard.PushBack(j);
        // (read without the console: the key handed back comes first)
        Assert.Equal(j, Keyboard.ReadKey());
        Assert.Null(Keyboard.TakePending());
    }

    [Fact]
    public void TheKeyThatHasAPageDrawnAgain_IsNoKeyOfAPage()
    {
        Assert.True(Keyboard.IsRedraw(Keyboard.RedrawKey));
        Assert.False(Keyboard.IsRedraw(new ConsoleKeyInfo('j', ConsoleKey.J, false, false, false)));
        // F24 typed (with a character) is not taken for it
        Assert.False(Keyboard.IsRedraw(new ConsoleKeyInfo('x', ConsoleKey.F24, false, false, false)));
        Assert.Equal('\0', Keyboard.RedrawKey.KeyChar);
    }

    [Fact]
    public void APartialCopyTakenOutOfTheTrash_IsLogged()
    {
        var partial = Path.Combine(_directory, "trash", "2026-10-24 10.00.00 notes");
        Directory.CreateDirectory(Path.Combine(partial, "sub"));
        File.WriteAllText(Path.Combine(partial, "sub", "a.md"), "x");
        EventLog.Clear();
        FileOperations.RemovePartialCopy(partial);
        Assert.False(Directory.Exists(partial));
        Assert.Contains(EventLog.All, line => line.Contains("Partial copy removed from the trash: " + partial));
        // Nothing to remove: nothing logged
        EventLog.Clear();
        FileOperations.RemovePartialCopy(partial);
        Assert.Empty(EventLog.All);
    }
}
