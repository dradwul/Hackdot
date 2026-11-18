using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class TabCompleterTests
{
    private static CommandInterpreter Create() => TestTerminal.Create(fs =>
    {
        fs.WriteFile("/home/user/notes.txt", "notes");
        fs.WriteFile("/home/user/notes-old.txt", "old notes");
        fs.WriteFile("/home/user/my file.txt", "spaces");
        fs.WriteFile("/home/user/.secret", "hidden");
        fs.CreateDirectory("/home/user/Projects");
        fs.CreateDirectory("/srv/locked", owner: "root", mode: Convert.ToInt32("700", 8));
        fs.WriteFile("/srv/locked/hidden-name.txt", "x", owner: "root", mode: Convert.ToInt32("600", 8));
        fs.CreateDirectory("/srv/passage", owner: "root", mode: Convert.ToInt32("711", 8));
        fs.WriteFile("/srv/passage/known.txt", "x", owner: "root", mode: Convert.ToInt32("644", 8));
    });

    private static Completion Complete(string input) => TabCompleter.Complete(Create(), input);

    [Theory]
    [InlineData("pw", "pwd ")]
    [InlineData("b", "base64 ")]
    [InlineData("ls | gr", "ls | grep ")]
    [InlineData("sudo ca", "sudo cat ")]
    [InlineData("  wh", "  whoami ")]
    public void FirstWord_CompletesACommand(string input, string expected)
    {
        var completion = Complete(input);

        Assert.Equal(expected, completion.Input);
        Assert.Empty(completion.Options);
    }

    [Fact]
    public void SeveralCommands_ExtendToWhatTheyShare_AndListTheOptions()
    {
        var completion = Complete("c");

        Assert.Equal("c", completion.Input);
        Assert.Equal(["cat", "cd", "chmod", "clear", "cut"], completion.Options.Select(option => option.Label));
        Assert.Equal("cat ", completion.Options[0].Input);
    }

    [Theory]
    [InlineData("cat Doc", "cat Documents/")]
    [InlineData("cat Documents/w", "cat Documents/welcome.txt ")]
    [InlineData("cat ~/Doc", "cat ~/Documents/")]
    [InlineData("cat /etc/pas", "cat /etc/passwd ")]
    [InlineData("cat ../../et", "cat ../../etc/")]
    [InlineData("grep root /etc/pas", "grep root /etc/passwd ")]
    [InlineData("cat Documents/welcome.txt | grep x /etc/ho", "cat Documents/welcome.txt | grep x /etc/hostname ")]
    [InlineData("cat ~", "cat ~/")]
    [InlineData("cd ..", "cd ../")]
    [InlineData("sudo cat /etc/mo", "sudo cat /etc/motd ")]
    public void OtherWords_CompleteAPath(string input, string expected)
    {
        Assert.Equal(expected, Complete(input).Input);
    }

    [Fact]
    public void SeveralFiles_ExtendToWhatTheyShare_AndListTheOptions()
    {
        var completion = Complete("cat no");

        Assert.Equal("cat notes", completion.Input);
        Assert.Equal(["notes-old.txt", "notes.txt"], completion.Options.Select(option => option.Label).Order(StringComparer.Ordinal));
        Assert.Contains(completion.Options, option => option.Input == "cat notes.txt ");
    }

    [Fact]
    public void AnEmptyWord_ListsTheVisibleNamesInTheFolder()
    {
        var labels = Complete("cat ").Options.Select(option => option.Label).ToList();

        Assert.Contains("Documents/", labels);
        Assert.Contains("notes.txt", labels);
        Assert.DoesNotContain(".bashrc", labels);
    }

    [Fact]
    public void HiddenNames_NeedALeadingDot()
    {
        var labels = Complete("cat .").Options.Select(option => option.Label).ToList();

        Assert.Contains(".bashrc", labels);
        Assert.Contains(".secret", labels);
        Assert.Equal("cat .bashrc ", Complete("cat .b").Input);
    }

    [Fact]
    public void Cd_OnlyOffersFolders()
    {
        var labels = Complete("cd ").Options.Select(option => option.Label).ToList();

        Assert.Contains("Documents/", labels);
        Assert.Contains("Projects/", labels);
        Assert.DoesNotContain("notes.txt", labels);
    }

    [Theory]
    [InlineData("cat my", @"cat my\ file.txt ")]
    [InlineData("cat 'my", "cat 'my file.txt' ")]
    [InlineData("cat \"my f", "cat \"my file.txt\" ")]
    public void NamesWithSpaces_AreEscapedOrQuoted(string input, string expected)
    {
        Assert.Equal(expected, Complete(input).Input);
    }

    [Fact]
    public void EscapedText_IsUnderstood()
    {
        Assert.Equal(@"cat my\ file.txt ", Complete(@"cat my\ f").Input);
    }

    [Theory]
    // A folder the player can't read: its names stay secret, just like with 'ls'.
    [InlineData("cat /srv/locked/")]
    [InlineData("cat /srv/locked/hid")]
    // A folder that can be passed through (711) but not listed.
    [InlineData("cat /srv/passage/kn")]
    public void FoldersThePlayerCantList_RevealNothing(string input)
    {
        var completion = Complete(input);

        Assert.Equal(input, completion.Input);
        Assert.Empty(completion.Options);
    }

    [Theory]
    [InlineData("cat nothing-here")]
    [InlineData("xyz")]
    [InlineData("cat /no/such/folder/")]
    [InlineData("cat notes.txt\"x")]
    public void NoMatch_LeavesTheLineAlone(string input)
    {
        var completion = Complete(input);

        Assert.Equal(input, completion.Input);
        Assert.Empty(completion.Options);
    }

    [Fact]
    public void CompletedLines_RunInTheTerminal()
    {
        var terminal = Create();
        var line = TabCompleter.Complete(terminal, "cat Documents/w").Input;

        Assert.Contains("Welcome", terminal.Run(line), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("spaces", terminal.Run(TabCompleter.Complete(terminal, "cat 'my").Input));
        Assert.Equal("spaces", terminal.Run(TabCompleter.Complete(terminal, "cat my").Input));
    }
}
