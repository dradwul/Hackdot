using Hackdot.Core.Models;
using Hackdot.Core.Services;
using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class SudoTests
{
    private const string Rules = """
        # comment
        Defaults        env_reset
        root    ALL=(ALL) ALL
        user    ALL=(root) NOPASSWD: /usr/bin/cat /var/log/*, /usr/bin/whoami
        admin   ALL=(root) /usr/bin/tail
        """;

    private static CommandInterpreter Create(string? sudoers = Rules) => TestTerminal.Create(fs =>
    {
        if (sudoers is not null)
        {
            fs.WriteFile(Sudoers.FilePath, sudoers, owner: "root", mode: 0b100_100_000); // 440
        }

        fs.AddUser(new VirtualUser("admin", "/home/admin", "Summer2026!"));
        fs.WriteFile("/var/log/app.log", "started");
        fs.WriteFile("/root/flag.txt", "FLAG{root}", owner: "root", mode: 0b110_000_000); // 600
    });

    [Fact]
    public void Parse_ReadsRulesAndSkipsCommentsAndDefaults()
    {
        var rules = Sudoers.Parse(Rules).ToList();

        Assert.Equal(4, rules.Count);
        Assert.Equal(new SudoRule("user", "root", true, "/usr/bin/cat", "/var/log/*"), rules[1]);
        Assert.Equal(new SudoRule("user", "root", true, "/usr/bin/whoami", null), rules[2]);
        Assert.False(rules[3].NoPassword);
        Assert.True(rules[0].AllowsAnyCommand);
    }

    [Theory]
    [InlineData("cat", "/var/log/app.log", true)]
    [InlineData("cat", "/var/log/../../root/flag.txt", true)]   // * matches '..' – the classic mistake
    [InlineData("cat", "/var/log/app.log /root/flag.txt", true)] // * matches spaces and extra files too
    [InlineData("cat", "/root/flag.txt", false)]
    [InlineData("tail", "/var/log/app.log", false)]
    public void Rule_MatchesArgumentsLikeTheRealSudo(string command, string arguments, bool allowed)
    {
        var rule = new SudoRule("user", "root", true, "/usr/bin/cat", "/var/log/*");

        Assert.Equal(allowed, rule.Allows(command, arguments.Split(' ')));
    }

    [Fact]
    public void SudoL_ListsTheUsersRules()
    {
        Assert.Equal(
            "User user may run the following commands on dotos:\n    (root) NOPASSWD: /usr/bin/cat /var/log/*\n    (root) NOPASSWD: /usr/bin/whoami",
            Create().Run("sudo -l"));
    }

    [Fact]
    public void SudoL_WithoutRules_IsNotAnError()
    {
        var result = Create(sudoers: null).Execute("sudo -l");

        Assert.Equal("Sorry, user user may not run sudo on dotos.", result.Lines.Single().Text);
        Assert.False(result.Lines.Single().IsError);
    }

    [Fact]
    public void Sudo_RunsAllowedCommandsAsRoot_JustOnce()
    {
        var terminal = Create();

        Assert.Equal("root", terminal.Run("sudo whoami"));
        Assert.Equal("user", terminal.Run("whoami"));
        Assert.Equal("started", terminal.Run("sudo /usr/bin/cat /var/log/app.log"));
    }

    [Fact]
    public void Sudo_WildcardCanBeAbusedToReadRootFiles()
    {
        var terminal = Create();

        Assert.Contains("Permission denied", terminal.Run("cat /root/flag.txt"));
        Assert.Equal("FLAG{root}", terminal.Run("sudo cat /var/log/../../root/flag.txt"));
        Assert.Equal("started\nFLAG{root}", terminal.Run("sudo cat /var/log/app.log /root/flag.txt"));
    }

    [Fact]
    public void Sudo_WorksInPipes()
    {
        Assert.Equal("RkxBR3tyb290fQo=", Create().Run("sudo cat /var/log/../../root/flag.txt | base64"));
        Assert.Equal("FLAG{root}", Create().Run("sudo cat /var/log/../../root/flag.txt | base64 | base64 -d"));
    }

    [Theory]
    [InlineData("sudo cat /root/flag.txt", "Sorry, user user is not allowed to execute '/usr/bin/cat /root/flag.txt' as root on dotos. Type 'sudo -l' to see what you may run.")]
    [InlineData("sudo ls /root", "Sorry, user user is not allowed to execute 'ls /root' as root on dotos. Type 'sudo -l' to see what you may run.")]
    [InlineData("sudo nmap", "sudo: nmap: command not found")]
    [InlineData("sudo", "sudo: missing operand. Try 'help sudo'.")]
    public void Sudo_RefusesWhatIsNotAllowed(string command, string error)
    {
        var result = Create().Execute(command);

        Assert.Equal(error, result.Lines.Single().Text);
        Assert.True(result.Lines.Single().IsError);
    }

    [Fact]
    public void Sudo_WithoutAnyRules_IsReported()
    {
        Assert.Equal("user is not in the sudoers file. This incident will be reported.", Create(sudoers: null).Run("sudo cat /root/flag.txt"));
    }

    [Fact]
    public void Sudo_AsksForYourOwnPassword_WhenTheRuleNeedsIt()
    {
        var terminal = Create();
        terminal.Execute("su admin");
        terminal.Execute("Summer2026!");

        terminal.Execute("sudo tail /root/flag.txt");
        Assert.Equal("[sudo] password for admin:", terminal.PendingInputLabel);
        Assert.True(terminal.PendingInputIsSecret);
        Assert.Equal("FLAG{root}", terminal.Run("Summer2026!"));

        terminal.Execute("sudo tail /root/flag.txt");
        Assert.Equal("sudo: incorrect password", terminal.Run("wrong"));
    }

    [Fact]
    public void SudoSu_GivesARootShell_UntilExit()
    {
        var terminal = TestTerminal.Create(fs => fs.WriteFile(Sudoers.FilePath, "user ALL=(ALL) NOPASSWD: ALL"));

        terminal.Execute("sudo su");
        Assert.Equal("root", terminal.Run("whoami"));

        terminal.Execute("exit");
        Assert.Equal("user", terminal.Run("whoami"));
    }
}
