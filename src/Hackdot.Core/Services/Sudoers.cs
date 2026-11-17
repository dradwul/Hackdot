using System.Text.RegularExpressions;
using Hackdot.Core.Models;
using Hackdot.Core.Services.Terminal;

namespace Hackdot.Core.Services;

/// <summary>
/// One permission from <c>/etc/sudoers</c>: <paramref name="User"/> may run <paramref name="CommandPath"/> as root,
/// optionally only with arguments matching <paramref name="ArgumentPattern"/>.
/// </summary>
public sealed record SudoRule(string User, string RunAs, bool NoPassword, string CommandPath, string? ArgumentPattern)
{
    public const string All = "ALL";

    public bool AllowsAnyCommand => CommandPath == All;

    /// <summary>The command without its folder, e.g. <c>cat</c> for <c>/usr/bin/cat</c>.</summary>
    public string CommandName => CommandPath[(CommandPath.LastIndexOf('/') + 1)..];

    public bool Allows(string commandName, IReadOnlyList<string> arguments)
    {
        if (AllowsAnyCommand)
        {
            return true;
        }

        if (!string.Equals(CommandName, commandName, StringComparison.Ordinal))
        {
            return false;
        }

        return ArgumentPattern switch
        {
            null => true,
            "\"\"" => arguments.Count == 0,

            // Like the real sudo, * matches ANY text in the arguments – slashes, spaces and '..' included.
            // That is exactly the classic mistake the missions teach.
            _ => Glob.IsMatch(string.Join(' ', arguments), ArgumentPattern),
        };
    }

    /// <summary>The rule the way <c>sudo -l</c> shows it, e.g. <c>(root) NOPASSWD: /usr/bin/cat /var/log/*</c>.</summary>
    public string Display =>
        $"({RunAs}) {(NoPassword ? "NOPASSWD: " : "")}{CommandPath}{(ArgumentPattern is null ? "" : " " + ArgumentPattern)}";
}

/// <summary>
/// Reads the simplified <c>/etc/sudoers</c> format used by HackOS:
/// <c>user host=(runas) [NOPASSWD:] /path/command [argument pattern], /path/other</c>.
/// Lines starting with <c>#</c> and <c>Defaults</c> lines are ignored.
/// </summary>
public static partial class Sudoers
{
    public const string FilePath = "/etc/sudoers";

    /// <summary>The rules for one user. sudo itself runs as root, so the file's permissions don't matter here.</summary>
    public static IReadOnlyList<SudoRule> RulesFor(VirtualFileSystem fileSystem, string userName) =>
        fileSystem.GetNode(FilePath) is VirtualFile file
            ? [.. Parse(file.Content).Where(rule => rule.User == userName)]
            : [];

    public static IEnumerable<SudoRule> Parse(string content)
    {
        foreach (var rawLine in TextHelpers.SplitLines(content))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("Defaults", StringComparison.Ordinal))
            {
                continue;
            }

            var match = RulePattern().Match(line);

            if (!match.Success)
            {
                continue;
            }

            var runAs = match.Groups["runas"].Success ? match.Groups["runas"].Value : VirtualUser.RootName;
            var noPassword = match.Groups["nopasswd"].Success;

            foreach (var command in match.Groups["commands"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var space = command.IndexOf(' ');
                yield return space < 0
                    ? new SudoRule(match.Groups["user"].Value, runAs, noPassword, command, null)
                    : new SudoRule(match.Groups["user"].Value, runAs, noPassword, command[..space], command[(space + 1)..].Trim());
            }
        }
    }

    [GeneratedRegex(@"^(?<user>\S+)\s+\S+\s*=\s*(?:\((?<runas>[^)]*)\))?\s*(?<nopasswd>NOPASSWD:)?\s*(?<commands>.+)$")]
    private static partial Regex RulePattern();
}
