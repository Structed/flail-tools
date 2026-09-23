using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Model;

namespace FlailTools.Core.Tests;

/// <summary>
/// What a seed promises: the same link rebuilds the same place, everywhere, forever.
/// </summary>
public sealed class GenerationTests
{
    [Theory]
    [InlineData(SiteKinds.Dungeon)]
    [InlineData(SiteKinds.Cave)]
    [InlineData(SiteKinds.Tower)]
    [InlineData(SiteKinds.Location)]
    [InlineData(SiteKinds.Landmark)]
    public async Task EveryKindGenerates(string kind)
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 40; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = kind });

            Assert.Equal(kind, site.Kind);
            Assert.False(string.IsNullOrWhiteSpace(site.Name));
            Assert.False(site.IsBlank);
            Assert.NotEmpty(site.Fields);
            Assert.InRange(site.Scale, 1, 6);
            Assert.False(string.IsNullOrEmpty(site.Shape));
        }
    }

    [Fact]
    public async Task TheSamePlanAlwaysProducesTheSameSite()
    {
        GameData data = await TestData.LoadAsync();

        SitePlan plan = new SitePlan { Seed = 12345 }
            .WithPin(FieldPaths.NameStem, "#2")
            .WithReroll(FieldPaths.Silhouette);

        Assert.Equal(
            SiteSummary.Describe(SiteGenerator.Generate(data, plan), plan),
            SiteSummary.Describe(SiteGenerator.Generate(data, plan), plan));
    }

    [Fact]
    public async Task DifferentSeedsGenerallyProduceDifferentSites()
    {
        GameData data = await TestData.LoadAsync();

        HashSet<string> shapes = [];

        for (uint seed = 1; seed <= 50; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed });
            shapes.Add($"{site.Kind}/{site.Silhouette}/{site.Areas.Count}");
        }

        Assert.True(shapes.Count > 10, $"Only {shapes.Count} distinct results across 50 seeds.");
    }

    [Fact]
    public async Task AChosenKindOverridesTheRoll()
    {
        GameData data = await TestData.LoadAsync();

        foreach (string kind in SiteKinds.All)
        {
            Assert.Equal(
                kind,
                SiteGenerator.Generate(data, new SitePlan { Seed = 7, Kind = kind }).Kind);
        }
    }

    [Fact]
    public async Task TheSilhouetteAlwaysAdmitsTheKindItWasChosenFor()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 60; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed });
            SilhouetteRow row = data.Silhouettes.Silhouettes.Single(candidate => candidate.Id == site.Silhouette);

            Assert.True(
                row.Allows(site.Kind),
                $"Seed {seed} drew a {site.Kind} as '{site.Silhouette}', which does not admit it.");
        }
    }

    [Fact]
    public async Task ADungeonHasEightToTwelveRoomsKeyedFromTheEntrance()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 40; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = SiteKinds.Dungeon });

            Assert.InRange(site.Areas.Count, 8, 12);
            Assert.Equal(AreaRoles.Entrance, site.Areas[0].Role);
            Assert.Equal(1, site.Areas[0].Number);
            Assert.Equal(site.Areas.Count, site.Areas[^1].Number);

            SiteArea finale = Assert.Single(site.Areas, area => area.Role == AreaRoles.Finale);
            Assert.Equal(Math.Min(10, site.Areas.Count), finale.Number);
        }
    }

    /// <summary>
    /// A tower is the book's handful of d6s with a d4 on top, so it is never a two-storey cottage.
    /// </summary>
    /// <remarks>
    /// The range is pinned at both ends because only one end is obvious. A tower that grew too tall
    /// would be noticed the moment anybody read one; a tower one floor too short reads perfectly
    /// well and simply is not the tower the book's dice build.
    /// </remarks>
    [Fact]
    public async Task ATowerIsAStackOfFloorsWithADifferentOneOnTop()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 40; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = SiteKinds.Tower });

            Assert.InRange(site.Areas.Count, 5, 7);
            Assert.InRange(site.Areas.Count - 1, 4, 6);
            Assert.Equal(AreaRoles.Top, site.Areas[^1].Role);
            Assert.All(site.Areas.Take(site.Areas.Count - 1), floor => Assert.Equal(AreaRoles.Plain, floor.Role));
        }
    }

    /// <summary>
    /// Every floor below the top names one of the four details belonging to its own kind of room.
    /// </summary>
    /// <remarks>
    /// <c>floorDetails</c> row <em>n</em> is only meaningful beside <c>floorTypes</c> face <em>n</em>,
    /// and nothing about a mismatch looks wrong: swap two rows and a library is stocked with a
    /// laboratory's four options, which reads as a perfectly plausible tower and passes every other
    /// test in the repository. So the pairing is checked on generated towers rather than in the file,
    /// where the alignment is an assumption rather than a fact.
    /// </remarks>
    [Fact]
    public async Task EveryFloorsDetailBelongsToItsOwnKindOfRoom()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 60; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = SiteKinds.Tower });

            foreach (SiteArea floor in site.Areas.Take(site.Areas.Count - 1))
            {
                string[] parts = floor.Value.Split(": ", 2, StringSplitOptions.None);

                Assert.Equal(2, parts.Length);

                int face = data.Tower.FloorTypes
                    .Select((type, index) => (type, index))
                    .Single(row => row.type == parts[0])
                    .index;

                Assert.Contains(parts[1], data.Tower.FloorDetails[face], StringComparer.Ordinal);
            }

            Assert.Contains(site.Areas[^1].Value, data.Tower.TopFloorTypes, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Every kind is drawn somewhere inside the map's band, and no kind pins itself to one end.
    /// </summary>
    /// <remarks>
    /// Scale is derived from a count, so widening a procedure can push it against the clamp without
    /// any test noticing: the maps still draw, they just all come out the same size. That is what
    /// happened to towers when the stack grew to the book's five to seven floors, so the spread is
    /// checked rather than only the bounds.
    /// </remarks>
    [Fact]
    public async Task NoKindWithAVaryingSizeIsAlwaysDrawnAtTheSameScale()
    {
        GameData data = await TestData.LoadAsync();

        foreach (string kind in new[] { SiteKinds.Dungeon, SiteKinds.Cave, SiteKinds.Tower })
        {
            HashSet<int> scales = [];

            for (uint seed = 1; seed <= 60; seed++)
            {
                AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = kind });

                Assert.InRange(site.Scale, 2, 6);
                scales.Add(site.Scale);
            }

            Assert.True(
                scales.Count >= 3,
                $"Across 60 seeds a {kind} was only ever drawn at {scales.Count} distinct scale(s): " +
                $"{string.Join(", ", scales.Order())}.");
        }
    }

    /// <summary>
    /// The dice drop should usually give a cave a way in and a heart, and occasionally lose one.
    /// </summary>
    /// <remarks>
    /// Checked across many seeds rather than one, because whether a die bounces off the paper is the
    /// whole point of the procedure and any single seed proves nothing either way.
    /// </remarks>
    [Fact]
    public async Task ACaveGetsAWayInAHeartAndSometimesSomethingHidden()
    {
        GameData data = await TestData.LoadAsync();

        int withEntry = 0;
        int withCore = 0;
        int withHidden = 0;

        for (uint seed = 1; seed <= 120; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = SiteKinds.Cave });

            Assert.InRange(site.Areas.Count, 5, 9);
            Assert.True(site.Areas.Count(area => area.Role == AreaRoles.Entry) <= 1);
            Assert.True(site.Areas.Count(area => area.Role == AreaRoles.Core) <= 1);

            withEntry += site.Areas.Any(area => area.Role == AreaRoles.Entry) ? 1 : 0;
            withCore += site.Areas.Any(area => area.Role == AreaRoles.Core) ? 1 : 0;
            withHidden += site.Areas.Any(area => area.Role == AreaRoles.Hidden) ? 1 : 0;
        }

        Assert.True(withEntry > 100, $"Only {withEntry} of 120 caves had a way in.");
        Assert.True(withCore > 100, $"Only {withCore} of 120 caves had a heart.");
        Assert.True(withHidden > 0, "No cave in 120 ever had a die land off the paper.");
    }

    [Fact]
    public async Task TheHexcrawlKindsHaveNoNumberedParts()
    {
        GameData data = await TestData.LoadAsync();

        foreach (string kind in new[] { SiteKinds.Location, SiteKinds.Landmark })
        {
            Assert.Empty(SiteGenerator.Generate(data, new SitePlan { Seed = 3, Kind = kind }).Areas);
        }
    }

    /// <summary>A locked field holds still while everything around it is re-rolled.</summary>
    [Fact]
    public async Task ALockedFieldSurvivesARerollOfItsNeighbours()
    {
        GameData data = await TestData.LoadAsync();

        SitePlan plan = new SitePlan { Seed = 99, Kind = SiteKinds.Tower };
        AdventureSite first = SiteGenerator.Generate(data, plan);

        SitePlan locked = plan
            .WithPin(FieldPaths.TowerGoal, first.PinValues[FieldPaths.TowerGoal])
            .WithReroll(FieldPaths.TowerOccupant)
            .WithReroll(FieldPaths.TowerShape);

        AdventureSite second = SiteGenerator.Generate(data, locked);

        Assert.Equal(first.PinValues[FieldPaths.TowerGoal], second.PinValues[FieldPaths.TowerGoal]);
    }

    /// <summary>
    /// Changing kind drops the old branch's locks, because a lock is a position, not a phrase.
    /// </summary>
    [Fact]
    public void ChangingKindAbandonsTheLocksThatNoLongerMeanAnything()
    {
        SitePlan plan = new SitePlan { Seed = 1, Kind = SiteKinds.Dungeon }
            .WithPin(FieldPaths.DungeonKeyFeature, "#3")
            .WithPin(FieldPaths.NameStem, "#4")
            .WithPin(FieldPaths.Silhouette, "#1");

        SitePlan changed = plan.WithKind(SiteKinds.Cave);

        Assert.DoesNotContain(FieldPaths.DungeonKeyFeature, changed.Pins.Keys, StringComparer.Ordinal);
        Assert.Equal("#4", changed.Pins[FieldPaths.NameStem]);
        Assert.Equal("#1", changed.Pins[FieldPaths.Silhouette]);
    }

    [Fact]
    public void RerollingTheKindAlsoAbandonsThem()
    {
        SitePlan plan = new SitePlan { Seed = 1, Kind = SiteKinds.Tower }
            .WithPin(FieldPaths.TowerGoal, "#2")
            .WithPin(FieldPaths.LocationBiome, "#5")
            .WithPin(FieldPaths.NameTail, "#6");

        SitePlan rerolled = plan.WithKindRerolled();

        Assert.Null(rerolled.Kind);
        Assert.Equal(["site/name/tail"], rerolled.Pins.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(1, rerolled.Rerolls[FieldPaths.Kind]);
    }

    [Fact]
    public void TheNameIsLockedAndRerolledAsOneField()
    {
        SitePlan plan = new SitePlan { Seed = 1 }
            .WithNamePinned(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FieldPaths.NameStem] = "#1",
                [FieldPaths.NameTail] = "#2"
            });

        Assert.True(plan.IsNamePinned);

        SitePlan rerolled = plan.WithNameRerolled();

        Assert.False(rerolled.IsNamePinned);
        Assert.Equal(1, rerolled.Rerolls[FieldPaths.NameStem]);
        Assert.Equal(1, rerolled.Rerolls[FieldPaths.NameTail]);
    }

    /// <summary>
    /// Empty tables still produce an honest blank result after the shipped tables gain content.
    /// </summary>
    [Fact]
    public async Task AnEmptyTableProducesABlankFieldRatherThanAFailure()
    {
        GameData data = await TestData.LoadEmptyAsync();

        AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = 1 });

        Assert.False(data.HasContent);
        Assert.Equal("", site.Name);
        Assert.True(site.IsBlank);
        Assert.All(site.Fields, field => Assert.Equal("", field.Value));
    }
}
