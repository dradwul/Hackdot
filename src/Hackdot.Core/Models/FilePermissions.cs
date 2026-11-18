using System.Text.RegularExpressions;

namespace Hackdot.Core.Models;

/// <summary>
/// Simplified Unix permission bits (owner/group/other × read/write/execute) plus the special bits
/// (set-user-ID, set-group-ID, sticky). Modes are stored as integers and written as octal strings in JSON,
/// e.g. <c>"755"</c> or <c>"4755"</c>.
/// </summary>
public static partial class FilePermissions
{
    public const int Read = 4;
    public const int Write = 2;
    public const int Execute = 1;

    /// <summary>Set-user-ID (4000): a program with this bit runs with its owner's permissions.</summary>
    public const int SetUid = 0b100_000_000_000;

    /// <summary>Set-group-ID (2000).</summary>
    public const int SetGid = 0b010_000_000_000;

    /// <summary>Sticky bit (1000), e.g. on <c>/tmp</c>.</summary>
    public const int Sticky = 0b001_000_000_000;

    /// <summary>rwxr-xr-x (755)</summary>
    public const int DefaultDirectory = 0b111_101_101;

    /// <summary>rw-r--r-- (644)</summary>
    public const int DefaultFile = 0b110_100_100;

    /// <summary>rwx------ (700)</summary>
    public const int PrivateDirectory = 0b111_000_000;

    private const int AllBits = 0b111_111_111_111;

    /// <summary>Parses an octal mode such as <c>"644"</c>, <c>"0755"</c> or <c>"4755"</c>.</summary>
    /// <exception cref="FormatException">The value is not a 3–4 digit octal number.</exception>
    public static int Parse(string octal)
    {
        var value = octal?.Trim() ?? "";

        if (value.Length is < 3 or > 4 || value.Any(c => c is < '0' or > '7'))
        {
            throw new FormatException($"Invalid file mode '{octal}'. Use 3–4 octal digits, e.g. \"644\" or \"755\".");
        }

        return Convert.ToInt32(value, 8) & AllBits;
    }

    public static string ToOctal(int mode) => Convert.ToString(mode & AllBits, 8).PadLeft(3, '0');

    public static bool HasSetUid(int mode) => (mode & SetUid) != 0;

    /// <summary>
    /// Formats a mode the way <c>ls -l</c> does, e.g. <c>rwxr-xr-x</c>. Special bits replace the x:
    /// <c>s</c>/<c>S</c> for set-user-ID and set-group-ID, <c>t</c>/<c>T</c> for sticky (lower case when x is set too).
    /// </summary>
    public static string ToSymbolic(int mode)
    {
        Span<char> chars = stackalloc char[9];

        for (var i = 0; i < 3; i++)
        {
            var bits = (mode >> (6 - (3 * i))) & 0b111;
            chars[3 * i] = (bits & Read) != 0 ? 'r' : '-';
            chars[(3 * i) + 1] = (bits & Write) != 0 ? 'w' : '-';
            chars[(3 * i) + 2] = (bits & Execute) != 0 ? 'x' : '-';
        }

        Special(chars, 2, SetUid, 's');
        Special(chars, 5, SetGid, 's');
        Special(chars, 8, Sticky, 't');

        return new string(chars);

        void Special(Span<char> text, int index, int bit, char letter)
        {
            if ((mode & bit) != 0)
            {
                text[index] = text[index] == 'x' ? letter : char.ToUpperInvariant(letter);
            }
        }
    }

    /// <summary>
    /// Applies a <c>chmod</c> mode to <paramref name="current"/>: octal (<c>600</c>, <c>4755</c>) or symbolic
    /// clauses separated by commas, like <c>u+r</c>, <c>go-w</c>, <c>a=rx</c>, <c>+x</c> or <c>u+s</c>.
    /// </summary>
    public static bool TryApply(string mode, int current, out int result)
    {
        result = current;

        if (mode.Length is >= 1 and <= 4 && mode.All(c => c is >= '0' and <= '7'))
        {
            result = Convert.ToInt32(mode, 8) & AllBits;
            return true;
        }

        foreach (var clause in mode.Split(','))
        {
            var match = SymbolicClause().Match(clause);

            if (!match.Success)
            {
                return false;
            }

            var who = match.Groups["who"].Value;
            if (who.Length == 0 || who.Contains('a'))
            {
                who = "ugo";
            }

            var bits = 0;
            foreach (var letter in match.Groups["perms"].Value)
            {
                foreach (var target in who)
                {
                    var shift = target switch { 'u' => 6, 'g' => 3, _ => 0 };
                    bits |= letter switch
                    {
                        'r' => Read << shift,
                        'w' => Write << shift,
                        'x' => Execute << shift,
                        's' when target == 'u' => SetUid,
                        's' when target == 'g' => SetGid,
                        't' when target == 'o' => Sticky,
                        _ => 0,
                    };
                }
            }

            // '=' replaces the permissions of the named classes and leaves the others alone.
            var affected = 0;
            foreach (var target in who)
            {
                affected |= target switch
                {
                    'u' => (0b111 << 6) | SetUid,
                    'g' => (0b111 << 3) | SetGid,
                    _ => 0b111 | Sticky,
                };
            }

            result = match.Groups["op"].Value switch
            {
                "+" => result | bits,
                "-" => result & ~bits,
                _ => (result & ~affected) | bits,
            };
        }

        return true;
    }

    [GeneratedRegex("^(?<who>[ugoa]*)(?<op>[+=-])(?<perms>[rwxst]*)$")]
    private static partial Regex SymbolicClause();
}
