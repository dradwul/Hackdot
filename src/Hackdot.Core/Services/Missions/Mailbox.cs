using Hackdot.Core.Models.Missions;

namespace Hackdot.Core.Services.Missions;

/// <summary>A mail that has arrived in the player's inbox.</summary>
/// <param name="Id">Stable id (<c>mission-id/trigger</c>), used to remember what has been read.</param>
/// <param name="Order">Arrival order: higher arrived later.</param>
public sealed record MailMessage(string Id, MissionDefinition Mission, MissionMail Mail, int Order)
{
    public bool IsAssignment => Mail.Trigger == MissionMail.Assigned;
}

public static class Mailbox
{
    public static string IdOf(MissionDefinition mission, MissionMail mail) => $"{mission.Id}/{mail.Trigger}";

    /// <summary>
    /// The mails that have arrived, newest first: a mission's "assigned" mail once it is unlocked, its "solved" mail
    /// once it is solved. Solving a mission brings its reply and the next job at the same time – the job on top.
    /// </summary>
    public static IReadOnlyList<MailMessage> Arrived(GameState state)
    {
        var messages = new List<MailMessage>();

        foreach (var mission in state.Catalog.Missions)
        {
            var index = state.Catalog.IndexOf(mission);

            foreach (var mail in mission.Mails)
            {
                var arrived = mail.Trigger switch
                {
                    MissionMail.Assigned => state.IsUnlocked(mission),
                    MissionMail.Solved => state.Progress(mission).Completed,
                    _ => false,
                };

                if (arrived)
                {
                    var order = (index * 2) + (mail.Trigger == MissionMail.Solved ? 1 : 0);
                    messages.Add(new MailMessage(IdOf(mission, mail), mission, mail, order));
                }
            }
        }

        return [.. messages.OrderByDescending(message => message.Order)];
    }
}
