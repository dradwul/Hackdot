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

    /// <summary>The file content. Use exactly one of <see cref="Content"/>, <see cref="Lines"/> or <see cref="Generate"/>.</summary>
    public string? Content { get; set; }

    /// <summary>The file content as lines – easier to read and write in JSON for longer files.</summary>
    public List<string>? Lines { get; set; }

    /// <summary>Generated content, e.g. a server log with thousands of lines.</summary>
    public GeneratorDefinition? Generate { get; set; }

    public string? Owner { get; set; }

    /// <summary>Octal mode, e.g. <c>"644"</c>.</summary>
    public string? Mode { get; set; }

    public DateTime? Modified { get; set; }
}

/// <summary>
/// Generates long, realistic file content deterministically (same seed = same file), so mission files
/// stay small. Extra lines – like the one with the flag – are inserted at fixed positions.
/// </summary>
public sealed class GeneratorDefinition
{
    /// <summary>
    /// The kind of content. Supported: <c>authLog</c> (SSH logins, like <c>/var/log/auth.log</c>) and
    /// <c>accessLog</c> (web server requests, like <c>/var/log/nginx/access.log</c>).
    /// </summary>
    public required string Kind { get; set; }

    public int Lines { get; set; } = 1000;

    public int Seed { get; set; } = 1;

    public List<GeneratedLine> Inserts { get; set; } = [];

    /// <summary>
    /// Lines that keep coming back at random positions, e.g. an attacker hammering the login page.
    /// Unlike <see cref="Inserts"/> they are meant to be counted, not found once.
    /// </summary>
    public List<GeneratedActor> Actors { get; set; } = [];
}

public sealed class GeneratedActor
{
    /// <summary>The line. <c>{timestamp}</c>, <c>{host}</c> and <c>{port}</c> are filled in for every line.</summary>
    public required string Text { get; set; }

    /// <summary>How often the line appears, in percent of all generated lines.</summary>
    public int Percent { get; set; }
}

public sealed class GeneratedLine
{
    /// <summary>1-based line number where the text is inserted.</summary>
    public int At { get; set; }

    /// <summary>The line. <c>{timestamp}</c> and <c>{host}</c> are replaced to blend in with the surrounding lines.</summary>
    public required string Text { get; set; }
}
