using Hackdot.Core.Models.Missions;

namespace Hackdot.Core.Services.Missions;

/// <summary>A title the player earns with points. The UI shows it as <c>Rank.{Id}</c>.</summary>
public sealed record Rank(string Id, int MinimumScore);

public static class Ranks
{
    /// <summary>From the first day at DotTech to the top of the security team. Lowest first.</summary>
    public static readonly IReadOnlyList<Rank> All =
    [
        new("intern", 0),
        new("junior", 250),
        new("pentester", 1000),
        new("senior", 2500),
        new("lead", 4500),
        new("architect", 7000),
        new("ciso", 10000),
    ];

    public static Rank For(int score) => All.Last(rank => score >= rank.MinimumScore);

    /// <summary>The next rank to reach, or <c>null</c> at the top.</summary>
    public static Rank? Next(int score) => All.FirstOrDefault(rank => rank.MinimumScore > score);
}

/// <summary>
/// Something the player did, worth a badge. The UI shows <c>Achievement.{Id}.Title</c> and <c>.Description</c>;
/// chapter achievements (<c>chapter-2</c>) share one text with the chapter number. Secret ones stay hidden until earned.
/// </summary>
public sealed record Achievement(string Id, bool Secret = false)
{
    public const string ChapterPrefix = "chapter-";

    /// <summary>The chapter of a <c>chapter-N</c> achievement, otherwise <c>null</c>.</summary>
    public int? Chapter => Id.StartsWith(ChapterPrefix, StringComparison.Ordinal) && int.TryParse(Id[ChapterPrefix.Length..], out var chapter)
        ? chapter
        : null;
}

/// <summary>What happened when the player ran one command line in the terminal.</summary>
/// <param name="Stages">The words of each command in the pipeline.</param>
/// <param name="RanAsRoot">Something ran as root: after <c>su</c>, via <c>sudo</c> or a set-user-ID program.</param>
/// <param name="WroteFile">Output was written to a file with <c>&gt;</c> or <c>&gt;&gt;</c> (not <c>/dev/null</c>).</param>
/// <param name="TimePassed">How far the clock moved (a minute, plus any <c>sleep</c>).</param>
public sealed record CommandRun(IReadOnlyList<IReadOnlyList<string>> Stages, bool RanAsRoot, bool WroteFile, TimeSpan TimePassed);

/// <summary>
/// Keeps track of earned achievements and the counters some of them need. Plain C#, saved with the progress.
/// </summary>
public sealed class AchievementTracker
{
    public const int SharpshooterMissions = 5;
    public const int TabCompletions = 25;
    public const int PatienceMinutes = 60;

    private const string TabStat = "tab";
    private const string SleepStat = "sleep-minutes";

    private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _stats = new(StringComparer.Ordinal);

    public AchievementTracker(MissionCatalog catalog, IEnumerable<string>? unlocked = null, IReadOnlyDictionary<string, int>? stats = null)
    {
        All =
        [
            new("first-flag"),
            new("no-hints"),
            new("sharpshooter"),
            .. catalog.Missions.Select(mission => mission.Chapter).Distinct().Order().Select(chapter => new Achievement(Achievement.ChapterPrefix + chapter)),
            new("ghost"),
            new("legend"),
            new("first-root"),
            new("scribe"),
            new("pipe-dream"),
            new("rtfm"),
            new("tab-tab"),
            new("patience"),
            new("night-owl", Secret: true),
        ];

        foreach (var id in unlocked ?? [])
        {
            if (All.Any(achievement => achievement.Id == id))
            {
                _unlocked.Add(id);
            }
        }

        foreach (var (key, value) in stats ?? new Dictionary<string, int>())
        {
            _stats[key] = Math.Max(0, value);
        }
    }

    /// <summary>Every achievement, in the order they are shown.</summary>
    public IReadOnlyList<Achievement> All { get; }

    public IReadOnlySet<string> Unlocked => _unlocked;

    public IReadOnlyDictionary<string, int> Stats => _stats;

    public bool IsUnlocked(Achievement achievement) => _unlocked.Contains(achievement.Id);

    /// <summary>Checks the mission achievements. <paramref name="localTime"/> is the player's own clock.</summary>
    /// <returns>The achievements earned just now.</returns>
    public IReadOnlyList<Achievement> CheckMissions(GameState state, DateTime localTime, bool justSolved = false)
    {
        var missions = state.Catalog.Missions;
        var completed = missions.Where(mission => state.Progress(mission).Completed).ToList();
        bool FullPoints(MissionDefinition mission) => state.Progress(mission).PointsEarned == mission.Points;

        var earned = new List<Achievement>();
        Check("first-flag", completed.Count > 0);
        Check("no-hints", completed.Any(FullPoints));
        Check("sharpshooter", completed.Count(mission => state.Progress(mission).FailedAttempts == 0) >= SharpshooterMissions);
        Check("legend", completed.Count == missions.Count);
        Check("night-owl", justSolved && localTime.Hour < 5);

        var chapters = missions.GroupBy(mission => mission.Chapter).ToList();
        foreach (var chapter in chapters)
        {
            Check(Achievement.ChapterPrefix + chapter.Key, chapter.All(mission => state.Progress(mission).Completed));
        }

        Check("ghost", chapters.Any(chapter => chapter.All(mission => state.Progress(mission).Completed && FullPoints(mission))));

        return earned;

        void Check(string id, bool condition)
        {
            if (condition && _unlocked.Add(id))
            {
                earned.Add(All.First(achievement => achievement.Id == id));
            }
        }
    }

    /// <returns>The achievements earned just now.</returns>
    public IReadOnlyList<Achievement> RecordCommand(CommandRun run)
    {
        var slept = (int)Math.Max(0, (run.TimePassed - TimeSpan.FromMinutes(1)).TotalMinutes);
        if (slept > 0)
        {
            Add(SleepStat, slept);
        }

        var earned = new List<Achievement>();
        Check("first-root", run.RanAsRoot);
        Check("scribe", run.WroteFile);
        Check("pipe-dream", run.Stages.Count >= 4);
        Check("rtfm", run.Stages.Any(words => words is ["help", _, ..]));
        Check("patience", Stat(SleepStat) >= PatienceMinutes);
        return earned;

        void Check(string id, bool condition)
        {
            if (condition && _unlocked.Add(id))
            {
                earned.Add(All.First(achievement => achievement.Id == id));
            }
        }
    }

    /// <returns>The achievements earned just now.</returns>
    public IReadOnlyList<Achievement> RecordTabCompletion()
    {
        Add(TabStat, 1);
        return Stat(TabStat) >= TabCompletions && _unlocked.Add("tab-tab") ? [All.First(achievement => achievement.Id == "tab-tab")] : [];
    }

    private int Stat(string key) => _stats.GetValueOrDefault(key);

    private void Add(string key, int amount) => _stats[key] = Stat(key) + amount;
}
