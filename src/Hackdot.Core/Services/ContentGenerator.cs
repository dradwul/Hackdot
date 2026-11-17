using System.Globalization;
using System.Text;
using Hackdot.Core.Models.Definitions;

namespace Hackdot.Core.Services;

/// <summary>Creates generated file content (see <see cref="GeneratorDefinition"/>).</summary>
public static class ContentGenerator
{
    private static readonly DateTime LogStart = new(2026, 3, 14, 0, 0, 0, DateTimeKind.Unspecified);

    private static readonly string[] Usernames =
        ["root", "admin", "test", "oracle", "postgres", "ubuntu", "pi", "guest", "ftp", "deploy", "git", "support", "backup"];

    // Documentation-only IP ranges (RFC 5737) – never real machines.
    private static readonly string[] Networks = ["192.0.2", "198.51.100", "203.0.113"];

    private static readonly (string Path, int Status, int Size)[] Pages =
    [
        ("/", 200, 5120),
        ("/about", 200, 3311),
        ("/products", 200, 8204),
        ("/contact", 200, 2950),
        ("/css/site.css", 200, 14022),
        ("/js/app.js", 200, 30117),
        ("/images/logo.png", 200, 9031),
        ("/favicon.ico", 404, 162),
        ("/robots.txt", 200, 67),
    ];

    private static readonly string[] Browsers =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Firefox/131.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) Safari/605.1.15",
        "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) Mobile/15E148",
        "Mozilla/5.0 (Linux; Android 15) Chrome/130.0 Mobile",
    ];

    public static string Generate(GeneratorDefinition definition, string hostname) =>
        definition.Kind switch
        {
            "authLog" => Generate(definition, hostname, SyslogTimestamp, RandomAuthEvent),
            "accessLog" => Generate(definition, hostname, AccessLogTimestamp, RandomRequest),
            _ => throw new InvalidDataException($"Unknown generator kind '{definition.Kind}'. Supported: authLog, accessLog."),
        };

    private static string Generate(
        GeneratorDefinition definition,
        string hostname,
        Func<DateTime, string> formatTimestamp,
        Func<Random, string, string, string> randomLine)
    {
        var random = new Random(definition.Seed);
        var inserts = definition.Inserts.ToLookup(insert => insert.At);
        var time = LogStart;
        var builder = new StringBuilder();

        for (var lineNumber = 1; lineNumber <= definition.Lines; lineNumber++)
        {
            time = time.AddSeconds(random.Next(1, 40));
            var timestamp = formatTimestamp(time);

            var template = inserts[lineNumber].FirstOrDefault()?.Text ?? PickActor(definition.Actors, random)?.Text;
            var line = template is not null
                ? FillIn(template, timestamp, hostname, random)
                : randomLine(random, timestamp, hostname);

            builder.Append(line).Append('\n');
        }

        return builder.ToString();
    }

    private static string FillIn(string template, string timestamp, string hostname, Random random)
    {
        var line = template.Replace("{timestamp}", timestamp).Replace("{host}", hostname);

        // Only roll for a port when one is needed, so adding {port} support did not change older logs.
        return line.Contains("{port}", StringComparison.Ordinal)
            ? line.Replace("{port}", random.Next(30000, 65000).ToString(CultureInfo.InvariantCulture))
            : line;
    }

    private static GeneratedActor? PickActor(List<GeneratedActor> actors, Random random)
    {
        // No dice roll without actors, so existing logs stay exactly the same for the same seed.
        if (actors.Count == 0)
        {
            return null;
        }

        var roll = random.Next(100);

        foreach (var actor in actors)
        {
            if (roll < actor.Percent)
            {
                return actor;
            }

            roll -= actor.Percent;
        }

        return null;
    }

    private static string RandomIp(Random random) => $"{Networks[random.Next(Networks.Length)]}.{random.Next(1, 255)}";

    /// <summary>An SSH/sudo/cron line in the style of <c>/var/log/auth.log</c>.</summary>
    private static string RandomAuthEvent(Random random, string timestamp, string hostname)
    {
        var pid = random.Next(1000, 32000);
        var port = random.Next(30000, 65000);
        var user = Usernames[random.Next(Usernames.Length)];
        var ip = RandomIp(random);

        var message = random.Next(100) switch
        {
            < 40 => $"sshd[{pid}]: Failed password for {user} from {ip} port {port} ssh2",
            < 60 => $"sshd[{pid}]: Failed password for invalid user {user} from {ip} port {port} ssh2",
            < 70 => $"sshd[{pid}]: Connection closed by authenticating user {user} {ip} port {port} [preauth]",
            < 75 => $"sshd[{pid}]: Accepted password for user from 10.0.0.12 port {port} ssh2",
            < 80 => $"sshd[{pid}]: pam_unix(sshd:session): session opened for user user(uid=1000) by (uid=0)",
            < 90 => $"CRON[{pid}]: pam_unix(cron:session): session opened for user root(uid=0) by (uid=0)",
            < 95 => $"CRON[{pid}]: pam_unix(cron:session): session closed for user root",
            _ => "sudo:     user : TTY=pts/0 ; PWD=/home/user ; USER=root ; COMMAND=/usr/bin/apt update",
        };

        return $"{timestamp} {hostname} {message}";
    }

    /// <summary>A web request in the "combined" log format used by nginx and Apache.</summary>
    private static string RandomRequest(Random random, string timestamp, string hostname)
    {
        var ip = RandomIp(random);
        var browser = Browsers[random.Next(Browsers.Length)];

        var (method, path, status, size) = random.Next(100) switch
        {
            // Normal visitors logging in: almost always right, now and then a typo.
            < 6 => ("POST", "/login", 302, 0),
            < 7 => ("POST", "/login", 401, 38),
            < 12 => ("GET", "/login", 200, 2210),
            _ => Page(random),
        };

        return $"{ip} - - [{timestamp}] \"{method} {path} HTTP/1.1\" {status} {size} \"-\" \"{browser}\"";

        static (string, string, int, int) Page(Random random)
        {
            var page = Pages[random.Next(Pages.Length)];
            return ("GET", page.Path, page.Status, page.Size);
        }
    }

    /// <summary>Syslog style, e.g. <c>Mar 14 03:17:42</c>.</summary>
    private static string SyslogTimestamp(DateTime time) =>
        $"{time.ToString("MMM", CultureInfo.InvariantCulture)} {time.Day,2} {time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}";

    /// <summary>Web server style, e.g. <c>14/Mar/2026:03:17:42 +0000</c>.</summary>
    private static string AccessLogTimestamp(DateTime time) =>
        time.ToString("dd/MMM/yyyy:HH:mm:ss", CultureInfo.InvariantCulture) + " +0000";
}
