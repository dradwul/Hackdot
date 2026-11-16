namespace Hackdot.Core.Models;

/// <param name="Name">Login name, e.g. <c>user</c> or <c>root</c>.</param>
/// <param name="Home">Home directory, e.g. <c>/home/user</c>.</param>
/// <param name="Password">
/// Optional password for missions with a login screen or <c>su</c>. This is a game – passwords are
/// stored in plain text on purpose so they can be hidden in files for the player to find.
/// </param>
public sealed record VirtualUser(string Name, string Home, string? Password = null)
{
    public const string RootName = "root";

    public bool IsRoot => Name == RootName;
}
