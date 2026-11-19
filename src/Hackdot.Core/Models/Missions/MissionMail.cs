namespace Hackdot.Core.Models.Missions;

/// <summary>
/// A mail in the Mail app that belongs to a mission. It arrives when the mission is unlocked
/// (<see cref="Assigned"/> – usually the job itself) or when it is solved (<see cref="Solved"/> – someone's reaction).
/// </summary>
public sealed class MissionMail
{
    public const string Assigned = "assigned";

    public const string Solved = "solved";

    /// <summary><see cref="Assigned"/> or <see cref="Solved"/>. Each mission has at most one of each.</summary>
    public required string Trigger { get; set; }

    /// <summary>The sender's name, e.g. "Vera Lind". Names are never translated.</summary>
    public required string From { get; set; }

    /// <summary>The sender's job, e.g. "Head of Security".</summary>
    public LocalizedText? Role { get; set; }

    public required LocalizedText Subject { get; set; }

    /// <summary>Paragraphs separated by blank lines; <c>`backticks`</c> and <c>**bold**</c> work.</summary>
    public required LocalizedText Body { get; set; }
}
