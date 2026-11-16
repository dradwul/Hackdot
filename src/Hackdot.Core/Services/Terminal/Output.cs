namespace Hackdot.Core.Services.Terminal;

/// <summary>Semantic styles; the UI decides the actual colors.</summary>
public enum OutputStyle
{
    Default,
    Muted,
    Accent,
    Directory,
    Success,
    Warning,
    Error,
    Highlight,
}

public readonly record struct OutputSegment(string Text, OutputStyle Style = OutputStyle.Default);

/// <summary>One line of terminal output, made of styled segments.</summary>
public sealed class OutputLine
{
    public OutputLine(IReadOnlyList<OutputSegment> segments, bool isError = false, int hangingIndent = 0)
    {
        Segments = segments;
        IsError = isError;
        HangingIndent = hangingIndent;
    }

    public IReadOnlyList<OutputSegment> Segments { get; }

    /// <summary>
    /// Number of character columns wrapped lines are indented by, so two-column output
    /// (like the command list in <c>help</c>) stays aligned on narrow screens.
    /// </summary>
    public int HangingIndent { get; }

    /// <summary>Error lines go to "stderr": they are shown but never piped into the next command.</summary>
    public bool IsError { get; }

    public string Text => string.Concat(Segments.Select(segment => segment.Text));

    public static OutputLine Plain(string text, OutputStyle style = OutputStyle.Default) => new([new(text, style)]);

    public static OutputLine Error(string text) => new([new(text, OutputStyle.Error)], isError: true);
}

/// <summary>Collects the output of a command, segment by segment.</summary>
public sealed class CommandOutput
{
    private readonly List<OutputLine> _lines = [];
    private List<OutputSegment>? _pending;
    private int _pendingIndent;

    public IReadOnlyList<OutputLine> Lines
    {
        get
        {
            Flush();
            return _lines;
        }
    }

    /// <summary>The non-error output as plain text – what a pipe passes on.</summary>
    public string StandardOutput => string.Join('\n', Lines.Where(line => !line.IsError).Select(line => line.Text));

    /// <summary>Appends text to the current line.</summary>
    public CommandOutput Write(string text, OutputStyle style = OutputStyle.Default)
    {
        if (text.Length > 0)
        {
            (_pending ??= []).Add(new OutputSegment(text, style));
        }

        return this;
    }

    /// <summary>Appends text and ends the line.</summary>
    public CommandOutput WriteLine(string text = "", OutputStyle style = OutputStyle.Default)
    {
        Write(text, style);
        EndLine();
        return this;
    }

    /// <summary>Sets the hanging indent (in characters) for the line currently being written.</summary>
    public CommandOutput HangingIndent(int columns)
    {
        _pendingIndent = columns;
        return this;
    }

    public void EndLine()
    {
        _lines.Add(new OutputLine(_pending is null ? [] : [.. _pending], hangingIndent: _pendingIndent));
        _pending = null;
        _pendingIndent = 0;
    }

    public void Error(string message)
    {
        Flush();
        _lines.Add(OutputLine.Error(message));
    }

    private void Flush()
    {
        if (_pending is not null)
        {
            EndLine();
        }
    }
}

public sealed class CommandResult
{
    public static CommandResult Empty { get; } = new([], clearScreen: false);

    public CommandResult(IReadOnlyList<OutputLine> lines, bool clearScreen)
    {
        Lines = lines;
        ClearScreen = clearScreen;
    }

    public IReadOnlyList<OutputLine> Lines { get; }

    /// <summary>The command asked the terminal to clear its scrollback (<c>clear</c>).</summary>
    public bool ClearScreen { get; }
}
