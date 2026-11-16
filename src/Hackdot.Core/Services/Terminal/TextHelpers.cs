namespace Hackdot.Core.Services.Terminal;

internal static class TextHelpers
{
    /// <summary>Splits text into lines. A trailing newline does not produce an extra empty line.</summary>
    public static IReadOnlyList<string> SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    /// <summary>Joins a path as typed by the player with a child name.</summary>
    public static string JoinDisplayPath(string parent, string name) =>
        parent.EndsWith('/') ? parent + name : $"{parent}/{name}";
}

/// <summary>Shell-style wildcards: <c>*</c> matches anything, <c>?</c> matches one character.</summary>
public static class Glob
{
    public static bool HasWildcards(string pattern) => pattern.AsSpan().IndexOfAny('*', '?') >= 0;

    public static bool IsMatch(string text, string pattern, bool ignoreCase = false)
    {
        int t = 0, p = 0, starPattern = -1, starText = 0;

        while (t < text.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || CharEquals(pattern[p], text[t], ignoreCase)))
            {
                t++;
                p++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                starPattern = p++;
                starText = t;
            }
            else if (starPattern >= 0)
            {
                p = starPattern + 1;
                t = ++starText;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }

    private static bool CharEquals(char a, char b, bool ignoreCase) =>
        ignoreCase ? char.ToLowerInvariant(a) == char.ToLowerInvariant(b) : a == b;
}
