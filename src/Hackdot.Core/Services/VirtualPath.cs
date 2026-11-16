namespace Hackdot.Core.Services;

/// <summary>
/// Sökvägshjälp för det virtuella filsystemet. Alla sökvägar är Unix-liknande
/// (<c>/</c> som separator) och returneras normaliserade och absoluta.
/// </summary>
public static class VirtualPath
{
    public const char Separator = '/';
    public const string Root = "/";
    public const string DefaultHome = "/home/user";

    /// <summary>
    /// Löser <paramref name="path"/> relativt <paramref name="currentDirectory"/>.
    /// Stöder absoluta sökvägar, <c>.</c>, <c>..</c> och <c>~</c>.
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

    /// <summary>Tar bort tomma segment, <c>.</c> och löser upp <c>..</c>. Går aldrig ovanför roten.</summary>
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

    /// <summary>Filer och mappar som börjar med punkt är dolda (som i Linux).</summary>
    public static bool IsHidden(string name) =>
        name.Length > 1 && name[0] == '.' && name != "..";

    /// <summary>Visar hemkatalogen som <c>~</c>, t.ex. <c>/home/user/Documents</c> → <c>~/Documents</c>.</summary>
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
