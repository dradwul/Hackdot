using Hackdot.Core.Models;
using Hackdot.Core.Services;

namespace Hackdot.Core.Tests;

public class FileSystemLoaderTests
{
    [Fact]
    public void Parse_SupportsCommentsTrailingCommasLinesAndModes()
    {
        var definition = FileSystemLoader.Parse("""
            // Comments are allowed
            {
              "hostname": "target",
              "users": [ { "name": "alice", "password": "s3cret" }, ],
              "defaultUser": "alice",
              "directories": [ { "path": "/srv/private", "owner": "alice", "mode": "700" } ],
              "files": [
                { "path": "/srv/private/log.txt", "lines": [ "one", "two" ], "mode": "600", "owner": "alice" },
              ],
            }
            """);

        var fs = FileSystemLoader.Build(definition);

        Assert.Equal("target", fs.Hostname);
        Assert.Equal("alice", fs.DefaultUser);
        var alice = fs.FindUser("alice")!;
        Assert.Equal("/home/alice", alice.Home);
        Assert.Equal("s3cret", alice.Password);
        Assert.Equal("drwx------", fs.GetNode("/srv/private")!.PermissionString);
        var log = fs.GetFile("/srv/private/log.txt")!;
        Assert.Equal("one\ntwo", log.Content);
        Assert.Equal("-rw-------", log.PermissionString);
        Assert.Equal("alice", log.Owner);
    }

    [Fact]
    public void Build_LaterLayersOverrideEarlierOnes()
    {
        var baseLayer = FileSystemLoader.Parse("""{ "files": [ { "path": "/etc/motd", "content": "base" } ] }""");
        var missionLayer = FileSystemLoader.Parse("""
            { "hostname": "mission", "files": [ { "path": "/etc/motd", "content": "mission" }, { "path": "/flag.txt", "content": "FLAG{x}" } ] }
            """);

        var fs = FileSystemLoader.Build(baseLayer, missionLayer);

        Assert.Equal("mission", fs.Hostname);
        Assert.Equal("mission", fs.GetFile("/etc/motd")!.Content);
        Assert.Equal("FLAG{x}", fs.GetFile("/flag.txt")!.Content);
    }

    [Theory]
    [InlineData("""{ "files": [ { "path": "/a", "owner": "ghost" } ] }""", "unknown user 'ghost'")]
    [InlineData("""{ "defaultUser": "ghost" }""", "unknown user 'ghost'")]
    [InlineData("""{ "files": [ { "path": "/a", "mode": "999" } ] }""", "Invalid file mode")]
    [InlineData("""{ "files": [ { "path": "/a", "content": "x", "lines": [] } ] }""", "use either 'content', 'lines' or 'generate'")]
    [InlineData("""{ "filez": [] }""", "Invalid file system definition")]
    [InlineData("""{ "schemaVersion": 2 }""", "Unsupported file system schema version")]
    public void InvalidDefinitions_FailWithHelpfulMessage(string json, string expectedMessage)
    {
        var ex = Assert.Throws<InvalidDataException>(() => FileSystemLoader.Build(FileSystemLoader.Parse(json)));

        Assert.Contains(expectedMessage, ex.Message);
    }

    [Fact]
    public void CreateDefault_LoadsEmbeddedBaseSystem()
    {
        var fs = FileSystemLoader.CreateDefault();

        Assert.Equal("dotos", fs.Hostname);
        Assert.Equal("user", fs.DefaultUser);
        Assert.NotNull(fs.GetFile("/home/user/.bashrc"));
        Assert.NotNull(fs.GetFile("/home/user/Documents/welcome.txt"));
        Assert.Equal("drwx------", fs.GetNode("/root")!.PermissionString);
        Assert.Equal("root", fs.GetNode("/root/top_secret.txt")!.Owner);
        Assert.Equal("drwxrwxrwx", fs.GetNode("/tmp")!.PermissionString);
    }

    [Fact]
    public void ShellSession_StartsInDefaultUsersHomeAndChecksPermissions()
    {
        var fs = FileSystemLoader.CreateDefault();
        var session = new ShellSession(fs);
        var secret = fs.GetFile("/root/top_secret.txt")!;
        var rootHome = fs.GetDirectory("/root")!;

        Assert.Equal("user", session.User.Name);
        Assert.Equal("/home/user", session.CurrentDirectory);
        Assert.Equal("~", session.DisplayDirectory);
        Assert.False(session.CanRead(secret));
        Assert.False(session.CanEnter(rootHome));
        Assert.True(session.CanRead(fs.GetFile("/etc/passwd")!));

        session.SwitchUser(fs.FindUser(VirtualUser.RootName)!);

        Assert.Equal("/root", session.CurrentDirectory);
        Assert.True(session.CanRead(secret));
    }
}
