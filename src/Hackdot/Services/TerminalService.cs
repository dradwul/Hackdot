using Hackdot.Core;
using Hackdot.Core.Services.Terminal;
using Microsoft.Extensions.Localization;

namespace Hackdot.Services;

/// <summary>
/// Keeps the terminal's scrollback and command history alive while the window is minimized or closed.
/// Starting a new mission connects the terminal to that mission's machine.
/// </summary>
public sealed class TerminalService
{
    private const int MaxLines = 2000;

    private readonly GameService _game;
    private readonly IStringLocalizer<TerminalResources> _text;
    private readonly List<OutputLine> _lines = [];
    private readonly List<string> _history = [];

    public TerminalService(GameService game, IStringLocalizer<TerminalResources> text)
    {
        _game = game;
        _text = text;
        Interpreter = Connect();
        _game.SessionChanged += OnSessionChanged;
    }

    public CommandInterpreter Interpreter { get; private set; }

    public IReadOnlyList<OutputLine> Lines => _lines;

    /// <summary>Previously run commands, oldest first (shared with the interpreter's <c>history</c> command).</summary>
    public IReadOnlyList<string> History => Interpreter.History;

    public string DisplayDirectory => Interpreter.Session.DisplayDirectory;

    public bool IsRoot => Interpreter.Session.User.IsRoot;

    /// <summary>Set while a command waits for an answer, e.g. <c>Password:</c> after <c>su</c>.</summary>
    public string? PendingInputLabel => Interpreter.PendingInputLabel;

    /// <summary>The answer should be typed in a hidden field and never echoed or remembered.</summary>
    public bool IsSecretInput => Interpreter.PendingInputIsSecret;

    /// <summary>Tab completion of the last word, limited to what the player is allowed to see.</summary>
    public Completion Complete(string input) => TabCompleter.Complete(Interpreter, input);

    public event Action? Changed;

    public void Submit(string input)
    {
        if (PendingInputLabel is { } label)
        {
            // Answering a question: show the question, but never the (secret) answer, and keep it out of history.
            var secret = IsSecretInput;
            _lines.Add(secret ? OutputLine.Plain(label) : new OutputLine([new(label + " "), new(input)]));
            AddLines(Interpreter.Execute(input));
            Changed?.Invoke();
            return;
        }

        // Capture the prompt before running: 'cd' changes it. The interpreter remembers the command in History.
        _lines.Add(Interpreter.Prompt(input));
        AddLines(Interpreter.Execute(input));
        Changed?.Invoke();
    }

    private void AddLines(CommandResult result)
    {
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
    }

    private CommandInterpreter Connect()
    {
        // The same history list is handed to every new interpreter, so ↑ still works after switching missions.
        var interpreter = new CommandInterpreter(_game.CreateSession(), _text, history: _history);

        _lines.Clear();
        _lines.AddRange(interpreter.Welcome());
        _lines.Insert(1, OutputLine.Plain(
            _text["Welcome.Mission", _game.FileSystem.Hostname, _game.ActiveMission.Title.Get()],
            OutputStyle.Success));

        return interpreter;
    }

    private void OnSessionChanged()
    {
        Interpreter = Connect();
        Changed?.Invoke();
    }
}
