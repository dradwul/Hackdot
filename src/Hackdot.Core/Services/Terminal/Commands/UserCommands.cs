using Hackdot.Core.Models;

namespace Hackdot.Core.Services.Terminal.Commands;

/// <summary><c>su [-] [user]</c>: become another user (root by default) by typing their password.</summary>
public sealed class SuCommand : ICommand
{
    public string Name => "su";

    public string Usage => "su [-] [user]";

    public IReadOnlyList<string> Examples { get; } = ["su admin", "su - admin", "exit"];

    public void Execute(CommandContext context)
    {
        var goHome = false;
        var names = new List<string>();

        foreach (var argument in context.Arguments)
        {
            if (argument is "-" or "-l" or "--login")
            {
                goHome = true;
            }
            else if (argument.StartsWith('-'))
            {
                context.Error("Error.InvalidOption", Name, argument.TrimStart('-'));
                return;
            }
            else
            {
                names.Add(argument);
            }
        }

        if (names.Count > 1)
        {
            context.Error("Error.TooManyArguments", Name);
            return;
        }

        var name = names.Count == 1 ? names[0] : VirtualUser.RootName;
        var session = context.Session;

        if (session.FileSystem.FindUser(name) is not { } target)
        {
            context.Error("Su.UnknownUser", name);
            return;
        }

        // Root may become anyone, and accounts without a password need none.
        if (session.User.IsRoot || !target.RequiresPassword)
        {
            Switch(context, session, target, goHome, context.Output);
            return;
        }

        context.RequestInput(context.Localize("Su.Password"), secret: true, (password, output) =>
        {
            if (target.Accepts(password))
            {
                Switch(context, session, target, goHome, output);
            }
            else
            {
                output.Error(context.Localize("Su.Failure"));
            }
        });
    }

    private static void Switch(CommandContext context, ShellSession session, VirtualUser target, bool goHome, CommandOutput output)
    {
        session.PushUser(target, goHome);
        output.WriteLine(context.Localize("Su.Switched", target.Name), OutputStyle.Muted);
    }
}

/// <summary><c>exit</c>: go back to the user you were before <c>su</c>.</summary>
public sealed class ExitCommand : ICommand
{
    public string Name => "exit";

    public string Usage => "exit";

    public IReadOnlyList<string> Examples { get; } = ["exit"];

    public void Execute(CommandContext context)
    {
        if (context.Session.PopUser())
        {
            context.Output.WriteLine(context.Localize("Exit.Back", context.Session.User.Name), OutputStyle.Muted);
        }
        else
        {
            context.Output.WriteLine(context.Localize("Exit.Nowhere"), OutputStyle.Muted);
        }
    }
}
