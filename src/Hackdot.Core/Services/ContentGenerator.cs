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

    public static string Generate(GeneratorDefinition definition, string hostname) =>
        definition.Kind switch
        {
            "authLog" => AuthLog(definition, hostname),
            _ => throw new InvalidDataException($"Unknown generator kind '{definition.Kind}'. Supported: authLog."),
        };

    /// <summary>An SSH/sudo/cron log in the style of <c>/var/log/auth.log</c>.</summary>
    private static string AuthLog(GeneratorDefinition definition, string hostname)
    {
        var random = new Random(definition.Seed);
        var inserts = definition.Inserts.ToLookup(insert => insert.At);
        var time = LogStart;
        var builder = new StringBuilder();

        for (var lineNumber = 1; lineNumber <= definition.Lines; lineNumber++)
        {
            time = time.AddSeconds(random.Next(1, 40));
            var timestamp = FormatTimestamp(time);

            var insert = inserts[lineNumber].FirstOrDefault();
            var line = insert is not null
                ? insert.Text.Replace("{timestamp}", timestamp).Replace("{host}", hostname)
                : $"{timestamp} {hostname} {RandomAuthEvent(random)}";

            builder.Append(line).Append('\n');
        }

        return builder.ToString();
    }

    private static string RandomAuthEvent(Random random)
    {
        var pid = random.Next(1000, 32000);
        var port = random.Next(30000, 65000);
        var user = Usernames[random.Next(Usernames.Length)];
        var ip = $"{Networks[random.Next(Networks.Length)]}.{random.Next(1, 255)}";

        return random.Next(100) switch
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
    }

    /// <summary>Syslog style, e.g. <c>Mar 14 03:17:42</c>.</summary>
    private static string FormatTimestamp(DateTime time) =>
        $"{time.ToString("MMM", CultureInfo.InvariantCulture)} {time.Day,2} {time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}";
}
