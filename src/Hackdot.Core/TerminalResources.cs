using Microsoft.Extensions.Localization;

// Resources for this assembly live in the Resources/ folder, regardless of the host's options.
[assembly: ResourceLocation("Resources")]

namespace Hackdot.Core;

/// <summary>
/// Marker type for terminal texts (help, error messages). Use <c>IStringLocalizer&lt;TerminalResources&gt;</c>;
/// strings live in <c>Resources/TerminalResources.resx</c> (English) and <c>TerminalResources.{culture}.resx</c>.
/// Command texts follow the convention <c>Cmd.{name}.Summary</c>, <c>Cmd.{name}.Details</c> and
/// <c>Cmd.{name}.Example{n}</c> (1-based, one per entry in <see cref="Services.Terminal.ICommand.Examples"/>).
/// </summary>
public sealed class TerminalResources;
