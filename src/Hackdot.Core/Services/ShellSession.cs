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

    /// <summary>Execute permission on a file means you may run it as a program.</summary>
    public bool CanExecute(VirtualFile file) => HasPermission(file, FilePermissions.Execute);

    /// <summary>Every folder on the way to the node can be passed through (it may still be unreadable itself).</summary>
    public bool CanReach(VirtualNode node) => User.IsRoot || node.Parent is not { } parent || HasPermission(parent, FilePermissions.Execute);

    /// <summary>
    /// A throw-away session for <paramref name="user"/> in the same folder – how <c>sudo</c> and set-user-ID
    /// programs run one command as someone else without changing who is logged in.
    /// </summary>
    public ShellSession RunningAs(VirtualUser user)
    {
        var session = new ShellSession(FileSystem, user.Name);

        if (FileSystem.GetDirectory(CurrentDirectory) is { } directory)
        {
            session.ChangeDirectory(directory);
        }

        return session;
    }

    public void ChangeDirectory(VirtualDirectory directory) => CurrentDirectory = directory.Path;

    /// <summary>Switches to another user (e.g. after logging in) and moves to their home directory.</summary>
    public void SwitchUser(VirtualUser user)
    {
        _previousUsers.Clear();
        User = user;
        CurrentDirectory = StartDirectoryFor(user);
    }

    /// <summary>
    /// Becomes another user like <c>su</c> does, remembering who you were so <c>exit</c> can go back.
    /// Plain <c>su</c> stays in the current folder; <c>su -</c> moves to the new user's home.
    /// </summary>
    public void PushUser(VirtualUser user, bool goHome)
    {
        _previousUsers.Push((User, CurrentDirectory));
        User = user;

        if (goHome)
        {
            CurrentDirectory = StartDirectoryFor(user);
        }
    }

    /// <summary>Goes back to the user before the last <see cref="PushUser"/>. Returns <c>false</c> if there is none.</summary>
    public bool PopUser()
    {
        if (!_previousUsers.TryPop(out var previous))
        {
            return false;
        }

        User = previous.User;
        CurrentDirectory = previous.Directory;
        return true;
    }

    /// <summary>The user you would return to with <c>exit</c>, if any.</summary>
    public VirtualUser? PreviousUser => _previousUsers.TryPeek(out var previous) ? previous.User : null;

    private readonly Stack<(VirtualUser User, string Directory)> _previousUsers = new();

    private bool HasPermission(VirtualNode node, int permission)
    {
        if (User.IsRoot)
        {
            return true;
        }

        // Like Linux: to get to anything, every folder on the way must let you pass through (x).
        for (var directory = node.Parent; directory is not null; directory = directory.Parent)
        {
            if (!HasBit(directory, FilePermissions.Execute))
            {
                return false;
            }
        }

        return HasBit(node, permission);
    }

    // Simplified model: owner bits for the owner, "other" bits for everyone else.
    private bool HasBit(VirtualNode node, int permission)
    {
        var shift = node.Owner == User.Name ? 6 : 0;
        return ((node.Mode >> shift) & permission) != 0;
    }

    private string StartDirectoryFor(VirtualUser user) =>
        FileSystem.GetDirectory(user.Home) is not null ? VirtualPath.Normalize(user.Home) : VirtualPath.Root;
}
