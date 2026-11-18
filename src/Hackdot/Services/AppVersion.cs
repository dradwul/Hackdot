using System.Reflection;

namespace Hackdot.Services;

/// <summary>
/// The app's version, from <c>&lt;Version&gt;</c> in Hackdot.csproj. The build adds the git commit after a '+'
/// (e.g. <c>1.4.0+0b34370…</c>), which is handy to tell two builds of the same version apart.
/// </summary>
public static class AppVersion
{
    public const string ChangelogUrl = "https://github.com/dradwul/Hackdot/blob/main/CHANGELOG.md";

    private static readonly string Informational =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppVersion).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    /// <summary>The version number, e.g. <c>1.4.0</c>.</summary>
    public static string Number => Informational.Split('+')[0];

    /// <summary>The short git commit the app was built from, if known.</summary>
    public static string? Commit =>
        Informational.Split('+') is [_, var commit, ..] && commit.Length > 0 ? commit[..Math.Min(7, commit.Length)] : null;
}
