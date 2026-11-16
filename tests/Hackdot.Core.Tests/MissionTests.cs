using System.Globalization;
using Hackdot.Core.Models.Definitions;
using Hackdot.Core.Models.Missions;
using Hackdot.Core.Services;
using Hackdot.Core.Services.Missions;
using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class MissionCatalogTests
{
    private static readonly MissionCatalog Catalog = MissionCatalog.LoadEmbedded();

    [Fact]
    public void EmbeddedMissions_LoadInOrder()
    {
        Assert.Equal(
            ["sloppy-intern", "hidden-in-the-shadows", "needle-in-the-log-stack"],
            Catalog.Missions.Select(mission => mission.Id));
    }

    [Fact]
    public void EmbeddedMissions_AreValidAndSolvable()
    {
        foreach (var mission in Catalog.Missions)
        {
            Assert.Empty(Catalog.Validate(mission));
        }
    }

    [Fact]
    public void EmbeddedMissions_HaveUniqueIdsAndFlags()
    {
        Assert.Equal(Catalog.Missions.Count, Catalog.Missions.Select(m => m.Id).Distinct().Count());
        Assert.Equal(Catalog.Missions.Count, Catalog.Missions.Select(m => m.Flag).Distinct().Count());
    }

    [Theory]
    [InlineData("sloppy-intern", "cat ~/Documents/server_setup.txt")]
    [InlineData("hidden-in-the-shadows", "cat /var/www/.secret_flag")]
    [InlineData("needle-in-the-log-stack", "grep FLAG /var/log/auth.log")]
    public void EmbeddedMissions_CanBeSolvedInTheTerminal(string id, string solution)
    {
        var mission = Catalog.Find(id)!;
        var terminal = new CommandInterpreter(new ShellSession(Catalog.CreateFileSystem(mission)), TestTerminal.Text);

        Assert.Contains(mission.Flag, terminal.Run(solution));
    }

    [Fact]
    public void HiddenFlag_IsInvisibleToPlainLs()
    {
        var terminal = new CommandInterpreter(
            new ShellSession(Catalog.CreateFileSystem(Catalog.Find("hidden-in-the-shadows")!)), TestTerminal.Text);

        Assert.DoesNotContain(".secret_flag", terminal.Run("ls /var/www"));
        Assert.Contains(".secret_flag", terminal.Run("ls -a /var/www"));
    }

    [Fact]
    public void LogMission_HasALongLogWithOneFlagLine()
    {
        var fileSystem = Catalog.CreateFileSystem(Catalog.Find("needle-in-the-log-stack")!);
        var log = fileSystem.GetFile("/var/log/auth.log")!.Content;
        var lines = log.TrimEnd('\n').Split('\n');

        Assert.Equal(1500, lines.Length);
        Assert.Single(lines, line => line.Contains("FLAG{"));
        Assert.Contains("novatech-srv sshd[31337]: Invalid user FLAG{", lines[1136]);
    }

    [Fact]
    public void Validate_ReportsUnsolvableMission()
    {
        var mission = MissionCatalog.Parse("""
            {
              "id": "Bad Id",
              "title": "t", "summary": "s", "briefing": "b", "objective": "o", "debrief": "d",
              "flag": "FLAG{locked}",
              "hints": [ "one" ],
              "fileSystem": { "files": [ { "path": "/root/flag.txt", "content": "FLAG{locked}", "owner": "root", "mode": "600" } ] }
            }
            """);

        var errors = Catalog.Validate(mission);

        Assert.Contains(errors, e => e.Contains("lowercase letters"));
        Assert.Contains(errors, e => e.Contains("exactly 3 hints"));
        Assert.Contains(errors, e => e.Contains("cannot read"));
    }

    [Fact]
    public void Parse_RejectsUnknownFieldsAndMissingRequiredOnes()
    {
        Assert.Throws<InvalidDataException>(() => MissionCatalog.Parse("""{ "id": "x", "titel": "typo" }"""));
    }
}

public class LocalizedTextTests
{
    [Fact]
    public void PlainString_IsEnglish()
    {
        var mission = MissionCatalog.Parse(MinimalMission("\"Hello\""));

        Assert.Equal("Hello", mission.Title.Get(CultureInfo.GetCultureInfo("sv-SE")));
    }

    [Fact]
    public void Object_PicksLanguageWithFallback()
    {
        var mission = MissionCatalog.Parse(MinimalMission("""{ "en": "Hello", "sv": "Hej" }"""));

        Assert.Equal("Hej", mission.Title.Get(CultureInfo.GetCultureInfo("sv-SE")));
        Assert.Equal("Hej", mission.Title.Get(CultureInfo.GetCultureInfo("sv")));
        Assert.Equal("Hello", mission.Title.Get(CultureInfo.GetCultureInfo("de-DE")));
        Assert.Equal("Hello", mission.Title.Get(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Object_MustIncludeEnglish()
    {
        Assert.Throws<InvalidDataException>(() => MissionCatalog.Parse(MinimalMission("""{ "sv": "Hej" }""")));
    }

    private static string MinimalMission(string title) => $$"""
        {
          "id": "x", "title": {{title}}, "summary": "s", "briefing": "b", "objective": "o", "debrief": "d",
          "flag": "FLAG{x}", "hints": [ "1", "2", "3" ]
        }
        """;
}

public class ContentGeneratorTests
{
    [Fact]
    public void AuthLog_IsDeterministicAndInsertsLines()
    {
        var definition = new GeneratorDefinition
        {
            Kind = "authLog",
            Lines = 50,
            Seed = 7,
            Inserts = [new GeneratedLine { At = 10, Text = "{timestamp} {host} special" }],
        };

        var first = ContentGenerator.Generate(definition, "srv");
        var second = ContentGenerator.Generate(definition, "srv");
        var lines = first.TrimEnd('\n').Split('\n');

        Assert.Equal(first, second);
        Assert.Equal(50, lines.Length);
        Assert.Matches(@"^Mar 14 \d\d:\d\d:\d\d srv special$", lines[9]);
        Assert.All(lines, line => Assert.StartsWith("Mar 14 ", line));
    }

    [Fact]
    public void UnknownKind_Throws()
    {
        Assert.Throws<InvalidDataException>(() => ContentGenerator.Generate(new GeneratorDefinition { Kind = "nope" }, "srv"));
    }
}

public class GameStateTests
{
    private static readonly MissionCatalog Catalog = MissionCatalog.LoadEmbedded();

    [Fact]
    public void NewGame_StartsAtFirstMissionWithOnlyItUnlocked()
    {
        var state = new GameState(Catalog);

        Assert.Equal("sloppy-intern", state.ActiveMission.Id);
        Assert.True(state.IsUnlocked(Catalog.Missions[0]));
        Assert.False(state.IsUnlocked(Catalog.Missions[1]));
        Assert.Throws<InvalidOperationException>(() => state.Activate(Catalog.Missions[1]));
    }

    [Theory]
    [InlineData("", FlagCheckResult.Empty)]
    [InlineData("password123", FlagCheckResult.WrongFormat)]
    [InlineData("FLAG{wrong}", FlagCheckResult.Incorrect)]
    [InlineData("  flag{N3V3R_wr1t3_p4ssw0rds_d0wn}  ", FlagCheckResult.Correct)]
    public void SubmitFlag_ChecksTheAnswer(string answer, FlagCheckResult expected)
    {
        var state = new GameState(Catalog);

        Assert.Equal(expected, state.SubmitFlag(answer));
    }

    [Fact]
    public void SolvingAMission_AwardsPointsAndUnlocksTheNext()
    {
        var state = new GameState(Catalog);

        state.SubmitFlag("FLAG{n3v3r_wr1t3_p4ssw0rds_d0wn}");

        Assert.True(state.ActiveProgress.Completed);
        Assert.Equal(100, state.TotalScore);
        Assert.True(state.IsUnlocked(Catalog.Missions[1]));
        Assert.Equal(FlagCheckResult.AlreadySolved, state.SubmitFlag("FLAG{n3v3r_wr1t3_p4ssw0rds_d0wn}"));
        Assert.Equal("hidden-in-the-shadows", state.NextMission!.Id);
    }

    [Fact]
    public void Hints_CostPointsAndRunOut()
    {
        var state = new GameState(Catalog);

        Assert.Equal(10, state.NextHintCost());
        Assert.True(state.UnlockNextHint());
        Assert.Equal(15, state.NextHintCost());
        Assert.True(state.UnlockNextHint());
        Assert.True(state.UnlockNextHint());
        Assert.Null(state.NextHintCost());
        Assert.False(state.UnlockNextHint());
        Assert.Equal(50, state.AvailablePoints(state.ActiveMission));

        state.SubmitFlag("FLAG{n3v3r_wr1t3_p4ssw0rds_d0wn}");

        Assert.Equal(50, state.TotalScore);
    }

    [Fact]
    public void Hints_AreFreeAfterSolving()
    {
        var state = new GameState(Catalog);
        state.SubmitFlag("FLAG{n3v3r_wr1t3_p4ssw0rds_d0wn}");

        Assert.Equal(0, state.NextHintCost());
        state.UnlockNextHint();
        Assert.Equal(100, state.TotalScore);
    }

    [Fact]
    public void Snapshot_RoundTripsProgress()
    {
        var state = new GameState(Catalog);
        state.UnlockNextHint();
        state.SubmitFlag("FLAG{n3v3r_wr1t3_p4ssw0rds_d0wn}");
        state.Activate(Catalog.Missions[1]);
        state.SubmitFlag("FLAG{nope}");

        var json = GameState.SerializeSnapshot(state.ToSnapshot());
        var restored = new GameState(Catalog, GameState.DeserializeSnapshot(json));

        Assert.Equal("hidden-in-the-shadows", restored.ActiveMission.Id);
        Assert.Equal(90, restored.TotalScore);
        Assert.Equal(1, restored.Progress(Catalog.Missions[0]).HintsUnlocked);
        Assert.Equal(1, restored.ActiveProgress.FailedAttempts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{ "version": 99 }""")]
    public void Snapshot_CorruptDataIsIgnored(string? json)
    {
        Assert.Null(GameState.DeserializeSnapshot(json));
    }

    [Fact]
    public void Snapshot_WithLockedOrUnknownActiveMission_FallsBackSafely()
    {
        var snapshot = new GameProgressSnapshot
        {
            ActiveMissionId = "needle-in-the-log-stack",
            Missions = { ["deleted-mission"] = new MissionProgressSnapshot { Completed = true, PointsEarned = 999 } },
        };

        var state = new GameState(Catalog, snapshot);

        Assert.Equal("sloppy-intern", state.ActiveMission.Id);
        Assert.Equal(0, state.TotalScore);
    }
}
