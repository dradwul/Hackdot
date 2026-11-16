using Hackdot.Core.Models;

namespace Hackdot.Core.Services;

/// <summary>
/// A logged-in user's session: who you are and where you are. Permission checks live here,
/// so a future login screen or <c>su</c> only has to switch the user.
/// </summary>
public sealed class ShellSession
{
    public ShellSession(VirtualFileSystem fileSystem, string? userName = null)
    {
        FileSystem = fileSystem;

        var user = (userName ?? fileSystem.DefaultUser) is { } name
            ? fileSystem.FindUser(name) ?? throw new ArgumentException($"Unknown user '{name}'.", nameof(userName))
            : fileSystem.Users.FirstOrDefault(u => !u.IsRoot) ?? fileSystem.Users[0];

        User = user;
        CurrentDirectory = StartDirectoryFor(user);
    }

    public VirtualFileSystem FileSystem { get; }

    public VirtualUser User { get; private set; }

    /// <summary>Normalized absolute path of the working directory.</summary>
    public string CurrentDirectory { get; private set; }

    /// <summary>Resolves a path typed by the player (relative, absolute or starting with <c>~</c>).</summary>
    public string Resolve(string? path) => VirtualPath.Resolve(CurrentDirectory, path, User.Home);

    public VirtualNode? GetNode(string? path) => FileSystem.GetNode(Resolve(path));

    /// <summary>The working directory as shown in the prompt, e.g. <c>~/Documents</c>.</summary>
    public string DisplayDirectory => VirtualPath.ToDisplayPath(CurrentDirectory, User.Home);

    public bool CanRead(VirtualNode node) => HasPermission(node, FilePermissions.Read);

    public bool CanWrite(VirtualNode node) => HasPermission(node, FilePermissions.Write);

    /// <summary>Execute permission on a directory means you may <c>cd</c> into it.</summary>
    public bool CanEnter(VirtualDirectory directory) => HasPermission(directory, FilePermissions.Execute);

    public void ChangeDirectory(VirtualDirectory directory) => CurrentDirectory = directory.Path;

    /// <summary>Switches to another user (login, <c>su</c>) and moves to their home directory.</summary>
    public void SwitchUser(VirtualUser user)
    {
        User = user;
        CurrentDirectory = StartDirectoryFor(user);
    }

    private bool HasPermission(VirtualNode node, int permission)
    {
        if (User.IsRoot)
        {
            return true;
        }

        // Simplified model: owner bits for the owner, "other" bits for everyone else.
        var shift = node.Owner == User.Name ? 6 : 0;
        return ((node.Mode >> shift) & permission) != 0;
    }

    private string StartDirectoryFor(VirtualUser user) =>
        FileSystem.GetDirectory(user.Home) is not null ? VirtualPath.Normalize(user.Home) : VirtualPath.Root;
}
