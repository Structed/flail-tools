using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Serialization;

namespace FlailTools.Core.Tests;

/// <summary>
/// The link is the save file, so it has to survive everything a link goes through.
/// </summary>
public sealed class SiteUrlTests
{
    [Theory]
    [InlineData("plain words")]
    [InlineData("a tilde ~ in the middle")]
    [InlineData("a star * and an equals =")]
    [InlineData("an ampersand & and a question ?")]
    [InlineData("a slash / and a percent %")]
    [InlineData("a hash # that looks like a pin reference")]
    [InlineData("#12")]
    [InlineData("a quote \" and an apostrophe '")]
    [InlineData("nôn-âscii ünd emoji 🜃")]
    [InlineData("")]
    public void TypedWordsSurviveTheRoundTrip(string typed)
    {
        SitePlan plan = new SitePlan { Seed = 1234, Kind = SiteKinds.Dungeon }
            .WithPin(FieldPaths.DungeonKeyFeature, typed);

        SitePlan restored = SiteUrl.FromQuery(SiteUrl.ToQuery(plan));

        Assert.Equal(typed, restored.Pins[FieldPaths.DungeonKeyFeature]);
    }

    [Fact]
    public void SeparatorsInOneValueDoNotSplitTheList()
    {
        SitePlan plan = new SitePlan { Seed = 9, Kind = SiteKinds.Cave }
            .WithPin(FieldPaths.CaveFlavour, "one ~ two * three")
            .WithPin(FieldPaths.CaveType, "#3");

        SitePlan restored = SiteUrl.FromQuery(SiteUrl.ToQuery(plan));

        Assert.Equal(2, restored.Pins.Count);
        Assert.Equal("one ~ two * three", restored.Pins[FieldPaths.CaveFlavour]);
        Assert.Equal("#3", restored.Pins[FieldPaths.CaveType]);
    }

    [Fact]
    public void PathsAreNotEscapedTwice()
    {
        SitePlan plan = new SitePlan { Seed = 1 }.WithPin(FieldPaths.NameStem, "#0");

        string query = SiteUrl.ToQuery(plan);

        Assert.Contains("site/name/stem", query, StringComparison.Ordinal);
        Assert.DoesNotContain("%2F", query, StringComparison.Ordinal);
        Assert.DoesNotContain("%25", query, StringComparison.Ordinal);
    }

    [Fact]
    public void RerollCountersTravelToo()
    {
        SitePlan plan = new SitePlan { Seed = 55, Kind = SiteKinds.Tower }
            .WithReroll(FieldPaths.TowerGoal)
            .WithReroll(FieldPaths.TowerGoal);

        SitePlan restored = SiteUrl.FromQuery(SiteUrl.ToQuery(plan));

        Assert.Equal(2, restored.Rerolls[FieldPaths.TowerGoal]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("?")]
    [InlineData("?s=")]
    [InlineData("?s=not-a-seed")]
    [InlineData("?k=mausoleum")]
    [InlineData("?p=broken")]
    [InlineData("?p=~~~")]
    [InlineData("?r=site/name/stem*not-a-number")]
    [InlineData("?nonsense")]
    public void AMangledLinkStillOpens(string? query)
    {
        SitePlan plan = SiteUrl.FromQuery(query);

        Assert.NotNull(plan);
        Assert.Null(plan.Kind);
        Assert.Empty(plan.Rerolls);
    }

    [Fact]
    public void ALinkWithNothingInItIsStillOneLink()
    {
        SitePlan plan = new() { Seed = 7 };

        Assert.Equal("?s=" + Structed.Inkwell.Randomness.SeedCodec.Encode(7), SiteUrl.ToQuery(plan));
    }
}
