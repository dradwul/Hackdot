using Hackdot.Core.Models;
using Hackdot.Core.Services;
using Hackdot.Core.Services.Terminal;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hackdot.Core.Tests;

internal static class TestTerminal
{
    public static IStringLocalizer<TerminalResources> Text { get; } =
        new StringLocalizer<TerminalResources>(
            new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));

    /// <summary>A terminal on the embedded base system, logged in as <c>user</c> in <c>/home/user</c>.</summary>
    public static CommandInterpreter Create(Action<VirtualFileSystem>? setup = null)
    {
        var fileSystem = FileSystemLoader.CreateDefault();
        setup?.Invoke(fileSystem);
        return new CommandInterpreter(new ShellSession(fileSystem), Text);
    }

    /// <summary>Runs a command and returns its output as plain text, one line per output line.</summary>
    public static string Run(this CommandInterpreter terminal, string input) =>
        string.Join('\n', terminal.Execute(input).Lines.Select(line => line.Text));
}
