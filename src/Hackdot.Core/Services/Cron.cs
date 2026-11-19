using System.Globalization;
using Hackdot.Core.Models;

namespace Hackdot.Core.Services;

/// <summary>
/// One line of <c>/etc/crontab</c>: five time fields (minute, hour, day of month, month, weekday), the user the
/// job runs as, and the command.
/// </summary>
public sealed record CronJob(string Minute, string Hour, string Day, string Month, string Weekday, string User, string Command)
{
    /// <summary>
    /// The script the job starts, if its command begins with an absolute path – the file an attacker would want
    /// to be able to change.
    /// </summary>
    public string? ScriptPath => Command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is { } first
        && first.StartsWith('/') ? first : null;

    public bool IsDue(DateTime time)
    {
        if (!Cron.Matches(Minute, time.Minute, 0, 59) || !Cron.Matches(Hour, time.Hour, 0, 23)
            || !Cron.Matches(Month, time.Month, 1, 12))
        {
            return false;
        }

        // Like real cron: when both day fields are restricted, either one matching is enough.
        var weekday = (int)time.DayOfWeek;
        var dayMatches = Cron.Matches(Day, time.Day, 1, 31);
        var weekdayMatches = Cron.Matches(Weekday, weekday, 0, 7) || (weekday == 0 && Cron.Matches(Weekday, 7, 0, 7));

        return Day != "*" && Weekday != "*" ? dayMatches || weekdayMatches : dayMatches && weekdayMatches;
    }
}

/// <summary>
/// The system crontab, <c>/etc/crontab</c>. Jobs run as the computer's clock (<see cref="VirtualFileSystem.Now"/>)
/// passes their time – see <c>CommandInterpreter</c>.
/// </summary>
public static class Cron
{
    public const string TablePath = "/etc/crontab";

    public const string LogPath = "/var/log/cron.log";

    /// <summary>Reads the jobs in <c>/etc/crontab</c>. Comments, variables (<c>PATH=…</c>) and odd lines are skipped.</summary>
    public static IReadOnlyList<CronJob> Jobs(VirtualFileSystem fileSystem)
    {
        if (fileSystem.GetFile(TablePath) is not { } table)
        {
            return [];
        }

        var jobs = new List<CronJob>();

        foreach (var raw in table.Content.Split('\n'))
        {
            var line = raw.Trim();

            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('@'))
            {
                continue;
            }

            var fields = line.Split((char[]?)null, 7, StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length == 7 && !fields[0].Contains('='))
            {
                jobs.Add(new CronJob(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], fields[6]));
            }
        }

        return jobs;
    }

    /// <summary>
    /// Whether a time field matches: <c>*</c>, a number, a range (<c>1-5</c>), a list (<c>0,30</c>) and steps
    /// (<c>*/5</c>, <c>0-30/10</c>).
    /// </summary>
    public static bool Matches(string field, int value, int min, int max)
    {
        foreach (var part in field.Split(','))
        {
            var (range, stepText) = part.Split('/') is [var r, var s] ? (r, s) : (part, "1");

            if (!int.TryParse(stepText, NumberStyles.None, CultureInfo.InvariantCulture, out var step) || step < 1)
            {
                continue;
            }

            int from, to;

            if (range == "*")
            {
                (from, to) = (min, max);
            }
            else if (range.Split('-') is [var a, var b]
                && int.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out from)
                && int.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out to))
            {
            }
            else if (int.TryParse(range, NumberStyles.None, CultureInfo.InvariantCulture, out from))
            {
                to = stepText == "1" ? from : max;
            }
            else
            {
                continue;
            }

            if (value >= from && value <= to && (value - from) % step == 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Adds a line to <c>/var/log/cron.log</c> (if the computer has one), like the real cron daemon.</summary>
    public static void Log(VirtualFileSystem fileSystem, CronJob job, DateTime time)
    {
        if (fileSystem.GetFile(LogPath) is not { } log)
        {
            return;
        }

        var stamp = time.ToString("MMM", CultureInfo.InvariantCulture) + " " + time.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2)
            + " " + time.ToString("HH:mm", CultureInfo.InvariantCulture) + ":01";
        var processId = 2000 + (time.Hour * 60) + time.Minute;
        var line = $"{stamp} {fileSystem.Hostname} CRON[{processId}]: ({job.User}) CMD ({job.Command})";

        log.Content = log.Content.Length == 0 || log.Content.EndsWith('\n') ? log.Content + line + "\n" : log.Content + "\n" + line + "\n";
        log.Modified = time;
    }
}
