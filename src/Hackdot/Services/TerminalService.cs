using Hackdot.Core.Services.Terminal;

namespace Hackdot.Services;

/// <summary>
/// Keeps the terminal's scrollback and command history alive while the window is minimized or closed.
/// </summary>
public sealed class TerminalService
{
    private const int MaxLines = 2000;

    private readonly List<OutputLine> _lines = [];
    private readonly List<string> _history = [];

    public TerminalService(CommandInterpreter interpreter)
    {
        Interpreter = interpreter;
        _lines.AddRange(interpreter.Welcome());
    }

    public CommandInterpreter Interpreter { get; }

    public IReadOnlyList<OutputLine> Lines => _lines;

    /// <summary>Previously run commands, oldest first.</summary>
    public IReadOnlyList<string> History => _history;

    public string DisplayDirectory => Interpreter.Session.DisplayDirectory;

    public bool IsRoot => Interpreter.Session.User.IsRoot;

    public event Action? Changed;

    public void Submit(string input)
    {
        // Capture the prompt before running: 'cd' changes it.
        _lines.Add(Interpreter.Prompt(input));

        if (!string.IsNullOrWhiteSpace(input) && (_history.Count == 0 || _history[^1] != input))
        {
            _history.Add(input);
        }

        var result = Interpreter.Execute(input);

        if (result.ClearScreen)
        {
            _lines.Clear();
        }
        else
        {
            _lines.AddRange(result.Lines);
        }

        if (_lines.Count > MaxLines)
        {
            _lines.RemoveRange(0, _lines.Count - MaxLines);
        }

        Changed?.Invoke();
    }
}
