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

/// <summary>
/// <c>sudo &lt;command&gt;</c>: run one command as root, if <c>/etc/sudoers</c> allows it. <c>sudo -l</c> lists what you may run.
/// </summary>
public sealed class SudoCommand : ICommand
{
    public string Name => "sudo";

    public string Usage => "sudo -l | sudo <command> [arguments]";

    public IReadOnlyList<string> Examples { get; } = ["sudo -l", "sudo cat /var/log/syslog"];

    public void Execute(CommandContext context)
    {
        var arguments = context.Arguments;
        var session = context.Session;

        if (arguments.Count == 0)
        {
            context.Error("Error.MissingOperand", Name);
            return;
        }

        if (arguments[0] == "-l")
        {
            List(context);
            return;
        }

        if (arguments[0].StartsWith('-'))
        {
            context.Error("Error.InvalidOption", Name, arguments[0].TrimStart('-'));
            return;
        }

        // 'sudo cat' and 'sudo /usr/bin/cat' are the same command.
        var commandName = arguments[0][(arguments[0].LastIndexOf('/') + 1)..];
        IReadOnlyList<string> commandArguments = [.. arguments.Skip(1)];

        if (context.Interpreter.FindCommand(commandName) is not { } command)
        {
            context.Error("Sudo.CommandNotFound", arguments[0]);
            return;
        }

        if (session.User.IsRoot)
        {
            Run(context, command, commandArguments, context.Output);
            return;
        }

        var rules = Sudoers.RulesFor(session.FileSystem, session.User.Name);

        if (rules.Count == 0)
        {
            context.Error("Sudo.NotInSudoers", session.User.Name);
            return;
        }

        if (rules.FirstOrDefault(rule => rule.Allows(command.Name, commandArguments)) is not { } allowed)
        {
            var path = rules.FirstOrDefault(rule => rule.CommandName == command.Name)?.CommandPath ?? arguments[0];
            context.Error("Sudo.NotAllowed", session.User.Name, string.Join(' ', commandArguments.Prepend(path)), session.FileSystem.Hostname);
            return;
        }

        if (allowed.NoPassword || !session.User.RequiresPassword)
        {
            Run(context, command, commandArguments, context.Output);
            return;
        }

        // sudo asks for YOUR password (not root's): it checks that it really is you at the keyboard.
        context.RequestInput(context.Localize("Sudo.Password", session.User.Name), secret: true, (password, output) =>
        {
            if (session.User.Accepts(password))
            {
                Run(context, command, commandArguments, output);
            }
            else
            {
                output.Error(context.Localize("Sudo.WrongPassword"));
            }
        });
    }

    private static void List(CommandContext context)
    {
        var session = context.Session;
        var hostname = session.FileSystem.Hostname;
        IReadOnlyList<SudoRule> rules = session.User.IsRoot
            ? [new SudoRule(VirtualUser.RootName, "ALL", false, SudoRule.All, null)]
            : Sudoers.RulesFor(session.FileSystem, session.User.Name);

        if (rules.Count == 0)
        {
            context.Output.WriteLine(context.Localize("Sudo.ListNone", session.User.Name, hostname), OutputStyle.Muted);
            return;
        }

        context.Output.WriteLine(context.Localize("Sudo.ListHeader", session.User.Name, hostname));

        foreach (var rule in rules)
        {
            context.Output.WriteLine("    " + rule.Display, OutputStyle.Success);
        }
    }

    private static void Run(CommandContext context, ICommand command, IReadOnlyList<string> arguments, CommandOutput output)
    {
        var session = context.Session;
        var root = session.FileSystem.FindUser(VirtualUser.RootName)!;

        // 'sudo su' is the classic way to get a root shell: it really switches the terminal's user.
        if (command is SuCommand)
        {
            var target = arguments.FirstOrDefault(argument => !argument.StartsWith('-')) ?? VirtualUser.RootName;

            if (session.FileSystem.FindUser(target) is not { } user)
            {
                output.Error(context.Localize("Su.UnknownUser", target));
                return;
            }

            session.PushUser(user, goHome: arguments.Any(argument => argument is "-" or "-l" or "--login"));
            output.WriteLine(context.Localize("Su.Switched", user.Name), OutputStyle.Muted);
            return;
        }

        // Everything else runs once, as root, in a throw-away session in the same folder.
        var rootSession = new ShellSession(session.FileSystem, root.Name);

        if (session.FileSystem.GetDirectory(session.CurrentDirectory) is { } directory)
        {
            rootSession.ChangeDirectory(directory);
        }

        var inner = new CommandContext(command.Name, arguments, context.StandardInput, rootSession, context.Text, context.Interpreter)
        {
            OutputIsPiped = context.OutputIsPiped,
        };

        command.Execute(inner);
        output.Append(inner.Output);
        context.ClearRequested |= inner.ClearRequested;
    }
}
