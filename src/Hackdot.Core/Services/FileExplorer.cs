using Hackdot.Core.Models;

namespace Hackdot.Core.Services;

public enum ExplorerResult
{
    Ok,
    NotFound,
    NotADirectory,
    IsADirectory,
    PermissionDenied,
}

/// <summary>A file or folder as shown in the file explorer.</summary>
public sealed class ExplorerEntry(VirtualNode node, bool canOpen, int? childCount)
{
    public VirtualNode Node { get; } = node;

    public string Name => Node.Name;

    public string Path => Node.Path;

    public bool IsDirectory => Node.IsDirectory;

    public bool IsHidden => Node.IsHidden;

    /// <summary>Whether the current user may open it (read a file, or enter and list a folder).</summary>
    public bool CanOpen { get; } = canOpen;

    /// <summary>Number of visible items in a folder, or <c>null</c> for files and folders you cannot open.</summary>
    public int? ChildCount { get; } = childCount;
}

/// <summary>
/// The graphical file explorer. It browses the same file system as the terminal, with the same user and
/// the same permissions, but keeps its own current folder (like two windows on a real computer).
/// </summary>
public sealed class FileExplorer
{
    public FileExplorer(ShellSession session)
    {
        Session = session;

        var home = session.FileSystem.GetDirectory(session.User.Home);
        CurrentPath = home is not null && CanOpen(home) ? home.Path : VirtualPath.Root;
    }

    public ShellSession Session { get; }

    /// <summary>Normalized absolute path of the folder being shown.</summary>
    public string CurrentPath { get; private set; }

    /// <summary>Show files and folders whose names start with a dot.</summary>
    public bool ShowHidden { get; set; }

    public string DisplayPath => VirtualPath.ToDisplayPath(CurrentPath, Session.User.Home);

    public bool CanGoUp => CurrentPath != VirtualPath.Root;

    public VirtualDirectory CurrentDirectory =>
        Session.FileSystem.GetDirectory(CurrentPath) ?? Session.FileSystem.Root;

    /// <summary>Folders first, then files – each sorted like <c>ls</c>.</summary>
    public IReadOnlyList<ExplorerEntry> Entries =>
        CurrentDirectory.SortedChildren()
            .Where(node => ShowHidden || !node.IsHidden)
            .OrderBy(node => node.IsDirectory ? 0 : 1)
            .Select(ToEntry)
            .ToList();

    /// <summary>True when the folder looks empty only because its contents are hidden.</summary>
    public bool HasOnlyHiddenItems =>
        !ShowHidden && CurrentDirectory.Children.Count > 0 && CurrentDirectory.Children.All(node => node.IsHidden);

    /// <summary>The path from the root to the current folder, e.g. <c>/</c>, <c>var</c>, <c>www</c>.</summary>
    public IReadOnlyList<(string Name, string Path)> Breadcrumbs
    {
        get
        {
            var crumbs = new List<(string, string)> { (VirtualPath.Root, VirtualPath.Root) };
            var path = "";

            foreach (var segment in VirtualPath.Segments(CurrentPath))
            {
                path += VirtualPath.Separator + segment;
                crumbs.Add((segment, path));
            }

            return crumbs;
        }
    }

    /// <summary>The terminal command that shows the same thing as the explorer right now.</summary>
    public string EquivalentListCommand => (ShowHidden ? "ls -a " : "ls ") + ShellPath(CurrentPath);

    /// <summary>Opens a folder (relative to the current one, absolute, or starting with <c>~</c>).</summary>
    public ExplorerResult Navigate(string path)
    {
        switch (Session.FileSystem.GetNode(Resolve(path)))
        {
            case null:
                return ExplorerResult.NotFound;
            case VirtualFile:
                return ExplorerResult.NotADirectory;
            case VirtualDirectory directory when !CanOpen(directory):
                return ExplorerResult.PermissionDenied;
            case VirtualDirectory directory:
                CurrentPath = directory.Path;
                return ExplorerResult.Ok;
            default:
                return ExplorerResult.NotFound;
        }
    }

    public ExplorerResult GoUp() => Navigate("..");

    public ExplorerResult GoHome() => Navigate("~");

    public ExplorerResult OpenFile(string path, out VirtualFile? file)
    {
        file = null;

        switch (Session.FileSystem.GetNode(Resolve(path)))
        {
            case null:
                return ExplorerResult.NotFound;
            case VirtualDirectory:
                return ExplorerResult.IsADirectory;
            case VirtualFile found when !Session.CanRead(found):
                return ExplorerResult.PermissionDenied;
            case VirtualFile found:
                file = found;
                return ExplorerResult.Ok;
            default:
                return ExplorerResult.NotFound;
        }
    }

    /// <summary>The terminal command that shows the contents of a file.</summary>
    public string EquivalentReadCommand(VirtualFile file) => "cat " + ShellPath(file.Path);

    private string Resolve(string path) => VirtualPath.Resolve(CurrentPath, path, Session.User.Home);

    private bool CanOpen(VirtualDirectory directory) => Session.CanEnter(directory) && Session.CanRead(directory);

    private ExplorerEntry ToEntry(VirtualNode node) => node switch
    {
        VirtualDirectory directory when CanOpen(directory) =>
            new ExplorerEntry(node, canOpen: true, directory.Children.Count(child => ShowHidden || !child.IsHidden)),
        VirtualDirectory => new ExplorerEntry(node, canOpen: false, childCount: null),
        _ => new ExplorerEntry(node, Session.CanRead(node), childCount: null),
    };

    /// <summary>
    /// A path as you would type it: <c>~/Documents</c> when possible. Paths with spaces or special characters
    /// are quoted – and then written in full, because a real shell does not expand <c>~</c> inside quotes.
    /// </summary>
    private string ShellPath(string absolutePath)
    {
        var needsQuotes = absolutePath.Any(c => char.IsWhiteSpace(c) || c is '\'' or '"' or '|' or '\\');

        return needsQuotes
            ? $"'{absolutePath.Replace("'", "'\\''")}'"
            : VirtualPath.ToDisplayPath(absolutePath, Session.User.Home);
    }
}
