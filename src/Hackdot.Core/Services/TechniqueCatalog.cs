using System.Text.Json;
using Hackdot.Core.Models.Guide;
using Hackdot.Core.Models.Missions;
using Hackdot.Core.Services.Missions;

namespace Hackdot.Core.Services;

/// <summary>The technique cards, and which of them a player has collected.</summary>
public static class TechniqueCatalog
{
    public const string CardsFile = "Guide/cards.json";

    public static IReadOnlyList<TechniqueCard> LoadEmbedded() => Parse(EmbeddedContent.Read(CardsFile));

    public static IReadOnlyList<TechniqueCard> Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, CoreJsonContext.Default.TechniqueCardsDefinition)?.Cards
                ?? throw new InvalidDataException("There are no technique cards.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid technique cards: {ex.Message}", ex);
        }
    }

    /// <summary>A card is collected once any mission that teaches it is solved.</summary>
    public static bool IsCollected(GameState state, TechniqueCard card) =>
        TaughtBy(state.Catalog, card).Any(mission => state.Progress(mission).Completed);

    /// <summary>The missions that teach a card, in play order.</summary>
    public static IEnumerable<MissionDefinition> TaughtBy(MissionCatalog catalog, TechniqueCard card) =>
        catalog.Missions.Where(mission => mission.Techniques.Contains(card.Id));
}
