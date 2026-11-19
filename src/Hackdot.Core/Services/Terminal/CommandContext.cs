using Hackdot.Core.Models;
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

    /// <summary>The command ran something as root on the player's behalf (e.g. <c>sudo</c>), for achievements.</summary>
    public bool RanAsRoot { get; set; }

    /// <summary>
    /// <c>true</c> when the output goes into another command through a pipe instead of to the screen.
    /// Like real Linux, <c>ls</c> then prints one name per line.
    /// </summary>
    public bool OutputIsPiped { get; init; }

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
    /// Reads the text a filter command works on: the named files, or the piped input when no file is named.
    /// Files that cannot be read get an error and are left out.
    /// </summary>
    public IReadOnlyList<TextInput> ReadInputs(IReadOnlyList<string> paths)
    {
        var inputs = new List<TextInput>();

        if (paths.Count == 0)
        {
            if (StandardInput is { } input)
            {
                inputs.Add(new TextInput(null, input));
            }
            else
            {
                Error("Error.NoInput", Name);
            }

            return inputs;
        }

        foreach (var path in paths)
        {
            switch (Session.GetNode(path))
            {
                case null:
                    PathNotFound("Error.NoSuchFile", path);
                    break;
                case VirtualDirectory:
                    Error("Error.IsADirectory", Name, path);
                    break;
                case VirtualFile file when !Session.CanRead(file):
                    Error("Error.PermissionDenied", Name, path);
                    break;
                case VirtualFile file:
                    inputs.Add(new TextInput(path, file.Content));
                    break;
            }
        }

        return inputs;
    }

    /// <summary>
    /// Splits the arguments into single-letter flags (<c>-la</c> → <c>l</c>, <c>a</c>) and operands.
    /// <c>--</c> ends the flags. Writes an error and returns <c>false</c> on an unknown flag.
    /// </summary>
    /// <param name="valueFlags">
    /// Flags that take a value, written either joined (<c>-n5</c>, <c>-d:</c>) or as the next argument (<c>-n 5</c>).
    /// If <c>n</c> is one of them, <c>-5</c> is short for <c>-n 5</c> (like <c>head -5</c>).
    /// </param>
    public bool TryParseOptions(string allowedFlags, out ParsedArguments parsed, string valueFlags = "")
    {
        var flags = new HashSet<char>();
        var values = new Dictionary<char, string>();
        var operands = new List<string>();
        var flagsEnded = false;
        parsed = ParsedArguments.Empty;

        for (var i = 0; i < Arguments.Count; i++)
        {
            var argument = Arguments[i];

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

            if (valueFlags.Contains('n') && argument.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0)
            {
                flags.Add('n');
                values['n'] = argument[1..];
                continue;
            }

            for (var j = 1; j < argument.Length; j++)
            {
                var flag = argument[j];

                if (valueFlags.Contains(flag))
                {
                    if (j + 1 < argument.Length)
                    {
                        values[flag] = argument[(j + 1)..];
                    }
                    else if (i + 1 < Arguments.Count)
                    {
                        values[flag] = Arguments[++i];
                    }
                    else
                    {
                        Error("Error.MissingArgument", Name, "-" + flag);
                        return false;
                    }

                    flags.Add(flag);
                    break;
                }

                if (!allowedFlags.Contains(flag))
                {
                    Error("Error.InvalidOption", Name, flag);
                    return false;
                }

                flags.Add(flag);
            }
        }

        parsed = new ParsedArguments(flags, operands, values);
        return true;
    }
}

/// <summary>Text for a filter command: a file's content (labelled with the path as typed) or piped input (no label).</summary>
public sealed record TextInput(string? Label, string Content);

public sealed class ParsedArguments(
    IReadOnlySet<char> flags,
    IReadOnlyList<string> operands,
    IReadOnlyDictionary<char, string>? values = null)
{
    public static ParsedArguments Empty { get; } = new(new HashSet<char>(), []);

    public IReadOnlySet<char> Flags { get; } = flags;

    public IReadOnlyList<string> Operands { get; } = operands;

    public bool Has(char flag) => Flags.Contains(flag);

    /// <summary>The value of a flag that takes one (e.g. <c>5</c> for <c>-n 5</c>), or <c>null</c>.</summary>
    public string? Value(char flag) => values?.GetValueOrDefault(flag);
}
