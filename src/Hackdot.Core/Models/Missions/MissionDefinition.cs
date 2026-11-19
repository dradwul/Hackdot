using System.Text.Json.Serialization;
using Hackdot.Core.Models.Definitions;

namespace Hackdot.Core.Models.Missions;

[JsonConverter(typeof(JsonStringEnumConverter<MissionDifficulty>))]
public enum MissionDifficulty
{
    Easy,
    Medium,
    Hard,
}

/// <summary>
/// A mission as defined in <c>Data/Missions/*.json</c>. All player-facing texts are <see cref="LocalizedText"/>.
/// Text may contain <c>`backticks`</c> to mark commands and paths, and blank lines to separate paragraphs.
/// </summary>
public sealed class MissionDefinition
{
    public const int HintCount = 3;

    public int SchemaVersion { get; set; } = 1;

    /// <summary>Stable id used for saved progress, e.g. <c>sloppy-intern</c>. Never change it once released.</summary>
    public required string Id { get; set; }

    /// <summary>Position in the mission list; missions unlock in this order.</summary>
    public int Order { get; set; }

    /// <summary>The chapter the mission belongs to (1, 2, 3, …), used for chapter achievements.</summary>
    public int Chapter { get; set; } = 1;

    public MissionDifficulty Difficulty { get; set; }

    /// <summary>Points for solving the mission without hints.</summary>
    public int Points { get; set; } = 100;

    public required LocalizedText Title { get; set; }

    /// <summary>One or two sentences for the mission list.</summary>
    public required LocalizedText Summary { get; set; }

    /// <summary>The story and context shown in the Mission app.</summary>
    public required LocalizedText Briefing { get; set; }

    /// <summary>What the player has to do, in one sentence.</summary>
    public required LocalizedText Objective { get; set; }

    /// <summary>Shown after solving: what the player just learned.</summary>
    public required LocalizedText Debrief { get; set; }

    /// <summary>The flag to submit, e.g. <c>FLAG{n3v3r_wr1t3_p4ssw0rds_d0wn}</c>. Must exist in a readable file.</summary>
    public required string Flag { get; set; }

    /// <summary>
    /// Exactly three hints, from vague to specific:
    /// 1) the concept, 2) the command or tool to use, 3) exactly where the flag is.
    /// </summary>
    public List<LocalizedText> Hints { get; set; } = [];

    /// <summary>Commands the mission practises, shown as chips, e.g. <c>ls</c>, <c>cat</c>.</summary>
    public List<string> Skills { get; set; } = [];

    /// <summary>The technique cards (ids in <c>Data/Guide/cards.json</c>) the player collects by solving this mission.</summary>
    public List<string> Techniques { get; set; } = [];

    /// <summary>Files on top of the shared base system.</summary>
    public FileSystemDefinition FileSystem { get; set; } = new();
}
