using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class CommandInterpreterTests
{
    [Fact]
    public void AllCommands_HaveLocalizedHelpTexts()
    {
        var terminal = TestTerminal.Create();
        var text = TestTerminal.Text;

        foreach (var command in terminal.Commands)
        {
            Assert.False(text[$"Cmd.{command.Name}.Summary"].ResourceNotFound, $"{command.Name}: Summary missing");
            Assert.False(text[$"Cmd.{command.Name}.Details"].ResourceNotFound, $"{command.Name}: Details missing");

            for (var i = 1; i <= command.Examples.Count; i++)
            {
                Assert.False(text[$"Cmd.{command.Name}.Example{i}"].ResourceNotFound, $"{command.Name}: Example{i} missing");
            }
        }
    }

    [Fact]
    public void Prompt_ShowsUserHostAndDirectory()
    {
        var terminal = TestTerminal.Create();

        Assert.Equal("user@hackos:~$ ls", terminal.Prompt("ls").Text);

        terminal.Run("cd /var/log");

        Assert.Equal("user@hackos:/var/log$ ", terminal.Prompt().Text);
    }

    [Fact]
    public void Welcome_IsLocalized()
    {
        var welcome = TestTerminal.Create().Welcome();

        Assert.Equal("Welcome to HackOS!", welcome[0].Text);
    }

    [Fact]
    public void BlankInput_ReturnsNothing()
    {
        Assert.Empty(TestTerminal.Create().Execute("   ").Lines);
    }

    [Fact]
    public void UnknownCommand_ExplainsWhatToDo()
    {
        var result = TestTerminal.Create().Execute("hack");

        var line = Assert.Single(result.Lines);
        Assert.True(line.IsError);
        Assert.Equal("hack: command not found. Type 'help' to see the available commands.", line.Text);
    }

    [Fact]
    public void CommandNames_AreCaseInsensitive()
    {
        Assert.Equal("user", TestTerminal.Create().Run("Whoami"));
    }

    [Fact]
    public void SyntaxError_IsReported()
    {
        var line = Assert.Single(TestTerminal.Create().Execute("cat 'oops").Lines);

        Assert.True(line.IsError);
        Assert.StartsWith("syntax error", line.Text);
    }

    [Fact]
    public void Pipe_PassesStandardOutputButShowsErrors()
    {
        var terminal = TestTerminal.Create(fs => fs.WriteFile("/home/user/log.txt", "ok\nFailed login\nok\nFailed again"));

        Assert.Equal("Failed login\nFailed again", terminal.Run("cat log.txt | grep Failed"));
        Assert.Equal("Failed again", terminal.Run("cat log.txt | grep Failed | grep again"));
        Assert.Equal("cat: missing.txt: No such file or directory", terminal.Run("cat missing.txt | grep Failed"));
    }

    [Fact]
    public void Clear_RequestsClearScreen()
    {
        var result = TestTerminal.Create().Execute("clear");

        Assert.True(result.ClearScreen);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Help_ListsEveryCommand()
    {
        var terminal = TestTerminal.Create();

        var output = terminal.Run("help");

        Assert.StartsWith("Available commands:", output);
        Assert.All(terminal.Commands, command => Assert.Contains(command.Name, output));
        Assert.DoesNotContain("Cmd.", output);

        var lsLine = terminal.Execute("help").Lines.Single(line => line.Text.StartsWith("  ls "));
        Assert.Equal(10, lsLine.HangingIndent);
    }

    [Fact]
    public void Help_ExplainsOneCommand()
    {
        var output = TestTerminal.Create().Run("help grep");

        Assert.Contains("Usage: grep [-i] [-n] [-v] [-r] <text> [file ...]", output);
        Assert.Contains("$ cat /var/log/auth.log | grep Failed", output);
        Assert.Contains("Send a file through a pipe into grep", output);
    }

    [Fact]
    public void Help_UnknownTopic()
    {
        Assert.StartsWith("help: no help topics match 'nope'", TestTerminal.Create().Run("help nope"));
    }

    [Fact]
    public void InvalidOption_IsReported()
    {
        Assert.Equal("ls: invalid option -- 'z'. Try 'help ls'.", TestTerminal.Create().Run("ls -z"));
    }

    [Fact]
    public void OutputLines_CarryStylesForTheUi()
    {
        var result = TestTerminal.Create().Execute("ls");

        var segments = Assert.Single(result.Lines).Segments;
        Assert.Contains(segments, segment => segment is { Text: "Documents/", Style: OutputStyle.Directory });
    }
}
