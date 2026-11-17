using Hackdot.Core.Models;
using Hackdot.Core.Services;

namespace Hackdot.Services;

/// <summary>
/// State of the Files app: the explorer (same file system and user as the terminal) and the open file.
/// Lives as long as the app so minimizing the window keeps your place; a new mission starts fresh.
/// </summary>
public sealed class FilesService
{
    private readonly GameService _game;

    public FilesService(GameService game)
    {
        _game = game;
        Explorer = new FileExplorer(game.Session);
        _game.MissionStarted += OnMissionStarted;
    }

    public FileExplorer Explorer { get; private set; }

    /// <summary>The file shown in the viewer, or <c>null</c> when browsing a folder.</summary>
    public VirtualFile? OpenedFile { get; private set; }

    /// <summary>Why the last action failed, and for which item – shown as a message in the app.</summary>
    public (ExplorerResult Result, VirtualNode? Node, string Path)? LastError { get; private set; }

    public event Action? Changed;

    public void Navigate(string path) => Run(path, () => Explorer.Navigate(path));

    public void GoUp() => Run("..", Explorer.GoUp);

    public void GoHome() => Run("~", Explorer.GoHome);

    public void Open(ExplorerEntry entry)
    {
        if (entry.IsDirectory)
        {
            Navigate(entry.Path);
            return;
        }

        var result = Explorer.OpenFile(entry.Path, out var file);
        OpenedFile = file;
        LastError = result == ExplorerResult.Ok ? null : (result, entry.Node, entry.Path);
        Changed?.Invoke();
    }

    public void CloseFile()
    {
        OpenedFile = null;
        Changed?.Invoke();
    }

    public void SetShowHidden(bool showHidden)
    {
        Explorer.ShowHidden = showHidden;
        Changed?.Invoke();
    }

    public void DismissError()
    {
        LastError = null;
        Changed?.Invoke();
    }

    private void Run(string path, Func<ExplorerResult> action)
    {
        // Resolve the target before navigating: relative paths are relative to the folder we start from.
        var target = FindTarget(path);
        var result = action();
        OpenedFile = null;
        LastError = result == ExplorerResult.Ok ? null : (result, target, path);
        Changed?.Invoke();
    }

    private VirtualNode? FindTarget(string path) =>
        Explorer.Session.FileSystem.GetNode(VirtualPath.Resolve(Explorer.CurrentPath, path, Explorer.Session.User.Home));

    private void OnMissionStarted()
    {
        Explorer = new FileExplorer(_game.Session) { ShowHidden = Explorer.ShowHidden };
        OpenedFile = null;
        LastError = null;
        Changed?.Invoke();
    }
}
