using Hackdot.Core.Models.Missions;

namespace Hackdot.Core.Models.Guide;

/// <summary>
/// The Guide app's "Learn" content (<c>Data/Guide/topics.json</c>). The command reference is not stored
/// here – it is built from the terminal's own commands and help texts, so it can never go out of date.
/// </summary>
public sealed class GuideDefinition
{
    public int SchemaVersion { get; set; } = 1;

    public List<GuideTopic> Topics { get; set; } = [];
}

public sealed class GuideTopic
{
    /// <summary>Stable id; mission skills can link to it (e.g. <c>pipes</c>).</summary>
    public required string Id { get; set; }

    public required LocalizedText Title { get; set; }

    /// <summary>Paragraphs separated by blank lines; <c>`backticks`</c> mark commands and paths.</summary>
    public required LocalizedText Body { get; set; }

    /// <summary>Commands to try. They must work on the base file system.</summary>
    public List<GuideExample> Examples { get; set; } = [];
}

public sealed class GuideExample
{
    /// <summary>The command line. Never translated.</summary>
    public required string Command { get; set; }

    public required LocalizedText Description { get; set; }
}
