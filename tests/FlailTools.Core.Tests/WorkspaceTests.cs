using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Model;
using FlailTools.Core.Serialization;

namespace FlailTools.Core.Tests;

/// <summary>
/// The interface's behaviour, tested without a browser.
/// </summary>
/// <remarks>
/// Every one of these is a button somebody presses. They live here rather than in a component test
/// because the component is markup over <see cref="SiteWorkspace"/> and nothing else.
/// </remarks>
public sealed class WorkspaceTests
{
    [Fact]
    public async Task ALockedFieldSurvivesRollingEverythingElseAgain()
    {
        SiteWorkspace workspace = await CreateAsync(new SitePlan { Seed = 4242, Kind = SiteKinds.Dungeon });

        string path = FieldPaths.DungeonKeyFeature;
        string pinValue = workspace.Site.PinValues[path];

        workspace.TogglePin(path);

        Assert.True(workspace.IsPinned(path));

        for (int i = 0; i < 12; i++)
        {
            workspace.RollAgain();

            Assert.True(workspace.IsPinned(path));
            Assert.Equal(pinValue, workspace.Site.PinValues[path]);
        }
    }

    [Fact]
    public async Task UnlockingLetsAFieldMoveAgain()
    {
        SiteWorkspace workspace = await CreateAsync(new SitePlan { Seed = 99, Kind = SiteKinds.Tower });

        workspace.TogglePin(FieldPaths.TowerOccupant);
        Assert.True(workspace.IsPinned(FieldPaths.TowerOccupant));

        workspace.TogglePin(FieldPaths.TowerOccupant);
        Assert.False(workspace.IsPinned(FieldPaths.TowerOccupant));
        Assert.DoesNotContain(FieldPaths.TowerOccupant, workspace.Plan.Pins.Keys);
    }

    [Fact]
    public async Task RerollingALockedFieldStillChangesIt()
    {
        SiteWorkspace workspace = await CreateAsync(new SitePlan { Seed = 7, Kind = SiteKinds.Cave });

        workspace.TogglePin(FieldPaths.CaveCreatures);
        workspace.Reroll(FieldPaths.CaveCreatures);

        Assert.False(workspace.IsPinned(FieldPaths.CaveCreatures));
        Assert.Equal(1, workspace.Plan.Rerolls[FieldPaths.CaveCreatures]);
    }

    [Fact]
    public async Task TypedWordsAreKeptWordForWord()
    {
        SiteWorkspace workspace = await CreateAsync(new SitePlan { Seed = 12, Kind = SiteKinds.Landmark });

        workspace.SetText(FieldPaths.LandmarkBiome, "A salt marsh that nobody crosses twice");

        Assert.Equal("A salt marsh that nobody crosses twice", workspace.Site.ValueOf(FieldPaths.LandmarkBiome));

        workspace.RollAgain();

        Assert.Equal("A salt marsh that nobody crosses twice", workspace.Site.ValueOf(FieldPaths.LandmarkBiome));
    }

    [Fact]
    public async Task ChangingKindDropsTheOldBranchLocksButKeepsTheName()
    {
        SiteWorkspace workspace = await CreateAsync(new SitePlan { Seed = 31, Kind = SiteKinds.Dungeon });

        workspace.TogglePin(FieldPaths.NameStem);
        workspace.TogglePin(FieldPaths.DungeonType);

        workspace.SetKind(SiteKinds.Tower);

        Assert.Equal(SiteKinds.Tower, workspace.Site.Kind);
        Assert.True(workspace.IsPinned(FieldPaths.NameStem));
        Assert.DoesNotContain(FieldPaths.DungeonType, workspace.Plan.Pins.Keys);
    }

    [Fact]
    public async Task TheLinkRebuildsExactlyWhatWasOnScreen()
    {
        SiteWorkspace workspace = await CreateAsync(new SitePlan { Seed = 808, Kind = SiteKinds.Dungeon });

        workspace.TogglePin(FieldPaths.DungeonFlavour);
        workspace.Reroll(FieldPaths.DungeonCreatures);
        workspace.SetText(FieldPaths.DungeonLocation, "Under the third mill");

        GameData data = await TestData.LoadAsync();
        SiteWorkspace opened = new(data, SiteUrl.FromQuery(workspace.Query));

        Assert.Equal(SiteSummary.Describe(workspace.Site, workspace.Plan), SiteSummary.Describe(opened.Site, opened.Plan));
        Assert.Equal(workspace.Query, opened.Query);
    }

    [Fact]
    public async Task ADownloadedFileOpensAsTheSameSite()
    {
        SiteWorkspace workspace = await CreateAsync(new SitePlan { Seed = 5150, Kind = SiteKinds.Cave });

        workspace.TogglePin(FieldPaths.CaveKeyFeature);

        GameData data = await TestData.LoadAsync();
        SiteWorkspace opened = new(data, SiteDocuments.Read(workspace.ToDocument()));

        Assert.Equal(SiteSummary.Describe(workspace.Site, workspace.Plan), SiteSummary.Describe(opened.Site, opened.Plan));
        Assert.EndsWith(".json", workspace.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheMapIsDrawnOnceAndRedrawnWhenTheSiteChanges()
    {
        SiteWorkspace workspace = await CreateAsync(new SitePlan { Seed = 64, Kind = SiteKinds.Dungeon });

        string first = workspace.MapSvg;

        Assert.Same(first, workspace.MapSvg);
        Assert.Contains("<svg", first, StringComparison.Ordinal);

        workspace.Apply(workspace.Plan.WithSeed(65));

        Assert.NotSame(first, workspace.MapSvg);
    }

    [Fact]
    public async Task EveryAreaIsGivenSomethingToCallItself()
    {
        GameData data = await TestData.LoadAsync();

        foreach (string kind in SiteKinds.All)
        {
            SiteWorkspace workspace = new(data, new SitePlan { Seed = 21, Kind = kind });

            foreach (SiteArea area in workspace.Site.Areas)
            {
                Assert.False(string.IsNullOrWhiteSpace(workspace.AreaName(area)));
            }
        }
    }

    [Fact]
    public async Task AnUnknownKindIsIgnoredRatherThanGenerated()
    {
        SiteWorkspace workspace = await CreateAsync(new SitePlan { Seed = 1, Kind = SiteKinds.Dungeon });

        workspace.SetKind("mausoleum");

        Assert.Null(workspace.Plan.Kind);
        Assert.Contains(workspace.Site.Kind, SiteKinds.All);
    }

    private static async Task<SiteWorkspace> CreateAsync(SitePlan plan) =>
        new(await TestData.LoadAsync(), plan);
}
