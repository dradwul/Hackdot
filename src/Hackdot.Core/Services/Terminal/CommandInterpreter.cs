using Hackdot.Core.Services.Terminal.Commands;
using Microsoft.Extensions.Localization;

namespace Hackdot.Core.Services.Terminal;

/// <summary>Parses and runs command lines against a <see cref="ShellSession"/>.</summary>
public sealed class CommandInterpreter
{
    private readonly Dictionary<string, ICommand> _commandsByName;

    public CommandInterpreter(ShellSession session, IStringLocalizer<TerminalResources> text, IEnumerable<ICommand>? commands = null)
    {
        Session = session;
        Text = text;
        Commands = [.. commands ?? DefaultCommands()];

        // Case-insensitive on purpose: phone keyboards love to capitalize the first letter.
        _commandsByName = Commands.ToDictionary(command => command.Name, StringComparer.OrdinalIgnoreCase);
    }

    public ShellSession Session { get; }

    public IStringLocalizer Text { get; }

    /// <summary>Commands in the order <c>help</c> lists them.</summary>
    public IReadOnlyList<ICommand> Commands { get; }

    public static IReadOnlyList<ICommand> DefaultCommands() =>
    [
        new HelpCommand(),
        new LsCommand(),
        new CdCommand(),
        new PwdCommand(),
        new CatCommand(),
        new GrepCommand(),
        new FindCommand(),
        new WhoamiCommand(),
        new ClearCommand(),
    ];

    public ICommand? FindCommand(string name) => _commandsByName.GetValueOrDefault(name);

    /// <summary>The prompt, e.g. <c>user@hackos:~/Documents$</c>, optionally followed by a typed command.</summary>
    public OutputLine Prompt(string? input = null)
    {
        List<OutputSegment> segments =
        [
            new($"{Session.User.Name}@{Session.FileSystem.Hostname}", OutputStyle.Success),
            new(":"),
            new(Session.DisplayDirectory, OutputStyle.Directory),
            new(Session.User.IsRoot ? "# " : "$ "),
        ];

        if (!string.IsNullOrEmpty(input))
        {
            segments.Add(new OutputSegment(input));
        }

        return new OutputLine(segments);
    }

    public IReadOnlyList<OutputLine> Welcome() =>
    [
        OutputLine.Plain(Text["Welcome.Title"], OutputStyle.Accent),
        OutputLine.Plain(Text["Welcome.Hint"], OutputStyle.Muted),
        OutputLine.Plain(""),
    ];

    public CommandResult Execute(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return CommandResult.Empty;
        }

        IReadOnlyList<IReadOnlyList<string>> pipeline;

        try
        {
            pipeline = CommandLineParser.Parse(input);
        }
        catch (CommandSyntaxException ex)
        {
            return new CommandResult([OutputLine.Error(Text[ex.ResourceKey])], clearScreen: false);
        }

        var lines = new List<OutputLine>();
        string? standardInput = null;
        var clearScreen = false;

        for (var i = 0; i < pipeline.Count; i++)
        {
            var stage = pipeline[i];
            var isLast = i == pipeline.Count - 1;

            if (FindCommand(stage[0]) is not { } command)
            {
                lines.Add(OutputLine.Error(Text["Error.CommandNotFound", stage[0]]));
                standardInput = "";
                continue;
            }

            var context = new CommandContext(command.Name, [.. stage.Skip(1)], standardInput, Session, Text, this);

            try
            {
                command.Execute(context);
            }
            catch (Exception)
            {
                // A bug in one command should never take down the whole terminal.
                context.Output.Error(Text["Error.Unexpected", command.Name]);
            }

            if (isLast)
            {
                lines.AddRange(context.Output.Lines);
                clearScreen = context.ClearRequested;
            }
            else
            {
                lines.AddRange(context.Output.Lines.Where(line => line.IsError));
                standardInput = context.Output.StandardOutput;
            }
        }

        return clearScreen ? new CommandResult([], clearScreen: true) : new CommandResult(lines, clearScreen: false);
    }
}
