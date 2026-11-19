using Hackdot.Core.Models;
using Hackdot.Core.Models.Missions;
using Hackdot.Core.Services;
using Hackdot.Core.Services.Missions;
using Microsoft.JSInterop;

namespace Hackdot.Services;

/// <summary>
/// The running game: mission progress (saved in the browser), the active mission's file system and who is
/// logged in. The terminal and the file explorer each get their own session for the logged-in user – so
/// <c>su</c> in the terminal does not change what the file explorer may open, just like on a real computer.
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
        FileSystem = State.CreateFileSystem();
        CurrentUser = DefaultUser(FileSystem);

        // Players who started before achievements existed get the ones they have already earned – quietly.
        if (State.Achievements.CheckMissions(State, DateTime.Now).Count > 0)
        {
            Save();
        }
    }

    public GameState State { get; }

    public MissionDefinition ActiveMission => State.ActiveMission;

    /// <summary>No saved progress was found – a good moment to show the first mission.</summary>
    public bool IsFirstLaunch { get; }

    public VirtualFileSystem FileSystem { get; private set; }

    /// <summary>The user logged in on the desktop.</summary>
    public VirtualUser CurrentUser { get; private set; }

    /// <summary>The login screen is showing (after logging out).</summary>
    public bool IsLocked { get; private set; }

    /// <summary>Progress or login state changed.</summary>
    public event Action? Changed;

    /// <summary>
    /// A new mission was started or another user logged in: apps should start a fresh session
    /// (see <see cref="CreateSession"/>).
    /// </summary>
    public event Action? SessionChanged;

    /// <summary>The player earned achievements and/or a new rank (<c>null</c> if the rank stayed the same).</summary>
    public event Action<IReadOnlyList<Achievement>, Rank?>? Rewarded;

    /// <summary>A new session for the logged-in user, starting in their home folder.</summary>
    public ShellSession CreateSession() => new(FileSystem, CurrentUser.Name);

    public void StartMission(MissionDefinition mission)
    {
        State.Activate(mission);
        FileSystem = State.CreateFileSystem();
        CurrentUser = DefaultUser(FileSystem);
        IsLocked = false;
        Save();
        SessionChanged?.Invoke();
        Changed?.Invoke();
    }

    public void LogOut()
    {
        IsLocked = true;
        Changed?.Invoke();
    }

    /// <summary>Logs in as <paramref name="user"/>. Returns <c>false</c> if the password is wrong.</summary>
    public bool LogIn(VirtualUser user, string? password)
    {
        if (!user.Accepts(password))
        {
            return false;
        }

        CurrentUser = user;
        IsLocked = false;
        SessionChanged?.Invoke();
        Changed?.Invoke();
        return true;
    }

    public FlagCheckResult SubmitFlag(string answer)
    {
        var rankBefore = State.Rank;
        var result = State.SubmitFlag(answer);
        var earned = result == FlagCheckResult.Correct
            ? State.Achievements.CheckMissions(State, DateTime.Now, justSolved: true)
            : [];

        Save();
        Changed?.Invoke();
        Reward(earned, rankBefore);
        return result;
    }

    /// <summary>The terminal ran a command line: it may have earned an achievement.</summary>
    public void RecordCommand(CommandRun run) => Reward(State.Achievements.RecordCommand(run), State.Rank);

    /// <summary>Tab completed something in the terminal.</summary>
    public void RecordTabCompletion() => Reward(State.Achievements.RecordTabCompletion(), State.Rank);

    private void Reward(IReadOnlyList<Achievement> earned, Rank rankBefore)
    {
        var newRank = State.Rank != rankBefore ? State.Rank : null;

        if (earned.Count == 0 && newRank is null)
        {
            return;
        }

        Save();
        Rewarded?.Invoke(earned, newRank);
    }

    public void UnlockNextHint()
    {
        if (State.UnlockNextHint())
        {
            Save();
            Changed?.Invoke();
        }
    }

    private static VirtualUser DefaultUser(VirtualFileSystem fileSystem) => new ShellSession(fileSystem).User;

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
