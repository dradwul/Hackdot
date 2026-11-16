using System.Text.Json;
using Hackdot.Core.Models;
using Hackdot.Core.Models.Missions;

namespace Hackdot.Core.Services.Missions;

public enum FlagCheckResult
{
    /// <summary>Nothing was entered.</summary>
    Empty,

    /// <summary>The answer does not look like <c>FLAG{...}</c> at all.</summary>
    WrongFormat,

    Incorrect,

    Correct,

    AlreadySolved,
}

public sealed class MissionProgress
{
    public int HintsUnlocked { get; set; }

    public bool Completed { get; set; }

    public int PointsEarned { get; set; }

    public int FailedAttempts { get; set; }
}

/// <summary>
/// The player's progress: which mission is active, which are solved, hints used and score.
/// Plain C# so it can be tested and saved; the UI only calls into it.
/// </summary>
public sealed class GameState
{
    /// <summary>
    /// What each hint costs, in percent of the mission's points. Hints get more specific – and more expensive.
    /// Using all three still leaves half the points: we want players to ask for help rather than give up.
    /// </summary>
    public static readonly IReadOnlyList<int> HintCostPercent = [10, 15, 25];

    private readonly Dictionary<string, MissionProgress> _progress = new(StringComparer.Ordinal);

    public GameState(MissionCatalog catalog, GameProgressSnapshot? snapshot = null)
    {
        if (catalog.Missions.Count == 0)
        {
            throw new ArgumentException("The catalog has no missions.", nameof(catalog));
        }

        Catalog = catalog;

        foreach (var (id, saved) in snapshot?.Missions ?? [])
        {
            if (catalog.Find(id) is { } mission)
            {
                _progress[id] = new MissionProgress
                {
                    Completed = saved.Completed,
                    HintsUnlocked = Math.Clamp(saved.HintsUnlocked, 0, mission.Hints.Count),
                    PointsEarned = Math.Clamp(saved.PointsEarned, 0, mission.Points),
                    FailedAttempts = Math.Max(0, saved.FailedAttempts),
                };
            }
        }

        ActiveMission = snapshot?.ActiveMissionId is { } activeId && catalog.Find(activeId) is { } active && IsUnlocked(active)
            ? active
            : catalog.Missions.FirstOrDefault(mission => !Progress(mission).Completed && IsUnlocked(mission)) ?? catalog.Missions[0];
    }

    public MissionCatalog Catalog { get; }

    public MissionDefinition ActiveMission { get; private set; }

    public MissionProgress ActiveProgress => Progress(ActiveMission);

    public int TotalScore => _progress.Values.Sum(progress => progress.PointsEarned);

    public int CompletedCount => _progress.Values.Count(progress => progress.Completed);

    /// <summary>The mission after the active one, if any.</summary>
    public MissionDefinition? NextMission
    {
        get
        {
            var index = Catalog.IndexOf(ActiveMission);
            return index + 1 < Catalog.Missions.Count ? Catalog.Missions[index + 1] : null;
        }
    }

    public MissionProgress Progress(MissionDefinition mission)
    {
        if (!_progress.TryGetValue(mission.Id, out var progress))
        {
            progress = new MissionProgress();
            _progress[mission.Id] = progress;
        }

        return progress;
    }

    /// <summary>Missions unlock in order: the first is always open, the rest once the previous one is solved.</summary>
    public bool IsUnlocked(MissionDefinition mission)
    {
        var index = Catalog.IndexOf(mission);
        return index <= 0 || Progress(mission).Completed || Progress(Catalog.Missions[index - 1]).Completed;
    }

    public void Activate(MissionDefinition mission)
    {
        if (!IsUnlocked(mission))
        {
            throw new InvalidOperationException($"Mission '{mission.Id}' is still locked.");
        }

        ActiveMission = mission;
    }

    /// <summary>A fresh file system for the active mission.</summary>
    public VirtualFileSystem CreateFileSystem() => Catalog.CreateFileSystem(ActiveMission);

    /// <summary>What the next hint costs, or <c>null</c> if there are no more hints.</summary>
    public int? NextHintCost()
    {
        var progress = ActiveProgress;

        if (progress.HintsUnlocked >= ActiveMission.Hints.Count)
        {
            return null;
        }

        // Hints are free once the mission is solved – then they are just for learning.
        return progress.Completed ? 0 : HintCost(ActiveMission, progress.HintsUnlocked);
    }

    /// <summary>Unlocks the next hint for the active mission. Returns <c>false</c> if all hints are already shown.</summary>
    public bool UnlockNextHint()
    {
        var progress = ActiveProgress;

        if (progress.HintsUnlocked >= ActiveMission.Hints.Count)
        {
            return false;
        }

        progress.HintsUnlocked++;
        return true;
    }

    /// <summary>The points the active mission is worth right now, after hint costs.</summary>
    public int AvailablePoints(MissionDefinition mission)
    {
        var progress = Progress(mission);

        if (progress.Completed)
        {
            return progress.PointsEarned;
        }

        var cost = Enumerable.Range(0, progress.HintsUnlocked).Sum(level => HintCost(mission, level));
        return Math.Max(0, mission.Points - cost);
    }

    public FlagCheckResult SubmitFlag(string? answer)
    {
        var progress = ActiveProgress;
        var attempt = answer?.Trim() ?? "";

        if (progress.Completed)
        {
            return FlagCheckResult.AlreadySolved;
        }

        if (attempt.Length == 0)
        {
            return FlagCheckResult.Empty;
        }

        // Friendly on purpose: case and surrounding whitespace don't matter.
        if (string.Equals(attempt, ActiveMission.Flag, StringComparison.OrdinalIgnoreCase))
        {
            progress.PointsEarned = AvailablePoints(ActiveMission);
            progress.Completed = true;
            return FlagCheckResult.Correct;
        }

        progress.FailedAttempts++;

        return attempt.StartsWith("FLAG{", StringComparison.OrdinalIgnoreCase) && attempt.EndsWith('}')
            ? FlagCheckResult.Incorrect
            : FlagCheckResult.WrongFormat;
    }

    public GameProgressSnapshot ToSnapshot() => new()
    {
        ActiveMissionId = ActiveMission.Id,
        Missions = _progress
            .Where(entry => entry.Value is { Completed: true } or { HintsUnlocked: > 0 } or { FailedAttempts: > 0 })
            .ToDictionary(entry => entry.Key, entry => new MissionProgressSnapshot
            {
                Completed = entry.Value.Completed,
                HintsUnlocked = entry.Value.HintsUnlocked,
                PointsEarned = entry.Value.PointsEarned,
                FailedAttempts = entry.Value.FailedAttempts,
            }),
    };

    public static string SerializeSnapshot(GameProgressSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, CoreJsonContext.Default.GameProgressSnapshot);

    /// <summary>Returns <c>null</c> for missing or unreadable data, so a corrupt save never breaks the game.</summary>
    public static GameProgressSnapshot? DeserializeSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize(json, CoreJsonContext.Default.GameProgressSnapshot);
            return snapshot?.Version == GameProgressSnapshot.CurrentVersion ? snapshot : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int HintCost(MissionDefinition mission, int level) =>
        mission.Points * HintCostPercent[Math.Min(level, HintCostPercent.Count - 1)] / 100;
}

/// <summary>Saved progress (e.g. in the browser's local storage).</summary>
public sealed class GameProgressSnapshot
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string? ActiveMissionId { get; set; }

    public Dictionary<string, MissionProgressSnapshot> Missions { get; set; } = [];
}

public sealed class MissionProgressSnapshot
{
    public bool Completed { get; set; }

    public int HintsUnlocked { get; set; }

    public int PointsEarned { get; set; }

    public int FailedAttempts { get; set; }
}
