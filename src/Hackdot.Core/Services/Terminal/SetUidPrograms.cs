using Hackdot.Core.Models;

namespace Hackdot.Core.Services.Terminal;

/// <summary>
/// Set-user-ID programs: a command whose program file (e.g. <c>/usr/bin/base64</c>) has the 4000 bit runs with
/// the permissions of the file's owner instead of the player's. Handy for <c>passwd</c> – and a classic hole when
/// a program that reads files is set-user-ID root.
/// </summary>
public static class SetUidPrograms
{
    /// <summary>Folders searched for a command's program file, like <c>$PATH</c>.</summary>
    public static readonly IReadOnlyList<string> SearchPath = ["/usr/local/bin", "/usr/bin", "/bin"];

    /// <summary>
    /// Commands that never change user this way: shell built-ins (no program file in real Linux either) and
    /// <c>su</c>/<c>sudo</c>, which are set-user-ID on purpose and check passwords and rules themselves.
    /// </summary>
    public static readonly IReadOnlySet<string> Ignored =
        new HashSet<string>(StringComparer.Ordinal) { "cd", "exit", "help", "history", "clear", "su", "sudo" };

    /// <summary>The program file the shell would run for <paramref name="commandName"/>, if it exists.</summary>
    public static VirtualFile? FindProgram(VirtualFileSystem fileSystem, string commandName) =>
        SearchPath
            .Select(folder => fileSystem.GetFile(folder + "/" + commandName))
            .FirstOrDefault(file => file is not null);

    /// <summary>
    /// The user a command runs as when <paramref name="program"/> is set-user-ID and the session may run it,
    /// or <c>null</c> to run it as the session's own user.
    /// </summary>
    public static VirtualUser? EffectiveUser(ShellSession session, string commandName, VirtualFile? program)
    {
        if (program is null || Ignored.Contains(commandName) || !FilePermissions.HasSetUid(program.Mode))
        {
            return null;
        }

        if (program.Owner == session.User.Name || !session.CanExecute(program))
        {
            return null;
        }

        return session.FileSystem.FindUser(program.Owner);
    }

    /// <summary>Every set-user-ID program for one of <paramref name="commandNames"/> (used to check missions).</summary>
    public static IEnumerable<VirtualFile> All(VirtualFileSystem fileSystem, IEnumerable<string> commandNames) =>
        commandNames
            .Where(name => !Ignored.Contains(name))
            .Select(name => FindProgram(fileSystem, name))
            .OfType<VirtualFile>()
            .Where(file => FilePermissions.HasSetUid(file.Mode));
}
