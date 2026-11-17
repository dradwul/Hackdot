using Hackdot.Core.Services;
using Hackdot.Core.Services.Missions;

namespace Hackdot.Core.Tests;

public class FileExplorerTests
{
    private static FileExplorer Create(Action<Models.VirtualFileSystem>? setup = null)
    {
        var fileSystem = FileSystemLoader.CreateDefault();
        setup?.Invoke(fileSystem);
        return new FileExplorer(new ShellSession(fileSystem));
    }

    [Fact]
    public void StartsInHomeFolder()
    {
        var explorer = Create();

        Assert.Equal("/home/user", explorer.CurrentPath);
        Assert.Equal("~", explorer.DisplayPath);
        Assert.Equal("ls ~", explorer.EquivalentListCommand);
    }

    [Fact]
    public void Entries_ListFoldersFirstAndHideDotFiles()
    {
        var explorer = Create(fs => fs.WriteFile("/home/user/notes.txt", "hi"));

        Assert.Equal(["Documents", "Downloads", "notes.txt"], explorer.Entries.Select(e => e.Name));
    }

    [Fact]
    public void ShowHidden_RevealsDotFilesAndChangesTheEquivalentCommand()
    {
        var explorer = Create();
        explorer.ShowHidden = true;

        Assert.Contains(explorer.Entries, entry => entry is { Name: ".bashrc", IsHidden: true });
        Assert.Equal("ls -a ~", explorer.EquivalentListCommand);
    }

    [Fact]
    public void Entries_CountVisibleChildrenAndFlagLockedFolders()
    {
        var explorer = Create();
        explorer.Navigate("/");

        var home = explorer.Entries.Single(entry => entry.Name == "home");
        var root = explorer.Entries.Single(entry => entry.Name == "root");

        Assert.True(home.CanOpen);
        Assert.Equal(1, home.ChildCount);
        Assert.False(root.CanOpen);
        Assert.Null(root.ChildCount);
    }

    [Fact]
    public void Navigate_ResolvesRelativeAbsoluteAndHomePaths()
    {
        var explorer = Create();

        Assert.Equal(ExplorerResult.Ok, explorer.Navigate("Documents"));
        Assert.Equal("/home/user/Documents", explorer.CurrentPath);
        Assert.Equal(ExplorerResult.Ok, explorer.Navigate("/var/www"));
        Assert.Equal(ExplorerResult.Ok, explorer.GoUp());
        Assert.Equal("/var", explorer.CurrentPath);
        Assert.Equal(ExplorerResult.Ok, explorer.GoHome());
        Assert.Equal("/home/user", explorer.CurrentPath);
    }

    [Theory]
    [InlineData("/nope", ExplorerResult.NotFound)]
    [InlineData("/etc/passwd", ExplorerResult.NotADirectory)]
    [InlineData("/root", ExplorerResult.PermissionDenied)]
    public void Navigate_FailuresKeepTheCurrentFolder(string path, ExplorerResult expected)
    {
        var explorer = Create();

        Assert.Equal(expected, explorer.Navigate(path));
        Assert.Equal("/home/user", explorer.CurrentPath);
    }

    [Fact]
    public void OpenFile_RespectsPermissions()
    {
        var explorer = Create();

        Assert.Equal(ExplorerResult.Ok, explorer.OpenFile("/etc/hostname", out var hostname));
        Assert.Equal("hackos", hostname!.Content);
        Assert.Equal("cat /etc/hostname", explorer.EquivalentReadCommand(hostname));

        Assert.Equal(ExplorerResult.PermissionDenied, explorer.OpenFile("/root/top_secret.txt", out var secret));
        Assert.Null(secret);
        Assert.Equal(ExplorerResult.IsADirectory, explorer.OpenFile("Documents", out _));
        Assert.Equal(ExplorerResult.NotFound, explorer.OpenFile("missing.txt", out _));
    }

    [Fact]
    public void EquivalentCommands_QuotePathsWithSpaces()
    {
        var explorer = Create(fs => fs.WriteFile("/home/user/My Files/to do.txt", "x"));
        explorer.Navigate("My Files");
        explorer.OpenFile("to do.txt", out var file);

        Assert.Equal("ls '/home/user/My Files'", explorer.EquivalentListCommand);
        Assert.Equal("cat '/home/user/My Files/to do.txt'", explorer.EquivalentReadCommand(file!));

        // The suggested commands must actually work in our terminal.
        var terminal = new Hackdot.Core.Services.Terminal.CommandInterpreter(explorer.Session, TestTerminal.Text);
        Assert.Equal("x", terminal.Run(explorer.EquivalentReadCommand(file!)));
    }

    [Fact]
    public void Breadcrumbs_RunFromRootToCurrentFolder()
    {
        var explorer = Create();
        explorer.Navigate("/var/www/html");

        Assert.Equal(
            [("/", "/"), ("var", "/var"), ("www", "/var/www"), ("html", "/var/www/html")],
            explorer.Breadcrumbs);
        Assert.True(explorer.CanGoUp);

        explorer.Navigate("/");
        Assert.False(explorer.CanGoUp);
    }

    [Fact]
    public void HasOnlyHiddenItems_NudgesTowardsShowHidden()
    {
        var explorer = Create(fs => fs.WriteFile("/srv/.only_hidden", "x"));
        explorer.Navigate("/srv");

        Assert.Empty(explorer.Entries);
        Assert.True(explorer.HasOnlyHiddenItems);

        explorer.ShowHidden = true;
        Assert.False(explorer.HasOnlyHiddenItems);
    }

    [Fact]
    public void Mission2_CanBeSolvedVisually()
    {
        var catalog = MissionCatalog.LoadEmbedded();
        var mission = catalog.Find("hidden-in-the-shadows")!;
        var explorer = new FileExplorer(new ShellSession(catalog.CreateFileSystem(mission)));

        explorer.Navigate("/var/www");
        Assert.DoesNotContain(explorer.Entries, entry => entry.Name == ".secret_flag");

        explorer.ShowHidden = true;
        var secret = explorer.Entries.Single(entry => entry.Name == ".secret_flag");
        Assert.Equal(ExplorerResult.Ok, explorer.OpenFile(secret.Path, out var file));
        Assert.Contains(mission.Flag, file!.Content);
    }
}
