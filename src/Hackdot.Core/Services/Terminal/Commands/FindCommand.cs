using Hackdot.Core.Models;

namespace Hackdot.Core.Services.Terminal.Commands;

/// <summary>
/// Supports the real syntax (<c>find [path] -name pattern -type f</c>) plus a beginner shortcut:
/// <c>find &lt;name&gt;</c> searches the whole system for names containing <c>&lt;name&gt;</c>.
/// </summary>
public sealed class FindCommand : ICommand
{
    public string Name => "find";

    public string Usage => "find [path] [-name <pattern>] [-iname <pattern>] [-type f|d] [-perm <mode>]";

    public IReadOnlyList<string> Examples { get; } = ["find flag", "find / -name \"*.txt\"", "find /var -type f", "find ~ -name \".*\"", "find / -perm -4000 2>/dev/null"];

    public void Execute(CommandContext context)
    {
        var arguments = context.Arguments;
        var paths = new List<string>();
        var filter = new Filter();
        var index = 0;

        while (index < arguments.Count && !arguments[index].StartsWith('-'))
        {
            paths.Add(arguments[index++]);
        }

        while (index < arguments.Count)
        {
            var option = arguments[index++];

            switch (option)
            {
                case "-name" or "-iname":
                    if (index >= arguments.Count)
                    {
                        context.Error("Error.MissingArgument", Name, option);
                        return;
                    }

                    filter = filter with { Pattern = arguments[index++], IgnoreCase = option == "-iname" };
                    break;

                case "-type":
                    if (index >= arguments.Count || arguments[index] is not ("f" or "d"))
                    {
                        context.Error("Error.FindInvalidType");
                        return;
                    }

                    filter = filter with { Type = arguments[index++][0] };
                    break;

                case "-perm":
                    if (index >= arguments.Count || !PermissionTest.TryParse(arguments[index], out var permission))
                    {
                        context.Error("Error.FindInvalidPerm", index < arguments.Count ? arguments[index] : "");
                        return;
                    }

                    index++;
                    filter = filter with { Permission = permission };
                    break;

                default:
                    context.Error("Error.FindUnknownPredicate", option);
                    return;
            }
        }

        if (IsQuickSearch(context, paths, filter))
        {
            var name = paths[0];
            filter = new Filter(name, IgnoreCase: true, Contains: !Glob.HasWildcards(name));
            paths = ["/"];
        }

        if (paths.Count == 0)
        {
            paths.Add(".");
        }

        foreach (var path in paths)
        {
            if (context.Session.GetNode(path) is not { } start)
            {
                context.PathNotFound("Error.NoSuchFile", path);
                continue;
            }

            Visit(context, start, path, filter);
        }
    }

    /// <summary><c>find secret</c> where "secret" is not an existing path: treat it as a name to look for.</summary>
    private static bool IsQuickSearch(CommandContext context, List<string> paths, Filter filter)
    {
        if (paths.Count != 1 || filter != new Filter())
        {
            return false;
        }

        var candidate = paths[0];
        var looksLikePath = candidate.Contains('/') || candidate.StartsWith('~') || candidate is "." or "..";

        return !looksLikePath && context.Session.GetNode(candidate) is null;
    }

    private void Visit(CommandContext context, VirtualNode node, string displayPath, Filter filter)
    {
        if (filter.Matches(node))
        {
            context.Output.WriteLine(displayPath, node.IsDirectory ? OutputStyle.Directory : OutputStyle.Default);
        }

        if (node is not VirtualDirectory directory)
        {
            return;
        }

        if (!context.Session.CanRead(directory) || !context.Session.CanEnter(directory))
        {
            context.Error("Error.PermissionDenied", Name, displayPath);
            return;
        }

        foreach (var child in directory.SortedChildren())
        {
            Visit(context, child, TextHelpers.JoinDisplayPath(displayPath, child.Name), filter);
        }
    }

    private sealed record Filter(string? Pattern = null, bool IgnoreCase = false, bool Contains = false, char? Type = null, PermissionTest? Permission = null)
    {
        public bool Matches(VirtualNode node)
        {
            if (Permission is { } permission && !permission.Matches(node.Mode))
            {
                return false;
            }

            if (Type is 'f' && node.IsDirectory || Type is 'd' && !node.IsDirectory)
            {
                return false;
            }

            if (Pattern is null)
            {
                return true;
            }

            return Contains
                ? node.Name.Contains(Pattern, StringComparison.OrdinalIgnoreCase)
                : Glob.IsMatch(node.Name, Pattern, IgnoreCase);
        }
    }
}

/// <summary>
/// <c>-perm</c>: <c>-4000</c> = all these bits are set, <c>/222</c> = any of them is set, <c>644</c> = exactly
/// this mode. Symbolic modes work too, e.g. <c>-u=s</c> (set-user-ID) or <c>-o=w</c> (writable by everyone).
/// </summary>
internal sealed record PermissionTest(int Bits, char Kind)
{
    public static bool TryParse(string value, out PermissionTest test)
    {
        test = new PermissionTest(0, '=');
        var kind = value.Length > 0 && value[0] is '-' or '/' ? value[0] : '=';
        var mode = kind == '=' ? value : value[1..];

        if (mode.Length == 0 || !FilePermissions.TryApply(mode, 0, out var bits))
        {
            return false;
        }

        test = new PermissionTest(bits, kind);
        return true;
    }

    public bool Matches(int mode) => Kind switch
    {
        '-' => (mode & Bits) == Bits,
        '/' => Bits == 0 || (mode & Bits) != 0,
        _ => FilePermissions.ToOctal(mode) == FilePermissions.ToOctal(Bits),
    };
}
