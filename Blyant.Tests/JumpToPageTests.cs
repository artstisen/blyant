namespace Blyant.Tests;

/// <summary>
/// ":jump general": a page of Blyant by its name, without the Jump menu
/// </summary>
[Collection("Static state")]
public class JumpToPageTests
{
    private static EditorEngine Editor() => new("text", 40, 10, false);

    [Fact]
    public void ThePages_HaveNames()
    {
        Assert.Equal(new[]
        {
            "general", "layout", "editor", "lists", "modules", "system", "board", "favorites", "history", "hotlist", "browse",
            "colors", "logos", "clipboard", "snippets", "completions", "log", "help", "about"
        }, JumpPages.Names);
        Assert.Equal("config/general", JumpPages.IdOf("general"));
        Assert.Equal("schemes", JumpPages.IdOf("colors"));
        Assert.Equal("about", JumpPages.IdOf("about"));
        // Upper and lower case alike
        Assert.Equal("config/system", JumpPages.IdOf("System"));
        // The names used in the jump history are not names here
        Assert.Null(JumpPages.IdOf("config/general"));
        Assert.Null(JumpPages.IdOf("schemes"));
        Assert.Null(JumpPages.IdOf("nothing"));
    }

    [Fact]
    public void TheCommandsOfTheLists_GoToTheirPages()
    {
        Assert.Equal("favorites", JumpPages.ListPageOf("favorites"));
        Assert.Equal("favorites", JumpPages.ListPageOf("favs"));
        Assert.Equal("history", JumpPages.ListPageOf("hist"));
        Assert.Equal("hotlist", JumpPages.ListPageOf("hot"));
        Assert.Equal("browse", JumpPages.ListPageOf("browse"));
        // ":fav" adds a favorite: not a page
        Assert.Null(JumpPages.ListPageOf("fav"));
        Assert.Equal("history", Jump.PageOf("history"));
        Assert.Equal("hotlist", Jump.PageOf("hot"));
        Assert.Equal("board main", Jump.PageOf("board main"));
        Assert.Equal("logos", Jump.PageOf("jump logos"));
    }

    [Fact]
    public void EveryName_IsAPage()
    {
        Assert.Equal(JumpPages.Ids, JumpPages.Names.Select(JumpPages.IdOf));
    }

    [Fact]
    public void TheConfigurationPages_LeaveThePage_AndTheOthersOpenOnTop()
    {
        foreach (var name in new[] { "general", "layout", "editor", "lists", "system" })
        {
            Assert.False(Jump.OpensInPlace(new JumpTarget(JumpKind.Page, JumpPages.IdOf(name)!)));
        }

        foreach (var name in new[] { "board", "colors", "logos", "clipboard", "snippets", "completions", "log", "help", "about" })
        {
            Assert.True(Jump.OpensInPlace(new JumpTarget(JumpKind.Page, JumpPages.IdOf(name)!)));
        }
    }

    [Theory]
    [InlineData(":jump general<CR>", "general")]
    [InlineData(":ju help<CR>", "help")]
    [InlineData(":jmp   logos  <CR>", "logos")]
    [InlineData(":jump<CR>", "")]
    public void TheEditor_TakesTheName(string keys, string argument)
    {
        var editor = Editor();
        Assert.Equal(EditorRequest.Jump, editor.Type(keys));
        Assert.Equal(argument, editor.JumpArgument);
    }

    [Fact]
    public void CtrlO_OpensTheMenu_AfterAJumpToAPage()
    {
        var editor = Editor();
        editor.Type(":jump general<CR>");
        Assert.Equal(EditorRequest.Jump, editor.Type("<C-o>"));
        Assert.Equal(string.Empty, editor.JumpArgument);
    }

    [Fact]
    public void AnotherWord_IsNotJump()
    {
        var editor = Editor();
        Assert.NotEqual(EditorRequest.Jump, editor.Type(":jumpy<CR>"));
    }
}
