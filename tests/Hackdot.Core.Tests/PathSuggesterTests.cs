using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class PathSuggesterTests
{
    private static CommandInterpreter Create() => TestTerminal.Create(fs =>
    {
        fs.WriteFile("/home/user/Documents/Notes.txt", "hello");
        fs.WriteFile("/var/www/html/About.html", "about");
        fs.CreateDirectory("/home/user/Music");
        fs.CreateDirectory("/home/user/music");
    });

    [Theory]
    [InlineData("documents", "Documents")]
    [InlineData("documents/", "Documents/")]
    [InlineData("DOCUMENTS/notes.TXT", "Documents/Notes.txt")]
    [InlineData("~/documents", "~/Documents")]
    [InlineData("/VAR/www/HTML/about.html", "/var/www/html/About.html")]
    [InlineData("./documents/../downloads", "./Documents/../Downloads")]
    public void Suggest_FixesOnlyTheCase_AndKeepsHowThePathWasWritten(string typed, string expected)
    {
        var terminal = Create();

        Assert.Equal(expected, PathSuggester.Suggest(terminal.Session, typed));
    }

    [Theory]
    [InlineData("Documents")]         // already correct
    [InlineData("docs")]              // a different name, not just case
    [InlineData("MUSIC")]             // ambiguous: Music and music both exist
    [InlineData("documents/notes.txt/x")] // a file in the middle of the path
    [InlineData("")]
    public void Suggest_ReturnsNull_WhenThereIsNothingSensibleToSuggest(string typed)
    {
        Assert.Null(PathSuggester.Suggest(Create().Session, typed));
    }

    [Theory]
    [InlineData("cd documents", "cd: documents: No such file or directory")]
    [InlineData("cat documents/notes.txt", "cat: documents/notes.txt: No such file or directory")]
    [InlineData("ls DOCUMENTS", "ls: cannot access 'DOCUMENTS': No such file or directory")]
    [InlineData("grep hello documents/Notes.txt", "grep: documents/Notes.txt: No such file or directory")]
    [InlineData("find documents -name x", "find: documents: No such file or directory")]
    public void Commands_AddADidYouMeanHint(string command, string error)
    {
        var lines = Create().Execute(command).Lines;

        Assert.Equal(2, lines.Count);
        Assert.Equal(error, lines[0].Text);
        Assert.StartsWith("Did you mean '", lines[1].Text);
        Assert.EndsWith("? Names in Linux are case-sensitive.", lines[1].Text);
        Assert.All(lines, line => Assert.True(line.IsError));
        Assert.Equal(OutputStyle.Warning, lines[1].Segments[0].Style);
    }

    [Fact]
    public void Cd_Hint_ShowsTheCorrectedFolder()
    {
        Assert.Equal(
            "cd: documents/: No such file or directory\nDid you mean 'Documents/'? Names in Linux are case-sensitive.",
            Create().Run("cd documents/"));
    }

    [Fact]
    public void Hint_IsNotPassedThroughPipes()
    {
        Assert.Equal(
            "cat: documents/notes.txt: No such file or directory\nDid you mean 'Documents/Notes.txt'? Names in Linux are case-sensitive.",
            Create().Run("cat documents/notes.txt | grep hello"));
    }
}
