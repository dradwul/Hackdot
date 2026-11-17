namespace Hackdot.Services;

public enum AppId
{
    Mission,
    Terminal,
    Files,
    Guide,
}

/// <param name="Accent">CSS color (or variable) used for the app's icon.</param>
public sealed record AppInfo(AppId Id, string Accent)
{
    /// <summary>Resource key for the app title, e.g. <c>App.Terminal.Title</c>.</summary>
    public string TitleKey => $"App.{Id}.Title";

    /// <summary>Resource key for the short app description.</summary>
    public string DescriptionKey => $"App.{Id}.Description";
}

/// <summary>
/// Window manager for the HackOS shell. Tracks which apps are open and which one is visible.
/// At most one window is visible at a time (mobile-first); minimized apps stay in the dock
/// and keep their state.
/// </summary>
public sealed class ShellService
{
    private readonly List<AppId> _openApps = [];

    public IReadOnlyList<AppInfo> Apps { get; } =
    [
        new(AppId.Mission, "var(--app-mission)"),
        new(AppId.Terminal, "var(--app-terminal)"),
        new(AppId.Files, "var(--app-files)"),
        new(AppId.Guide, "var(--app-guide)"),
    ];

    public IReadOnlyList<AppId> OpenApps => _openApps;

    public AppId? ActiveApp { get; private set; }

    public event Action? Changed;

    public AppInfo Get(AppId id) => Apps.First(app => app.Id == id);

    public void Open(AppId id)
    {
        if (!_openApps.Contains(id))
        {
            _openApps.Add(id);
        }

        ActiveApp = id;
        Changed?.Invoke();
    }

    public void Minimize(AppId id)
    {
        if (ActiveApp != id)
        {
            return;
        }

        ActiveApp = null;
        Changed?.Invoke();
    }

    public void Close(AppId id)
    {
        if (!_openApps.Remove(id))
        {
            return;
        }

        if (ActiveApp == id)
        {
            ActiveApp = null;
        }

        Changed?.Invoke();
    }

    /// <summary>Dock button: show the app, or minimize it if it is already visible.</summary>
    public void ToggleFromDock(AppId id)
    {
        if (ActiveApp == id)
        {
            Minimize(id);
        }
        else
        {
            Open(id);
        }
    }

    public void ShowDesktop()
    {
        ActiveApp = null;
        Changed?.Invoke();
    }

    /// <summary>Closes every window (e.g. when logging out).</summary>
    public void CloseAll()
    {
        _openApps.Clear();
        ActiveApp = null;
        Changed?.Invoke();
    }
}
