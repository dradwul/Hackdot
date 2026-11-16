using Hackdot.Core.Services;

namespace Hackdot.Core.Tests;

public class VirtualPathTests
{
    [Theory]
    [InlineData("/home/user", "Documents", "/home/user/Documents")]
    [InlineData("/home/user", "..", "/home")]
    [InlineData("/home/user", "../..", "/")]
    [InlineData("/home/user", "../../../..", "/")]
    [InlineData("/home/user", ".", "/home/user")]
    [InlineData("/home/user", "./Documents/../Downloads", "/home/user/Downloads")]
    [InlineData("/home/user", "/var/www", "/var/www")]
    [InlineData("/var/log", "~", "/home/user")]
    [InlineData("/var/log", "~/Documents", "/home/user/Documents")]
    [InlineData("/var/log", "", "/var/log")]
    [InlineData("/var/log", "   ", "/var/log")]
    [InlineData("/", "var//log/", "/var/log")]
    public void Resolve_HandlesRelativeAbsoluteAndHomePaths(string cwd, string input, string expected)
    {
        Assert.Equal(expected, VirtualPath.Resolve(cwd, input));
    }

    [Theory]
    [InlineData("/var/www/.secret_flag", "/var/www")]
    [InlineData("/var", "/")]
    [InlineData("/", "/")]
    public void GetParent_ReturnsParentDirectory(string path, string expected)
    {
        Assert.Equal(expected, VirtualPath.GetParent(path));
    }

    [Theory]
    [InlineData("/var/www/.secret_flag", ".secret_flag")]
    [InlineData("/var", "var")]
    [InlineData("/", "/")]
    public void GetName_ReturnsLastSegment(string path, string expected)
    {
        Assert.Equal(expected, VirtualPath.GetName(path));
    }

    [Theory]
    [InlineData(".hidden_notes.txt", true)]
    [InlineData(".secret_flag", true)]
    [InlineData("notes.txt", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    public void IsHidden_TreatsDotFilesAsHidden(string name, bool expected)
    {
        Assert.Equal(expected, VirtualPath.IsHidden(name));
    }

    [Theory]
    [InlineData("/home/user", "~")]
    [InlineData("/home/user/Documents", "~/Documents")]
    [InlineData("/home/username", "/home/username")]
    [InlineData("/var/log", "/var/log")]
    public void ToDisplayPath_ShortensHomeDirectory(string path, string expected)
    {
        Assert.Equal(expected, VirtualPath.ToDisplayPath(path));
    }

    [Fact]
    public void Segments_SplitsNormalizedPath()
    {
        Assert.Equal(["var", "log"], VirtualPath.Segments("/var/./log/"));
        Assert.Empty(VirtualPath.Segments("/"));
    }
}
