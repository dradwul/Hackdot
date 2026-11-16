using Hackdot.Core.Models;

namespace Hackdot.Core.Tests;

public class VirtualFileSystemTests
{
    [Fact]
    public void NewFileSystem_HasRootUserWithPrivateHome()
    {
        var fs = new VirtualFileSystem();

        var root = Assert.Single(fs.Users);
        Assert.True(root.IsRoot);
        var home = Assert.IsType<VirtualDirectory>(fs.GetNode("/root"));
        Assert.Equal("drwx------", home.PermissionString);
        Assert.Equal("/", fs.Root.Path);
    }

    [Fact]
    public void WriteFile_CreatesParentsOwnedByHomeUser()
    {
        var fs = new VirtualFileSystem();
        fs.AddUser(new VirtualUser("user", "/home/user"));

        var file = fs.WriteFile("/home/user/Documents/notes.txt", "hello");

        Assert.Equal("/home/user/Documents/notes.txt", file.Path);
        Assert.Equal("user", file.Owner);
        Assert.Equal("user", fs.GetNode("/home/user/Documents")!.Owner);
        Assert.Equal("root", fs.GetNode("/home")!.Owner);
        Assert.Equal("-rw-r--r--", file.PermissionString);
        Assert.Equal(5, file.Size);
    }

    [Fact]
    public void WriteFile_ReplacesExistingFile()
    {
        var fs = new VirtualFileSystem();
        fs.WriteFile("/tmp/a.txt", "old");

        fs.WriteFile("/tmp/a.txt", "new");

        Assert.Equal("new", fs.GetFile("/tmp/a.txt")!.Content);
        Assert.Single(fs.GetDirectory("/tmp")!.Children);
    }

    [Fact]
    public void WriteFile_OverDirectory_Throws()
    {
        var fs = new VirtualFileSystem();
        fs.CreateDirectory("/var/www");

        Assert.Throws<InvalidOperationException>(() => fs.WriteFile("/var/www", "x"));
    }

    [Fact]
    public void CreateDirectory_ThroughFile_Throws()
    {
        var fs = new VirtualFileSystem();
        fs.WriteFile("/etc/hostname", "hackos");

        Assert.Throws<InvalidOperationException>(() => fs.CreateDirectory("/etc/hostname/sub"));
    }

    [Theory]
    [InlineData("/var/www/.secret_flag", true)]
    [InlineData("/var/www/index.html", true)]
    [InlineData("/var/www/missing", false)]
    [InlineData("/var/www/index.html/child", false)]
    public void GetNode_FindsExistingNodesOnly(string path, bool exists)
    {
        var fs = new VirtualFileSystem();
        fs.WriteFile("/var/www/.secret_flag", "FLAG{x}");
        fs.WriteFile("/var/www/index.html", "<html>");

        Assert.Equal(exists, fs.GetNode(path) is not null);
    }

    [Fact]
    public void SortedChildren_IgnoresLeadingDotAndCase()
    {
        var fs = new VirtualFileSystem();
        fs.WriteFile("/d/zeta", "");
        fs.WriteFile("/d/.bashrc", "");
        fs.WriteFile("/d/Alpha", "");
        fs.CreateDirectory("/d/cache");

        var names = fs.GetDirectory("/d")!.SortedChildren().Select(n => n.Name);

        Assert.Equal(["Alpha", ".bashrc", "cache", "zeta"], names);
    }

    [Fact]
    public void Descendants_WalksDepthFirst()
    {
        var fs = new VirtualFileSystem();
        fs.WriteFile("/a/b/c.txt", "");
        fs.WriteFile("/a/d.txt", "");

        var paths = VirtualFileSystem.Descendants(fs.GetDirectory("/a")!).Select(n => n.Path);

        Assert.Equal(["/a/b", "/a/b/c.txt", "/a/d.txt"], paths);
    }

    [Theory]
    [InlineData("755", "rwxr-xr-x")]
    [InlineData("644", "rw-r--r--")]
    [InlineData("0700", "rwx------")]
    [InlineData("000", "---------")]
    public void FilePermissions_ParseAndFormat(string octal, string symbolic)
    {
        var mode = FilePermissions.Parse(octal);

        Assert.Equal(symbolic, FilePermissions.ToSymbolic(mode));
        Assert.Equal(octal.TrimStart('0').PadLeft(3, '0'), FilePermissions.ToOctal(mode));
    }

    [Theory]
    [InlineData("")]
    [InlineData("78")]
    [InlineData("888")]
    [InlineData("rwx")]
    public void FilePermissions_InvalidMode_Throws(string octal)
    {
        Assert.Throws<FormatException>(() => FilePermissions.Parse(octal));
    }
}
