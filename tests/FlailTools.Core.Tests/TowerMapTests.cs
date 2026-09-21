using System.Globalization;
using System.Text.RegularExpressions;
using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Mapping;
using FlailTools.Core.Model;

namespace FlailTools.Core.Tests;

/// <summary>
/// The tower is the one site drawn from the side, because it is the one site built as a stack.
/// </summary>
public sealed partial class TowerMapTests
{
    /// <summary>
    /// Every storey shows the pips of the die it was read from, so the drawing can be checked
    /// against the book by counting.
    /// </summary>
    [Fact]
    public async Task TheElevationDrawsThePipsEachFloorWasReadFrom()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 20; seed++)
        {
            SitePlan plan = new() { Seed = seed, Kind = SiteKinds.Tower };
            AdventureSite site = SiteGenerator.Generate(data, plan);
            string svg = SiteMapper.RenderSvg(site, plan, data.Ui);

            int expected = site.Areas.Where(area => area.Role != AreaRoles.Top).Sum(area => area.Face);

            Assert.Equal(expected, Pips().Matches(svg).Count);
        }
    }

    /// <summary>The d4 balanced on top is a numeral, because a d4 seen from the side shows one.</summary>
    [Fact]
    public async Task TheRoofShowsTheNumberOnTheDieBalancedOnTop()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 20; seed++)
        {
            SitePlan plan = new() { Seed = seed, Kind = SiteKinds.Tower };
            AdventureSite site = SiteGenerator.Generate(data, plan);
            string svg = SiteMapper.RenderSvg(site, plan, data.Ui);

            SiteArea top = site.Areas.Single(area => area.Role == AreaRoles.Top);

            Assert.Contains($">{top.Face}</text>", svg, StringComparison.Ordinal);
            Assert.Equal(site.Areas.Count, Keys().Matches(svg).Count);
        }
    }

    /// <summary>A silhouette that asked for water gets a moat, and one that did not gets none.</summary>
    [Fact]
    public async Task OnlyAMoatedTowerIsDrawnStandingInWater()
    {
        GameData data = await TestData.LoadAsync();

        Assert.Contains("#dbe5e6", Map(data, 1337), StringComparison.Ordinal);
        Assert.DoesNotContain("#dbe5e6", Map(data, 42), StringComparison.Ordinal);
    }

    /// <summary>
    /// The other four kinds are still drawn from above by the engine.
    /// </summary>
    /// <remarks>
    /// Towers bypass the engine because FLAIL! gives them no cartography to follow. That is a
    /// deliberate exception for one kind, not a replacement of the map, and it should stay one.
    /// </remarks>
    [Theory]
    [InlineData(SiteKinds.Dungeon)]
    [InlineData(SiteKinds.Cave)]
    [InlineData(SiteKinds.Location)]
    [InlineData(SiteKinds.Landmark)]
    public async Task EveryOtherKindKeepsTheEnginesPlanView(string kind)
    {
        GameData data = await TestData.LoadAsync();
        SitePlan plan = new() { Seed = 42, Kind = kind };

        string svg = SiteMapper.RenderSvg(SiteGenerator.Generate(data, plan), plan, data.Ui);

        Assert.Contains("viewBox=\"0 0 420 320\"", svg, StringComparison.Ordinal);
        Assert.Contains("map-host", svg, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing drawn ever leaves the canvas, for any tower the generator can produce.
    /// </summary>
    /// <remarks>
    /// The drawing is laid out from a storey count that varies, inked with a pen that wobbles, and
    /// given a moat on some silhouettes and not others. Any of those can push a stroke off the
    /// paper, and an SVG that overflows its own viewBox is simply clipped — silently, and only on
    /// the seeds that happen to do it.
    /// </remarks>
    [Fact]
    public async Task NothingDrawnEverLeavesThePaper()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 200; seed++)
        {
            string svg = Map(data, seed);

            foreach ((double x, double y) in Points(svg))
            {
                Assert.InRange(x, 0, 300);
                Assert.InRange(y, 0, 400);
            }
        }
    }

    /// <summary>Every coordinate the drawing puts on the page: path vertices and circle centres.</summary>
    private static IEnumerable<(double X, double Y)> Points(string svg)
    {
        foreach (Match path in PathData().Matches(svg))
        {
            string[] parts = path.Groups[1].Value
                .Replace(',', ' ')
                .Replace("M", " ", StringComparison.Ordinal)
                .Replace("C", " ", StringComparison.Ordinal)
                .Replace("Z", " ", StringComparison.Ordinal)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            for (int index = 0; index + 1 < parts.Length; index += 2)
            {
                yield return (Coordinate(parts[index]), Coordinate(parts[index + 1]));
            }
        }

        foreach (Match circle in CircleCentres().Matches(svg))
        {
            yield return (Coordinate(circle.Groups[1].Value), Coordinate(circle.Groups[2].Value));
        }
    }

    private static double Coordinate(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    private static string Map(GameData data, uint seed)
    {
        SitePlan plan = new() { Seed = seed, Kind = SiteKinds.Tower };

        return SiteMapper.RenderSvg(SiteGenerator.Generate(data, plan), plan, data.Ui);
    }

    [GeneratedRegex("r=\"3\\.2\"")]
    private static partial Regex Pips();

    [GeneratedRegex("r=\"7\"")]
    private static partial Regex Keys();

    [GeneratedRegex("d=\"([^\"]*)\"")]
    private static partial Regex PathData();

    [GeneratedRegex("<circle cx=\"(-?[\\d.]+)\" cy=\"(-?[\\d.]+)\"")]
    private static partial Regex CircleCentres();
}
