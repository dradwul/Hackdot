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
        var flagFiles = allFiles.Where(file => file.Content.Contains(mission.Flag, StringComparison.Ordinal)).ToList();

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
    /// The users a player can become: the starting user, accounts without a password, and accounts whose
    /// password appears in a file the starting user can read (the "hidden password" mission pattern).
    /// </summary>
    private static IEnumerable<ShellSession> ReachableUsers(VirtualFileSystem fileSystem, List<VirtualFile> allFiles)
    {
        var start = new ShellSession(fileSystem);
        yield return start;

        var readableText = allFiles.Where(file => CanReadFile(start, file)).Select(file => file.Content).ToList();

        foreach (var user in fileSystem.Users.Where(user => user != start.User && user.CanLogIn))
        {
            if (!user.RequiresPassword || readableText.Any(text => text.Contains(user.Password!, StringComparison.Ordinal)))
            {
                yield return new ShellSession(fileSystem, user.Name);
            }
        }
    }

    private static bool CanReadFile(ShellSession session, VirtualFile file) => session.CanRead(file) && CanReach(session, file);

    private static bool CanReach(ShellSession session, VirtualFile file)
    {
        for (var directory = file.Parent; directory is not null; directory = directory.Parent)
        {
            if (!session.CanEnter(directory))
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^FLAG\{[^{}\s]+\}$")]
    private static partial Regex FlagPattern();
}
