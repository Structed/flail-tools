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
    /// A tower is four to six d6s with a d4 balanced on top, and each die is a floor.
    /// </summary>
    [Fact]
    public async Task ATowerIsAStackOfDiceWithADifferentFloorOnTop()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 40; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = SiteKinds.Tower });

            Assert.InRange(site.Areas.Count, 5, 7);
            Assert.Equal(AreaRoles.Top, site.Areas[^1].Role);
            Assert.Equal(FieldPaths.TowerTopFloor, site.Areas[^1].Path);
            Assert.InRange(site.Areas[^1].Face, 1, 4);

            Assert.All(site.Areas.Take(site.Areas.Count - 1), floor => Assert.Equal(AreaRoles.Plain, floor.Role));
            Assert.All(site.Areas.Take(site.Areas.Count - 1), floor => Assert.InRange(floor.Face, 1, 6));

            for (int index = 0; index < site.Areas.Count; index++)
            {
                Assert.Equal(index + 1, site.Areas[index].Number);
            }
        }
    }

    /// <summary>
    /// "Only high-level mages have their own towers", and the book says how high.
    /// </summary>
    [Fact]
    public async Task ATowerComesWithTheWizardWhoseTowerItIs()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 60; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = SiteKinds.Tower });

            Assert.InRange(Stat(site, FieldPaths.TowerWizardLevel), 6, 10);
            Assert.InRange(Stat(site, FieldPaths.TowerWizardHitPoints), 10, 20);
            Assert.InRange(Stat(site, FieldPaths.TowerWizardMana), 20, 30);
        }
    }

    /// <summary>
    /// Walking round a tower changes every floor at once, and never shows a face the stack is sitting on.
    /// </summary>
    /// <remarks>
    /// This is the whole difference between reading a stack of dice and rolling a d6 per floor. A
    /// stacked die hides the face under the storey above and the face on the storey below, so two
    /// of its six numbers can never reach a façade — and which two is fixed for that die, however
    /// far you walk. Re-rolling only the façade leaves every die pinned where it landed, so anything
    /// that changes here changed because the reader moved, not because the tower did.
    /// </remarks>
    [Fact]
    public async Task TurningATowerRoundShowsADifferentTowerBuiltFromTheSameDice()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 12; seed++)
        {
            SitePlan plan = new() { Seed = seed, Kind = SiteKinds.Tower };
            AdventureSite first = SiteGenerator.Generate(data, plan);

            int storeys = first.Areas.Count - 1;
            List<HashSet<int>> shown = [.. Enumerable.Range(0, storeys).Select(_ => new HashSet<int>())];
            HashSet<string> readings = new(StringComparer.Ordinal);

            for (int turn = 0; turn < 24; turn++)
            {
                AdventureSite site = SiteGenerator.Generate(data, plan);

                Assert.Equal(first.Areas.Count, site.Areas.Count);
                readings.Add(Field(site, FieldPaths.TowerFacade));

                for (int index = 0; index < storeys; index++)
                {
                    shown[index].Add(site.Areas[index].Face);
                }

                plan = plan.WithReroll(FieldPaths.TowerFacade);
            }

            Assert.True(readings.Count > 1, $"seed {seed}: turning the tower never changed what the façade reads.");

            foreach (HashSet<int> faces in shown)
            {
                Assert.True(faces.Count <= 4, $"seed {seed}: a stacked die showed more than its four side faces.");

                Assert.Contains(
                    Enumerable.Range(1, 3),
                    pair => !faces.Contains(pair) && !faces.Contains(7 - pair));
            }
        }
    }

    /// <summary>
    /// A lock holds a number through a change of seed, and a lock that has stopped meaning anything
    /// is re-rolled rather than clamped into range.
    /// </summary>
    [Fact]
    public async Task ALockOnAWizardsNumbersHoldsAndAStaleOneIsDropped()
    {
        GameData data = await TestData.LoadAsync();

        SitePlan plan = new() { Seed = 9, Kind = SiteKinds.Tower };
        AdventureSite rolled = SiteGenerator.Generate(data, plan);

        SitePlan held = plan
            .WithPin(FieldPaths.TowerWizardLevel, rolled.PinValues[FieldPaths.TowerWizardLevel])
            .WithSeed(77);

        Assert.Equal(
            Stat(rolled, FieldPaths.TowerWizardLevel),
            Stat(SiteGenerator.Generate(data, held), FieldPaths.TowerWizardLevel));

        // A position from some wider range, and somebody's own typing: neither is an offset here.
        foreach (string stale in new[] { "#99", "-1", "Archmage" })
        {
            AdventureSite site = SiteGenerator.Generate(
                data, plan.WithPin(FieldPaths.TowerWizardLevel, stale));

            Assert.InRange(Stat(site, FieldPaths.TowerWizardLevel), 6, 10);
        }
    }

    private static string Field(AdventureSite site, string path) =>
        site.Fields.Single(field => string.Equals(field.Path, path, StringComparison.Ordinal)).Value;

    private static int Stat(AdventureSite site, string path) =>
        int.Parse(Field(site, path), System.Globalization.CultureInfo.InvariantCulture);

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
