using System.Globalization;

namespace Hackdot.Core.Services.Terminal.Commands;

/// <summary>Shared helpers for <c>head</c> and <c>tail</c>.</summary>
internal static class LineSelection
{
    public const int DefaultCount = 10;

    public static void Run(CommandContext context, Func<IReadOnlyList<string>, int, IEnumerable<string>> select)
    {
        if (!context.TryParseOptions("", out var arguments, valueFlags: "n"))
        {
            return;
        }

        var count = DefaultCount;

        if (arguments.Value('n') is { } value
            && (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out count)))
        {
            context.Error("Error.InvalidLineCount", context.Name, value);
            return;
        }

        var inputs = context.ReadInputs(arguments.Operands);
        var printHeaders = inputs.Count > 1;

        for (var i = 0; i < inputs.Count; i++)
        {
            if (printHeaders)
            {
                if (i > 0)
                {
                    context.Output.WriteLine();
                }

                context.Output.WriteLine($"==> {inputs[i].Label} <==", OutputStyle.Muted);
            }

            foreach (var line in select(TextHelpers.SplitLines(inputs[i].Content), count))
            {
                context.Output.WriteLine(line);
            }
        }
    }
}

public sealed class HeadCommand : ICommand
{
    public string Name => "head";

    public string Usage => "head [-n lines] [file ...]";

    public IReadOnlyList<string> Examples { get; } = ["head /var/log/syslog", "head -n 3 /etc/passwd"];

    public void Execute(CommandContext context) => LineSelection.Run(context, (lines, count) => lines.Take(count));
}

public sealed class TailCommand : ICommand
{
    public string Name => "tail";

    public string Usage => "tail [-n lines] [file ...]";

    public IReadOnlyList<string> Examples { get; } = ["tail /var/log/syslog", "tail -n 1 /etc/passwd"];

    public void Execute(CommandContext context) => LineSelection.Run(context, (lines, count) => lines.TakeLast(count));
}

public sealed class WcCommand : ICommand
{
    public string Name => "wc";

    public string Usage => "wc [-l] [-w] [-c] [file ...]";

    public IReadOnlyList<string> Examples { get; } = ["wc -l /var/log/syslog", "wc /etc/passwd", "ls /etc | wc -l"];

    public void Execute(CommandContext context)
    {
        if (!context.TryParseOptions("lwc", out var arguments))
        {
            return;
        }

        // No flags = all three counts, like the real wc.
        var all = arguments.Flags.Count == 0;
        var columns = new[] { 'l', 'w', 'c' }.Where(flag => all || arguments.Has(flag)).ToList();

        var rows = context.ReadInputs(arguments.Operands)
            .Select(input => (input.Label, Counts: Count(input.Content)))
            .ToList();

        if (rows.Count == 0)
        {
            return;
        }

        if (rows.Count > 1)
        {
            rows.Add(("total", new Counts(rows.Sum(r => r.Counts.Lines), rows.Sum(r => r.Counts.Words), rows.Sum(r => r.Counts.Bytes))));
        }

        // A single number on its own needs no padding; otherwise line the columns up.
        var padded = columns.Count > 1 || rows.Count > 1;
        var width = padded ? rows.Max(row => columns.Max(column => Format(row.Counts.Get(column)).Length)) : 0;

        foreach (var (label, counts) in rows)
        {
            var numbers = string.Join(' ', columns.Select(column => Format(counts.Get(column)).PadLeft(width)));
            context.Output.WriteLine(label is null ? numbers : $"{numbers} {label}");
        }
    }

    private static Counts Count(string content) => new(
        TextHelpers.SplitLines(content).Count,
        content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length,
        System.Text.Encoding.UTF8.GetByteCount(content));

    private static string Format(long value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed record Counts(long Lines, long Words, long Bytes)
    {
        public long Get(char column) => column switch
        {
            'l' => Lines,
            'w' => Words,
            _ => Bytes,
        };
    }
}

public sealed class SortCommand : ICommand
{
    public string Name => "sort";

    public string Usage => "sort [-r] [-n] [-u] [file ...]";

    public IReadOnlyList<string> Examples { get; } = ["sort /etc/passwd", "sort -r /etc/passwd", "cut -d: -f7 /etc/passwd | sort | uniq -c | sort -rn"];

    public void Execute(CommandContext context)
    {
        if (!context.TryParseOptions("rnu", out var arguments))
        {
            return;
        }

        var lines = context.ReadInputs(arguments.Operands)
            .SelectMany(input => TextHelpers.SplitLines(input.Content))
            .ToList();

        // Plain byte order (like LC_ALL=C): predictable, and capital letters come before small ones.
        IComparer<string> comparer = arguments.Has('n') ? NumericComparer.Instance : StringComparer.Ordinal;
        lines.Sort(comparer);

        if (arguments.Has('r'))
        {
            lines.Reverse();
        }

        IEnumerable<string> result = arguments.Has('u') ? lines.Distinct(StringComparer.Ordinal) : lines;

        foreach (var line in result)
        {
            context.Output.WriteLine(line);
        }
    }

    /// <summary>Compares the number at the start of each line (after spaces); lines without one count as 0.</summary>
    private sealed class NumericComparer : IComparer<string>
    {
        public static NumericComparer Instance { get; } = new();

        public int Compare(string? x, string? y)
        {
            var result = LeadingNumber(x).CompareTo(LeadingNumber(y));
            return result != 0 ? result : string.CompareOrdinal(x, y);
        }

        private static double LeadingNumber(string? line)
        {
            var text = (line ?? "").TrimStart();
            var length = 0;

            while (length < text.Length && (char.IsAsciiDigit(text[length]) || text[length] == '.' || (length == 0 && text[0] == '-')))
            {
                length++;
            }

            return double.TryParse(text.AsSpan(0, length), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var number) ? number : 0;
        }
    }
}

public sealed class UniqCommand : ICommand
{
    public string Name => "uniq";

    public string Usage => "uniq [-c] [file]";

    public IReadOnlyList<string> Examples { get; } = ["cut -d: -f7 /etc/passwd | sort | uniq", "cut -d: -f7 /etc/passwd | sort | uniq -c"];

    public void Execute(CommandContext context)
    {
        if (!context.TryParseOptions("c", out var arguments))
        {
            return;
        }

        if (arguments.Operands.Count > 1)
        {
            context.Error("Error.TooManyArguments", Name);
            return;
        }

        var lines = context.ReadInputs(arguments.Operands).SelectMany(input => TextHelpers.SplitLines(input.Content));
        var counted = arguments.Has('c');
        string? previous = null;
        var count = 0;

        foreach (var line in lines)
        {
            if (line == previous)
            {
                count++;
                continue;
            }

            Write(previous, count);
            previous = line;
            count = 1;
        }

        Write(previous, count);

        void Write(string? line, int times)
        {
            if (line is null)
            {
                return;
            }

            if (counted)
            {
                context.Output.Write($"{times,7} ", OutputStyle.Success);
            }

            context.Output.WriteLine(line);
        }
    }
}

public sealed class CutCommand : ICommand
{
    public string Name => "cut";

    public string Usage => "cut -d <delimiter> -f <fields> [file ...]";

    public IReadOnlyList<string> Examples { get; } = ["cut -d: -f1 /etc/passwd", "cut -d: -f1,6 /etc/passwd", "cut -d' ' -f5 /var/log/syslog"];

    public void Execute(CommandContext context)
    {
        if (!context.TryParseOptions("", out var arguments, valueFlags: "df"))
        {
            return;
        }

        if (arguments.Value('f') is not { } fieldList)
        {
            context.Error("Error.CutNeedsFields");
            return;
        }

        if (!TryParseFields(fieldList, out var ranges))
        {
            context.Error("Error.CutInvalidFields", fieldList);
            return;
        }

        // Without -d, cut splits on tabs – just like the real one.
        var delimiter = arguments.Value('d') ?? "\t";

        if (delimiter.Length != 1)
        {
            context.Error("Error.CutInvalidDelimiter");
            return;
        }

        foreach (var input in context.ReadInputs(arguments.Operands))
        {
            foreach (var line in TextHelpers.SplitLines(input.Content))
            {
                // Lines without the delimiter are printed unchanged (again like the real cut).
                if (!line.Contains(delimiter[0]))
                {
                    context.Output.WriteLine(line);
                    continue;
                }

                var fields = line.Split(delimiter[0]);
                var selected = Enumerable.Range(1, fields.Length)
                    .Where(number => ranges.Any(range => number >= range.From && number <= range.To))
                    .Select(number => fields[number - 1]);

                context.Output.WriteLine(string.Join(delimiter, selected));
            }
        }
    }

    /// <summary>Parses field lists like <c>1</c>, <c>1,3</c>, <c>2-4</c>, <c>3-</c> or <c>-2</c>.</summary>
    private static bool TryParseFields(string text, out List<(int From, int To)> ranges)
    {
        ranges = [];

        foreach (var part in text.Split(','))
        {
            var dash = part.IndexOf('-');
            var fromText = dash < 0 ? part : part[..dash];
            var toText = dash < 0 ? part : part[(dash + 1)..];

            var from = fromText.Length == 0 ? 1 : ParseField(fromText);
            var to = toText.Length == 0 ? int.MaxValue : ParseField(toText);

            if (from < 1 || to < from || (fromText.Length == 0 && toText.Length == 0))
            {
                return false;
            }

            ranges.Add((from, to));
        }

        return true;
    }

    private static int ParseField(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1;
}

public sealed class EchoCommand : ICommand
{
    public string Name => "echo";

    public string Usage => "echo [-n] [text ...]";

    public IReadOnlyList<string> Examples { get; } = ["echo hello", "echo hello | base64"];

    public void Execute(CommandContext context)
    {
        var words = context.Arguments;

        if (words.Count > 0 && words[0] == "-n")
        {
            context.Output.OmitFinalNewline = true;
            words = [.. words.Skip(1)];
        }

        context.Output.WriteLine(string.Join(' ', words));
    }
}

/// <summary>
/// Base64 turns any data into plain letters and digits – and back. It is an <em>encoding</em>, not encryption:
/// there is no key, so anyone can decode it.
/// </summary>
public sealed class Base64Command : ICommand
{
    private const int LineWidth = 76;

    public string Name => "base64";

    public string Usage => "base64 [-d] [file]";

    public IReadOnlyList<string> Examples { get; } = ["echo hello | base64", "echo aGVsbG8K | base64 -d"];

    public void Execute(CommandContext context)
    {
        if (!context.TryParseOptions("d", out var arguments))
        {
            return;
        }

        if (arguments.Operands.Count > 1)
        {
            context.Error("Error.TooManyArguments", Name);
            return;
        }

        foreach (var input in context.ReadInputs(arguments.Operands))
        {
            if (arguments.Has('d'))
            {
                Decode(context, input.Content);
            }
            else
            {
                Encode(context.Output, input.Content);
            }
        }
    }

    private static void Encode(CommandOutput output, string content)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content));

        for (var start = 0; start < encoded.Length; start += LineWidth)
        {
            output.WriteLine(encoded.Substring(start, Math.Min(LineWidth, encoded.Length - start)));
        }
    }

    private static void Decode(CommandContext context, string content)
    {
        var text = new string([.. content.Where(c => !char.IsWhiteSpace(c))]);

        // Copied values often lose their '=' padding at the end – be forgiving and put it back.
        if (text.Length % 4 is 2 or 3)
        {
            text = text.PadRight(text.Length + 4 - text.Length % 4, '=');
        }

        var buffer = new byte[text.Length];

        if (text.Length == 0 || !Convert.TryFromBase64String(text, buffer, out var length))
        {
            context.Error("Error.Base64Invalid");
            return;
        }

        var decoded = System.Text.Encoding.UTF8.GetString(buffer, 0, length);

        foreach (var line in TextHelpers.SplitLines(decoded))
        {
            context.Output.WriteLine(line);
        }

        context.Output.OmitFinalNewline = !decoded.EndsWith('\n');
    }
}

public sealed class HistoryCommand : ICommand
{
    public string Name => "history";

    public string Usage => "history [-c] [count]";

    public IReadOnlyList<string> Examples { get; } = ["history", "history 5", "history | grep cat"];

    public void Execute(CommandContext context)
    {
        if (!context.TryParseOptions("c", out var arguments))
        {
            return;
        }

        if (arguments.Has('c'))
        {
            context.Interpreter.ClearHistory();
            return;
        }

        var history = context.Interpreter.History;
        var count = history.Count;

        if (arguments.Operands.Count > 0
            && !int.TryParse(arguments.Operands[0], NumberStyles.None, CultureInfo.InvariantCulture, out count))
        {
            context.Error("Error.InvalidLineCount", Name, arguments.Operands[0]);
            return;
        }

        for (var i = Math.Max(0, history.Count - count); i < history.Count; i++)
        {
            context.Output.Write($"{i + 1,5}  ", OutputStyle.Muted).WriteLine(history[i]);
        }
    }
}
