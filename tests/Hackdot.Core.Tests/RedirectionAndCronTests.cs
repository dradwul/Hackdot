using Hackdot.Core.Models;
using Hackdot.Core.Services;
using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class RedirectionAndCronTests
{
    private static int Octal(string value) => Convert.ToInt32(value, 8);

    private static string RunAll(CommandInterpreter terminal, params string[] inputs) =>
        inputs.Select(terminal.Run).ToList().Last();

    [Theory]
    [InlineData("echo hi > out.txt", 1, "out.txt", false)]
    [InlineData("echo hi >out.txt", 1, "out.txt", false)]
    [InlineData("echo hi >> out.txt", 1, "out.txt", true)]
    [InlineData("ls nope 2> err.txt", 2, "err.txt", false)]
    [InlineData("ls nope 2>/dev/null", 2, "/dev/null", false)]
    [InlineData("echo hi > 'my file.txt'", 1, "my file.txt", false)]
    public void Parser_ReadsRedirections(string input, int stream, string target, bool append)
    {
        var command = Assert.Single(CommandLineParser.ParseLine(input));

        Assert.Equal(new Redirection(stream, target, append), Assert.Single(command.Redirections));
        Assert.DoesNotContain(command.Words, word => word.Contains('>'));
    }

    [Fact]
    public void Parser_KeepsQuotedArrowsAsText()
    {
        var command = Assert.Single(CommandLineParser.ParseLine("echo 'cat a > b' >> s.sh"));

        Assert.Equal(["echo", "cat a > b"], command.Words);
        Assert.Equal(new Redirection(1, "s.sh", true), Assert.Single(command.Redirections));
    }

    [Theory]
    [InlineData("echo 2>x", new[] { "echo" }, 2)]
    [InlineData("echo a2>x", new[] { "echo", "a2" }, 1)]
    [InlineData("echo '2'>x", new[] { "echo", "2" }, 1)]
    public void Parser_OnlyAPlain2RedirectsErrors(string input, string[] words, int stream)
    {
        var command = Assert.Single(CommandLineParser.ParseLine(input));

        Assert.Equal(words, command.Words);
        Assert.Equal(stream, Assert.Single(command.Redirections).Stream);
    }

    [Fact]
    public void Parser_RequiresAFileName()
    {
        var error = Assert.Throws<CommandSyntaxException>(() => CommandLineParser.ParseLine("echo hi >"));

        Assert.Equal("Error.MissingRedirectTarget", error.ResourceKey);
    }

    [Fact]
    public void Redirect_WritesAndAppends()
    {
        var terminal = TestTerminal.Create();

        Assert.Equal("", terminal.Run("echo one > notes.txt"));
        terminal.Run("echo two >> notes.txt");
        Assert.Equal("one\ntwo", terminal.Run("cat notes.txt"));

        terminal.Run("echo three > notes.txt");
        Assert.Equal("three", terminal.Run("cat notes.txt"));
        Assert.StartsWith("-rw-r--r-- 1 user", terminal.Run("ls -l notes.txt"));
    }

    [Fact]
    public void Redirect_WorksAtTheEndOfAPipeline()
    {
        var terminal = TestTerminal.Create();

        terminal.Run("cat /etc/passwd | grep root > /tmp/root.txt");

        Assert.Equal("root:x:0:0:root:/root:/bin/bash", terminal.Run("cat /tmp/root.txt"));
    }

    [Fact]
    public void Redirect_KeepsErrorsOnTheScreen_Unless2IsRedirectedToo()
    {
        var terminal = TestTerminal.Create();

        Assert.Equal("ls: cannot access 'nope': No such file or directory", terminal.Run("ls nope > /tmp/out.txt"));
        Assert.Equal("", terminal.Run("cat /tmp/out.txt"));

        Assert.Equal("", terminal.Run("ls nope 2> /tmp/err.txt"));
        Assert.Equal("ls: cannot access 'nope': No such file or directory", terminal.Run("cat /tmp/err.txt"));

        Assert.Equal("", terminal.Run("ls nope > /tmp/all.txt 2>&1"));
        Assert.Equal("ls: cannot access 'nope': No such file or directory", terminal.Run("cat /tmp/all.txt"));
    }

    [Theory]
    [InlineData("echo x > /etc/passwd", "bash: /etc/passwd: Permission denied")]
    [InlineData("echo x > /root/new.txt", "bash: /root/new.txt: Permission denied")]
    [InlineData("echo x > /etc/new.txt", "bash: /etc/new.txt: Permission denied")]
    [InlineData("echo x > /no/such/folder/file", "bash: /no/such/folder/file: No such file or directory")]
    [InlineData("echo x > /tmp", "bash: /tmp: Is a directory")]
    public void Redirect_NeedsWritePermission(string command, string error)
    {
        var result = TestTerminal.Create().Execute(command);

        Assert.Equal(error, Assert.Single(result.Lines).Text);
        Assert.True(result.Lines[0].IsError);
    }

    [Fact]
    public void Redirect_IsDoneByTheShellUser_NotBySetUidOwners()
    {
        var terminal = TestTerminal.Create(fs =>
        {
            fs.CreateDirectory("/usr/bin", "root", Octal("755"));
            fs.WriteFile("/usr/bin/cat", "ELF", "root", Octal("4755"));
        });

        // cat runs as root and can read the file, but the user can't write into /etc.
        Assert.Equal("bash: /etc/copy.txt: Permission denied", terminal.Run("cat /root/top_secret.txt > /etc/copy.txt"));
        terminal.Run("cat /root/top_secret.txt > /tmp/copy.txt");
        Assert.StartsWith("-rw-r--r-- 1 user", terminal.Run("ls -l /tmp/copy.txt"));
    }

    [Fact]
    public void Touch_CreatesEmptyFiles()
    {
        var terminal = TestTerminal.Create();

        Assert.Equal("", terminal.Run("touch /tmp/empty.txt"));
        Assert.Equal("0 /tmp/empty.txt", terminal.Run("wc -c /tmp/empty.txt"));
        Assert.Equal("touch: cannot touch '/root/x': Permission denied", terminal.Run("touch /root/x"));
        Assert.Equal("touch: cannot touch '/nope/x': No such file or directory", terminal.Run("touch /nope/x"));
    }

    [Fact]
    public void Clock_MovesOnAMinutePerCommand_AndWithSleep()
    {
        var terminal = TestTerminal.Create();

        Assert.Equal("Sat Mar 14 09:12:00 UTC 2026", terminal.Run("date"));
        Assert.Equal("Sat Mar 14 09:13:00 UTC 2026", terminal.Run("date"));
        terminal.Run("sleep 5m");
        Assert.Equal("Sat Mar 14 09:20:00 UTC 2026", terminal.Run("date"));
        terminal.Run("sleep 30");
        Assert.Equal("Sat Mar 14 09:22:30 UTC 2026", terminal.Run("date"));
    }

    [Theory]
    [InlineData("sleep", "sleep: missing operand. Try 'help sleep'.")]
    [InlineData("sleep soon", "sleep: invalid time interval 'soon'. Try 'sleep 60' or 'sleep 5m'.")]
    [InlineData("sleep 2h", "sleep: DotOS sleeps at most one hour at a time.")]
    public void Sleep_ReportsProblems(string command, string error)
    {
        Assert.Equal(error, TestTerminal.Create().Run(command));
    }

    [Fact]
    public void Scripts_RunLineByLine_AsTheCurrentUser()
    {
        var terminal = TestTerminal.Create(fs => fs.WriteFile(
            "/home/user/hello.sh", "#!/bin/bash\n# a comment\n\nwhoami\necho hi > /tmp/hi.txt\ncd /\n", "user", Octal("755")));

        Assert.Equal("user", terminal.Run("./hello.sh"));
        Assert.Equal("hi", terminal.Run("cat /tmp/hi.txt"));
        Assert.Equal("/home/user", terminal.Run("pwd"));
    }

    [Fact]
    public void Scripts_NeedExecutePermission()
    {
        var terminal = TestTerminal.Create(fs => fs.WriteFile("/home/user/hello.sh", "whoami", "user", Octal("644")));

        Assert.StartsWith("bash: ./hello.sh: Permission denied", terminal.Run("./hello.sh"));
        terminal.Run("chmod +x hello.sh");
        Assert.Equal("user", terminal.Run("./hello.sh"));
    }

    [Fact]
    public void Scripts_CannotStartThemselvesForever()
    {
        var terminal = TestTerminal.Create(fs => fs.WriteFile("/home/user/loop.sh", "/home/user/loop.sh", "user", Octal("755")));

        Assert.Contains("too many scripts", terminal.Run("./loop.sh"));
    }

    [Theory]
    [InlineData("*", 7, true)]
    [InlineData("7", 7, true)]
    [InlineData("8", 7, false)]
    [InlineData("*/5", 10, true)]
    [InlineData("*/5", 12, false)]
    [InlineData("1-5", 3, true)]
    [InlineData("1-5", 6, false)]
    [InlineData("0,30", 30, true)]
    [InlineData("0-30/10", 20, true)]
    [InlineData("0-30/10", 40, false)]
    [InlineData("x", 1, false)]
    public void Cron_MatchesTimeFields(string field, int value, bool expected)
    {
        Assert.Equal(expected, Cron.Matches(field, value, 0, 59));
    }

    [Fact]
    public void Cron_ReadsTheSystemCrontab()
    {
        var fs = FileSystemLoader.CreateDefault();
        fs.WriteFile(Cron.TablePath, "# comment\nSHELL=/bin/bash\n*/5 * * * * root /opt/job.sh --quiet\n@reboot root /x\nbroken line\n");

        var job = Assert.Single(Cron.Jobs(fs));

        Assert.Equal(new CronJob("*/5", "*", "*", "*", "*", "root", "/opt/job.sh --quiet"), job);
        Assert.Equal("/opt/job.sh", job.ScriptPath);
        Assert.True(job.IsDue(new DateTime(2026, 3, 14, 9, 15, 0)));
        Assert.False(job.IsDue(new DateTime(2026, 3, 14, 9, 16, 0)));
    }

    [Fact]
    public void Cron_WeekdaysAndDaysOfMonth()
    {
        // 2026-03-14 is a Saturday (6); Sunday may be written as 0 or 7.
        Assert.True(new CronJob("0", "9", "*", "*", "6", "root", "x").IsDue(new DateTime(2026, 3, 14, 9, 0, 0)));
        Assert.False(new CronJob("0", "9", "*", "*", "1-5", "root", "x").IsDue(new DateTime(2026, 3, 14, 9, 0, 0)));
        Assert.True(new CronJob("0", "9", "*", "*", "7", "root", "x").IsDue(new DateTime(2026, 3, 15, 9, 0, 0)));
        // Both day fields restricted: either may match.
        Assert.True(new CronJob("0", "9", "14", "*", "1", "root", "x").IsDue(new DateTime(2026, 3, 14, 9, 0, 0)));
    }

    [Fact]
    public void CronJobs_RunAsTheirUser_WhenTheirTimeComes()
    {
        var terminal = TestTerminal.Create(fs =>
        {
            fs.WriteFile(Cron.TablePath, "*/5 * * * * root /opt/job.sh\n");
            fs.WriteFile("/opt/job.sh", "whoami >> /tmp/job.log\n", "root", Octal("777"));
            fs.WriteFile(Cron.LogPath, "", "root", Octal("644"));
        });

        // 09:12 → 09:13 and 09:14: not yet.
        terminal.Run("pwd");
        terminal.Run("pwd");
        Assert.Equal("cat: /tmp/job.log: No such file or directory", terminal.Run("cat /tmp/job.log"));

        // 09:15 has passed now.
        Assert.Equal("root", terminal.Run("cat /tmp/job.log"));
        Assert.Equal("Mar 14 09:15:01 dotos CRON[2555]: (root) CMD (/opt/job.sh)", terminal.Run("cat /var/log/cron.log"));

        // Sleeping runs every job that was due in the meantime: 09:20, 09:25 and 09:30.
        terminal.Run("sleep 15m");
        Assert.Equal("root\nroot\nroot\nroot", terminal.Run("cat /tmp/job.log"));
    }

    [Fact]
    public void ScriptsAndJobs_DoNotMoveTheClockThemselves()
    {
        var terminal = TestTerminal.Create(fs => fs.WriteFile("/home/user/many.sh", "pwd\npwd\npwd\npwd\n", "user", Octal("755")));

        terminal.Run("./many.sh");

        Assert.Equal("Sat Mar 14 09:13:00 UTC 2026", terminal.Run("date"));
    }

    [Fact]
    public void Validate_FollowsWritableCronScripts()
    {
        const string template = """
            {
              "id": "cron", "title": "t", "summary": "s", "briefing": "b", "objective": "o", "debrief": "d",
              "flag": "FLAG{cron}", "hints": [ "1", "2", "3" ],
              "fileSystem": {
                "files": [
                  { "path": "/etc/crontab", "content": "* * * * * root /opt/job.sh" },
                  { "path": "/opt/job.sh", "content": "true", "owner": "root", "mode": "MODE" },
                  { "path": "/root/flag.txt", "content": "FLAG{cron}", "owner": "root", "mode": "600" }
                ]
              }
            }
            """;
        var catalog = Services.Missions.MissionCatalog.LoadEmbedded();

        Assert.Empty(catalog.Validate(Services.Missions.MissionCatalog.Parse(template.Replace("MODE", "777"))));
        Assert.NotEmpty(catalog.Validate(Services.Missions.MissionCatalog.Parse(template.Replace("MODE", "755"))));
    }
}
