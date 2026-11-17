using Hackdot.Core.Models;

namespace Hackdot.Core.Services.Terminal;

/// <summary>
/// Finds what the player probably meant when a path only differs in upper/lower case,
/// e.g. <c>documents/notes.TXT</c> → <c>Documents/notes.txt</c>. Linux names are case-sensitive,
/// so we don't silently fix it – we suggest it, which teaches the rule.
/// </summary>
public static class PathSuggester
{
    /// <summary>
    /// Returns the typed path with each segment's case corrected, written the same way the player wrote it
    /// (relative, absolute or starting with <c>~</c>). Returns <c>null</c> if there is no unambiguous match.
    /// </summary>
    public static string? Suggest(ShellSession session, string typedPath)
    {
        if (string.IsNullOrWhiteSpace(typedPath))
        {
            return null;
        }

        var path = typedPath.Trim();
        var (prefix, startPath) = path switch
        {
            _ when path.StartsWith('/') => ("/", VirtualPath.Root),
            "~" => ("~", session.User.Home),
            _ when path.StartsWith("~/", StringComparison.Ordinal) => ("~/", session.User.Home),
            _ => ("", session.CurrentDirectory),
        };

        if (session.FileSystem.GetDirectory(startPath) is not { } current)
        {
            return null;
        }

        var rest = path[prefix.Length..];
        var corrected = new List<string>();
        var changed = false;
        var reachedFile = false;

        foreach (var segment in rest.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            // Only the last segment of a path can be a file.
            if (reachedFile)
            {
                return null;
            }

            if (segment is "." or "..")
            {
                current = segment == ".." ? current.Parent ?? current : current;
                corrected.Add(segment);
                continue;
            }

            var node = current.GetChild(segment);

            if (node is null)
            {
                // Looking through a folder's names needs read permission – never leak names the player can't list.
                if (!session.CanRead(current))
                {
                    return null;
                }

                var matches = current.Children
                    .Where(child => string.Equals(child.Name, segment, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count != 1)
                {
                    return null;
                }

                node = matches[0];
                changed = true;
            }

            corrected.Add(node.Name);

            if (node is VirtualDirectory directory)
            {
                current = directory;
            }
            else
            {
                reachedFile = true;
            }
        }

        if (!changed)
        {
            return null;
        }

        var trailingSlash = rest.EndsWith('/') ? "/" : "";
        return prefix + string.Join('/', corrected) + trailingSlash;
    }
}
