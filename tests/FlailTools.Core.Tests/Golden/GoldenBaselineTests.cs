using System.Text;
using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Mapping;
using FlailTools.Core.Model;
using FlailTools.Core.Serialization;

namespace FlailTools.Core.Tests.Golden;

/// <summary>
/// A handful of fixed seeds, written down before anybody shares a link.
/// </summary>
/// <remarks>
/// <para>
/// Every other test here says what ought to be true in general. This one says what is actually true
/// right now, for particular seeds, down to the byte — which is the only kind of test that catches a
/// change nobody meant to make. A field path quietly renamed, a table reordered, a roll moved one
/// line earlier: all of these keep every general test passing and silently change what a seed
/// produces.
/// </para>
/// <para>
/// Pinned now, while the tool is unpublished and no link exists to break. Once seeds are out in the
/// world, a diff here is a diff to somebody's saved dungeon.
/// </para>
/// </remarks>
public sealed class GoldenBaselineTests
{
    private static readonly uint[] Seeds = [1u, 7u, 42u, 1337u, 65535u, 2_147_483_647u];

    [Fact]
    public async Task TheBaselineHasNotMoved()
    {
        GameData data = await TestData.LoadAsync();
        StringBuilder manifest = new();

        foreach (string kind in SiteKinds.All)
        {
            foreach (uint seed in Seeds)
            {
                SitePlan plan = new() { Seed = seed, Kind = kind };
                AdventureSite site = SiteGenerator.Generate(data, plan);

                SiteSummary.Append(manifest, site, plan);
            }
        }

        GoldenFile.Verify("site-baseline.txt", manifest.ToString());
    }

    /// <summary>The kind roll itself, which no other baseline exercises because they all fix it.</summary>
    [Fact]
    public async Task TheUnguidedRollHasNotMoved()
    {
        GameData data = await TestData.LoadAsync();
        StringBuilder manifest = new();

        for (uint seed = 1; seed <= 32; seed++)
        {
            SitePlan plan = new() { Seed = seed };
            AdventureSite site = SiteGenerator.Generate(data, plan);

            manifest.Append(seed).Append('\t')
                .Append(site.Kind).Append('\t')
                .Append(site.Silhouette).Append('\t')
                .Append(site.Scale).Append('\t')
                .Append(site.Areas.Count).Append('\n');
        }

        GoldenFile.Verify("kind-roll.txt", manifest.ToString());
    }

    /// <summary>
    /// The inked map, which is where a line-ending bug hides.
    /// </summary>
    /// <remarks>
    /// The sibling Mausritter generator shipped a renderer that emitted CRLF on Windows and LF on
    /// Linux, so the same seed rendered different bytes depending on which machine built it. Nothing
    /// looked wrong anywhere. The assertion below is the cheap, permanent version of that lesson.
    /// </remarks>
    [Fact]
    public async Task TheMapContainsNoCarriageReturnsOnAnyPlatform()
    {
        GameData data = await TestData.LoadAsync();

        foreach (string kind in SiteKinds.All)
        {
            foreach (uint seed in Seeds)
            {
                SitePlan plan = new() { Seed = seed, Kind = kind };
                string svg = SiteMapper.RenderSvg(SiteGenerator.Generate(data, plan), plan, data.Ui);

                Assert.DoesNotContain('\r', svg);
            }
        }
    }

    [Fact]
    public async Task TheMapHasNotMoved()
    {
        GameData data = await TestData.LoadAsync();
        StringBuilder manifest = new();

        foreach (string kind in SiteKinds.All)
        {
            SitePlan plan = new() { Seed = 42, Kind = kind };
            AdventureSite site = SiteGenerator.Generate(data, plan);

            manifest.Append("== ").Append(kind).Append('\n')
                .Append(SiteMapper.RenderSvg(site, plan, data.Ui))
                .Append('\n');
        }

        GoldenFile.Verify("map-baseline.txt", manifest.ToString());
    }

    [Fact]
    public async Task ASavedFileRoundTripsBackToTheSameSite()
    {
        GameData data = await TestData.LoadAsync();

        SitePlan plan = new SitePlan { Seed = 4242, Kind = SiteKinds.Dungeon }
            .WithPin(FieldPaths.DungeonFlavour, "#2")
            .WithReroll(FieldPaths.DungeonCreatures);

        AdventureSite site = SiteGenerator.Generate(data, plan);
        string json = SiteDocuments.Write(site, plan);

        Assert.True(SiteDocuments.Matches(json));

        SitePlan restored = SiteDocuments.Read(json);

        Assert.Equal(SiteSummary.Describe(site, plan), SiteSummary.Describe(SiteGenerator.Generate(data, restored), plan));
    }

    [Fact]
    public async Task ALinkRoundTripsBackToTheSameSite()
    {
        GameData data = await TestData.LoadAsync();

        SitePlan plan = new SitePlan { Seed = 4242, Kind = SiteKinds.Cave }
            .WithPin(FieldPaths.CaveKeyFeature, "#3")
            .WithPin(FieldPaths.NameStem, "a name someone typed")
            .WithReroll(FieldPaths.CaveType);

        AdventureSite site = SiteGenerator.Generate(data, plan);
        SitePlan restored = SiteUrl.FromQuery(SiteUrl.ToQuery(plan));

        Assert.Equal(plan.Seed, restored.Seed);
        Assert.Equal(plan.Kind, restored.Kind);
        Assert.Equal(SiteSummary.Describe(site, plan), SiteSummary.Describe(SiteGenerator.Generate(data, restored), plan));
    }

    [Fact]
    public void AMangledLinkStillGivesSomething()
    {
        SitePlan plan = SiteUrl.FromQuery("?s=&k=notakind&p=broken&r=x*y");

        Assert.Null(plan.Kind);
        Assert.Empty(plan.Pins);
        Assert.Empty(plan.Rerolls);
    }

    [Fact]
    public void ALinkIsStableWhateverOrderTheLocksWereAddedIn()
    {
        SitePlan first = new SitePlan { Seed = 5 }
            .WithPin(FieldPaths.CaveType, "#1")
            .WithPin(FieldPaths.CaveFlavour, "#2");

        SitePlan second = new SitePlan { Seed = 5 }
            .WithPin(FieldPaths.CaveFlavour, "#2")
            .WithPin(FieldPaths.CaveType, "#1");

        Assert.Equal(SiteUrl.ToQuery(first), SiteUrl.ToQuery(second));
    }
}
