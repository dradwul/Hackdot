using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class LsTests
{
    [Fact]
    public void Ls_HidesDotFilesByDefault()
    {
        Assert.Equal("Documents/  Downloads/", TestTerminal.Create().Run("ls"));
    }

    [Fact]
    public void LsA_ShowsHiddenFilesAndDotEntries()
    {
        Assert.Equal(".  ..  .bashrc  Documents/  Downloads/", TestTerminal.Create().Run("ls -a"));
    }

    [Fact]
    public void LsLa_ShowsLongFormat()
    {
        var terminal = TestTerminal.Create(fs => fs.WriteFile("/var/www/.secret_flag", "FLAG{hidden}"));

        var lines = terminal.Run("ls -la /var/www").Split('\n');

        Assert.Equal("total 16", lines[0]);
        Assert.Equal("drwxr-xr-x 3 root 4096 Mar 14 09:12 .", lines[1]);
        Assert.Equal("drwxr-xr-x 4 root 4096 Mar 14 09:12 ..", lines[2]);
        Assert.Equal("drwxr-xr-x 2 root 4096 Mar 14 09:12 html", lines[3]);
        Assert.Equal("-rw-r--r-- 1 root   12 Mar 14 09:12 .secret_flag", lines[4]);
    }

    [Fact]
    public void Ls_SeparateFlagsWorkLikeCombinedFlags()
    {
        var terminal = TestTerminal.Create();

        Assert.Equal(terminal.Run("ls -la"), terminal.Run("ls -l -a"));
    }

    [Fact]
    public void Ls_FileOperandListsTheFile()
    {
        Assert.Equal("/etc/passwd", TestTerminal.Create().Run("ls /etc/passwd"));
    }

    [Fact]
    public void Ls_MultipleDirectoriesGetHeaders()
    {
        var output = TestTerminal.Create().Run("ls ~ /var");

        Assert.Equal("~:\nDocuments/  Downloads/\n\n/var:\nlog/  www/", output);
    }

    [Fact]
    public void Ls_Errors()
    {
        var terminal = TestTerminal.Create();

        Assert.Equal("ls: cannot access 'nope': No such file or directory", terminal.Run("ls nope"));
        Assert.Equal("ls: cannot open directory '/root': Permission denied", terminal.Run("ls /root"));
    }
}

public class CdAndPwdTests
{
    [Theory]
    [InlineData("cd Documents", "/home/user/Documents")]
    [InlineData("cd ..", "/home")]
    [InlineData("cd /var/log", "/var/log")]
    [InlineData("cd /", "/")]
    [InlineData("cd ~/Documents", "/home/user/Documents")]
    public void Cd_ChangesDirectory(string command, string expected)
    {
        var terminal = TestTerminal.Create();

        Assert.Equal("", terminal.Run(command));
        Assert.Equal(expected, terminal.Run("pwd"));
    }

    [Fact]
    public void Cd_WithoutArgumentGoesHome()
    {
        var terminal = TestTerminal.Create();
        terminal.Run("cd /var/log");

        terminal.Run("cd");

        Assert.Equal("/home/user", terminal.Run("pwd"));
    }

    [Theory]
    [InlineData("cd nope", "cd: nope: No such file or directory")]
    [InlineData("cd /etc/passwd", "cd: /etc/passwd: Not a directory")]
    [InlineData("cd /root", "cd: /root: Permission denied")]
    [InlineData("cd a b", "cd: too many arguments")]
    public void Cd_Errors_DoNotMove(string command, string error)
    {
        var terminal = TestTerminal.Create();

        Assert.Equal(error, terminal.Run(command));
        Assert.Equal("/home/user", terminal.Run("pwd"));
    }

    [Fact]
    public void Whoami_ShowsUser()
    {
        Assert.Equal("user", TestTerminal.Create().Run("whoami"));
    }
}

public class CatTests
{
    [Fact]
    public void Cat_PrintsFileContent()
    {
        Assert.Equal("hackos", TestTerminal.Create().Run("cat /etc/hostname"));
    }

    [Fact]
    public void Cat_RelativePathAndLineNumbers()
    {
        var terminal = TestTerminal.Create(fs => fs.WriteFile("/home/user/Documents/todo.txt", "one\ntwo\n"));
        terminal.Run("cd Documents");

        Assert.Equal("     1  one\n     2  two", terminal.Run("cat -n todo.txt"));
    }

    [Fact]
    public void Cat_MultipleFilesAreConcatenated()
    {
        var terminal = TestTerminal.Create(fs =>
        {
            fs.WriteFile("/tmp/a", "A");
            fs.WriteFile("/tmp/b", "B");
        });

        Assert.Equal("A\nB", terminal.Run("cat /tmp/a /tmp/b"));
    }

    [Theory]
    [InlineData("cat", "cat: missing operand. Try 'help cat'.")]
    [InlineData("cat nope.txt", "cat: nope.txt: No such file or directory")]
    [InlineData("cat Documents", "cat: Documents: Is a directory")]
    [InlineData("cat /root/top_secret.txt", "cat: /root/top_secret.txt: Permission denied")]
    public void Cat_Errors(string command, string error)
    {
        var result = TestTerminal.Create().Execute(command);

        var line = Assert.Single(result.Lines);
        Assert.True(line.IsError);
        Assert.Equal(error, line.Text);
    }
}

public class GrepTests
{
    private static CommandInterpreter CreateWithLog() => TestTerminal.Create(fs => fs.WriteFile(
        "/var/log/auth.log",
        "Mar 14 sshd: Accepted password for user\n" +
        "Mar 14 sshd: Failed password for root\n" +
        "Mar 14 sshd: failed password for admin\n" +
        "Mar 14 CRON: session opened FLAG{grep_master}\n"));

    [Fact]
    public void Grep_PrintsMatchingLines()
    {
        Assert.Equal("Mar 14 sshd: Failed password for root", CreateWithLog().Run("grep Failed /var/log/auth.log"));
    }

    [Fact]
    public void Grep_HighlightsMatches()
    {
        var line = Assert.Single(CreateWithLog().Execute("grep FLAG /var/log/auth.log").Lines);

        Assert.Contains(line.Segments, segment => segment is { Text: "FLAG", Style: OutputStyle.Highlight });
        Assert.Equal("Mar 14 CRON: session opened FLAG{grep_master}", line.Text);
    }

    [Fact]
    public void Grep_IgnoreCaseAndLineNumbers()
    {
        Assert.Equal(
            "2:Mar 14 sshd: Failed password for root\n3:Mar 14 sshd: failed password for admin",
            CreateWithLog().Run("grep -in failed /var/log/auth.log"));
    }

    [Fact]
    public void Grep_Invert()
    {
        Assert.Equal("Mar 14 CRON: session opened FLAG{grep_master}", CreateWithLog().Run("grep -v password /var/log/auth.log"));
    }

    [Fact]
    public void Grep_QuotedPatternWithSpaces()
    {
        Assert.Equal("Mar 14 sshd: Failed password for root", CreateWithLog().Run("grep \"Failed password\" /var/log/auth.log"));
    }

    [Fact]
    public void Grep_Recursive_LabelsFilesAndReportsDeniedFolders()
    {
        var terminal = TestTerminal.Create(fs =>
        {
            fs.WriteFile("/home/user/Documents/notes.txt", "password: hunter2");
            fs.WriteFile("/home/user/.hidden_notes.txt", "Password reminder");
        });

        Assert.Equal(
            "./Documents/notes.txt:password: hunter2\n./.hidden_notes.txt:Password reminder",
            terminal.Run("grep -ri password"));

        Assert.Contains("grep: /root: Permission denied", terminal.Run("grep -r secret /"));
    }

    [Theory]
    [InlineData("grep", "grep: missing operand. Try 'help grep'.")]
    [InlineData("grep x /nope", "grep: /nope: No such file or directory")]
    [InlineData("grep x /var", "grep: /var: Is a directory")]
    [InlineData("grep x /root/top_secret.txt", "grep: /root/top_secret.txt: Permission denied")]
    public void Grep_Errors(string command, string error)
    {
        Assert.Equal(error, TestTerminal.Create().Run(command));
    }

    [Fact]
    public void Grep_WithoutFile_ExplainsHowToUseIt()
    {
        Assert.StartsWith("grep: no file to search.", TestTerminal.Create().Run("grep FLAG"));
    }
}

public class FindTests
{
    private static CommandInterpreter Create() => TestTerminal.Create(fs =>
    {
        fs.WriteFile("/var/www/.secret_flag", "FLAG{x}");
        fs.WriteFile("/home/user/Documents/flag_hint.txt", "nope");
    });

    [Fact]
    public void Find_QuickSearch_SearchesEverywhereIgnoringCase()
    {
        var output = Create().Run("find FLAG");

        Assert.Equal("/home/user/Documents/flag_hint.txt\nfind: /root: Permission denied\n/var/www/.secret_flag", output);
    }

    [Fact]
    public void Find_Name_UsesGlobPatterns()
    {
        Assert.Equal("/var/www/.secret_flag", Create().Run("find /var -name \".*\""));
        Assert.Equal("./Documents/flag_hint.txt\n./Documents/welcome.txt", Create().Run("find . -name *.txt"));
    }

    [Fact]
    public void Find_Type_FiltersFilesOrDirectories()
    {
        Assert.Equal("/var/www\n/var/www/html", Create().Run("find /var/www -type d"));
        Assert.Equal("/var/www/html/index.html\n/var/www/.secret_flag", Create().Run("find /var/www -type f"));
    }

    [Fact]
    public void Find_ExistingDirectoryIsAPath()
    {
        Assert.Equal("Documents\nDocuments/flag_hint.txt\nDocuments/welcome.txt", Create().Run("find Documents"));
    }

    [Theory]
    [InlineData("find /var -name", "find: missing argument to '-name'")]
    [InlineData("find /var -type x", "find: -type must be followed by f (file) or d (directory)")]
    [InlineData("find /var -size 10", "find: unknown option '-size'. Try 'help find'.")]
    [InlineData("find /nope -name x", "find: /nope: No such file or directory")]
    public void Find_Errors(string command, string error)
    {
        Assert.Equal(error, Create().Run(command));
    }
}
