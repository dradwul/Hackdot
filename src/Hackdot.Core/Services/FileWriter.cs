using Hackdot.Core.Models;

namespace Hackdot.Core.Services;

/// <summary>
/// Writes files on behalf of a user, with the same rules as Linux: an existing file needs write permission
/// (<c>w</c>), and a new file needs write permission on its folder. New files belong to the user who writes
/// them and get mode 644. <c>/dev/null</c> swallows everything.
/// </summary>
public static class FileWriter
{
    public const string DevNull = "/dev/null";

    /// <returns>
    /// <c>null</c> on success, otherwise the resource key of the problem: <c>Error.RedirectNoSuchFile</c>,
    /// <c>Error.RedirectIsADirectory</c> or <c>Error.RedirectPermissionDenied</c>.
    /// </returns>
    public static string? Write(ShellSession session, string path, string text, bool append)
    {
        var fileSystem = session.FileSystem;
        var fullPath = session.Resolve(path);

        if (fullPath == DevNull)
        {
            return null;
        }

        switch (fileSystem.GetNode(fullPath))
        {
            case VirtualDirectory:
                return "Error.RedirectIsADirectory";

            case VirtualFile file:
                if (!session.CanWrite(file))
                {
                    return "Error.RedirectPermissionDenied";
                }

                file.Content = append ? file.Content + text : text;
                file.Modified = fileSystem.Now;
                return null;

            default:
                if (fileSystem.GetDirectory(VirtualPath.GetParent(fullPath)) is not { } folder)
                {
                    return "Error.RedirectNoSuchFile";
                }

                if (!session.CanWrite(folder))
                {
                    return "Error.RedirectPermissionDenied";
                }

                fileSystem.WriteFile(fullPath, text, session.User.Name, FilePermissions.DefaultFile, fileSystem.Now);
                return null;
        }
    }
}
