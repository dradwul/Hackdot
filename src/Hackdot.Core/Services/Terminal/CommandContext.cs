using Microsoft.Extensions.Localization;

namespace Hackdot.Core.Services.Terminal;

/// <summary>Everything a command needs while it runs.</summary>
public sealed class CommandContext
{
    internal CommandContext(
        string name,
        IReadOnlyList<string> arguments,
        string? standardInput,
        ShellSession session,
        IStringLocalizer text,
        CommandInterpreter interpreter)
    {
        Name = name;
        Arguments = arguments;
        StandardInput = standardInput;
        Session = session;
        Text = text;
        Interpreter = interpreter;
    }

    public string Name { get; }

    public IReadOnlyList<string> Arguments { get; }

    /// <summary>Text piped in from the previous command, or <c>null</c> when not in a pipeline.</summary>
    public string? StandardInput { get; }

    public ShellSession Session { get; }

    public IStringLocalizer Text { get; }

    public CommandInterpreter Interpreter { get; }

    public CommandOutput Output { get; } = new();

    public bool ClearRequested { get; set; }

    public string Localize(string key, params object[] arguments) => Text[key, arguments];

    /// <summary>
    /// Asks the player for one more line of input (like <c>Password:</c>). <paramref name="onInput"/> runs with
    /// the answer on the next submitted line; its output becomes that line's output.
    /// </summary>
    public void RequestInput(string label, bool secret, Action<string, CommandOutput> onInput) =>
        Interpreter.RequestInput(label, secret, onInput);

    /// <summary>Writes a localized error line.</summary>
    public void Error(string key, params object[] arguments) => Output.Error(Text[key, arguments]);

    /// <summary>
    /// Reports a path that does not exist (<paramref name="errorKey"/> gets the command name and the path),
    /// plus a "Did you mean …?" tip when the path only has the wrong upper/lower case.
    /// </summary>
    public void PathNotFound(string errorKey, string path)
    {
        Error(errorKey, Name, path);

        if (PathSuggester.Suggest(Session, path) is { } suggestion)
        {
            Output.Hint(Text["Hint.DidYouMean", suggestion]);
        }
    }

    /// <summary>
    /// Splits the arguments into single-letter flags (<c>-la</c> → <c>l</c>, <c>a</c>) and operands.
    /// <c>--</c> ends the flags. Writes an error and returns <c>false</c> on an unknown flag.
    /// </summary>
    public bool TryParseOptions(string allowedFlags, out ParsedArguments parsed)
    {
        var flags = new HashSet<char>();
        var operands = new List<string>();
        var flagsEnded = false;

        foreach (var argument in Arguments)
        {
            if (flagsEnded || argument.Length < 2 || argument[0] != '-')
            {
                operands.Add(argument);
                continue;
            }

            if (argument == "--")
            {
                flagsEnded = true;
                continue;
            }

            foreach (var flag in argument.AsSpan(1))
            {
                if (!allowedFlags.Contains(flag))
                {
                    Error("Error.InvalidOption", Name, flag);
                    parsed = ParsedArguments.Empty;
                    return false;
                }

                flags.Add(flag);
            }
        }

        parsed = new ParsedArguments(flags, operands);
        return true;
    }
}

public sealed class ParsedArguments(IReadOnlySet<char> flags, IReadOnlyList<string> operands)
{
    public static ParsedArguments Empty { get; } = new(new HashSet<char>(), []);

    public IReadOnlySet<char> Flags { get; } = flags;

    public IReadOnlyList<string> Operands { get; } = operands;

    public bool Has(char flag) => Flags.Contains(flag);
}
