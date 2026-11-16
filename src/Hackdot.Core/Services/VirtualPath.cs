namespace Hackdot.Core.Services;

/// <summary>
/// Path helpers for the virtual file system. All paths are Unix-style
/// (<c>/</c> separator) and are returned normalized and absolute.
/// </summary>
public static class VirtualPath
{
    public const char Separator = '/';
    public const string Root = "/";
    public const string DefaultHome = "/home/user";

    /// <summary>
    /// Resolves <paramref name="path"/> relative to <paramref name="currentDirectory"/>.
    /// Supports absolute paths, <c>.</c>, <c>..</c> and <c>~</c>.
    /// </summary>
    public static string Resolve(string currentDirectory, string? path, string homeDirectory = DefaultHome)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Normalize(currentDirectory);
        }

        path = path.Trim();

        var combined = path switch
        {
            "~" => homeDirectory,
            _ when path.StartsWith("~/", StringComparison.Ordinal) => homeDirectory + path[1..],
            _ when path.StartsWith(Separator) => path,
            _ => currentDirectory.TrimEnd(Separator) + Separator + path,
        };

        return Normalize(combined);
    }

    /// <summary>Removes empty segments and <c>.</c>, and resolves <c>..</c>. Never goes above the root.</summary>
    public static string Normalize(string path)
    {
        var parts = new List<string>();

        foreach (var part in path.Split(Separator, StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part)
            {
                case ".":
                    continue;
                case "..":
                    if (parts.Count > 0)
                    {
                        parts.RemoveAt(parts.Count - 1);
                    }
                    continue;
                default:
                    parts.Add(part);
                    break;
            }
        }

        return Root + string.Join(Separator, parts);
    }

    public static IReadOnlyList<string> Segments(string path) =>
        Normalize(path).Split(Separator, StringSplitOptions.RemoveEmptyEntries);

    public static string GetParent(string path)
    {
        var normalized = Normalize(path);
        var index = normalized.LastIndexOf(Separator);
        return index <= 0 ? Root : normalized[..index];
    }

    public static string GetName(string path)
    {
        var normalized = Normalize(path);
        return normalized == Root ? Root : normalized[(normalized.LastIndexOf(Separator) + 1)..];
    }

    /// <summary>Files and directories starting with a dot are hidden (like in Linux).</summary>
    public static bool IsHidden(string name) =>
        name.Length > 1 && name[0] == '.' && name != "..";

    /// <summary>Shows the home directory as <c>~</c>, e.g. <c>/home/user/Documents</c> → <c>~/Documents</c>.</summary>
    public static string ToDisplayPath(string path, string homeDirectory = DefaultHome)
    {
        var normalized = Normalize(path);

        if (normalized == homeDirectory)
        {
            return "~";
        }

        return normalized.StartsWith(homeDirectory + Separator, StringComparison.Ordinal)
            ? "~" + normalized[homeDirectory.Length..]
            : normalized;
    }
}
