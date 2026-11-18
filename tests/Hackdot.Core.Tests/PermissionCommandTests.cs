using Hackdot.Core.Models;
using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class PermissionCommandTests
{
    private static int Octal(string value) => Convert.ToInt32(value, 8);

    private static CommandInterpreter Create() => TestTerminal.Create(fs =>
    {
        fs.WriteFile("/home/user/notes.txt", "my notes", "user", Octal("000"));
        fs.WriteFile("/home/user/shared.txt", "shared", "user", Octal("644"));
        fs.CreateDirectory("/usr/bin", "root", Octal("755"));
        fs.WriteFile("/usr/bin/cat", "ELF", "root", Octal("755"));
        fs.WriteFile("/usr/bin/head", "ELF", "root", Octal("4755"));
        fs.WriteFile("/usr/bin/tail", "ELF", "root", Octal("4750"));
        fs.WriteFile("/usr/bin/whoami", "ELF", "root", Octal("4755"));
        fs.WriteFile("/usr/bin/su", "ELF", "root", Octal("4755"));
    });

    [Theory]
    [InlineData("4755", "rwsr-xr-x")]
    [InlineData("4644", "rwSr--r--")]
    [InlineData("2755", "rwxr-sr-x")]
    [InlineData("1777", "rwxrwxrwt")]
    [InlineData("1666", "rw-rw-rwT")]
    public void SpecialBits_ShowInSymbolicModes(string octal, string symbolic)
    {
        var mode = FilePermissions.Parse(octal);

        Assert.Equal(symbolic, FilePermissions.ToSymbolic(mode));
        Assert.Equal(octal, FilePermissions.ToOctal(mode));
    }

    [Theory]
    [InlineData("644", "600", "600")]
    [InlineData("644", "4755", "4755")]
    [InlineData("000", "u+r", "400")]
    [InlineData("644", "go-r", "600")]
    [InlineData("600", "a+r", "644")]
    [InlineData("644", "+x", "755")]
    [InlineData("755", "u=rw,go=r", "644")]
    [InlineData("755", "u+s", "4755")]
    [InlineData("4755", "u-s", "755")]
    [InlineData("777", "o-w,g-w", "755")]
    [InlineData("644", "o=", "640")]
    public void Chmod_AppliesOctalAndSymbolicModes(string before, string mode, string after)
    {
        Assert.True(FilePermissions.TryApply(mode, Octal(before), out var result));
        Assert.Equal(after, FilePermissions.ToOctal(result));
    }

    [Theory]
    [InlineData("rwx")]
    [InlineData("u+q")]
    [InlineData("8")]
    [InlineData("12345")]
    [InlineData("u+r,")]
    public void Chmod_RejectsInvalidModes(string mode)
    {
        Assert.False(FilePermissions.TryApply(mode, 0, out _));
    }

    [Fact]
    public void Chmod_TheOwnerCanUnlockTheirOwnFile()
    {
        var terminal = Create();

        Assert.Equal("cat: notes.txt: Permission denied", terminal.Run("cat notes.txt"));
        Assert.Equal("", terminal.Run("chmod u+r notes.txt"));
        Assert.Equal("my notes", terminal.Run("cat notes.txt"));
        Assert.StartsWith("-r-------- 1 user", terminal.Run("ls -l notes.txt"));
    }

    [Theory]
    [InlineData("chmod 600", "chmod: missing operand. Try 'help chmod'.")]
    [InlineData("chmod abc notes.txt", "chmod: invalid mode: 'abc'. Try a number like 600 or letters like u+r.")]
    [InlineData("chmod 600 nope.txt", "chmod: cannot access 'nope.txt': No such file or directory")]
    [InlineData("chmod 777 /etc/passwd", "chmod: changing permissions of '/etc/passwd': Operation not permitted")]
    [InlineData("chmod 644 /root/top_secret.txt", "chmod: /root/top_secret.txt: Permission denied")]
    public void Chmod_ReportsProblems(string command, string error)
    {
        var result = Create().Execute(command);

        Assert.Equal(error, result.Lines[0].Text);
        Assert.True(result.Lines[0].IsError);
    }

    [Fact]
    public void Chmod_RootMayChangeAnything()
    {
        var terminal = TestTerminal.Create(fs => fs.AddUser(new VirtualUser("root", "/root", "toor")));
        terminal.Run("su root");
        terminal.Run("toor");

        Assert.Equal("", terminal.Run("chmod 600 /etc/passwd"));
        terminal.Run("exit");
        Assert.Equal("cat: /etc/passwd: Permission denied", terminal.Run("cat /etc/passwd"));
    }

    [Theory]
    [InlineData("find /home/user -perm 000", "/home/user/notes.txt")]
    [InlineData("find /home/user -perm 644 -name \"s*\"", "/home/user/shared.txt")]
    [InlineData("find /usr/bin -perm -4000", "/usr/bin/head\n/usr/bin/su\n/usr/bin/tail\n/usr/bin/whoami")]
    [InlineData("find /usr/bin -perm -u=s -name \"h*\"", "/usr/bin/head")]
    [InlineData("find /usr/bin -type f -perm /4000 -name \"c*\"", "")]
    [InlineData("find /usr/bin -type f -perm 755", "/usr/bin/cat")]
    public void Find_FiltersByPermissions(string command, string expected)
    {
        Assert.Equal(expected, Create().Run(command));
    }

    [Fact]
    public void Find_RejectsAnInvalidPermission()
    {
        Assert.Equal("find: invalid mode 'xyz' after -perm. Try for example -perm -4000 or -perm 644.", Create().Run("find / -perm xyz"));
    }

    [Fact]
    public void DiscardingErrors_HidesPermissionDenied_ButKeepsTheOutput()
    {
        var terminal = Create();

        Assert.Contains("Permission denied", terminal.Run("find / -name top_secret.txt"));
        Assert.Equal("", terminal.Run("find / -name top_secret.txt 2>/dev/null"));
        Assert.Equal("/etc/passwd", terminal.Run("find / -name passwd 2>/dev/null"));
        Assert.Equal("", terminal.Run("nosuchcommand 2>/dev/null"));
        Assert.Equal("root:x:0:0:root:/root:/bin/bash", terminal.Run("cat /etc/passwd 2>/dev/null | head -n 1"));
    }

    [Fact]
    public void SetUidPrograms_RunAsTheirOwner()
    {
        var terminal = Create();

        Assert.Equal("If you can read this, you are root.", terminal.Run("head -n 1 /root/top_secret.txt"));
        Assert.Equal("root", terminal.Run("whoami"));
        Assert.Equal("cat: /root/top_secret.txt: Permission denied", terminal.Run("cat /root/top_secret.txt"));

        // The player's own session never changes.
        Assert.Equal("user", terminal.Session.User.Name);
    }

    [Fact]
    public void SetUidPrograms_NeedExecutePermission()
    {
        // tail is 4750: only root may run it, so it runs as the player (and can't read root's file).
        Assert.Equal("tail: /root/top_secret.txt: Permission denied", Create().Run("tail /root/top_secret.txt"));
    }

    [Fact]
    public void SetUidSu_StillAsksForThePassword()
    {
        var terminal = TestTerminal.Create(fs =>
        {
            fs.AddUser(new VirtualUser("root", "/root", "toor"));
            fs.CreateDirectory("/usr/bin", "root", Octal("755"));
            fs.WriteFile("/usr/bin/su", "ELF", "root", Octal("4755"));
        });

        terminal.Execute("su root");

        Assert.NotNull(terminal.PendingInputLabel);
    }

    [Theory]
    [InlineData("/usr/bin/head -n 1 /root/top_secret.txt", "If you can read this, you are root.")]
    [InlineData("/usr/bin/cat /etc/hostname", "dotos")]
    [InlineData("/bin/cat /etc/hostname", "dotos")]
    [InlineData("/home/user/cat /etc/hostname", "/home/user/cat: command not found. Type 'help' to see the available commands.")]
    public void Commands_CanBeRunByTheirProgramPath(string command, string expected)
    {
        Assert.Equal(expected, Create().Run(command));
    }
}
