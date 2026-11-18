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
            [
                "first-steps",
                "sloppy-intern", "hidden-in-the-shadows", "needle-in-the-log-stack", "behind-closed-doors",
                "who-keeps-knocking", "encoded-not-encrypted", "footprints", "a-little-too-generous", "inside-job",
                "leftovers", "locked-out", "clockwork", "set-user-id",
            ],
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

    [Fact]
    public void Tutorial_TabCompletesTheNamesTheBriefingPromises()
    {
        var terminal = new CommandInterpreter(
            new ShellSession(Catalog.CreateFileSystem(Catalog.Find("first-steps")!)), TestTerminal.Text);

        Assert.Equal("cat start_here.txt ", TabCompleter.Complete(terminal, "cat st").Input);
        Assert.Equal("cd training/", TabCompleter.Complete(terminal, "cd tr").Input);
    }

    private static CommandInterpreter Play(string id) =>
        new(new ShellSession(Catalog.CreateFileSystem(Catalog.Find(id)!)), TestTerminal.Text);

    [Fact]
    public void Leftovers_TheRealConfigIsLocked_ButTheCopyIsNot()
    {
        var terminal = Play("leftovers");

        Assert.Equal("cat: /var/www/shop/config.php: Permission denied", terminal.Run("cat /var/www/shop/config.php"));
        Assert.Equal("/var/www/shop/config.php.bak", terminal.Run("find / -name \"*.bak\" 2>/dev/null"));
    }

    [Fact]
    public void LockedOut_OnlyTheOwnerCanUnlockAFile()
    {
        var terminal = Play("locked-out");

        Assert.Equal("cat: ~/recovery_codes.txt: Permission denied", terminal.Run("cat ~/recovery_codes.txt"));
        Assert.StartsWith("---------- 1 user", terminal.Run("ls -l ~").Split('\n').First(line => line.EndsWith("recovery_codes.txt")));
        Assert.Equal(
            "chmod: changing permissions of 'payroll_2026.csv': Operation not permitted",
            RunAll(terminal, "cd ~", "chmod 600 payroll_2026.csv"));
    }

    [Fact]
    public void Clockwork_TheCronJobLeadsToTheCopies()
    {
        var terminal = Play("clockwork");

        Assert.Contains("root  /opt/scripts/sync-notes.sh", terminal.Run("cat /etc/crontab"));
        Assert.Equal("cat: /root/notes/passwords.txt: Permission denied", terminal.Run("cat /root/notes/passwords.txt"));
        Assert.Contains(".sync/", terminal.Run("ls -a /srv/share"));
    }

    [Fact]
    public void SetUserId_FindListsTheSetUidPrograms_AndOnlyBase64LeaksFiles()
    {
        var terminal = Play("set-user-id");

        Assert.Equal(
            "/usr/bin/base64\n/usr/bin/passwd\n/usr/bin/su\n/usr/bin/sudo",
            terminal.Run("find / -perm -4000 2>/dev/null"));
        Assert.Contains("-rwsr-xr-x 1 root", terminal.Run("ls -l /usr/bin"));
        Assert.Equal("cat: /root/signing_key.txt: Permission denied", terminal.Run("cat /root/signing_key.txt"));
        Assert.Equal("user", terminal.Run("whoami"));

        // su and sudo are set-user-ID on purpose, but still check passwords and rules themselves.
        Assert.Equal("user is not in the sudoers file. This incident will be reported.", terminal.Run("sudo cat /root/signing_key.txt"));
    }

    private static string RunAll(CommandInterpreter terminal, params string[] inputs) =>
        inputs.Select(terminal.Run).ToList().Last();

    [Theory]
    [InlineData("first-steps", "ls\ncat start_here.txt\ncd training\nls\ncat flag.txt")]
    [InlineData("sloppy-intern", "cat ~/Documents/server_setup.txt")]
    [InlineData("hidden-in-the-shadows", "cat /var/www/.secret_flag")]
    [InlineData("needle-in-the-log-stack", "grep FLAG /var/log/auth.log")]
    [InlineData("behind-closed-doors", "su admin\nSummer2026!\ncat /home/admin/quarterly_report.txt")]
    [InlineData("who-keeps-knocking", "cat /srv/honeypot/sessions/203.0.113.77.log")]
    [InlineData("encoded-not-encrypted", "echo 'Umt4QlIzdGlOSE16TmpSZk1YTmZiakIwWHpOdVkzSjVjSFF4TUc1OQ==' | base64 -d | base64 -d")]
    [InlineData("footprints", "base64 -d /var/tmp/.../c.b64 | grep -i canary")]
    [InlineData("a-little-too-generous", "sudo cat /var/log/../../root/flag.txt")]
    [InlineData("inside-job", "su ops\nD0t-Ops-42\nsudo base64 /var/backups/../../root/roadmap.txt | base64 -d")]
    [InlineData("leftovers", "cat /var/www/shop/config.php.bak")]
    [InlineData("locked-out", "chmod 600 ~/recovery_codes.txt\ncat ~/recovery_codes.txt")]
    [InlineData("clockwork", "cat /srv/share/.sync/passwords.txt")]
    [InlineData("set-user-id", "base64 /root/signing_key.txt | base64 -d")]
    public void EmbeddedMissions_CanBeSolvedInTheTerminal(string id, string solution)
    {
        // Each line is one submitted input (a command, or the answer to a prompt like "Password:").
        var mission = Catalog.Find(id)!;
        var terminal = new CommandInterpreter(new ShellSession(Catalog.CreateFileSystem(mission)), TestTerminal.Text);

        // ToList() first: LINQ's Last() on Select(array) would only run the final step.
        var output = solution.Split('\n').Select(terminal.Run).ToList().Last();

        Assert.Contains(mission.Flag, output);
    }

    [Fact]
    public void LockedMission_CannotBeSolvedWithoutBecomingAdmin()
    {
        var mission = Catalog.Find("behind-closed-doors")!;
        var terminal = new CommandInterpreter(new ShellSession(Catalog.CreateFileSystem(mission)), TestTerminal.Text);

        Assert.Contains("Permission denied", terminal.Run("cat /home/admin/quarterly_report.txt"));
        Assert.Contains("Summer2026!", terminal.Run("grep -ri password /opt"));
    }

    [Fact]
    public void BruteForceMission_NeedsCounting_AndFilteringForFailedLogins()
    {
        var mission = Catalog.Find("who-keeps-knocking")!;
        var terminal = new CommandInterpreter(new ShellSession(Catalog.CreateFileSystem(mission)), TestTerminal.Text);
        const string log = "/var/log/nginx/access.log";

        // The solution from hint 3 points at the attacker…
        Assert.EndsWith(" 203.0.113.77", terminal.Run($"grep 401 {log} | cut -d' ' -f1 | sort | uniq -c | sort -n | tail -n 1"));

        // …while counting every request points at the innocent health check: filtering matters.
        Assert.EndsWith(" 10.0.0.5", terminal.Run($"cut -d' ' -f1 {log} | sort | uniq -c | sort -n | tail -n 1"));

        // The runner-up has a decoy file without the flag.
        var top = terminal.Run($"grep 401 {log} | cut -d' ' -f1 | sort | uniq -c | sort -rn | head -n 2").Split('\n');
        Assert.EndsWith(" 198.51.100.23", top[1]);
        Assert.DoesNotContain(mission.Flag, terminal.Run("cat /srv/honeypot/sessions/198.51.100.23.log"));

        // The honeypot folder can't be browsed or searched – only opened by exact name.
        Assert.Contains("Permission denied", terminal.Run("ls /srv/honeypot/sessions"));
        Assert.DoesNotContain(mission.Flag, terminal.Run("grep -r FLAG /srv"));
        Assert.DoesNotContain(mission.Flag, terminal.Run("find / -name \"*.log\""));
        Assert.Equal(2400, int.Parse(terminal.Run($"wc -l {log}").Split(' ')[0], CultureInfo.InvariantCulture));
    }

    [Fact]
    public void EncodingMission_DecodingOnceIsNotEnough()
    {
        var mission = Catalog.Find("encoded-not-encrypted")!;
        var terminal = new CommandInterpreter(new ShellSession(Catalog.CreateFileSystem(mission)), TestTerminal.Text);

        Assert.Contains("adminToken", terminal.Run("grep -r -i token /var/www/html/portal"));
        Assert.DoesNotContain(mission.Flag, terminal.Run("grep -r FLAG /"));

        var once = terminal.Run("echo 'Umt4QlIzdGlOSE16TmpSZk1YTmZiakIwWHpOdVkzSjVjSFF4TUc1OQ==' | base64 -d");
        Assert.Equal("RkxBR3tiNHMzNjRfMXNfbjB0XzNuY3J5cHQxMG59", once);
    }

    [Fact]
    public void FootprintsMission_TheTrailIsInTheHistory_AndTheOriginalIsLocked()
    {
        var mission = Catalog.Find("footprints")!;
        var terminal = new CommandInterpreter(new ShellSession(Catalog.CreateFileSystem(mission)), TestTerminal.Text);

        Assert.Contains("/var/tmp/...", terminal.Run("cat /home/intern/.bash_history"));
        Assert.DoesNotContain("...", terminal.Run("ls /var/tmp"));
        Assert.Contains("...", terminal.Run("ls -a /var/tmp"));
        Assert.Contains("Permission denied", terminal.Run("cat /var/backups/db/customers.sql"));
        Assert.DoesNotContain(mission.Flag, terminal.Run("grep -r FLAG /var /home"));
        Assert.Contains(mission.Flag, terminal.Run("cat /var/tmp/.../c.b64 | base64 -d"));
    }

    [Fact]
    public void SudoMissions_AreLockedWithoutTheSudoTrick()
    {
        var generous = Catalog.Find("a-little-too-generous")!;
        var terminal = new CommandInterpreter(new ShellSession(Catalog.CreateFileSystem(generous)), TestTerminal.Text);

        Assert.Contains("Permission denied", terminal.Run("cat /root/flag.txt"));
        Assert.Contains("Permission denied", terminal.Run("cat /etc/sudoers"));
        Assert.Contains("/usr/bin/cat /var/log/*", terminal.Run("sudo -l"));
        Assert.Contains("not allowed", terminal.Run("sudo cat /root/flag.txt"));
        Assert.Contains(generous.Flag, terminal.Run("sudo cat /var/log/syslog /root/flag.txt"));

        var inside = Catalog.Find("inside-job")!;
        terminal = new CommandInterpreter(new ShellSession(Catalog.CreateFileSystem(inside)), TestTerminal.Text);

        Assert.Contains("may not run sudo", terminal.Run("sudo -l"));
        Assert.Equal("D0t-Ops-42", terminal.Run("echo 'RDB0LU9wcy00Mg==' | base64 -d"));
        terminal.Execute("su ops");
        terminal.Execute("D0t-Ops-42");
        Assert.Contains("/usr/bin/base64 /var/backups/*", terminal.Run("sudo -l"));
        Assert.Contains("not allowed", terminal.Run("sudo cat /root/roadmap.txt"));
    }

    [Fact]
    public void Validate_FollowsPasswordsAndSudoRulesToRoot()
    {
        const string template = """
            {
              "id": "chain", "title": "t", "summary": "s", "briefing": "b", "objective": "o", "debrief": "d",
              "flag": "FLAG{chain}", "hints": [ "1", "2", "3" ],
              "fileSystem": {
                "users": [ { "name": "ops", "password": "pw123456" } ],
                "files": [
                  { "path": "/opt/conf.txt", "content": "password = cHcxMjM0NTY=" },
                  { "path": "/root/flag.txt", "content": "FLAG{chain}", "owner": "root", "mode": "600" }
                  SUDOERS
                ]
              }
            }
            """;

        var withSudo = MissionCatalog.Parse(template.Replace("SUDOERS",
            """, { "path": "/etc/sudoers", "content": "ops ALL=(root) NOPASSWD: /usr/bin/base64 /var/backups/*", "mode": "440" }"""));
        var withoutSudo = MissionCatalog.Parse(template.Replace("SUDOERS", ""));

        Assert.Empty(Catalog.Validate(withSudo));
        Assert.NotEmpty(Catalog.Validate(withoutSudo));
    }

    [Theory]
    // The player owns the locked file, so they can chmod it.
    [InlineData("""{ "path": "/home/user/flag.txt", "content": "FLAG{x}", "owner": "user", "mode": "000" }""", true)]
    [InlineData("""{ "path": "/home/user/flag.txt", "content": "FLAG{x}", "owner": "root", "mode": "000" }""", false)]
    // A set-user-ID root program that reads files makes root's files reachable.
    [InlineData("""{ "path": "/usr/bin/cat", "content": "ELF", "owner": "root", "mode": "4755" }""", true)]
    [InlineData("""{ "path": "/usr/bin/cat", "content": "ELF", "owner": "root", "mode": "755" }""", false)]
    [InlineData("""{ "path": "/usr/bin/cat", "content": "ELF", "owner": "root", "mode": "4750" }""", false)]
    // sudo is set-user-ID on purpose, but checks its own rules.
    [InlineData("""{ "path": "/usr/bin/sudo", "content": "ELF", "owner": "root", "mode": "4755" }""", false)]
    public void Validate_KnowsAboutChmodAndSetUidPrograms(string file, bool solvable)
    {
        var mission = MissionCatalog.Parse($$"""
            {
              "id": "perm", "title": "t", "summary": "s", "briefing": "b", "objective": "o", "debrief": "d",
              "flag": "FLAG{x}", "hints": [ "1", "2", "3" ],
              "fileSystem": {
                "files": [
                  { "path": "/root/flag.txt", "content": "FLAG{x}", "owner": "root", "mode": "600" },
                  {{file}}
                ]
              }
            }
            """);

        Assert.Equal(solvable, Catalog.Validate(mission).Count == 0);
    }

    [Fact]
    public void Validate_AcceptsAFlagThatIsOnlyStoredInBase64()
    {
        const string template = """
            {
              "id": "encoded", "title": "t", "summary": "s", "briefing": "b", "objective": "o", "debrief": "d",
              "flag": "FLAG{x}", "hints": [ "1", "2", "3" ],
              "fileSystem": { "files": [ { "path": "/tmp/token.txt", "content": "VALUE" } ] }
            }
            """;

        Assert.Empty(Catalog.Validate(MissionCatalog.Parse(template.Replace("VALUE", "RkxBR3t4fQ=="))));         // once
        Assert.Empty(Catalog.Validate(MissionCatalog.Parse(template.Replace("VALUE", "Umt4QlIzdDRmUT09"))));     // twice
        Assert.NotEmpty(Catalog.Validate(MissionCatalog.Parse(template.Replace("VALUE", "nothing here"))));
    }

    [Fact]
    public void Validate_AcceptsFlagsReachableThroughAFoundPassword_ButNotSecretOnes()
    {
        const string template = """
            {
              "id": "locked", "title": "t", "summary": "s", "briefing": "b", "objective": "o", "debrief": "d",
              "flag": "FLAG{locked}", "hints": [ "1", "2", "3" ],
              "fileSystem": {
                "users": [ { "name": "boss", "password": "s3cret" } ],
                "directories": [ { "path": "/home/boss", "owner": "boss", "mode": "700" } ],
                "files": [
                  { "path": "/home/boss/flag.txt", "content": "FLAG{locked}", "owner": "boss", "mode": "600" }
                  CLUE
                ]
              }
            }
            """;

        var withClue = MissionCatalog.Parse(template.Replace("CLUE", """, { "path": "/tmp/note.txt", "content": "boss pw: s3cret" }"""));
        var withoutClue = MissionCatalog.Parse(template.Replace("CLUE", ""));

        Assert.Empty(Catalog.Validate(withClue));
        Assert.Contains(Catalog.Validate(withoutClue), e => e.Contains("cannot read"));
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
        Assert.Contains("dottech-srv sshd[31337]: Invalid user FLAG{", lines[1136]);
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

    private static readonly MissionDefinition Tutorial = Catalog.Find("first-steps")!;

    private static readonly MissionDefinition SloppyIntern = Catalog.Find("sloppy-intern")!;

    /// <summary>A game where the tutorial is done and the first real mission (100 points) is active.</summary>
    private static GameState AfterTutorial()
    {
        var state = new GameState(Catalog);
        state.SubmitFlag(Tutorial.Flag);
        state.Activate(SloppyIntern);
        return state;
    }

    [Fact]
    public void NewGame_StartsWithTheTutorialAndOnlyItUnlocked()
    {
        var state = new GameState(Catalog);

        Assert.Same(Tutorial, Catalog.Missions[0]);
        Assert.Equal("first-steps", state.ActiveMission.Id);
        Assert.True(state.IsUnlocked(Catalog.Missions[0]));
        Assert.False(state.IsUnlocked(Catalog.Missions[1]));
        Assert.Throws<InvalidOperationException>(() => state.Activate(Catalog.Missions[1]));
    }

    [Theory]
    [InlineData("", FlagCheckResult.Empty)]
    [InlineData("password123", FlagCheckResult.WrongFormat)]
    [InlineData("FLAG{wrong}", FlagCheckResult.Incorrect)]
    [InlineData("  flag{H3LL0_d0t0s}  ", FlagCheckResult.Correct)]
    public void SubmitFlag_ChecksTheAnswer(string answer, FlagCheckResult expected)
    {
        var state = new GameState(Catalog);

        Assert.Equal(expected, state.SubmitFlag(answer));
    }

    [Fact]
    public void SolvingAMission_AwardsPointsAndUnlocksTheNext()
    {
        var state = new GameState(Catalog);

        state.SubmitFlag(Tutorial.Flag);

        Assert.True(state.ActiveProgress.Completed);
        Assert.Equal(Tutorial.Points, state.TotalScore);
        Assert.True(state.IsUnlocked(SloppyIntern));
        Assert.Equal(FlagCheckResult.AlreadySolved, state.SubmitFlag(Tutorial.Flag));
        Assert.Equal("sloppy-intern", state.NextMission!.Id);
    }

    [Fact]
    public void Hints_CostPointsAndRunOut()
    {
        var state = AfterTutorial();

        Assert.Equal(10, state.NextHintCost());
        Assert.True(state.UnlockNextHint());
        Assert.Equal(15, state.NextHintCost());
        Assert.True(state.UnlockNextHint());
        Assert.True(state.UnlockNextHint());
        Assert.Null(state.NextHintCost());
        Assert.False(state.UnlockNextHint());
        Assert.Equal(50, state.AvailablePoints(state.ActiveMission));

        state.SubmitFlag("FLAG{n3v3r_wr1t3_p4ssw0rds_d0wn}");

        Assert.Equal(50, state.ActiveProgress.PointsEarned);
    }

    [Fact]
    public void Hints_AreFreeAfterSolving()
    {
        var state = AfterTutorial();
        state.SubmitFlag("FLAG{n3v3r_wr1t3_p4ssw0rds_d0wn}");

        Assert.Equal(0, state.NextHintCost());
        state.UnlockNextHint();
        Assert.Equal(100, state.ActiveProgress.PointsEarned);
    }

    [Fact]
    public void Snapshot_RoundTripsProgress()
    {
        var state = AfterTutorial();
        state.UnlockNextHint();
        state.SubmitFlag("FLAG{n3v3r_wr1t3_p4ssw0rds_d0wn}");
        state.Activate(Catalog.Find("hidden-in-the-shadows")!);
        state.SubmitFlag("FLAG{nope}");

        var json = GameState.SerializeSnapshot(state.ToSnapshot());
        var restored = new GameState(Catalog, GameState.DeserializeSnapshot(json));

        Assert.Equal("hidden-in-the-shadows", restored.ActiveMission.Id);
        Assert.Equal(Tutorial.Points + 90, restored.TotalScore);
        Assert.Equal(1, restored.Progress(SloppyIntern).HintsUnlocked);
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

        Assert.Equal("first-steps", state.ActiveMission.Id);
        Assert.Equal(0, state.TotalScore);
    }

    [Fact]
    public void ProgressSavedBeforeTheTutorialExisted_StaysUnlocked()
    {
        // Players who started before the tutorial was added have solved missions but not the tutorial.
        var snapshot = new GameProgressSnapshot
        {
            ActiveMissionId = "needle-in-the-log-stack",
            Missions =
            {
                ["sloppy-intern"] = new MissionProgressSnapshot { Completed = true, PointsEarned = 100 },
                ["hidden-in-the-shadows"] = new MissionProgressSnapshot { Completed = true, PointsEarned = 100 },
            },
        };

        var state = new GameState(Catalog, snapshot);

        Assert.Equal("needle-in-the-log-stack", state.ActiveMission.Id);
        Assert.True(state.IsUnlocked(SloppyIntern));
        Assert.True(state.IsUnlocked(Catalog.Find("hidden-in-the-shadows")!));
        Assert.True(state.IsUnlocked(Tutorial));
    }
}
