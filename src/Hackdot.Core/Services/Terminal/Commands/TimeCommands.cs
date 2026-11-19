using System.Globalization;
using Hackdot.Core.Models;

namespace Hackdot.Core.Services.Terminal.Commands;

/// <summary><c>date</c> – the computer's clock, in the same format as Linux (<c>Sat Mar 14 09:21:00 UTC 2026</c>).</summary>
public sealed class DateCommand : ICommand
{
    public string Name => "date";

    public string Usage => "date";

    public IReadOnlyList<string> Examples { get; } = ["date"];

    public void Execute(CommandContext context)
    {
        var now = context.Session.FileSystem.Now;
        var day = now.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2);

        context.Output.WriteLine(
            now.ToString("ddd MMM ", CultureInfo.InvariantCulture) + day + now.ToString(" HH:mm:ss 'UTC' yyyy", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// <c>sleep 60</c> – waits. In DotOS nobody has to sit and wait for real: the clock simply jumps ahead, and cron
/// jobs that were due in the meantime run.
/// </summary>
public sealed class SleepCommand : ICommand
{
    /// <summary>Longer than this would skip a whole day of cron jobs at once.</summary>
    private static readonly TimeSpan Longest = TimeSpan.FromHours(1);

    public string Name => "sleep";

    public string Usage => "sleep <seconds>[s|m|h]";

    public IReadOnlyList<string> Examples { get; } = ["sleep 60", "sleep 5m"];

    public void Execute(CommandContext context)
    {
        if (context.Arguments.Count == 0)
        {
            context.Error("Error.MissingOperand", Name);
            return;
        }

        var total = TimeSpan.Zero;

        foreach (var argument in context.Arguments)
        {
            if (!TryParse(argument, out var duration))
            {
                context.Error("Error.SleepInvalid", argument);
                return;
            }

            total += duration;
        }

        if (total > Longest)
        {
            context.Error("Error.SleepTooLong");
            return;
        }

        context.Session.FileSystem.Now += total;
    }

    private static bool TryParse(string text, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        var unit = text.Length > 0 && char.IsLetter(text[^1]) ? text[^1] : 's';
        var number = char.IsLetter(unit) && text.Length > 0 && char.IsLetter(text[^1]) ? text[..^1] : text;

        if (!double.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            return false;
        }

        duration = unit switch
        {
            's' => TimeSpan.FromSeconds(value),
            'm' => TimeSpan.FromMinutes(value),
            'h' => TimeSpan.FromHours(value),
            _ => TimeSpan.MinValue,
        };

        return duration != TimeSpan.MinValue;
    }
}

/// <summary><c>touch file</c> – creates an empty file, or updates the time of an existing one.</summary>
public sealed class TouchCommand : ICommand
{
    public string Name => "touch";

    public string Usage => "touch <file>...";

    public IReadOnlyList<string> Examples { get; } = ["touch /tmp/notes.txt"];

    public void Execute(CommandContext context)
    {
        if (context.Arguments.Count == 0)
        {
            context.Error("Error.MissingOperand", Name);
            return;
        }

        var session = context.Session;

        foreach (var path in context.Arguments)
        {
            if (session.GetNode(path) is { } existing)
            {
                if (!session.User.IsRoot && existing.Owner != session.User.Name && !session.CanWrite(existing))
                {
                    context.Error("Error.TouchDenied", path);
                    continue;
                }

                existing.Modified = session.FileSystem.Now;
                continue;
            }

            if (FileWriter.Write(session, path, "", append: true) is { } problem)
            {
                context.Error(problem == "Error.RedirectNoSuchFile" ? "Error.TouchNoSuchFile" : "Error.TouchDenied", path);
            }
        }
    }
}
