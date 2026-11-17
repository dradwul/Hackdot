using Hackdot.Core.Models;
using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class SuTests
{
    private static CommandInterpreter Create() => TestTerminal.Create(fs =>
    {
        fs.AddUser(new VirtualUser("admin", "/home/admin", "Summer2026!"));
        fs.CreateDirectory("/home/admin", "admin", FilePermissions.PrivateDirectory);
        fs.WriteFile("/home/admin/secret.txt", "top secret", "admin", FilePermissions.Parse("600"));
        fs.AddUser(new VirtualUser("guest", "/home/guest"));
    });

    [Fact]
    public void Su_AsksForAPasswordWithoutRunningAnythingYet()
    {
        var terminal = Create();

        var result = terminal.Execute("su admin");

        Assert.Empty(result.Lines);
        Assert.Equal("Password:", terminal.PendingInputLabel);
        Assert.True(terminal.PendingInputIsSecret);
        Assert.Equal("user", terminal.Session.User.Name);
    }

    [Fact]
    public void Su_WithTheRightPassword_SwitchesUserAndKeepsTheFolder()
    {
        var terminal = Create();
        terminal.Run("cd /tmp");

        terminal.Run("su admin");
        var output = terminal.Run("Summer2026!");

        Assert.Equal("You are now admin. Type 'exit' to go back.", output);
        Assert.Null(terminal.PendingInputLabel);
        Assert.Equal("admin", terminal.Run("whoami"));
        Assert.Equal("/tmp", terminal.Run("pwd"));
        Assert.Equal("top secret", terminal.Run("cat /home/admin/secret.txt"));
        Assert.Equal("admin@hackos:/tmp$ ", terminal.Prompt().Text);
    }

    [Fact]
    public void Su_WithTheWrongPassword_Fails()
    {
        var terminal = Create();
        terminal.Run("su admin");

        var result = terminal.Execute("summer2026!");

        Assert.Equal("su: Authentication failure", Assert.Single(result.Lines).Text);
        Assert.Equal("user", terminal.Run("whoami"));
    }

    [Fact]
    public void SuDash_GoesToTheNewUsersHome_AndExitComesBack()
    {
        var terminal = Create();
        terminal.Run("su - admin");
        terminal.Run("Summer2026!");

        Assert.Equal("/home/admin", terminal.Run("pwd"));

        Assert.Equal("Back to user.", terminal.Run("exit"));
        Assert.Equal("user", terminal.Run("whoami"));
        Assert.Equal("/home/user", terminal.Run("pwd"));
        Assert.StartsWith("There is no previous user", terminal.Run("exit"));
    }

    [Fact]
    public void Su_ToAnAccountWithoutPassword_NeedsNoPassword()
    {
        var terminal = Create();

        Assert.Equal("You are now guest. Type 'exit' to go back.", terminal.Run("su guest"));
        Assert.Null(terminal.PendingInputLabel);
    }

    [Fact]
    public void Su_ToLockedRoot_AlwaysFails()
    {
        var terminal = Create();

        terminal.Run("su");
        Assert.Equal("su: Authentication failure", terminal.Run(""));
        Assert.Equal("user", terminal.Run("whoami"));
    }

    [Theory]
    [InlineData("su nobody", "su: user nobody does not exist.")]
    [InlineData("su -x admin", "su: invalid option -- 'x'.")]
    [InlineData("su admin guest", "su: too many arguments")]
    public void Su_Errors(string command, string expectedStart)
    {
        Assert.StartsWith(expectedStart, Create().Run(command));
    }

    [Fact]
    public void RootPrompt_IsMarkedWithHashAndErrorStyle()
    {
        var terminal = Create();
        terminal.Session.PushUser(terminal.Session.FileSystem.FindUser("root")!, goHome: true);

        var prompt = terminal.Prompt();

        // Root's home is /root, so it is shown as ~ – just like on a real system.
        Assert.Equal("root@hackos:~# ", prompt.Text);
        Assert.Equal(OutputStyle.Error, prompt.Segments[0].Style);
    }

    [Fact]
    public void Accounts_LoginRules()
    {
        var root = new VirtualUser("root", "/root");
        var guest = new VirtualUser("guest", "/home/guest");
        var admin = new VirtualUser("admin", "/home/admin", "Pw");

        Assert.False(root.CanLogIn);
        Assert.False(root.Accepts(""));
        Assert.True(guest.Accepts(null));
        Assert.True(admin.Accepts("Pw"));
        Assert.False(admin.Accepts("pw"));
    }

    [Fact]
    public void LoginUsers_PutTheDefaultUserFirstAndHideLockedRoot()
    {
        var terminal = Create();

        Assert.Equal(["user", "admin", "guest"], terminal.Session.FileSystem.LoginUsers.Select(u => u.Name));
    }
}
