using Hackdot.Core.Models;
using Hackdot.Core.Models.Guide;
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
        Cards = TechniqueCatalog.LoadEmbedded();
        FileSystem = State.CreateFileSystem();
        CurrentUser = DefaultUser(FileSystem);

        // Players who started before achievements existed get the ones they have already earned – quietly.
        if (State.Achievements.CheckMissions(State, DateTime.Now).Count > 0)
        {
            Save();
        }
    }

    public GameState State { get; }

    /// <summary>Every technique card; <see cref="IsCollected"/> tells which ones the player has.</summary>
    public IReadOnlyList<TechniqueCard> Cards { get; }

    public bool IsCollected(TechniqueCard card) => TechniqueCatalog.IsCollected(State, card);

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

    /// <summary>The player earned something: achievements, a new rank (or <c>null</c>) and technique cards.</summary>
    public event Action<Rewards>? Rewarded;

    /// <summary>New mails arrived (after solving a mission: the reply and the next job).</summary>
    public event Action<IReadOnlyList<MailMessage>>? MailArrived;

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
        var cardsBefore = Cards.Where(IsCollected).ToList();
        var mailBefore = State.Inbox.Select(message => message.Id).ToHashSet();
        var result = State.SubmitFlag(answer);
        var earned = result == FlagCheckResult.Correct
            ? State.Achievements.CheckMissions(State, DateTime.Now, justSolved: true)
            : [];

        Save();
        Changed?.Invoke();
        Reward(earned, rankBefore, [.. Cards.Where(IsCollected).Except(cardsBefore)]);

        if (State.Inbox.Where(message => !mailBefore.Contains(message.Id)).ToList() is { Count: > 0 } arrived)
        {
            MailArrived?.Invoke(arrived);
        }

        return result;
    }

    /// <summary>Marks a mail as read (when the player opens it).</summary>
    public void MarkRead(MailMessage message)
    {
        if (State.MarkRead(message))
        {
            Save();
            Changed?.Invoke();
        }
    }

    /// <summary>The terminal ran a command line: it may have earned an achievement.</summary>
    public void RecordCommand(CommandRun run) => Reward(State.Achievements.RecordCommand(run), State.Rank);

    /// <summary>Tab completed something in the terminal.</summary>
    public void RecordTabCompletion() => Reward(State.Achievements.RecordTabCompletion(), State.Rank);

    private void Reward(IReadOnlyList<Achievement> earned, Rank rankBefore, IReadOnlyList<TechniqueCard>? cards = null)
    {
        var newRank = State.Rank != rankBefore ? State.Rank : null;
        cards ??= [];

        if (earned.Count == 0 && newRank is null && cards.Count == 0)
        {
            return;
        }

        Save();
        Rewarded?.Invoke(new Rewards(earned, newRank, cards));
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

/// <summary>What the player just earned.</summary>
public sealed record Rewards(IReadOnlyList<Achievement> Achievements, Rank? NewRank, IReadOnlyList<TechniqueCard> Cards);
