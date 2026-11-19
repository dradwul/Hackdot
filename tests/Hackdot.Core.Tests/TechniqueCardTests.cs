using Hackdot.Core.Models.Guide;
using Hackdot.Core.Services;
using Hackdot.Core.Services.Missions;

namespace Hackdot.Core.Tests;

public class TechniqueCardTests
{
    private static readonly IReadOnlyList<TechniqueCard> Cards = TechniqueCatalog.LoadEmbedded();

    private static readonly MissionCatalog Catalog = MissionCatalog.LoadEmbedded();

    [Fact]
    public void Cards_HaveUniqueIdsAndAllTheirTexts()
    {
        Assert.Equal(Cards.Count, Cards.Select(card => card.Id).Distinct().Count());

        foreach (var card in Cards)
        {
            Assert.False(string.IsNullOrWhiteSpace(card.Title.Get()), card.Id);
            Assert.False(string.IsNullOrWhiteSpace(card.Category.Get()), card.Id);
            Assert.False(string.IsNullOrWhiteSpace(card.Summary.Get()), card.Id);
            Assert.False(string.IsNullOrWhiteSpace(card.RealWorld.Get()), card.Id);
            Assert.False(string.IsNullOrWhiteSpace(card.Defense.Get()), card.Id);
            Assert.True(card.Attack is null || TechniqueCard.IsValidAttackId(card.Attack), $"{card.Id}: {card.Attack}");
        }
    }

    [Fact]
    public void EveryMission_TeachesAtLeastOneExistingCard()
    {
        var ids = Cards.Select(card => card.Id).ToHashSet();

        foreach (var mission in Catalog.Missions)
        {
            Assert.NotEmpty(mission.Techniques);
            Assert.All(mission.Techniques, id => Assert.Contains(id, ids));
        }
    }

    [Fact]
    public void EveryCard_CanBeCollected()
    {
        Assert.All(Cards, card => Assert.NotEmpty(TechniqueCatalog.TaughtBy(Catalog, card)));
    }

    [Fact]
    public void Cards_AreCollectedBySolvingTheirMission()
    {
        var state = new GameState(Catalog);
        var exploring = Cards.First(card => card.Id == "exploring");

        Assert.False(TechniqueCatalog.IsCollected(state, exploring));
        state.SubmitFlag(Catalog.Missions[0].Flag);
        Assert.True(TechniqueCatalog.IsCollected(state, exploring));
    }

    [Theory]
    [InlineData("T1552.001", "https://attack.mitre.org/techniques/T1552/001/")]
    [InlineData("T1083", "https://attack.mitre.org/techniques/T1083/")]
    public void Cards_LinkToMitreAttack(string id, string url)
    {
        var card = new TechniqueCard
        {
            Id = "x", Title = new("t"), Category = new("c"), Summary = new("s"), RealWorld = new("r"), Defense = new("d"), Attack = id,
        };

        Assert.Equal(url, card.AttackUrl);
    }
}
