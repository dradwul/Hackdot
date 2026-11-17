using Hackdot.Core.Services;
using Hackdot.Core.Services.Missions;
using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Tests;

public class GuideTests
{
    private static readonly Models.Guide.GuideDefinition Guide = GuideCatalog.LoadEmbedded();

    [Fact]
    public void Topics_LoadWithUniqueIds()
    {
        Assert.NotEmpty(Guide.Topics);
        Assert.Equal(Guide.Topics.Count, Guide.Topics.Select(topic => topic.Id).Distinct().Count());
        Assert.All(Guide.Topics, topic => Assert.False(string.IsNullOrWhiteSpace(topic.Body.Get())));
    }

    [Fact]
    public void Examples_RunWithoutErrorsOnTheBaseSystem()
    {
        foreach (var example in Guide.Topics.SelectMany(topic => topic.Examples))
        {
            // A fresh terminal per example, so 'cd' in one example cannot break the next.
            var result = TestTerminal.Create().Execute(example.Command);

            Assert.True(
                result.Lines.All(line => !line.IsError),
                $"'{example.Command}' printed an error: {string.Join(" / ", result.Lines.Where(l => l.IsError).Select(l => l.Text))}");
        }
    }

    [Fact]
    public void MissionSkills_LinkToACommandOrATopic()
    {
        var commands = CommandInterpreter.DefaultCommands().Select(command => command.Name).ToHashSet();
        var topics = Guide.Topics.Select(topic => topic.Id).ToHashSet();

        foreach (var skill in MissionCatalog.LoadEmbedded().Missions.SelectMany(mission => mission.Skills))
        {
            var command = skill.Split(' ')[0];
            Assert.True(commands.Contains(command) || topics.Contains(skill), $"Skill '{skill}' has no guide entry.");
        }
    }
}
