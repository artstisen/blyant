namespace Blyant.Tests;

/// <summary>
/// The commands given before on a command line, brought back with Up and Down: one list for each place, the
/// newest first, at most CommandHistory.MaxCount, cleared at logout
/// </summary>
[Collection("Static state")]
public class CommandHistoryTests : IDisposable
{
    public CommandHistoryTests()
    {
        CommandHistory.Clear();
    }

    public void Dispose()
    {
        CommandHistory.Clear();
    }

    [Fact]
    public void TheTestsThatTypeCommands_AreInTheCollectionStaticState()
    {
        // A command typed in an editor goes in the command history, which all editors share (a list of each place,
        // for Up and Down). Test classes that type commands are not run at the same time as each other: else two
        // of them change the history at once, and one of them fails now and then (in Dictionary or List)
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Blyant.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var outside = Directory.GetFiles(Path.Combine(directory!.FullName, "Blyant.Tests"), "*.cs")
            .Where(path => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(path), @"\.Type\(""[^""]*:"))
            .Where(path => !File.ReadAllText(path).Contains("[Collection(\"Static state\")]"))
            .Select(Path.GetFileName).ToList();
        Assert.True(outside.Count == 0, "Not in the collection \"Static state\": " + string.Join(", ", outside));
    }

    [Fact]
    public void Commands_AreKeptForEachPlace_TheNewestFirst_AndOnce()
    {
        CommandHistory.Add(CommandContext.Files, "copy");
        CommandHistory.Add(CommandContext.Files, " info ");
        CommandHistory.Add(CommandContext.Files, "copy");
        CommandHistory.Add(CommandContext.Files, "  ");
        CommandHistory.Add(CommandContext.Editor, "w");

        Assert.Equal(new[] { "copy", "info" }, CommandHistory.Of(CommandContext.Files));
        Assert.Equal(new[] { "w" }, CommandHistory.Of(CommandContext.Editor));
        Assert.Empty(CommandHistory.Of(CommandContext.Jump));
    }

    [Fact]
    public void TheOldest_AreForgotten_AndAllWhenCleared()
    {
        for (var i = 0; i < CommandHistory.MaxCount + 5; i++) CommandHistory.Add(CommandContext.Files, "cd " + i);
        var kept = CommandHistory.Of(CommandContext.Files);
        Assert.Equal(CommandHistory.MaxCount, kept.Count);
        Assert.Equal("cd " + (CommandHistory.MaxCount + 4), kept[0]);

        CommandHistory.Clear();
        Assert.Empty(CommandHistory.Of(CommandContext.Files));
    }

    [Fact]
    public void UpAndDown_GoThroughTheCommands_AndBackToWhatWasTyped()
    {
        CommandHistory.Add(CommandContext.Files, "delete");
        CommandHistory.Add(CommandContext.Files, "copy");
        CommandHistory.Add(CommandContext.Files, "color");
        var recall = new CommandRecall(CommandContext.Files);

        Assert.Null(recall.Next());
        Assert.Equal("color", recall.Previous(""));
        Assert.Equal("copy", recall.Previous("color"));
        Assert.Equal("delete", recall.Previous("copy"));
        Assert.Null(recall.Previous("delete"));
        Assert.Equal("copy", recall.Next());
        Assert.Equal("color", recall.Next());
        Assert.Equal("", recall.Next());
    }

    [Fact]
    public void WhatIsTyped_ChoosesTheCommandsThatStartWithIt()
    {
        CommandHistory.Add(CommandContext.Files, "copy");
        CommandHistory.Add(CommandContext.Files, "delete");
        CommandHistory.Add(CommandContext.Files, "Color");
        var recall = new CommandRecall(CommandContext.Files);

        Assert.Equal("Color", recall.Previous("co"));
        Assert.Equal("copy", recall.Previous("Color"));
        Assert.Null(recall.Previous("copy"));
        Assert.Equal("Color", recall.Next());
        Assert.Equal("co", recall.Next());

        // Changed by hand: Up starts from the new text
        recall.Reset();
        Assert.Equal("delete", recall.Previous("d"));
    }

    [Fact]
    public void TheEditor_KeepsItsCommands_AndUpBringsThemBack()
    {
        var editor = new EditorEngine("one", 40, 10, false);
        editor.Type(":number<CR>");
        editor.Type(":set wrap<CR>");
        Assert.Equal(new[] { "set wrap", "number" }, CommandHistory.Of(CommandContext.Editor));

        editor.Type(":<Up><Up>");
        Assert.Equal("number", editor.CommandText);
        editor.Type("<Down>");
        Assert.Equal("set wrap", editor.CommandText);
        editor.Type("<Down>");
        Assert.Equal("", editor.CommandText);
        editor.Type("<Esc>");

        // A search does not bring back commands, and a field of one line keeps none
        editor.Type("/<Up>");
        Assert.Equal("", editor.CommandText);
        editor.Type("<Esc>");
        var field = new EditorEngine("x", 10, 1, true);
        field.Type("<Esc>:trim<CR>");
        Assert.Equal(new[] { "set wrap", "number" }, CommandHistory.Of(CommandContext.Editor));
    }

    [Fact]
    public void ACommandBroughtBack_IsRecordedInAMacroAsTyped()
    {
        var editor = new EditorEngine("  one  ", 40, 10, false);
        editor.Type(":trim<CR>");
        editor.StartRecording();
        editor.Type(":<Up><CR>");
        Assert.Equal(new[] { "command trim" }, editor.StopRecording());
    }
}
