using Hackdot.Core.Models;
using Hackdot.Core.Services.Terminal.Commands;
using Microsoft.Extensions.Localization;

namespace Hackdot.Core.Services.Terminal;

/// <summary>Parses and runs command lines against a <see cref="ShellSession"/>.</summary>
public sealed class CommandInterpreter
{
    private readonly Dictionary<string, ICommand> _commandsByName;
    private readonly List<string> _history;
    private readonly IStringLocalizer<TerminalResources> _text;

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
        _text = text;
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
        new ChmodCommand(),
        new TouchCommand(),
        new WcCommand(),
        new SortCommand(),
        new UniqCommand(),
        new CutCommand(),
        new EchoCommand(),
        new Base64Command(),
        new DateCommand(),
        new SleepCommand(),
        new WhoamiCommand(),
        new SuCommand(),
        new SudoCommand(),
        new ExitCommand(),
        new HistoryCommand(),
        new ClearCommand(),
    ];

    public ICommand? FindCommand(string name) => _commandsByName.GetValueOrDefault(name);

    /// <summary>
    /// Finds the command for the first word of a stage: a name (<c>cat</c>) or a path to its program
    /// (<c>/usr/bin/cat</c>). <paramref name="program"/> is the program file, which may be set-user-ID.
    /// </summary>
    private ICommand? ResolveCommand(string word, out VirtualFile? program)
    {
        program = null;

        if (!word.Contains('/'))
        {
            var command = FindCommand(word);
            program = command is null ? null : SetUidPrograms.FindProgram(Session.FileSystem, command.Name);
            return command;
        }

        var path = Session.Resolve(word);
        var name = VirtualPath.GetName(path);
        program = Session.FileSystem.GetFile(path);

        // Only the program folders hold commands – even when the mission didn't put a file there. Anywhere else,
        // a file is a script (or nothing at all).
        return SetUidPrograms.SearchPath.Contains(VirtualPath.GetParent(path)) ? FindCommand(name) : null;
    }

    /// <summary>
    /// Whether time passes (and cron jobs run) after each command. Off for the shells that run scripts and jobs.
    /// </summary>
    public bool RunsScheduledJobs { get; init; } = true;

    /// <summary>How many scripts deep this shell is (0 for the terminal).</summary>
    public int ScriptDepth { get; init; }

    /// <summary>The terminal ran a command line (not raised for scripts and cron jobs). Used for achievements.</summary>
    public event Action<Missions.CommandRun>? CommandRan;

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

    /// <summary>The prompt, e.g. <c>user@dotos:~/Documents$</c>, optionally followed by a typed command.</summary>
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

            CommandRan?.Invoke(new Missions.CommandRun([], Session.User.IsRoot, false, TimeSpan.Zero));
            return new CommandResult(output.Lines, clearScreen: false);
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return CommandResult.Empty;
        }

        // Spaces at the end (Tab completion leaves one) don't change the command, so they aren't remembered –
        // unless the last one is escaped, like in 'cat my\ '.
        var remembered = input.TrimEnd();
        if (remembered.EndsWith('\\'))
        {
            remembered = input;
        }

        // Like bash with 'ignoredups': running the same command twice in a row is remembered once.
        if (_history.Count == 0 || _history[^1] != remembered)
        {
            _history.Add(remembered);
        }

        IReadOnlyList<ParsedCommand> pipeline;

        try
        {
            pipeline = CommandLineParser.ParseLine(input);
        }
        catch (CommandSyntaxException ex)
        {
            return new CommandResult([OutputLine.Error(Text[ex.ResourceKey])], clearScreen: false);
        }

        var startTime = Session.FileSystem.Now;
        var lines = new List<OutputLine>();
        string? standardInput = null;
        var clearScreen = false;
        var ranAsRoot = false;

        for (var i = 0; i < pipeline.Count; i++)
        {
            var isLast = i == pipeline.Count - 1;
            var stage = RunStage(pipeline[i], standardInput, !isLast);
            ranAsRoot |= stage.RanAsRoot;
            var kept = Redirect(pipeline[i].Redirections, stage.Output, out var stdoutRedirected);

            if (isLast)
            {
                lines.AddRange(kept);
                clearScreen = stage.ClearRequested;
            }
            else
            {
                lines.AddRange(kept.Where(line => line.IsError));
                standardInput = stdoutRedirected ? "" : TextOf(kept.Where(line => !line.IsError).ToList(), stage.OmitFinalNewline);
            }
        }

        if (RunsScheduledJobs)
        {
            PassTime(startTime);

            var wroteFile = pipeline.SelectMany(command => command.Redirections)
                .Any(redirection => redirection.Target is not (Redirection.Nowhere or Redirection.ToOutput));
            CommandRan?.Invoke(new Missions.CommandRun(
                [.. pipeline.Select(command => command.Words)], ranAsRoot || Session.User.IsRoot, wroteFile, Session.FileSystem.Now - startTime));
        }

        return clearScreen ? new CommandResult([], clearScreen: true) : new CommandResult(lines, clearScreen: false);
    }

    private sealed record StageResult(IReadOnlyList<OutputLine> Output, bool OmitFinalNewline, bool ClearRequested, bool RanAsRoot = false);

    private StageResult RunStage(ParsedCommand stage, string? standardInput, bool outputIsPiped)
    {
        var words = stage.Words;
        var output = new CommandOutput();

        // A line with only a redirection, like '> notes.txt', just creates (or empties) the file.
        if (words.Count == 0)
        {
            return new StageResult([], false, false);
        }

        if (ResolveCommand(words[0], out var program) is not { } command)
        {
            if (words[0].Contains('/') && Session.GetNode(words[0]) is VirtualFile script)
            {
                RunScript(words[0], script, output);
            }
            else
            {
                output.Error(Text["Error.CommandNotFound", words[0]]);
            }

            return new StageResult(output.Lines, false, false);
        }

        // A set-user-ID program runs as its owner – the session of the logged-in user stays as it is.
        var session = SetUidPrograms.EffectiveUser(Session, command.Name, program) is { } owner
            ? Session.RunningAs(owner)
            : Session;

        var context = new CommandContext(command.Name, [.. words.Skip(1)], standardInput, session, Text, this)
        {
            OutputIsPiped = outputIsPiped,
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

        return new StageResult(
            context.Output.Lines, context.Output.OmitFinalNewline, context.ClearRequested, context.RanAsRoot || (session != Session && session.User.IsRoot));
    }

    /// <summary>
    /// Applies <c>&gt;</c>, <c>&gt;&gt;</c>, <c>2&gt;</c> and <c>2&gt;&amp;1</c>, and returns the lines that are left for the
    /// screen (or the next command). The shell opens the files as the logged-in user – not as a set-user-ID owner.
    /// </summary>
    private List<OutputLine> Redirect(IReadOnlyList<Redirection> redirections, IReadOnlyList<OutputLine> output, out bool stdoutRedirected)
    {
        var lines = output.ToList();
        var problems = new List<OutputLine>();
        var errorRedirect = redirections.LastOrDefault(redirection => redirection.Stream == 2);
        var outputRedirect = redirections.LastOrDefault(redirection => redirection.Stream == 1);
        stdoutRedirected = outputRedirect is not null;

        // 2>&1: error messages become normal output, so they follow it into a file or a pipe.
        if (errorRedirect?.Target == Redirection.ToOutput)
        {
            lines = [.. lines.Select(line => line.IsError ? OutputLine.Plain(line.Text) : line)];
            errorRedirect = null;
        }

        // Every redirection opens (and creates) its file – even an earlier one that the last one overrides.
        foreach (var redirection in redirections.Where(redirection => redirection.Target != Redirection.ToOutput))
        {
            var content = redirection == errorRedirect ? lines.Where(line => line.IsError).ToList()
                : redirection == outputRedirect ? lines.Where(line => !line.IsError).ToList()
                : [];

            if (FileWriter.Write(Session, redirection.Target, TextOf(content, false), redirection.Append) is { } problem)
            {
                problems.Add(OutputLine.Error(Text[problem, redirection.Target]));
            }
        }

        if (errorRedirect is not null)
        {
            lines.RemoveAll(line => line.IsError);
        }

        if (outputRedirect is not null)
        {
            lines.RemoveAll(line => !line.IsError);
        }

        lines.AddRange(problems);
        return lines;
    }

    /// <summary>The lines as text, each ending in a line break – what a pipe or a file gets.</summary>
    private static string TextOf(IReadOnlyList<OutputLine> lines, bool omitFinalNewline)
    {
        var text = string.Join('\n', lines.Select(line => line.Text));
        return lines.Count == 0 || omitFinalNewline ? text : text + "\n";
    }

    /// <summary>Scripts can start scripts, but not forever.</summary>
    private const int MaxScriptDepth = 4;

    /// <summary>
    /// Runs a shell script: every line that isn't empty or a <c># comment</c> is run as a command, as the current
    /// user, in a fresh session (like a sub-shell, so a <c>cd</c> in the script doesn't move the caller).
    /// </summary>
    private void RunScript(string displayPath, VirtualFile script, CommandOutput output)
    {
        if (!Session.CanExecute(script))
        {
            output.Error(Text["Error.ScriptPermissionDenied", displayPath]);
            return;
        }

        if (ScriptDepth >= MaxScriptDepth)
        {
            output.Error(Text["Error.ScriptTooDeep", displayPath]);
            return;
        }

        var shell = Subshell(Session.RunningAs(Session.User));

        foreach (var line in TextHelpers.SplitLines(script.Content).Select(line => line.Trim()))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            foreach (var result in shell.Execute(line).Lines)
            {
                if (result.IsError)
                {
                    output.Error(result.Text);
                }
                else
                {
                    output.WriteLine(result.Text);
                }
            }
        }
    }

    private CommandInterpreter Subshell(ShellSession session) =>
        new(session, _text, Commands)
        {
            RunsScheduledJobs = false,
            ScriptDepth = ScriptDepth + 1,
        };

    /// <summary>
    /// Time moves on a minute for every command (plus whatever <c>sleep</c> added). Cron jobs that were due in that
    /// time run now, in the background: their output goes nowhere, like on a real server.
    /// </summary>
    private void PassTime(DateTime startTime)
    {
        var fileSystem = Session.FileSystem;
        fileSystem.Now = fileSystem.Now.AddMinutes(1);

        var minute = new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, startTime.Minute, 0).AddMinutes(1);

        // A day at most: enough for any sleep, and never a hang.
        for (var runs = 0; minute <= fileSystem.Now && runs < 24 * 60; runs++, minute = minute.AddMinutes(1))
        {
            foreach (var job in Cron.Jobs(fileSystem).Where(job => job.IsDue(minute)))
            {
                RunJob(job, minute);
            }
        }
    }

    private void RunJob(CronJob job, DateTime time)
    {
        var fileSystem = Session.FileSystem;

        if (fileSystem.FindUser(job.User) is not { } user)
        {
            return;
        }

        Subshell(new ShellSession(fileSystem, user.Name)).Execute(job.Command);
        Cron.Log(fileSystem, job, time);
    }
}
