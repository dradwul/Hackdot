using Hackdot.Core.Models.Missions;
using Hackdot.Core.Services.Missions;

namespace Hackdot.Core.Tests;

public class MailTests
{
    private static readonly MissionCatalog Catalog = MissionCatalog.LoadEmbedded();

    [Fact]
    public void EveryMission_HasAJobAndAReply()
    {
        foreach (var mission in Catalog.Missions)
        {
            Assert.Contains(mission.Mails, mail => mail.Trigger == MissionMail.Assigned);
            Assert.Contains(mission.Mails, mail => mail.Trigger == MissionMail.Solved);
            Assert.All(mission.Mails, mail =>
            {
                Assert.False(string.IsNullOrWhiteSpace(mail.From), mission.Id);
                Assert.False(string.IsNullOrWhiteSpace(mail.Subject.Get()), mission.Id);
                Assert.False(string.IsNullOrWhiteSpace(mail.Body.Get()), mission.Id);
            });
        }
    }

    [Fact]
    public void ANewGame_StartsWithTheWelcomeMail_Unread()
    {
        var state = new GameState(Catalog);

        var mail = Assert.Single(state.Inbox);
        Assert.Equal("first-steps/assigned", mail.Id);
        Assert.True(mail.IsAssignment);
        Assert.Equal(1, state.UnreadMailCount);
    }

    [Fact]
    public void SolvingAMission_BringsTheReplyAndTheNextJob_NewestFirst()
    {
        var state = new GameState(Catalog);
        state.SubmitFlag(Catalog.Missions[0].Flag);

        Assert.Equal(["sloppy-intern/assigned", "first-steps/solved", "first-steps/assigned"], state.Inbox.Select(mail => mail.Id));
        Assert.Equal(3, state.UnreadMailCount);
    }

    [Fact]
    public void ReadMails_AreRemembered()
    {
        var state = new GameState(Catalog);
        var welcome = state.Inbox[0];

        Assert.True(state.MarkRead(welcome));
        Assert.False(state.MarkRead(welcome));
        Assert.Equal(0, state.UnreadMailCount);

        var restored = new GameState(Catalog, GameState.DeserializeSnapshot(GameState.SerializeSnapshot(state.ToSnapshot())));
        Assert.True(restored.IsRead(restored.Inbox[0]));
    }

    [Fact]
    public void SavesFromBeforeTheMailApp_StartWithEverythingRead()
    {
        var snapshot = new GameProgressSnapshot
        {
            ActiveMissionId = "needle-in-the-log-stack",
            Missions =
            {
                ["sloppy-intern"] = new MissionProgressSnapshot { Completed = true, PointsEarned = 100 },
                ["hidden-in-the-shadows"] = new MissionProgressSnapshot { Completed = true, PointsEarned = 200 },
            },
        };

        var state = new GameState(Catalog, snapshot);

        Assert.NotEmpty(state.Inbox);
        Assert.Equal(0, state.UnreadMailCount);
    }

    [Theory]
    [InlineData("""[ { "trigger": "assigned", "from": "a", "subject": "s", "body": "b" }, { "trigger": "assigned", "from": "a", "subject": "s", "body": "b" } ]""", "Only one")]
    [InlineData("""[ { "trigger": "later", "from": "a", "subject": "s", "body": "b" } ]""", "trigger")]
    public void Validate_ChecksMailTriggers(string mails, string error)
    {
        var mission = MissionCatalog.Parse($$"""
            {
              "id": "mail", "title": "t", "summary": "s", "briefing": "b", "objective": "o", "debrief": "d",
              "flag": "FLAG{x}", "hints": [ "1", "2", "3" ], "mails": {{mails}},
              "fileSystem": { "files": [ { "path": "/home/user/flag.txt", "content": "FLAG{x}" } ] }
            }
            """);

        Assert.Contains(Catalog.Validate(mission), message => message.Contains(error));
    }
}
