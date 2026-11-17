using Hackdot.Core.Models;

namespace Hackdot.Core.Services.Terminal.Commands;

/// <summary>
/// Supports the real syntax (<c>find [path] -name pattern -type f</c>) plus a beginner shortcut:
/// <c>find &lt;name&gt;</c> searches the whole system for names containing <c>&lt;name&gt;</c>.
/// </summary>
public sealed class FindCommand : ICommand
{
    public string Name => "find";

    public string Usage => "find [path] [-name <pattern>] [-iname <pattern>] [-type f|d]";

    public IReadOnlyList<string> Examples { get; } = ["find flag", "find / -name \"*.txt\"", "find /var -type f", "find ~ -name \".*\""];

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
        if (paths.Count != 1 || filter.Pattern is not null || filter.Type is not null)
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

    private sealed record Filter(string? Pattern = null, bool IgnoreCase = false, bool Contains = false, char? Type = null)
    {
        public bool Matches(VirtualNode node)
        {
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
