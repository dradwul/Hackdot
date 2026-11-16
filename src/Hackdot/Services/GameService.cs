using Hackdot.Core.Models;
using Hackdot.Core.Models.Missions;
using Hackdot.Core.Services;
using Hackdot.Core.Services.Missions;
using Microsoft.JSInterop;

namespace Hackdot.Services;

/// <summary>
/// The running game: mission progress (saved in the browser) and the file system of the active mission,
/// shared by the terminal and the file explorer.
/// </summary>
public sealed class GameService
{
    private const string StorageKey = "hackdot.progress.v1";

    // Blazor WebAssembly can call JavaScript synchronously, which keeps loading and saving simple.
    private readonly IJSInProcessRuntime? _js;

    public GameService(IJSRuntime js)
    {
        _js = js as IJSInProcessRuntime;

        var snapshot = GameState.DeserializeSnapshot(ReadStorage());
        IsFirstLaunch = snapshot is null;
        State = new GameState(MissionCatalog.LoadEmbedded(), snapshot);
        (FileSystem, Session) = CreateWorld();
    }

    public GameState State { get; }

    public MissionDefinition ActiveMission => State.ActiveMission;

    /// <summary>No saved progress was found – a good moment to show the first mission.</summary>
    public bool IsFirstLaunch { get; }

    public VirtualFileSystem FileSystem { get; private set; }

    public ShellSession Session { get; private set; }

    /// <summary>Progress changed (hint unlocked, flag solved, …).</summary>
    public event Action? Changed;

    /// <summary>A mission was started and the file system was replaced.</summary>
    public event Action? MissionStarted;

    public void StartMission(MissionDefinition mission)
    {
        State.Activate(mission);
        (FileSystem, Session) = CreateWorld();
        Save();
        MissionStarted?.Invoke();
        Changed?.Invoke();
    }

    public FlagCheckResult SubmitFlag(string answer)
    {
        var result = State.SubmitFlag(answer);
        Save();
        Changed?.Invoke();
        return result;
    }

    public void UnlockNextHint()
    {
        if (State.UnlockNextHint())
        {
            Save();
            Changed?.Invoke();
        }
    }

    private (VirtualFileSystem, ShellSession) CreateWorld()
    {
        var fileSystem = State.CreateFileSystem();
        return (fileSystem, new ShellSession(fileSystem));
    }

    private string? ReadStorage()
    {
        try
        {
            return _js?.Invoke<string?>("localStorage.getItem", StorageKey);
        }
        catch (JSException)
        {
            // Storage can be blocked (private mode, strict settings). The game still works, it just won't remember.
            return null;
        }
    }

    private void Save()
    {
        try
        {
            _js?.InvokeVoid("localStorage.setItem", StorageKey, GameState.SerializeSnapshot(State.ToSnapshot()));
        }
        catch (JSException)
        {
        }
    }
}
