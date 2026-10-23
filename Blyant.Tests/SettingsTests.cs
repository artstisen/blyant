namespace Blyant.Tests;

[Collection("Static state")]
public class SettingsTests : IDisposable
{
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    private readonly string _workDirectory =
        Path.Combine(Path.GetTempPath(), "blyant-tests-" + Guid.NewGuid().ToString("N"));

    private string SettingsFile => Path.Combine(_workDirectory, "data", "settings.ini");

    // A settings file as written by Marbles v1.0.1.x, including the key mapping and the data file that no longer
    // exist
    private const string LegacySettings = @"; Last modified 2024-03-15 09:55:06
[SETTINGS]

; DIMENSIONS
linewidth=100
linecount=30

; KEY MAPPING
kbtrigger=W
kbsave=E
kbsavexit=Q
kbrepeat=K
kbnxtword=L
kbpreword=N
kbselect=O

; OPTIONAL

; STYLING
asciistyle=1
logo=7
logotxt=a=b text
(clpreset)=5
color1=2
color2=4
color3=1
color4=14
color5=12
color6=14
color7=12
color8=0
color9=10
color10=15
color11=4
color12=0

; FILES
subdir=
file=notes.mbls
backup=
";

    public SettingsTests()
    {
        Directory.CreateDirectory(Path.Combine(_workDirectory, "data"));
        Directory.SetCurrentDirectory(_workDirectory);
        Init.UsingDefaultSettings = false;
    }

    public void Dispose()
    {
        Init.SetDefaultOutputDimensions();
        Init.AutoSize = true;
        Logo.Animation = Logo.AnimationSpeed.Fast;
        AsciiStyles.ResetAsciiStyle();
        Projects.Directory = string.Empty;
        Projects.AskEachTime = false;
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_workDirectory, true);
    }

    [Fact]
    public void ASettingsFileThatCannotBeRead_IsKeptAsACopy_BeforeItIsWritten()
    {
        File.WriteAllText(SettingsFile, LegacySettings);
        // Held open by another program (a sync client) while Blyant starts
        using (new FileStream(SettingsFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(FileIo.LoadSettings());
        }

        Assert.Equal(DialogText.Success, FileIo.WriteSettingsFile("linewidth=90"));
        Assert.Equal(LegacySettings, File.ReadAllText(SettingsFile + FileIo.UnreadSettingsExtension));
        Assert.Equal("linewidth=90", File.ReadAllText(SettingsFile));
        // Only the first time: the copy is not replaced by the settings written since
        Assert.Equal(DialogText.Success, FileIo.WriteSettingsFile("linewidth=95"));
        Assert.Equal(LegacySettings, File.ReadAllText(SettingsFile + FileIo.UnreadSettingsExtension));
    }

    [Fact]
    public void ANumberInOtherDigits_IsNotASetting()
    {
        // Arabic-Indic digits are digits to \d, but not to Convert.ToInt32
        File.WriteAllText(SettingsFile, "[SETTINGS]\nlinewidth=٨٠\nlinecount=30\n");
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(30, Init.LineCount);
        Assert.False(Tools.IsNumeric("٨٠"));
        Assert.True(Tools.IsNumeric("80"));
    }

    [Fact]
    public void SettingsFromEarlierVersions_AreLoaded()
    {
        File.WriteAllText(SettingsFile, LegacySettings);

        Assert.True(FileIo.LoadSettings());
        Assert.False(Init.UsingDefaultSettings);
        Assert.Equal(100, Init.LineWidth);
        Assert.Equal(30, Init.LineCount);
        Assert.Equal(1, AsciiStyles.GetAsciiStyle());
    }

    [Fact]
    public void TheProjectsFolder_IsLoadedAndSaved_AndResetWithTheSettings()
    {
        var folder = Path.Combine(_workDirectory, "My projects");
        File.WriteAllText(SettingsFile, LegacySettings + "\nprojectsdir=" + folder + "\n");
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(folder, Projects.Directory);
        FileIo.SaveSettings();
        Assert.Equal(folder, FileIo.ParseSettings(File.ReadAllText(SettingsFile))["projectsdir"]);
        // Not set: none until the user decides (and saved as none, so it is still not set the next time)
        File.WriteAllText(SettingsFile, LegacySettings);
        Assert.True(FileIo.LoadSettings());
        Assert.False(Projects.HasFolder);
        Assert.False(Projects.IsDecided);
        FileIo.SaveSettings();
        Assert.Equal(string.Empty, FileIo.ParseSettings(File.ReadAllText(SettingsFile))["projectsdir"]);
        Projects.Directory = folder;
        Actions.ApplyDefaultSettings();
        Assert.False(Projects.HasFolder);
    }

    [Fact]
    public void TheKindOfNewFiles_AndStartingOnABlankPage_AreLoadedAndSaved()
    {
        File.WriteAllText(SettingsFile, LegacySettings + "\nfiletype=txt\nstartmode=blank\n");
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(".txt", FileOperations.DefaultExtension);
        Assert.Equal(StartMenu.StartMode.Blank, StartMenu.Mode);
        FileIo.SaveSettings();
        var saved = FileIo.ParseSettings(File.ReadAllText(SettingsFile));
        Assert.Equal("txt", saved["filetype"]);
        Assert.Equal("blank", saved["startmode"]);
        // Not a kind of the list: Markdown
        File.WriteAllText(SettingsFile, LegacySettings + "\nfiletype=docx\n");
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(".md", FileOperations.DefaultExtension);
        FileOperations.DefaultExtension = ".html";
        Actions.ApplyDefaultSettings();
        Assert.Equal(".md", FileOperations.DefaultExtension);
        Assert.Equal(StartMenu.StartMode.Select, StartMenu.Mode);
    }

    [Fact]
    public void WhereTheFolderPickersStart_IsLoadedAndSaved()
    {
        var place = Path.Combine(_workDirectory, "Writing", "Essays");
        File.WriteAllText(SettingsFile, LegacySettings + "\npickdir=" + place + "\n");
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(place, FileExplorer.PickPlace);
        FileIo.SaveSettings();
        Assert.Equal(place, FileIo.ParseSettings(File.ReadAllText(SettingsFile))["pickdir"]);
        // Not a full path on this system: where the browser was
        File.WriteAllText(SettingsFile, LegacySettings + "\npickdir=Writing\n");
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(string.Empty, FileExplorer.PickPlace);
    }

    [Fact]
    public void NoProjectsFolder_AskEachTime_IsSaved()
    {
        File.WriteAllText(SettingsFile, LegacySettings + "\nprojectsdir=\nprojectsask=true\n");
        Assert.True(FileIo.LoadSettings());
        Assert.True(Projects.AskEachTime);
        Assert.True(Projects.IsDecided);
        FileIo.SaveSettings();
        Assert.Equal("true", FileIo.ParseSettings(File.ReadAllText(SettingsFile))["projectsask"]);
        // A projects folder set: it is used, not asked
        File.WriteAllText(SettingsFile, LegacySettings + "\nprojectsdir=" + _workDirectory + "\nprojectsask=true\n");
        Assert.True(FileIo.LoadSettings());
        Assert.False(Projects.AskEachTime);
        Actions.ApplyDefaultSettings();
        Assert.False(Projects.IsDecided);
    }

    [Fact]
    public void Recovery_IsOnUnlessTurnedOff_AndSaved()
    {
        File.WriteAllText(SettingsFile, LegacySettings);
        Assert.True(FileIo.LoadSettings());
        Assert.True(Recovery.Enabled);
        File.WriteAllText(SettingsFile, LegacySettings + "\nrecovery=false\n");
        Assert.True(FileIo.LoadSettings());
        Assert.False(Recovery.Enabled);
        FileIo.SaveSettings();
        Assert.Equal("false", FileIo.ParseSettings(File.ReadAllText(SettingsFile))["recovery"]);
        Actions.ApplyDefaultSettings();
        Assert.True(Recovery.Enabled);
    }

    [Fact]
    public void DeleteToTrash_IsOnUnlessTurnedOff_AndSaved()
    {
        File.WriteAllText(SettingsFile, LegacySettings);
        Assert.True(FileIo.LoadSettings());
        Assert.True(FileOperations.DeleteToTrash);
        File.WriteAllText(SettingsFile, LegacySettings + "\ndeletetotrash=false\n");
        Assert.True(FileIo.LoadSettings());
        Assert.False(FileOperations.DeleteToTrash);
        FileIo.SaveSettings();
        Assert.Equal("false", FileIo.ParseSettings(File.ReadAllText(SettingsFile))["deletetotrash"]);
        Actions.ApplyDefaultSettings();
        Assert.True(FileOperations.DeleteToTrash);
    }

    [Theory]
    [InlineData("logoanim=slow", "Slow", "slow")]
    [InlineData("logoanim= VeryFast ", "VeryFast", "veryfast")]
    [InlineData("logoanim=off", "Off", "off")]
    [InlineData("logoanim=false", "Off", "off")] // As it was written before
    [InlineData("logoanim=true", "Fast", "fast")]
    [InlineData("", "Fast", "fast")]
    public void TheSpeedOfTheLogoAnimation_IsLoadedAndSaved_AndResetWithTheSettings(string setting, string expected,
        string saved)
    {
        File.WriteAllText(SettingsFile, LegacySettings + "\n" + setting + "\n");
        try
        {
            Assert.True(FileIo.LoadSettings());
            Assert.Equal(expected, Logo.Animation.ToString());
            Assert.Equal(expected != "Off", Logo.AnimationEnabled);
            FileIo.SaveSettings();
            Assert.Equal(saved, FileIo.ParseSettings(File.ReadAllText(SettingsFile))["logoanim"]);
            Actions.ApplyDefaultSettings();
            Assert.Equal(Logo.AnimationSpeed.Fast, Logo.Animation);
        }
        finally
        {
            Logo.Animation = Logo.AnimationSpeed.Fast;
        }
    }

    [Fact]
    public void TheLogoAnimation_TakesLongerTheSlowerItIs()
    {
        Assert.True(Logo.AnimationDuration(Logo.AnimationSpeed.Slow) > Logo.AnimationDuration(Logo.AnimationSpeed.Fast));
        Assert.True(Logo.AnimationDuration(Logo.AnimationSpeed.Fast) > Logo.AnimationDuration(Logo.AnimationSpeed.VeryFast));
        Assert.True(Logo.AnimationDuration(Logo.AnimationSpeed.VeryFast) > 0);
        // The columns are done evenly over the time, the last one at the end
        Assert.Equal(800, Logo.ColumnTime(LogoArt.Width - 1, 800));
        Assert.Equal(12, Logo.ColumnTime(0, 800));
        Assert.True(Logo.ColumnTime(31, 800) is > 390 and < 420);
    }

    [Theory]
    [InlineData("startmode=notes", "Select")] // Marbles: the login page
    [InlineData("startmode= Editor ", "Editor")]
    [InlineData("startmode=select", "Select")]
    [InlineData("startmode=other", "Select")]
    [InlineData("", "Select")]
    public void StartMode_IsLoadedAndSaved(string setting, string expected)
    {
        File.WriteAllText(SettingsFile, LegacySettings + "\n" + setting + "\n");
        try
        {
            Assert.True(FileIo.LoadSettings());
            Assert.Equal(expected, StartMenu.Mode.ToString());
            FileIo.SaveSettings();
            Assert.Equal(expected.ToLowerInvariant(), FileIo.ParseSettings(File.ReadAllText(SettingsFile))["startmode"]);
            Assert.Equal(expected == "Editor" ? "editor" : "select", StartMenu.SettingName(StartMenu.Mode));
        }
        finally
        {
            StartMenu.Mode = StartMenu.StartMode.Select;
        }
    }

    [Fact]
    public void SavedSettings_HaveNoKeyMapping_AndLoadAgain()
    {
        File.WriteAllText(SettingsFile, LegacySettings);
        FileIo.LoadSettings();
        FileIo.SaveSettings();

        var saved = File.ReadAllText(SettingsFile);
        Assert.DoesNotContain("kb", saved);
        Assert.DoesNotContain("KEY MAPPING", saved);
        var settings = FileIo.ParseSettings(saved);
        Assert.Equal("100", settings["linewidth"]);
        // The logo presets are gone: the logo in use is a setting by its name
        Assert.Equal("Blyant", settings["logo"]);
        Assert.False(settings.ContainsKey("logotxt"));
        // The settings of the data files of Marbles are gone
        Assert.False(settings.ContainsKey("file"));
        Assert.False(settings.ContainsKey("subdir"));
        Assert.False(settings.ContainsKey("backup"));
        Assert.False(settings.ContainsKey("onlymode"));
        Assert.False(settings.ContainsKey("clipboardnotes"));
        Assert.False(settings.ContainsKey("stayloggedin"));
        Assert.Equal(36, settings.Count);
        Assert.Equal("md", settings["filetype"]);
        Assert.Equal(string.Empty, settings["pickdir"]);
        Assert.Equal("false", settings["projectsask"]);
        Assert.Equal("true", settings["recovery"]);
        Assert.Equal(string.Empty, settings["projectsdir"]);
        Assert.Equal("true", settings["deletetotrash"]);
        Assert.Equal("fast", settings["logoanim"]);
        Assert.Equal("true", settings["wrap"]);
        Assert.Equal("file", settings["snipsort"]);
        Assert.Equal("true", settings["scrolloff"]);
        Assert.Equal("12", settings["scrolllines"]);
        // The colors are kept in the color schemes: only the name of the one in use is a setting
        Assert.Equal("Default", settings["colorscheme"]);
        Assert.False(settings.ContainsKey("color1"));
        Assert.False(settings.ContainsKey("background"));
        Assert.Equal("false", settings["startfavorites"]);
        Assert.Equal("true", settings["osc52"]);
        Assert.Equal("false", settings["pastebin"]);
        Assert.Equal("all", settings["clipboardyank"]);
        Assert.Equal("false", settings["pastebinappend"]);
        Assert.Equal("paste-bin.txt", settings["pastebinfile"]);
        Assert.Equal("en", settings["language"]);
        Assert.Equal("false", settings["linenumbers"]);
        Assert.Equal("true", settings["browsedual"]);
        Assert.Equal("true", settings["autocomplete"]);
        Assert.Equal("1", settings["browsepane"]);
        Assert.Equal(string.Empty, settings["browsesel1"]);
        Assert.Equal(string.Empty, settings["browsesel2"]);
        Assert.Equal("true", settings["syntaxcolors"]);
        Assert.Equal("false", settings["monochrome"]);
        Assert.Equal("select", settings["startmode"]);

        Init.SetDefaultOutputDimensions();
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(100, Init.LineWidth);
    }

    [Fact]
    public void TheNamesSelectedInTheFileBrowser_AreLoadedAndSaved()
    {
        File.WriteAllText(SettingsFile, "browsesel1=notes.md\nbrowsesel2=My folder\n");
        Assert.True(FileIo.LoadSettings());
        Assert.Equal("notes.md", FileExplorer.StartSelected);
        Assert.Equal("My folder", FileExplorer.StartSelected2);
        FileIo.SaveSettings();
        var settings = FileIo.ParseSettings(File.ReadAllText(SettingsFile));
        Assert.Equal("notes.md", settings["browsesel1"]);
        Assert.Equal("My folder", settings["browsesel2"]);

        // Missing: the first row
        File.WriteAllText(SettingsFile, "linecount=40\n");
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(string.Empty, FileExplorer.StartSelected);
        Assert.Equal(string.Empty, FileExplorer.StartSelected2);
    }

    [Fact]
    public void AutomaticSize_IsOnUnlessTurnedOff_AndOnWhenTheSettingsAreReset()
    {
        // Not in the settings file (a new one, or one from before): on
        File.WriteAllText(SettingsFile, LegacySettings);
        Assert.True(FileIo.LoadSettings());
        Assert.True(Init.AutoSize);
        File.WriteAllText(SettingsFile, LegacySettings + "\nautosize=false\n");
        Assert.True(FileIo.LoadSettings());
        Assert.False(Init.AutoSize);
        FileIo.SaveSettings();
        Assert.Equal("false", FileIo.ParseSettings(File.ReadAllText(SettingsFile))["autosize"]);
        // Reset: on again
        Actions.ApplyDefaultSettings();
        Assert.True(Init.AutoSize);
        Assert.Equal("true", FileIo.ParseSettings(File.ReadAllText(SettingsFile))["autosize"]);
    }

    [Fact]
    public void SettingsCanBeInAnyOrder_AndMissingOnesGetDefaults()
    {
        File.WriteAllText(SettingsFile, "# comment\nlogoanim=false\n\nnot a setting\nLINECOUNT=40\nlinewidth=nonsense\n");
        Assert.True(FileIo.LoadSettings());
        Assert.False(Logo.AnimationEnabled);
        Assert.Equal(40, Init.LineCount);
        Assert.Equal(Init.MinLineWidth, Init.LineWidth);
    }

    [Theory]
    [InlineData("colorscheme=Deep Blue", "Deep Blue")]
    [InlineData("colorscheme=ice warp", "Ice Warp")]
    [InlineData("colorscheme=Mine", "Mine")]
    [InlineData("colorscheme=Missing", "Default")]
    [InlineData("", "Default")]
    public void TheColorScheme_IsLoadedAndSavedByName(string setting, string expected)
    {
        ColorSchemes.DirectoryOverride = Path.Combine(_workDirectory, "data", ColorSchemes.DirectoryName);
        try
        {
            var mine = ColorScheme.Default;
            mine.Name = "Mine";
            mine.Text = ConsoleColor.White;
            Assert.Equal(DialogText.Success, ColorSchemes.Save(mine));
            File.WriteAllText(SettingsFile, setting + "\n");
                Assert.True(FileIo.LoadSettings());
            Assert.Equal(expected, ConsoleColors.Current.Name);
            if (expected == "Mine") Assert.Equal(ConsoleColor.White, ConsoleColors.Current.Text);
            FileIo.SaveSettings();
            Assert.Equal(expected, FileIo.ParseSettings(File.ReadAllText(SettingsFile))["colorscheme"]);
        }
        finally
        {
            ColorSchemes.DirectoryOverride = null;
            ConsoleColors.Use(ColorScheme.Default);
        }
    }

    [Fact]
    public void MissingSettingsFile_UsesDefaults()
    {
        Assert.True(FileIo.LoadSettings());
        Assert.True(Init.UsingDefaultSettings);
    }
}
