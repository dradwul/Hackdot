using System.Globalization;
using Hackdot.Core.Models;

namespace Hackdot.Core.Services.Terminal.Commands;

public sealed class LsCommand : ICommand
{
    public string Name => "ls";

    public string Usage => "ls [-a] [-l] [path ...]";

    public IReadOnlyList<string> Examples { get; } = ["ls", "ls -a", "ls -la /var/www"];

    public void Execute(CommandContext context)
    {
        if (!context.TryParseOptions("al", out var arguments))
        {
            return;
        }

        var showHidden = arguments.Has('a');
        var longFormat = arguments.Has('l');
        IReadOnlyList<string> paths = arguments.Operands.Count > 0 ? arguments.Operands : ["."];
        var printHeaders = paths.Count > 1;
        var first = true;

        foreach (var path in paths)
        {
            switch (context.Session.GetNode(path))
            {
                case null:
                    context.Error("Error.CannotAccess", Name, path);
                    break;

                case VirtualFile file:
                    Render(context.Output, [(path, file)], longFormat, includeTotal: false);
                    break;

                case VirtualDirectory directory when !context.Session.CanRead(directory):
                    context.Error("Error.CannotOpenDirectory", Name, path);
                    break;

                case VirtualDirectory directory:
                    if (printHeaders)
                    {
                        if (!first)
                        {
                            context.Output.WriteLine();
                        }

                        context.Output.WriteLine(path + ":", OutputStyle.Muted);
                    }

                    Render(context.Output, Entries(directory, showHidden), longFormat, includeTotal: true);
                    first = false;
                    break;
            }
        }
    }

    private static List<(string Name, VirtualNode Node)> Entries(VirtualDirectory directory, bool showHidden)
    {
        var entries = new List<(string, VirtualNode)>();

        if (showHidden)
        {
            entries.Add((".", directory));
            entries.Add(("..", directory.Parent ?? directory));
        }

        entries.AddRange(directory.SortedChildren()
            .Where(child => showHidden || !child.IsHidden)
            .Select(child => (child.Name, child)));

        return entries;
    }

    private static void Render(CommandOutput output, List<(string Name, VirtualNode Node)> entries, bool longFormat, bool includeTotal)
    {
        if (!longFormat)
        {
            if (entries.Count == 0)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    output.Write("  ");
                }

                WriteName(output, entries[i].Name, entries[i].Node, folderSuffix: true);
            }

            output.EndLine();
            return;
        }

        if (includeTotal)
        {
            output.WriteLine($"total {entries.Sum(entry => Blocks(entry.Node))}", OutputStyle.Muted);
        }

        var linkWidth = entries.Max(entry => Links(entry.Node).ToString(CultureInfo.InvariantCulture).Length);
        var ownerWidth = entries.Max(entry => entry.Node.Owner.Length);
        var sizeWidth = entries.Max(entry => entry.Node.Size.ToString(CultureInfo.InvariantCulture).Length);

        foreach (var (name, node) in entries)
        {
            var links = Links(node).ToString(CultureInfo.InvariantCulture).PadLeft(linkWidth);
            var owner = node.Owner.PadRight(ownerWidth);
            var size = node.Size.ToString(CultureInfo.InvariantCulture).PadLeft(sizeWidth);

            // HackOS has no groups, so the group column is left out (like 'ls -o'). It also fits phone screens better.
            output.Write($"{node.PermissionString} {links} {owner} {size} {FormatDate(node.Modified)} ", OutputStyle.Muted);
            WriteName(output, name, node, folderSuffix: false);
            output.EndLine();
        }
    }

    private static void WriteName(CommandOutput output, string name, VirtualNode node, bool folderSuffix)
    {
        if (node.IsDirectory)
        {
            // A trailing slash (like 'ls -F') makes folders easy to spot, even without colors.
            var suffix = folderSuffix && name is not "." and not ".." ? "/" : "";
            output.Write(name + suffix, OutputStyle.Directory);
        }
        else
        {
            output.Write(name, node.IsHidden ? OutputStyle.Muted : OutputStyle.Default);
        }
    }

    private static int Links(VirtualNode node) =>
        node is VirtualDirectory directory ? 2 + directory.Children.Count(child => child.IsDirectory) : 1;

    private static long Blocks(VirtualNode node) =>
        node.IsDirectory ? 4 : (node.Size + 4095) / 4096 * 4;

    private static string FormatDate(DateTime date) =>
        $"{date.ToString("MMM", CultureInfo.InvariantCulture)} {date.Day,2} {date.ToString("HH:mm", CultureInfo.InvariantCulture)}";
}
