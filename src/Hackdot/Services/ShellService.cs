namespace Hackdot.Services;

public enum AppId
{
    Mission,
    Terminal,
    Files,
    Guide,
}

/// <param name="Accent">CSS-färg (eller variabel) som används för appens ikon.</param>
public sealed record AppInfo(AppId Id, string Title, string Description, string Accent);

/// <summary>
/// Fönsterhanterare för HackOS-skalet. Håller reda på vilka appar som är öppna
/// och vilken som visas. Max ett synligt fönster åt gången (mobile-first);
/// minimerade appar ligger kvar i dockan och behåller sitt tillstånd.
/// </summary>
public sealed class ShellService
{
    private readonly List<AppId> _openApps = [];

    public IReadOnlyList<AppInfo> Apps { get; } =
    [
        new(AppId.Mission, "Uppdrag", "Bakgrund, flaggor och ledtrådar", "var(--app-mission)"),
        new(AppId.Terminal, "Terminal", "Kommandon som ls, cd, cat och grep", "var(--app-terminal)"),
        new(AppId.Files, "Filer", "Utforska filsystemet visuellt", "var(--app-files)"),
        new(AppId.Guide, "Guide", "Anteckningar och fusklapp", "var(--app-guide)"),
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

    /// <summary>Dockknapp: visa appen, eller minimera den om den redan visas.</summary>
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
}
