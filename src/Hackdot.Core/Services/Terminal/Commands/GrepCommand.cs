using Hackdot.Core.Models;

namespace Hackdot.Core.Services.Terminal.Commands;

/// <summary>
/// Searches for plain text (not regular expressions – friendlier for beginners) in files or piped input.
/// </summary>
public sealed class GrepCommand : ICommand
{
    public string Name => "grep";

    public string Usage => "grep [-i] [-n] [-v] [-r] <text> [file ...]";

    public IReadOnlyList<string> Examples { get; } =
    [
        "grep FLAG notes.txt",
        "grep -n error /var/log/syslog",
        "grep -ri password /home",
        "cat /var/log/auth.log | grep Failed",
    ];

    public void Execute(CommandContext context)
    {
        if (!context.TryParseOptions("invr", out var arguments))
        {
            return;
        }

        if (arguments.Operands.Count == 0)
        {
            context.Error("Error.MissingOperand", Name);
            return;
        }

        var search = new Search(
            arguments.Operands[0],
            arguments.Has('i') ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal,
            Invert: arguments.Has('v'),
            Numbered: arguments.Has('n'));

        var recursive = arguments.Has('r');
        var paths = arguments.Operands.Skip(1).ToList();

        if (paths.Count == 0)
        {
            if (recursive)
            {
                paths.Add(".");
            }
            else if (context.StandardInput is { } input)
            {
                WriteMatches(context.Output, label: null, input, search);
                return;
            }
            else
            {
                context.Error("Error.GrepNoInput", search.Text);
                return;
            }
        }

        var targets = new List<(string Label, VirtualFile File)>();

        foreach (var path in paths)
        {
            switch (context.Session.GetNode(path))
            {
                case null:
                    context.PathNotFound("Error.NoSuchFile", path);
                    break;
                case VirtualFile file when !context.Session.CanRead(file):
                    context.Error("Error.PermissionDenied", Name, path);
                    break;
                case VirtualFile file:
                    targets.Add((path, file));
                    break;
                case VirtualDirectory when !recursive:
                    context.Error("Error.IsADirectory", Name, path);
                    break;
                case VirtualDirectory directory:
                    CollectFiles(context, directory, path, targets);
                    break;
            }
        }

        var showLabels = recursive || targets.Count > 1;

        foreach (var (label, file) in targets)
        {
            WriteMatches(context.Output, showLabels ? label : null, file.Content, search);
        }
    }

    private void CollectFiles(CommandContext context, VirtualDirectory directory, string label, List<(string, VirtualFile)> targets)
    {
        if (!context.Session.CanRead(directory) || !context.Session.CanEnter(directory))
        {
            context.Error("Error.PermissionDenied", Name, label);
            return;
        }

        foreach (var child in directory.SortedChildren())
        {
            var childLabel = TextHelpers.JoinDisplayPath(label, child.Name);

            switch (child)
            {
                case VirtualDirectory subdirectory:
                    CollectFiles(context, subdirectory, childLabel, targets);
                    break;
                case VirtualFile file when context.Session.CanRead(file):
                    targets.Add((childLabel, file));
                    break;
                default:
                    context.Error("Error.PermissionDenied", Name, childLabel);
                    break;
            }
        }
    }

    private static void WriteMatches(CommandOutput output, string? label, string content, Search search)
    {
        var lines = TextHelpers.SplitLines(content);

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (line.Contains(search.Text, search.Comparison) == search.Invert)
            {
                continue;
            }

            if (label is not null)
            {
                output.Write(label, OutputStyle.Accent).Write(":", OutputStyle.Muted);
            }

            if (search.Numbered)
            {
                output.Write((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), OutputStyle.Success)
                    .Write(":", OutputStyle.Muted);
            }

            if (search.Invert || search.Text.Length == 0)
            {
                output.WriteLine(line);
            }
            else
            {
                WriteHighlighted(output, line, search);
            }
        }
    }

    private static void WriteHighlighted(CommandOutput output, string line, Search search)
    {
        var position = 0;

        while (position < line.Length)
        {
            var index = line.IndexOf(search.Text, position, search.Comparison);

            if (index < 0)
            {
                break;
            }

            output.Write(line[position..index]);
            output.Write(line.Substring(index, search.Text.Length), OutputStyle.Highlight);
            position = index + search.Text.Length;
        }

        output.WriteLine(line[position..]);
    }

    private sealed record Search(string Text, StringComparison Comparison, bool Invert, bool Numbered);
}
