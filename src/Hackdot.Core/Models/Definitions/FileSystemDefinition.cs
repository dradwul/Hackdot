namespace Hackdot.Core.Models.Definitions;

/// <summary>
/// JSON description of a file system. Several definitions can be layered on top of each other
/// (e.g. the shared base system + a mission's own files); later layers win.
/// </summary>
/// <example>
/// <code>
/// {
///   "hostname": "hackos",
///   "users": [ { "name": "user", "home": "/home/user" } ],
///   "defaultUser": "user",
///   "directories": [ { "path": "/root", "owner": "root", "mode": "700" } ],
///   "files": [
///     { "path": "/home/user/notes.txt", "content": "Hello!" },
///     { "path": "/var/log/auth.log", "lines": [ "line 1", "line 2" ] }
///   ]
/// }
/// </code>
/// </example>
public sealed class FileSystemDefinition
{
    public int SchemaVersion { get; set; } = 1;

    public string? Hostname { get; set; }

    public List<UserDefinition> Users { get; set; } = [];

    public string? DefaultUser { get; set; }

    public List<DirectoryDefinition> Directories { get; set; } = [];

    public List<FileDefinition> Files { get; set; } = [];
}

public sealed class UserDefinition
{
    public required string Name { get; set; }

    /// <summary>Defaults to <c>/home/{name}</c> (or <c>/root</c> for root).</summary>
    public string? Home { get; set; }

    public string? Password { get; set; }
}

public sealed class DirectoryDefinition
{
    public required string Path { get; set; }

    public string? Owner { get; set; }

    /// <summary>Octal mode, e.g. <c>"755"</c>.</summary>
    public string? Mode { get; set; }

    public DateTime? Modified { get; set; }
}

public sealed class FileDefinition
{
    public required string Path { get; set; }

    /// <summary>The file content. Use either this or <see cref="Lines"/>.</summary>
    public string? Content { get; set; }

    /// <summary>The file content as lines – easier to read and write in JSON for longer files.</summary>
    public List<string>? Lines { get; set; }

    public string? Owner { get; set; }

    /// <summary>Octal mode, e.g. <c>"644"</c>.</summary>
    public string? Mode { get; set; }

    public DateTime? Modified { get; set; }
}
