using Hackdot.Core.Models;

namespace Hackdot.Core.Services.Terminal.Commands;

public sealed class HelpCommand : ICommand
{
    public string Name => "help";

    public string Usage => "help [command]";

    public IReadOnlyList<string> Examples { get; } = ["help", "help grep"];

    public void Execute(CommandContext context)
    {
        var output = context.Output;

        if (context.Arguments.Count == 0)
        {
            var commands = context.Interpreter.Commands;
            var width = commands.Max(command => command.Name.Length) + 2;

            output.WriteLine(context.Localize("Help.Header"), OutputStyle.Accent);

            foreach (var command in commands)
            {
                output.HangingIndent(width + 2).Write("  " + command.Name.PadRight(width), OutputStyle.Success);
                output.WriteLine(context.Localize($"Cmd.{command.Name}.Summary"));
            }

            output.WriteLine();
            output.WriteLine(context.Localize("Help.Footer"), OutputStyle.Muted);
            return;
        }

        if (context.Interpreter.FindCommand(context.Arguments[0]) is not { } topic)
        {
            context.Error("Error.NoHelpTopic", context.Arguments[0]);
            return;
        }

        output.Write(topic.Name, OutputStyle.Accent).WriteLine(" – " + context.Localize($"Cmd.{topic.Name}.Summary"));
        output.Write(context.Localize("Help.Usage") + " ", OutputStyle.Muted).WriteLine(topic.Usage, OutputStyle.Success);
        output.WriteLine();

        foreach (var line in TextHelpers.SplitLines(context.Localize($"Cmd.{topic.Name}.Details")))
        {
            output.WriteLine(line);
        }

        if (topic.Examples.Count == 0)
        {
            return;
        }

        output.WriteLine();
        output.WriteLine(context.Localize("Help.Examples"), OutputStyle.Muted);

        for (var i = 0; i < topic.Examples.Count; i++)
        {
            output.Write("  $ ", OutputStyle.Muted).WriteLine(topic.Examples[i], OutputStyle.Success);
            output.HangingIndent(4).WriteLine("    " + context.Localize($"Cmd.{topic.Name}.Example{i + 1}"), OutputStyle.Muted);
        }
    }
}

public sealed class CdCommand : ICommand
{
    public string Name => "cd";

    public string Usage => "cd [folder]";

    public IReadOnlyList<string> Examples { get; } = ["cd Documents", "cd ..", "cd /var/log"];

    public void Execute(CommandContext context)
    {
        if (context.Arguments.Count > 1)
        {
            context.Error("Error.TooManyArguments", Name);
            return;
        }

        var target = context.Arguments.Count == 0 ? "~" : context.Arguments[0];

        switch (context.Session.GetNode(target))
        {
            case null:
                context.Error("Error.NoSuchFile", Name, target);
                break;
            case VirtualFile:
                context.Error("Error.NotADirectory", Name, target);
                break;
            case VirtualDirectory directory when !context.Session.CanEnter(directory):
                context.Error("Error.PermissionDenied", Name, target);
                break;
            case VirtualDirectory directory:
                context.Session.ChangeDirectory(directory);
                break;
        }
    }
}

public sealed class PwdCommand : ICommand
{
    public string Name => "pwd";

    public string Usage => "pwd";

    public IReadOnlyList<string> Examples { get; } = ["pwd"];

    public void Execute(CommandContext context) => context.Output.WriteLine(context.Session.CurrentDirectory);
}

public sealed class WhoamiCommand : ICommand
{
    public string Name => "whoami";

    public string Usage => "whoami";

    public IReadOnlyList<string> Examples { get; } = ["whoami"];

    public void Execute(CommandContext context) => context.Output.WriteLine(context.Session.User.Name);
}

public sealed class ClearCommand : ICommand
{
    public string Name => "clear";

    public string Usage => "clear";

    public IReadOnlyList<string> Examples { get; } = ["clear"];

    public void Execute(CommandContext context) => context.ClearRequested = true;
}

public sealed class CatCommand : ICommand
{
    public string Name => "cat";

    public string Usage => "cat [-n] <file> ...";

    public IReadOnlyList<string> Examples { get; } = ["cat notes.txt", "cat -n /etc/passwd"];

    public void Execute(CommandContext context)
    {
        if (!context.TryParseOptions("n", out var arguments))
        {
            return;
        }

        var numbered = arguments.Has('n');

        if (arguments.Operands.Count == 0)
        {
            if (context.StandardInput is { } input)
            {
                WriteContent(context.Output, input, numbered);
            }
            else
            {
                context.Error("Error.MissingOperand", Name);
            }

            return;
        }

        foreach (var path in arguments.Operands)
        {
            switch (context.Session.GetNode(path))
            {
                case null:
                    context.Error("Error.NoSuchFile", Name, path);
                    break;
                case VirtualDirectory:
                    context.Error("Error.IsADirectory", Name, path);
                    break;
                case VirtualFile file when !context.Session.CanRead(file):
                    context.Error("Error.PermissionDenied", Name, path);
                    break;
                case VirtualFile file:
                    WriteContent(context.Output, file.Content, numbered);
                    break;
            }
        }
    }

    private static void WriteContent(CommandOutput output, string content, bool numbered)
    {
        var lines = TextHelpers.SplitLines(content);

        for (var i = 0; i < lines.Count; i++)
        {
            if (numbered)
            {
                output.Write($"{i + 1,6}  ", OutputStyle.Muted);
            }

            output.WriteLine(lines[i]);
        }
    }
}
