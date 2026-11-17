using Hackdot.Core.Services.Terminal.Commands;
using Microsoft.Extensions.Localization;

namespace Hackdot.Core.Services.Terminal;

/// <summary>Parses and runs command lines against a <see cref="ShellSession"/>.</summary>
public sealed class CommandInterpreter
{
    private readonly Dictionary<string, ICommand> _commandsByName;
    private readonly List<string> _history;

    /// <param name="history">
    /// Where typed commands are remembered (for <c>history</c> and the ↑ key). Pass the same list to the next
    /// interpreter to keep the history when the terminal reconnects to another machine.
    /// </param>
    public CommandInterpreter(
        ShellSession session,
        IStringLocalizer<TerminalResources> text,
        IEnumerable<ICommand>? commands = null,
        List<string>? history = null)
    {
        Session = session;
        Text = text;
        Commands = [.. commands ?? DefaultCommands()];
        _history = history ?? [];

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
        new HeadCommand(),
        new TailCommand(),
        new GrepCommand(),
        new FindCommand(),
        new WcCommand(),
        new SortCommand(),
        new UniqCommand(),
        new CutCommand(),
        new EchoCommand(),
        new Base64Command(),
        new WhoamiCommand(),
        new SuCommand(),
        new SudoCommand(),
        new ExitCommand(),
        new HistoryCommand(),
        new ClearCommand(),
    ];

    public ICommand? FindCommand(string name) => _commandsByName.GetValueOrDefault(name);

    /// <summary>Commands the player has typed, oldest first. Answers to questions (like passwords) are never included.</summary>
    public IReadOnlyList<string> History => _history;

    public void ClearHistory() => _history.Clear();

    /// <summary>
    /// Set while a command waits for the player to type something, e.g. <c>Password:</c> after <c>su</c>.
    /// The next <see cref="Execute"/> call answers the question instead of running a command.
    /// </summary>
    public string? PendingInputLabel => _pendingInput?.Label;

    /// <summary>The pending input should not be shown or remembered (a password).</summary>
    public bool PendingInputIsSecret => _pendingInput?.Secret == true;

    private PendingInput? _pendingInput;

    private sealed record PendingInput(string Label, bool Secret, Action<string, CommandOutput> OnInput);

    internal void RequestInput(string label, bool secret, Action<string, CommandOutput> onInput) =>
        _pendingInput = new PendingInput(label, secret, onInput);

    /// <summary>The prompt, e.g. <c>user@hackos:~/Documents$</c>, optionally followed by a typed command.</summary>
    public OutputLine Prompt(string? input = null)
    {
        List<OutputSegment> segments =
        [
            // Root's prompt is red: a classic reminder that you can break things now.
            new($"{Session.User.Name}@{Session.FileSystem.Hostname}", Session.User.IsRoot ? OutputStyle.Error : OutputStyle.Success),
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
        if (_pendingInput is { } pending)
        {
            _pendingInput = null;
            var output = new CommandOutput();

            try
            {
                pending.OnInput(input, output);
            }
            catch (Exception)
            {
                output.Error(Text["Error.Unexpected", "?"]);
            }

            return new CommandResult(output.Lines, clearScreen: false);
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return CommandResult.Empty;
        }

        // Like bash with 'ignoredups': running the same command twice in a row is remembered once.
        if (_history.Count == 0 || _history[^1] != input)
        {
            _history.Add(input);
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

            var context = new CommandContext(command.Name, [.. stage.Skip(1)], standardInput, Session, Text, this)
            {
                OutputIsPiped = !isLast,
            };

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
