using Hackdot.Core.Models.Guide;
using Hackdot.Core.Services;
using Microsoft.JSInterop;

namespace Hackdot.Services;

public enum GuideTab
{
    Learn,
    Commands,
    Cards,
    Notes,
}

/// <summary>
/// State of the Guide app: the open tab, which entries are expanded and the player's notes
/// (saved in the browser, like a real hacker's notebook).
/// </summary>
public sealed class GuideService
{
    private const string NotesStorageKey = "hackdot.notes.v1";

    private readonly IJSInProcessRuntime? _js;
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _commandNames;

    public GuideService(IJSRuntime js, TerminalService terminal)
    {
        _js = js as IJSInProcessRuntime;
        Guide = GuideCatalog.LoadEmbedded();
        _commandNames = terminal.Interpreter.Commands.Select(command => command.Name).ToHashSet(StringComparer.Ordinal);
        Notes = ReadNotes();
    }

    public GuideDefinition Guide { get; }

    public GuideTab Tab { get; private set; } = GuideTab.Learn;

    public string Notes { get; private set; }

    public event Action? Changed;

    public bool IsExpanded(string key) => _expanded.Contains(key);

    public void SelectTab(GuideTab tab)
    {
        Tab = tab;
        Changed?.Invoke();
    }

    public void Toggle(string key)
    {
        if (!_expanded.Remove(key))
        {
            _expanded.Add(key);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Opens the guide entry for a mission skill: a command (<c>ls -a</c> → <c>ls</c>) or a topic (<c>pipes</c>).
    /// </summary>
    public void Focus(string skill)
    {
        var command = skill.Split(' ', 2)[0];

        if (_commandNames.Contains(command))
        {
            Tab = GuideTab.Commands;
            _expanded.Add(CommandKey(command));
        }
        else if (Guide.Topics.Any(topic => topic.Id == skill))
        {
            Tab = GuideTab.Learn;
            _expanded.Add(TopicKey(skill));
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// How a mission skill is shown: commands as typed (<c>grep -r</c>), topics by their title
    /// (<c>privilege-escalation</c> → "Privilege escalation and sudo").
    /// </summary>
    public string SkillLabel(string skill) =>
        Guide.Topics.FirstOrDefault(topic => topic.Id == skill)?.Title.Get() ?? skill;

    public void SaveNotes(string notes)
    {
        Notes = notes;

        try
        {
            _js?.InvokeVoid("localStorage.setItem", NotesStorageKey, notes);
        }
        catch (JSException)
        {
            // Storage blocked: the notes still work for this session.
        }
    }

    public static string CommandKey(string name) => "cmd:" + name;

    public static string TopicKey(string id) => "topic:" + id;

    public static string CardKey(string id) => "card:" + id;

    /// <summary>Opens the Cards tab with one card unfolded (e.g. the one a mission just gave).</summary>
    public void FocusCard(string id)
    {
        Tab = GuideTab.Cards;
        _expanded.Add(CardKey(id));
        Changed?.Invoke();
    }

    private string ReadNotes()
    {
        try
        {
            return _js?.Invoke<string?>("localStorage.getItem", NotesStorageKey) ?? "";
        }
        catch (JSException)
        {
            return "";
        }
    }
}
