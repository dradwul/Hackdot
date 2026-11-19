using Hackdot.Core.Services.Missions;
using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class AchievementTests
{
    private static readonly MissionCatalog Catalog = MissionCatalog.LoadEmbedded();

    private static readonly DateTime Afternoon = new(2026, 11, 19, 15, 0, 0);

    private static IEnumerable<string> Ids(IEnumerable<Achievement> achievements) => achievements.Select(achievement => achievement.Id);

    [Theory]
    [InlineData(0, "intern")]
    [InlineData(249, "intern")]
    [InlineData(250, "junior")]
    [InlineData(4500, "lead")]
    [InlineData(99999, "ciso")]
    public void Ranks_FollowTheScore(int score, string rank)
    {
        Assert.Equal(rank, Ranks.For(score).Id);
    }

    [Fact]
    public void Ranks_KnowTheNextOne()
    {
        Assert.Equal("junior", Ranks.Next(0)!.Id);
        Assert.Null(Ranks.Next(10000));
        Assert.True(Ranks.All.Last().MinimumScore <= Catalog.Missions.Sum(mission => mission.Points));
    }

    [Fact]
    public void ThereIsAChapterAchievementForEveryChapter()
    {
        var tracker = new GameState(Catalog).Achievements;

        Assert.Equal(["chapter-1", "chapter-2", "chapter-3"], tracker.All.Where(a => a.Chapter is not null).Select(a => a.Id));
        Assert.Equal(3, tracker.All.First(a => a.Id == "chapter-3").Chapter);
    }

    [Fact]
    public void SolvingTheFirstMission_EarnsFirstFlagAndNoHints()
    {
        var state = new GameState(Catalog);
        state.SubmitFlag(Catalog.Missions[0].Flag);

        var earned = state.Achievements.CheckMissions(state, Afternoon, justSolved: true);

        Assert.Equal(["first-flag", "no-hints"], Ids(earned));
        Assert.Empty(state.Achievements.CheckMissions(state, Afternoon));
    }

    [Fact]
    public void Hints_CostTheNoHintsAchievement()
    {
        var state = new GameState(Catalog);
        state.UnlockNextHint();
        state.SubmitFlag(Catalog.Missions[0].Flag);

        Assert.Equal(["first-flag"], Ids(state.Achievements.CheckMissions(state, Afternoon)));
    }

    [Fact]
    public void FinishingAChapterWithoutHints_EarnsTheChapterAndGhost()
    {
        var state = new GameState(Catalog);

        foreach (var mission in Catalog.Missions.Where(mission => mission.Chapter == 1))
        {
            state.Activate(mission);
            state.SubmitFlag(mission.Flag);
        }

        var earned = Ids(state.Achievements.CheckMissions(state, Afternoon)).ToList();

        Assert.Contains("chapter-1", earned);
        Assert.Contains("ghost", earned);
        Assert.Contains("sharpshooter", earned);
        Assert.DoesNotContain("chapter-2", earned);
        Assert.DoesNotContain("legend", earned);
    }

    [Fact]
    public void NightOwl_IsForSolvingAfterMidnight()
    {
        var state = new GameState(Catalog);
        state.SubmitFlag(Catalog.Missions[0].Flag);

        Assert.Contains("night-owl", Ids(state.Achievements.CheckMissions(state, new DateTime(2026, 11, 19, 2, 30, 0), justSolved: true)));
        Assert.True(state.Achievements.All.First(a => a.Id == "night-owl").Secret);
    }

    [Fact]
    public void TerminalCommands_EarnTheirAchievements()
    {
        var tracker = new GameState(Catalog).Achievements;
        var minute = TimeSpan.FromMinutes(1);

        Assert.Empty(tracker.RecordCommand(new CommandRun([["ls"]], false, false, minute)));
        Assert.Equal(["first-root"], Ids(tracker.RecordCommand(new CommandRun([["whoami"]], true, false, minute))));
        Assert.Equal(["scribe"], Ids(tracker.RecordCommand(new CommandRun([["echo", "hi"]], false, true, minute))));
        Assert.Equal(["pipe-dream"], Ids(tracker.RecordCommand(new CommandRun([["cat", "a"], ["grep", "x"], ["sort"], ["uniq", "-c"]], false, false, minute))));
        Assert.Equal(["rtfm"], Ids(tracker.RecordCommand(new CommandRun([["help", "grep"]], false, false, minute))));
        Assert.Empty(tracker.RecordCommand(new CommandRun([["help"]], false, false, minute)));
    }

    [Fact]
    public void Patience_AddsUpTheTimeSpentSleeping()
    {
        var tracker = new GameState(Catalog).Achievements;

        Assert.Empty(tracker.RecordCommand(new CommandRun([["sleep", "30m"]], false, false, TimeSpan.FromMinutes(31))));
        Assert.Equal(["patience"], Ids(tracker.RecordCommand(new CommandRun([["sleep", "30m"]], false, false, TimeSpan.FromMinutes(31)))));
    }

    [Fact]
    public void TabTab_CountsTabCompletions()
    {
        var tracker = new GameState(Catalog).Achievements;

        for (var i = 1; i < AchievementTracker.TabCompletions; i++)
        {
            Assert.Empty(tracker.RecordTabCompletion());
        }

        Assert.Equal(["tab-tab"], Ids(tracker.RecordTabCompletion()));
        Assert.Empty(tracker.RecordTabCompletion());
    }

    [Fact]
    public void Achievements_AreSavedWithTheProgress()
    {
        var state = new GameState(Catalog);
        state.Achievements.RecordCommand(new CommandRun([["whoami"]], true, false, TimeSpan.FromMinutes(1)));
        state.Achievements.RecordTabCompletion();

        var restored = new GameState(Catalog, GameState.DeserializeSnapshot(GameState.SerializeSnapshot(state.ToSnapshot())));

        Assert.Contains("first-root", restored.Achievements.Unlocked);
        Assert.Equal(1, restored.Achievements.Stats["tab"]);
    }

    [Fact]
    public void UnknownSavedAchievements_AreIgnored()
    {
        var snapshot = new GameProgressSnapshot { Achievements = ["first-root", "removed-one"] };

        Assert.Equal(["first-root"], new GameState(Catalog, snapshot).Achievements.Unlocked);
    }

    [Fact]
    public void TheTerminal_ReportsWhatACommandLineDid()
    {
        var runs = new List<CommandRun>();
        var terminal = TestTerminal.Create(fs =>
        {
            fs.AddUser(new Models.VirtualUser("root", "/root", "toor"));
            fs.WriteFile("/etc/sudoers", "user ALL=(root) NOPASSWD: /usr/bin/cat /var/log/*", "root", Convert.ToInt32("440", 8));
        });
        terminal.CommandRan += runs.Add;

        terminal.Run("ls / | grep e > /tmp/list.txt 2>/dev/null");
        terminal.Run("sleep 5m");
        terminal.Run("sudo cat /var/log/syslog");
        terminal.Run("ls 2>/dev/null");

        Assert.Equal(2, runs[0].Stages.Count);
        Assert.True(runs[0].WroteFile);
        Assert.False(runs[0].RanAsRoot);
        Assert.Equal(TimeSpan.FromMinutes(6), runs[1].TimePassed);
        Assert.True(runs[2].RanAsRoot);
        Assert.False(runs[3].WroteFile);
    }
}
