using System.Text.Json;
using Hackdot.Core.Models;
using Hackdot.Core.Models.Definitions;

namespace Hackdot.Core.Services;

/// <summary>Builds a <see cref="VirtualFileSystem"/> from one or more JSON definitions.</summary>
public static class FileSystemLoader
{
    /// <summary>The shared Linux-like base system every mission starts from.</summary>
    public const string BaseFileSystem = "FileSystems/base.json";

    public static FileSystemDefinition Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, CoreJsonContext.Default.FileSystemDefinition)
                ?? throw new InvalidDataException("The file system definition is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid file system definition: {ex.Message}", ex);
        }
    }

    public static FileSystemDefinition LoadEmbedded(string relativePath) => Parse(EmbeddedContent.Read(relativePath));

    /// <summary>Creates the default file system (the base layer only).</summary>
    public static VirtualFileSystem CreateDefault() => Build(LoadEmbedded(BaseFileSystem));

    /// <summary>Applies the layers in order. Later layers add to – and override – earlier ones.</summary>
    public static VirtualFileSystem Build(params FileSystemDefinition[] layers)
    {
        var fileSystem = new VirtualFileSystem();

        foreach (var layer in layers)
        {
            Apply(fileSystem, layer);
        }

        return fileSystem;
    }

    private static void Apply(VirtualFileSystem fileSystem, FileSystemDefinition layer)
    {
        if (layer.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported file system schema version {layer.SchemaVersion}.");
        }

        if (!string.IsNullOrWhiteSpace(layer.Hostname))
        {
            fileSystem.Hostname = layer.Hostname;
        }

        foreach (var user in layer.Users)
        {
            var home = user.Home ?? (user.Name == VirtualUser.RootName ? "/root" : $"/home/{user.Name}");
            fileSystem.AddUser(new VirtualUser(user.Name, VirtualPath.Normalize(home), user.Password));
        }

        if (layer.DefaultUser is not null)
        {
            fileSystem.DefaultUser = RequireUser(fileSystem, layer.DefaultUser, "defaultUser");
        }

        foreach (var directory in layer.Directories)
        {
            var created = fileSystem.CreateDirectory(
                directory.Path,
                OptionalUser(fileSystem, directory.Owner, directory.Path),
                ParseMode(directory.Mode, directory.Path));

            if (directory.Modified is { } modified)
            {
                created.Modified = modified;
            }
        }

        foreach (var file in layer.Files)
        {
            var sources = (file.Content is not null ? 1 : 0) + (file.Lines is not null ? 1 : 0) + (file.Generate is not null ? 1 : 0);

            if (sources > 1)
            {
                throw new InvalidDataException($"'{file.Path}': use either 'content', 'lines' or 'generate', not several.");
            }

            var content = file switch
            {
                { Lines: not null } => string.Join('\n', file.Lines),
                { Generate: not null } => ContentGenerator.Generate(file.Generate, fileSystem.Hostname),
                _ => file.Content ?? "",
            };

            fileSystem.WriteFile(
                file.Path,
                content,
                OptionalUser(fileSystem, file.Owner, file.Path),
                ParseMode(file.Mode, file.Path),
                file.Modified);
        }
    }

    private static string RequireUser(VirtualFileSystem fileSystem, string name, string context) =>
        fileSystem.FindUser(name)?.Name
        ?? throw new InvalidDataException($"{context}: unknown user '{name}'. Add it to 'users' first.");

    private static string? OptionalUser(VirtualFileSystem fileSystem, string? name, string path) =>
        name is null ? null : RequireUser(fileSystem, name, $"'{path}'");

    private static int? ParseMode(string? mode, string path)
    {
        if (mode is null)
        {
            return null;
        }

        try
        {
            return FilePermissions.Parse(mode);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"'{path}': {ex.Message}", ex);
        }
    }
}
