using Blyant.Format.Core;
using Blyant.Format.Css;
using Blyant.Format.Html;
using Blyant.Format.Xml;

namespace Blyant.Tests;

/// <summary>
/// ":format" in the editor: an HTML, CSS or XML file formatted as Blyant Validator formats it (the formatters
/// themselves are tested in Format/, with the tests of Blyant Validator)
/// </summary>
public class FormatTests
{
    private static EditorEngine Editor(string text, SyntaxType syntax) =>
        new(text, 60, 10, false) { CompletionSyntax = syntax };

    // What the formatter of Blyant Validator makes of the text (lines ending in "\n")
    private static string Formatted(IFormatter formatter, string text, DocumentKind kind) =>
        formatter.Format(new SourceDocument(string.Empty, text, kind), new FormatOptions { NewLine = "\n" });

    [Theory]
    [InlineData("Html", "<div><p>One</p><ul><li>a</li><li>b</li></ul></div>\n")]
    [InlineData("Css", "a,b>c{color:red;margin:0 auto}\n@media (min-width:600px){.x{padding:1px 2px}}\n")]
    [InlineData("Xml", "<urlset><url><loc>https://example.com/</loc></url></urlset>\n")]
    public void Format_FormatsTheText_AsBlyantValidator_AndUndoTakesItBack(string type, string text)
    {
        // (the kind of file by its name: SyntaxType is internal)
        var syntax = Enum.Parse<SyntaxType>(type);
        var (formatter, kind) = syntax switch
        {
            SyntaxType.Html => ((IFormatter)new HtmlFormatter(), DocumentKind.Html),
            SyntaxType.Css => (new CssFormatter(), DocumentKind.Css),
            _ => ((IFormatter)new XmlFormatter(), DocumentKind.Xml)
        };
        var expected = Formatted(formatter, text, kind);
        Assert.NotEqual(text, expected);

        var editor = Editor(text, syntax);
        editor.Type(":format<CR>");
        Assert.Equal(expected, editor.GetText());
        Assert.False(editor.MessageIsError);
        Assert.Equal(Lang.T("Formatted (u undoes it)"), editor.Message);
        // One change: one undo
        editor.Type("u");
        Assert.Equal(text, editor.GetText());
        // Formatted again, it is the same
        editor.Type(":format<CR>:format<CR>");
        Assert.Equal(expected, editor.GetText());
        Assert.Equal(Lang.T("Already formatted"), editor.Message);
    }

    [Fact]
    public void Format_OfAFileWithCrLf_GivesTheSameLines()
    {
        // The editor keeps lines (the line endings of the file are written when it is saved)
        var lf = Editor("<div><p>One</p></div>\n", SyntaxType.Html);
        var crlf = Editor("<div><p>One</p></div>\r\n", SyntaxType.Html);
        lf.Type(":format<CR>");
        crlf.Type(":format<CR>");
        Assert.Equal(lf.GetText(), crlf.GetText());
        Assert.DoesNotContain('\r', crlf.GetText());
    }

    [Fact]
    public void Format_KeepsTheCursorOnItsLine_AtItsFirstCharacter()
    {
        var editor = Editor("a{color:red}\nb{color:blue}\nc{color:green}\n", SyntaxType.Css);
        editor.Type("jj:format<CR>");
        Assert.Equal(2, editor.Cursor.Line);
        Assert.Equal(editor.Buffer[2].Length - editor.Buffer[2].TrimStart().Length, editor.Cursor.Col);
    }

    [Fact]
    public void Format_LeavesTextItCannotFormatSafely_AndTellsWhy()
    {
        // A block that is never closed, and XML that is not well-formed
        var css = Editor("a{color:red\n", SyntaxType.Css);
        css.Type(":format<CR>");
        Assert.Equal("a{color:red\n", css.GetText());
        Assert.True(css.MessageIsError);
        Assert.StartsWith(Lang.F("Not formatted: {0}", string.Empty), css.Message);
        var xml = Editor("<a><b></a>\n", SyntaxType.Xml);
        xml.Type(":format<CR>");
        Assert.Equal("<a><b></a>\n", xml.GetText());
        Assert.True(xml.MessageIsError);
        Assert.Contains("line", xml.Message);
    }

    [Theory]
    [InlineData("Markdown")]
    [InlineData("Typst")]
    [InlineData("Php")]
    [InlineData("None")]
    public void Format_OnlyFormatsHtmlCssAndXml(string type)
    {
        var syntax = Enum.Parse<SyntaxType>(type);
        var editor = Editor("<div><p>One</p></div>\n", syntax);
        editor.Type(":format<CR>");
        Assert.Equal("<div><p>One</p></div>\n", editor.GetText());
        Assert.True(editor.MessageIsError);
        Assert.Equal(Lang.T("Only HTML, CSS and XML files can be formatted"), editor.Message);
    }

    [Fact]
    public void Format_IsRefused_InAReadOnlyText()
    {
        var editor = new EditorEngine("a{color:red}\n", 60, 10, false)
            { CompletionSyntax = SyntaxType.Css, ReadOnly = true };
        editor.Type(":format<CR>");
        Assert.Equal("a{color:red}\n", editor.GetText());
        Assert.True(editor.MessageIsError);
        Assert.Equal(DialogText.EditorReadOnly, editor.Message);
    }

    [Fact]
    public void Format_ChangesOnlyThePartThatDiffers()
    {
        // The lines before and after the change stay (and a mark on them)
        const string text = "a {\n  color: red;\n}\n\nb{color:blue}\n\nc {\n  color: green;\n}\n";
        var editor = Editor(text, SyntaxType.Css);
        editor.Type("Gkkma:format<CR>"); // (the mark on "  color: green;")
        Assert.Equal(Formatted(new CssFormatter(), text, DocumentKind.Css), editor.GetText());
        editor.Type("gg'a");
        Assert.Equal("  color: green;", editor.Buffer[editor.Cursor.Line]);
    }
}
