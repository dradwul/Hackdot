using System.Text.Json;
using System.Text.Json.Serialization;
using Hackdot.Core.Models.Definitions;
using Hackdot.Core.Models.Guide;
using Hackdot.Core.Models.Missions;
using Hackdot.Core.Services.Missions;

namespace Hackdot.Core.Services;

/// <summary>
/// Source-generated JSON metadata (trim-safe for Blazor WebAssembly). Unknown properties are
/// rejected so typos in mission files fail loudly instead of being silently ignored.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(FileSystemDefinition))]
[JsonSerializable(typeof(MissionDefinition))]
[JsonSerializable(typeof(GameProgressSnapshot))]
[JsonSerializable(typeof(GuideDefinition))]
[JsonSerializable(typeof(TechniqueCardsDefinition))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
