using System.Text.Json;
using Hackdot.Core.Models.Guide;

namespace Hackdot.Core.Services;

public static class GuideCatalog
{
    public const string TopicsFile = "Guide/topics.json";

    public static GuideDefinition LoadEmbedded() => Parse(EmbeddedContent.Read(TopicsFile));

    public static GuideDefinition Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, CoreJsonContext.Default.GuideDefinition)
                ?? throw new InvalidDataException("The guide is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid guide definition: {ex.Message}", ex);
        }
    }
}
