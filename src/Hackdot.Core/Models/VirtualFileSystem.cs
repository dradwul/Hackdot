using Hackdot.Core.Services;

namespace Hackdot.Core.Models;

/// <summary>
/// An in-memory, Unix-like file system with users, owners and permission bits.
/// The terminal and the file explorer both work against the same instance.
/// </summary>
public sealed class VirtualFileSystem
{
    /// <summary>Timestamp used when nothing else is specified, so output is deterministic.</summary>
    public static readonly DateTime DefaultTimestamp = new(2026, 3, 14, 9, 12, 0, DateTimeKind.Unspecified);

    private readonly List<VirtualUser> _users = [];

    public VirtualFileSystem(string hostname = "dotos")
    {
        Hostname = hostname;
        Root = new VirtualDirectory(VirtualPath.Root, VirtualUser.RootName, FilePermissions.DefaultDirectory, DefaultTimestamp);
        AddUser(new VirtualUser(VirtualUser.RootName, "/root"));
    }

    public string Hostname { get; set; }

    public VirtualDirectory Root { get; }

    public IReadOnlyList<VirtualUser> Users => _users;

    /// <summary>The user a new session starts as. Falls back to the first non-root user.</summary>
    public string? DefaultUser { get; set; }

    public VirtualUser? FindUser(string name) => _users.Find(user => user.Name == name);

    /// <summary>The accounts shown on the login screen: the default user first, locked accounts left out.</summary>
    public IReadOnlyList<VirtualUser> LoginUsers =>
        _users.Where(user => user.CanLogIn)
            .OrderBy(user => user.Name == DefaultUser ? 0 : 1)
            .ThenBy(user => user.IsRoot ? 1 : 0)
            .ThenBy(user => user.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>Adds (or replaces) a user and makes sure their home directory exists.</summary>
    public VirtualUser AddUser(VirtualUser user)
    {
        _users.RemoveAll(existing => existing.Name == user.Name);
        _users.Add(user);

        var home = CreateDirectory(user.Home);
        home.Owner = user.Name;
        home.Mode = user.IsRoot ? FilePermissions.PrivateDirectory : FilePermissions.DefaultDirectory;

        return user;
    }

    public VirtualNode? GetNode(string path)
    {
        VirtualNode current = Root;

        foreach (var segment in VirtualPath.Segments(path))
        {
            if (current is not VirtualDirectory directory || directory.GetChild(segment) is not { } child)
            {
                return null;
            }

            current = child;
        }

        return current;
    }

    public VirtualDirectory? GetDirectory(string path) => GetNode(path) as VirtualDirectory;

    public VirtualFile? GetFile(string path) => GetNode(path) as VirtualFile;

    /// <summary>
    /// Creates a directory and any missing parents (like <c>mkdir -p</c>). Missing parents get default
    /// permissions and are owned by the user whose home they are in, otherwise by root.
    /// If the directory already exists, only the given owner/mode are applied.
    /// </summary>
    public VirtualDirectory CreateDirectory(string path, string? owner = null, int? mode = null)
    {
        var current = Root;

        foreach (var segment in VirtualPath.Segments(path))
        {
            current = current.GetChild(segment) switch
            {
                VirtualDirectory existing => existing,
                VirtualFile file => throw new InvalidOperationException($"Cannot create directory '{path}': '{file.Path}' is a file."),
                _ => current.SetChild(new VirtualDirectory(
                    segment,
                    DefaultOwnerFor(VirtualPath.Resolve(current.Path, segment)),
                    FilePermissions.DefaultDirectory,
                    DefaultTimestamp)),
            };
        }

        if (owner is not null)
        {
            current.Owner = owner;
        }

        if (mode is not null)
        {
            current.Mode = mode.Value;
        }

        return current;
    }

    /// <summary>Creates or replaces a file, creating parent directories as needed.</summary>
    public VirtualFile WriteFile(string path, string content, string? owner = null, int? mode = null, DateTime? modified = null)
    {
        var normalized = VirtualPath.Normalize(path);

        if (normalized == VirtualPath.Root)
        {
            throw new InvalidOperationException("Cannot write a file at the root path.");
        }

        var parent = CreateDirectory(VirtualPath.GetParent(normalized));
        var name = VirtualPath.GetName(normalized);

        if (parent.GetChild(name) is VirtualDirectory)
        {
            throw new InvalidOperationException($"Cannot write file '{normalized}': it is a directory.");
        }

        return parent.SetChild(new VirtualFile(
            name,
            content,
            owner ?? DefaultOwnerFor(normalized),
            mode ?? FilePermissions.DefaultFile,
            modified ?? DefaultTimestamp));
    }

    /// <summary>All nodes below <paramref name="directory"/>, depth-first in <c>ls</c> order.</summary>
    public static IEnumerable<VirtualNode> Descendants(VirtualDirectory directory)
    {
        foreach (var child in directory.SortedChildren())
        {
            yield return child;

            if (child is VirtualDirectory subdirectory)
            {
                foreach (var descendant in Descendants(subdirectory))
                {
                    yield return descendant;
                }
            }
        }
    }

    private string DefaultOwnerFor(string path)
    {
        var owner = _users
            .Where(user => path == user.Home || path.StartsWith(user.Home + VirtualPath.Separator, StringComparison.Ordinal))
            .MaxBy(user => user.Home.Length);

        return owner?.Name ?? VirtualUser.RootName;
    }
}
