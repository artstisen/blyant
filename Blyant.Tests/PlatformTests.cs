namespace Blyant.Tests;

/// <summary>
/// What Blyant does for Linux and macOS terminals, tested where it can be on any system: the bright colors on a
/// terminal of 8 colors (the text console of a Raspberry Pi), Esc and a key that come as one, names of copies and
/// of the trash that every file system takes, and the terminals that drop the clipboard sequence
/// </summary>
public class PlatformTests
{
    [Theory]
    [InlineData("linux", true)]
    [InlineData("linux-16color", true)]
    [InlineData("xterm", true)]
    [InlineData("screen", true)]
    [InlineData("xterm-256color", false)]
    [InlineData("tmux-256color", false)]
    [InlineData(null, false)]
    public void TheTerminalsOfEightColors_AreKnownByTheirName(string? term, bool expected)
    {
        Assert.Equal(expected, ConsoleColors.HasEightColors(term));
    }

    [Theory]
    [InlineData(ConsoleColor.DarkGray, false, "\u001b[90m")]
    [InlineData(ConsoleColor.White, false, "\u001b[97m")]
    [InlineData(ConsoleColor.Red, false, "\u001b[91m")]
    [InlineData(ConsoleColor.Blue, true, "\u001b[104m")]
    [InlineData(ConsoleColor.Yellow, true, "\u001b[103m")]
    public void ABrightColor_IsSetWithItsOwnCode(ConsoleColor color, bool background, string expected)
    {
        Assert.Equal(expected, ConsoleColors.BrightCode(color, background));
    }

    [Theory]
    [InlineData(ConsoleColor.Black)]
    [InlineData(ConsoleColor.Gray)]
    [InlineData(ConsoleColor.DarkBlue)]
    public void ADarkColor_IsLeftToDotNet(ConsoleColor color)
    {
        Assert.Null(ConsoleColors.BrightCode(color, false));
    }

    [Fact]
    public void EscAndAKeyThatCameAsOne_AreSplit_ButNotAltGrOrControlKeys()
    {
        Assert.True(TextArea.IsEscapeAndKey(new ConsoleKeyInfo('j', ConsoleKey.J, false, true, false)));
        Assert.True(TextArea.IsEscapeAndKey(new ConsoleKeyInfo('U', ConsoleKey.U, true, true, false)));
        // AltGr (Ctrl+Alt) types a character; Alt with a control character (Alt+Enter) is no text
        Assert.False(TextArea.IsEscapeAndKey(new ConsoleKeyInfo('@', ConsoleKey.D2, false, true, true)));
        Assert.False(TextArea.IsEscapeAndKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, true, false)));
        Assert.False(TextArea.IsEscapeAndKey(new ConsoleKeyInfo('j', ConsoleKey.J, false, false, false)));
    }

    [Theory]
    [InlineData("chapter 1.md", "chapter 1.md")]
    [InlineData("a:b?.md", "a_b_.md")]
    [InlineData("what*is\"it\".txt", "what_is_it_.txt")]
    [InlineData("back\\slash|pipe<>.md", "back_slash_pipe__.md")]
    [InlineData("ends with dot.", "ends with dot")]
    [InlineData("...", "_")]
    public void AStoredName_CanBeMadeOnEveryFileSystem(string name, string expected)
    {
        Assert.Equal(expected, FileOperations.StoredName(name));
    }

    [Fact]
    public void AStoredName_IsShortEnoughForATimeOrACodeToBeAdded()
    {
        var stored = FileOperations.StoredName(new string('a', 250) + ".md");
        Assert.Equal(180, stored.Length);
        // Counted in bytes, as Linux and macOS count a name: "ø" takes two, and no character is split
        var danish = FileOperations.StoredName(new string('ø', 150) + ".md");
        Assert.Equal(90, danish.Length);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount("2026-10-24 10.00.00 " + danish) <= 255);
        var emoji = FileOperations.StoredName(new string('a', 179) + "😀");
        Assert.Equal(179, emoji.Length);
    }

    [Theory]
    [InlineData("TERM=linux", true)]
    [InlineData("TERM=xterm-256color VTE_VERSION=7600", true)]
    [InlineData("TERM=xterm-256color TERM_PROGRAM=Apple_Terminal", true)]
    [InlineData("TERM=xterm-256color TERM_PROGRAM=iTerm.app", false)]
    [InlineData("TERM=xterm-kitty", false)]
    // VTE_VERSION passed on from GNOME Terminal to another terminal, or to tmux, that takes the sequence
    [InlineData("TERM=xterm-kitty VTE_VERSION=7600", false)]
    [InlineData("TERM=xterm-256color VTE_VERSION=7600 KITTY_WINDOW_ID=1", false)]
    [InlineData("TERM=wezterm VTE_VERSION=7600", false)]
    [InlineData("TERM=xterm-256color VTE_VERSION=7600 WEZTERM_EXECUTABLE=/usr/bin/wezterm", false)]
    [InlineData("TERM=alacritty VTE_VERSION=7600", false)]
    [InlineData("TERM=tmux-256color VTE_VERSION=7600 TMUX=/tmp/tmux-1000/default", false)]
    [InlineData("TERM=xterm-256color VTE_VERSION=7600 TERM_PROGRAM=vscode", false)]
    public void TheTerminalsThatDropTheClipboard_AreKnown(string environment, bool expected)
    {
        var variables = environment.Split(' ').Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1]);
        Assert.Equal(expected, SystemClipboard.WithoutClipboard(name => variables.GetValueOrDefault(name)));
    }
}
