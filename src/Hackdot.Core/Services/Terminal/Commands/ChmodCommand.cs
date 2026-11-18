using Hackdot.Core.Models;

namespace Hackdot.Core.Services.Terminal.Commands;

/// <summary>
/// <c>chmod MODE FILE...</c> – changes permissions. Like Linux, only the owner (or root) may do it, whatever
/// the current permissions are: permissions protect a file from others, not from its owner.
/// </summary>
public sealed class ChmodCommand : ICommand
{
    public string Name => "chmod";

    public string Usage => "chmod <mode> <file>...";

    public IReadOnlyList<string> Examples { get; } = ["chmod 600 ~/Documents/welcome.txt", "chmod u+x,o-r ~/Documents/welcome.txt"];

    public void Execute(CommandContext context)
    {
        var arguments = context.Arguments;

        if (arguments.Count < 2)
        {
            context.Error("Error.MissingOperand", Name);
            return;
        }

        var mode = arguments[0];

        if (!FilePermissions.TryApply(mode, 0, out _))
        {
            context.Error("Error.ChmodInvalidMode", mode);
            return;
        }

        var session = context.Session;

        foreach (var path in arguments.Skip(1))
        {
            if (session.GetNode(path) is not { } node)
            {
                context.PathNotFound("Error.CannotAccess", path);
                continue;
            }

            if (!session.CanReach(node))
            {
                context.Error("Error.PermissionDenied", Name, path);
                continue;
            }

            if (!session.User.IsRoot && node.Owner != session.User.Name)
            {
                context.Error("Error.ChmodNotOwner", path);
                continue;
            }

            FilePermissions.TryApply(mode, node.Mode, out var updated);
            node.Mode = updated;
        }
    }
}
