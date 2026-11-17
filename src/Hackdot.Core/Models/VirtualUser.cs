namespace Hackdot.Core.Models;

/// <param name="Name">Login name, e.g. <c>user</c> or <c>root</c>.</param>
/// <param name="Home">Home directory, e.g. <c>/home/user</c>.</param>
/// <param name="Password">
/// Optional password for the login screen and <c>su</c>. This is a game – passwords are stored in plain
/// text on purpose so they can be hidden in files for the player to find. A user without a password can log
/// in without one, except <c>root</c>: like on Ubuntu, root without a password is locked.
/// </param>
public sealed record VirtualUser(string Name, string Home, string? Password = null)
{
    public const string RootName = "root";

    public bool IsRoot => Name == RootName;

    /// <summary>Whether this account can be used at all (root without a password is locked).</summary>
    public bool CanLogIn => !(IsRoot && Password is null);

    /// <summary>Whether a password has to be typed to become this user.</summary>
    public bool RequiresPassword => Password is not null || IsRoot;

    /// <summary>Passwords are case-sensitive, like on a real system.</summary>
    public bool Accepts(string? password) =>
        CanLogIn && (Password is null || string.Equals(password, Password, StringComparison.Ordinal));
}
