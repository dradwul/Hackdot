namespace Hackdot.Core.Models;

/// <summary>
/// Simplified Unix permission bits (owner/group/other × read/write/execute).
/// Modes are stored as integers and written as octal strings in JSON, e.g. <c>"755"</c>.
/// </summary>
public static class FilePermissions
{
    public const int Read = 4;
    public const int Write = 2;
    public const int Execute = 1;

    /// <summary>rwxr-xr-x (755)</summary>
    public const int DefaultDirectory = 0b111_101_101;

    /// <summary>rw-r--r-- (644)</summary>
    public const int DefaultFile = 0b110_100_100;

    /// <summary>rwx------ (700)</summary>
    public const int PrivateDirectory = 0b111_000_000;

    private const int AllBits = 0b111_111_111;

    /// <summary>Parses an octal mode such as <c>"644"</c> or <c>"0755"</c>.</summary>
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

    /// <summary>Formats a mode the way <c>ls -l</c> does, e.g. <c>rwxr-xr-x</c>.</summary>
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

        return new string(chars);
    }
}
