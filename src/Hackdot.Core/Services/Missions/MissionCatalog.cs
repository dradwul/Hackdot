using System.Text.Json;
using System.Text.RegularExpressions;
using Hackdot.Core.Models;
using Hackdot.Core.Models.Definitions;
using Hackdot.Core.Models.Missions;

namespace Hackdot.Core.Services.Missions;

/// <summary>All available missions, in play order, plus the shared base file system.</summary>
public sealed partial class MissionCatalog
{
    public const string MissionFolder = "Missions";

    private readonly FileSystemDefinition _baseFileSystem;

    public MissionCatalog(IEnumerable<MissionDefinition> missions, FileSystemDefinition baseFileSystem)
    {
        Missions = [.. missions.OrderBy(mission => mission.Order).ThenBy(mission => mission.Id, StringComparer.Ordinal)];
        _baseFileSystem = baseFileSystem;
    }

    public IReadOnlyList<MissionDefinition> Missions { get; }

    /// <summary>Loads the missions embedded in <c>Data/Missions/</c>.</summary>
    public static MissionCatalog LoadEmbedded() => new(
        EmbeddedContent.ReadAll(MissionFolder).Select(file => Parse(file.Content, file.Name)),
        FileSystemLoader.LoadEmbedded(FileSystemLoader.BaseFileSystem));

    public static MissionDefinition Parse(string json, string source = "mission")
    {
        try
        {
            return JsonSerializer.Deserialize(json, CoreJsonContext.Default.MissionDefinition)
                ?? throw new InvalidDataException($"{source}: the mission is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{source}: invalid mission definition: {ex.Message}", ex);
        }
    }

    public MissionDefinition? Find(string id) => Missions.FirstOrDefault(mission => mission.Id == id);

    public int IndexOf(MissionDefinition mission) => Missions.ToList().IndexOf(mission);

    /// <summary>A fresh file system for the mission: the base system with the mission's files on top.</summary>
    public VirtualFileSystem CreateFileSystem(MissionDefinition mission) =>
        FileSystemLoader.Build(_baseFileSystem, mission.FileSystem);

    /// <summary>
    /// Checks that a mission can actually be played: required texts, three hints and – most importantly –
    /// that the flag exists in a file the player is allowed to read.
    /// </summary>
    public IReadOnlyList<string> Validate(MissionDefinition mission)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(mission.Id) || !IdPattern().IsMatch(mission.Id))
        {
            errors.Add($"Id '{mission.Id}' must be lowercase letters, digits and dashes.");
        }

        if (!FlagPattern().IsMatch(mission.Flag))
        {
            errors.Add($"Flag '{mission.Flag}' must look like FLAG{{...}}.");
        }

        if (mission.Hints.Count != MissionDefinition.HintCount)
        {
            errors.Add($"Expected exactly {MissionDefinition.HintCount} hints, found {mission.Hints.Count}.");
        }

        if (mission.Points <= 0)
        {
            errors.Add("Points must be greater than zero.");
        }

        VirtualFileSystem fileSystem;

        try
        {
            fileSystem = CreateFileSystem(mission);
        }
        catch (InvalidDataException ex)
        {
            errors.Add(ex.Message);
            return errors;
        }

        var allFiles = VirtualFileSystem.Descendants(fileSystem.Root).OfType<VirtualFile>().ToList();
        var flagFiles = allFiles.Where(file => ContainsSecret(file.Content, mission.Flag)).ToList();

        if (flagFiles.Count == 0)
        {
            errors.Add($"The flag {mission.Flag} does not appear in any file.");
        }
        else if (!ReachableUsers(fileSystem, allFiles).Any(session => flagFiles.Any(file => CanReadFile(session, file))))
        {
            errors.Add($"The flag {mission.Flag} is only in files the player cannot read – not even as another user whose password can be found.");
        }

        return errors;
    }

    /// <summary>
    /// The users a player can become, step by step like a real attack: the starting user, accounts without a
    /// password, accounts whose password appears in a file a reachable user can read, and root when a reachable
    /// user has sudo rules. Sudo rules are assumed to be exploitable – the mission's solution test proves it.
    /// </summary>
    private static List<ShellSession> ReachableUsers(VirtualFileSystem fileSystem, List<VirtualFile> allFiles)
    {
        var reachable = new List<ShellSession> { new(fileSystem) };
        bool foundMore;

        do
        {
            foundMore = false;
            var readableText = allFiles
                .Where(file => reachable.Any(session => CanReadFile(session, file)))
                .Select(file => file.Content)
                .ToList();
            var canSudo = reachable.Any(session => Sudoers.RulesFor(fileSystem, session.User.Name).Count > 0);

            foreach (var user in fileSystem.Users.Where(user => reachable.All(session => session.User != user)))
            {
                var passwordKnown = user.CanLogIn
                    && (!user.RequiresPassword || readableText.Any(text => ContainsSecret(text, user.Password!)));

                if (passwordKnown || (user.IsRoot && canSudo))
                {
                    reachable.Add(new ShellSession(fileSystem, user.Name));
                    foundMore = true;
                }
            }
        }
        while (foundMore);

        return reachable;
    }

    // CanRead also checks that every folder on the way can be passed through.
    private static bool CanReadFile(ShellSession session, VirtualFile file) => session.CanRead(file);

    /// <summary>
    /// The secret is in the text as-is or base64-encoded once or twice – or the whole text is base64 that
    /// decodes to something containing it. The player decodes such things with <c>base64 -d</c>.
    /// </summary>
    private static bool ContainsSecret(string text, string secret, int depth = 0)
    {
        var encoded = secret;

        for (var layer = 0; layer <= 2; layer++)
        {
            if (text.Contains(encoded, StringComparison.Ordinal))
            {
                return true;
            }

            encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(encoded));
        }

        return depth < 2 && TryDecodeBase64(text, out var decoded) && ContainsSecret(decoded, secret, depth + 1);
    }

    private static bool TryDecodeBase64(string text, out string decoded)
    {
        var compact = new string([.. text.Where(c => !char.IsWhiteSpace(c))]);
        var buffer = new byte[compact.Length];
        decoded = "";

        if (compact.Length < 8 || compact.Length % 4 != 0 || !Convert.TryFromBase64String(compact, buffer, out var length))
        {
            return false;
        }

        decoded = System.Text.Encoding.UTF8.GetString(buffer, 0, length);
        return true;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^FLAG\{[^{}\s]+\}$")]
    private static partial Regex FlagPattern();
}
