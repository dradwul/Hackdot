using System.Text.RegularExpressions;
using Hackdot.Core.Models.Missions;

namespace Hackdot.Core.Models.Guide;

/// <summary>The technique cards (<c>Data/Guide/cards.json</c>), collected by solving missions.</summary>
public sealed class TechniqueCardsDefinition
{
    public int SchemaVersion { get; set; } = 1;

    public List<TechniqueCard> Cards { get; set; } = [];
}

/// <summary>
/// One technique an attacker (or defender) uses – unlocked by solving a mission that lists its id in
/// <c>techniques</c>. Texts are localizable like missions; paragraphs, <c>`backticks`</c> and <c>**bold**</c> work.
/// </summary>
public sealed partial class TechniqueCard
{
    /// <summary>Stable id, referenced by missions. Never change it once released.</summary>
    public required string Id { get; set; }

    public required LocalizedText Title { get; set; }

    /// <summary>A short label like "Credential access" or "Privilege escalation".</summary>
    public required LocalizedText Category { get; set; }

    /// <summary>What the technique is, in a sentence or two.</summary>
    public required LocalizedText Summary { get; set; }

    /// <summary>Where it shows up outside the game.</summary>
    public required LocalizedText RealWorld { get; set; }

    /// <summary>How to protect against it (or detect it).</summary>
    public required LocalizedText Defense { get; set; }

    /// <summary>The MITRE ATT&amp;CK technique, e.g. <c>T1552.001</c>, if there is a matching one.</summary>
    public string? Attack { get; set; }

    /// <summary>Link to the technique on attack.mitre.org, e.g. <c>…/techniques/T1552/001/</c>.</summary>
    public string? AttackUrl => Attack is null ? null : $"https://attack.mitre.org/techniques/{Attack.Replace('.', '/')}/";

    public static bool IsValidAttackId(string id) => AttackIdPattern().IsMatch(id);

    [GeneratedRegex(@"^T\d{4}(\.\d{3})?$")]
    private static partial Regex AttackIdPattern();
}
