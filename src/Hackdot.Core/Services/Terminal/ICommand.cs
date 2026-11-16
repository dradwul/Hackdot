namespace Hackdot.Core.Services.Terminal;

/// <summary>
/// A terminal command. Its help texts are looked up in <see cref="TerminalResources"/> using the keys
/// <c>Cmd.{Name}.Summary</c>, <c>Cmd.{Name}.Details</c> and <c>Cmd.{Name}.Example{n}</c>.
/// </summary>
public interface ICommand
{
    /// <summary>The command name, e.g. <c>ls</c>. Never translated.</summary>
    string Name { get; }

    /// <summary>Syntax summary, e.g. <c>ls [-a] [-l] [path ...]</c>. Never translated.</summary>
    string Usage { get; }

    /// <summary>Example command lines shown by <c>help &lt;command&gt;</c>.</summary>
    IReadOnlyList<string> Examples { get; }

    void Execute(CommandContext context);
}
