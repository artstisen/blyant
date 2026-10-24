using System.Globalization;

namespace Blyant.Tests;

/// <summary>
/// Fixes of the editor and the colors from a code review: comments on a line of the C preprocessor, blocks of
/// code in Markdown closed by their own kind of fence, case changes and smartcase, options given at startup, texts
/// cut for the screen, and a bookmark added in the Jump menu after one was removed
/// </summary>
[Collection("Static state")]
public class EditorFixTests : IDisposable
{
    private const string Codes = ".123456BICKQLAUR#SWN9";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "blyant-editorfix-" + Guid.NewGuid().ToString("N"));
    private readonly string? _bookmarksPath = Bookmarks.FilePathOverride;

    public EditorFixTests()
    {
        Directory.CreateDirectory(_directory);
        Bookmarks.FilePathOverride = Path.Combine(_directory, Bookmarks.FileName);
        Bookmarks.Clear();
    }

    public void Dispose()
    {
        Bookmarks.FilePathOverride = _bookmarksPath;
        Bookmarks.Clear();
        Directory.Delete(_directory, true);
    }

    private static string[] Styles(SyntaxType type, params string[] lines)
    {
        var result = new string[lines.Length];
        var state = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var styles = SyntaxHighlighter.Highlight(type, lines[i], state, out state);
            result[i] = styles == null
                ? new string('.', lines[i].Length)
                : string.Concat(styles.Select(style => Codes[(int)style]));
        }

        return result;
    }

    [Fact]
    public void ACommentOnALineOfThePreprocessor_IsAComment_AndGoesOn()
    {
        Assert.Equal(new[] { "NNNNNNNNNNNNNNN########", "########.WWW" },
            Styles(SyntaxType.Cpp, "#include \"a.h\" /* start", "still */ int"));
        Assert.Equal("NNNNNNNNNNNN#######", Styles(SyntaxType.Cpp, "#define X 1 // note")[0]);
        // (not a comment mark inside a string of the line)
        Assert.Equal("NNNNNNNNNNNNNNNNN", Styles(SyntaxType.Cpp, "#include \"a//b.h\"")[0]);
    }

    [Fact]
    public void ABlockOfCode_EndsOnlyAtAFenceOfItsOwnKind()
    {
        var tilde = MarkdownHighlighter.NextFenceState("~~~", 0);
        Assert.NotEqual(0, tilde);
        Assert.Equal(tilde, MarkdownHighlighter.NextFenceState("```", tilde));
        Assert.Equal(0, MarkdownHighlighter.NextFenceState("~~~", tilde));
        // A longer fence is closed by one at least as long, with nothing after it
        var four = MarkdownHighlighter.NextFenceState("````md", 0);
        Assert.Equal(four, MarkdownHighlighter.NextFenceState("```", four));
        Assert.Equal(four, MarkdownHighlighter.NextFenceState("```` x", four));
        Assert.Equal(0, MarkdownHighlighter.NextFenceState("`````", four));
        // In the editor: the heading after the block is a heading again
        var lines = Styles(SyntaxType.Markdown, "~~~", "```", "code", "~~~", "# Title");
        Assert.Equal("KKKK", lines[2]);
        Assert.Equal("1111111", lines[4]);
    }

    [Fact]
    public void ChangingCase_IsTheSameInEveryLanguageOfTheComputer()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var editor = new EditorEngine("istanbul IZMIR", 40, 10, false);
            editor.Type("gUiw");
            Assert.Equal("ISTANBUL IZMIR", editor.Buffer.GetText());
            editor.Type("wguiw");
            Assert.Equal("ISTANBUL izmir", editor.Buffer.GetText());
            editor.Type("0g~iw");
            Assert.Equal("istanbul izmir", editor.Buffer.GetText());
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void Smartcase_DoesNotCountTheLetterAfterABackslash()
    {
        // \S is a class: "foo" still ignores case
        var editor = new EditorEngine("abc\nxFOO", 40, 10, false);
        editor.Type("/\\Sfoo<CR>");
        Assert.Equal(1, editor.Cursor.Line);
        // An uppercase letter of its own makes the search keep case
        editor.Type("gg/\\SFoo<CR>");
        Assert.Equal(0, editor.Cursor.Line);
    }

    [Fact]
    public void AnOption_IsAnsweredAndNeverTakenForAFileToMake()
    {
        File.WriteAllText(Path.Combine(_directory, "-notes.md"), "x");
        Assert.True(StartArgument.IsOption("--help", _directory));
        Assert.True(StartArgument.IsOption("-v", _directory));
        // A file of that name that is there, and "-" alone, are not options
        Assert.False(StartArgument.IsOption("-notes.md", _directory));
        Assert.False(StartArgument.IsOption("-", _directory));
        Assert.False(StartArgument.IsOption("notes.md", _directory));

        var output = new StringWriter();
        Assert.Equal(0, StartArgument.AnswerOption("--version", output));
        Assert.Contains(DialogText.ApplicationName + " " + DialogText.ApplicationVersion, output.ToString());
        output = new StringWriter();
        Assert.Equal(0, StartArgument.AnswerOption("-h", output));
        Assert.StartsWith("Usage: blyant", output.ToString());
        Assert.Equal(1, StartArgument.AnswerOption("--nope", new StringWriter()));
    }

    [Theory]
    [InlineData("abcdefghij", 10, true, "abcdefghij")]
    [InlineData("abcdefghijk", 10, true, "abcdefg...")]
    [InlineData("abcdefghijk", 10, false, "abcdefghij")]
    [InlineData("abcdef", 2, true, "ab")]
    [InlineData("abc", 0, true, "")]
    // Wide characters take two cells each
    [InlineData("日本語テキスト", 6, true, "日...")]
    [InlineData("日本語", 6, true, "日本語")]
    public void ATextCutForTheScreen_FitsItsCells(string text, int cells, bool dots, string expected)
    {
        Assert.Equal(expected, Tools.TruncateString(text, cells, dots));
    }

    [Fact]
    public void ABookmarkAddedInTheJumpMenu_KeepsTheRemovedOneToPutBack()
    {
        foreach (var name in new[] { "a", "b" }) Bookmarks.Add(new JumpTarget(JumpKind.File, Path.Combine(_directory, name)));
        Assert.Equal(DialogText.Success, Bookmarks.Save());
        Bookmarks.RemoveAt(0);
        // B in the Jump menu, on the list it has read: u still puts "a" back, where it was
        Bookmarks.AddWithMessage(new JumpTarget(JumpKind.File, Path.Combine(_directory, "c")), false);
        Assert.Equal(0, Bookmarks.UndoRemove());
        Assert.Equal(new[] { "a", "b", "c" }, Bookmarks.Items.Select(entry => entry.Shown).OrderBy(name => name));
    }
}
