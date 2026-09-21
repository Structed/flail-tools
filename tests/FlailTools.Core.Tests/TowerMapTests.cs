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
    /// Every floor is named on the drawing, and between them the seeds draw every name the tables
    /// can produce.
    /// </summary>
    /// <remarks>
    /// Which, together with <see cref="NothingDrawnEverLeavesThePaper"/>, is what holds the tables
    /// to the space beside the tower: this proves each entry gets drawn, and that proves what is
    /// drawn stays on the page. A floor type too long for the gutter therefore fails the build
    /// rather than running off the paper on whichever seeds happen to roll it.
    /// </remarks>
    [Fact]
    public async Task EveryFloorIsNamedAndEveryNameInTheTablesGetsDrawn()
    {
        GameData data = await TestData.LoadAsync();

        HashSet<string> drawn = new(StringComparer.Ordinal);

        for (uint seed = 1; seed <= 200; seed++)
        {
            SitePlan plan = new() { Seed = seed, Kind = SiteKinds.Tower };
            AdventureSite site = SiteGenerator.Generate(data, plan);

            string[] names = [.. Names(SiteMapper.RenderSvg(site, plan, data.Ui))];

            Assert.Equal([.. site.Areas.Select(area => area.Kind)], names);

            drawn.UnionWith(names);
        }

        foreach (string name in data.Tower.FloorTypes.Concat(data.Tower.TopFloorTypes))
        {
            Assert.Contains(name, drawn);
        }
    }

    /// <summary>A floor is named by what it is, not by the whole of its entry.</summary>
    [Fact]
    public async Task AFloorIsNamedByItsTypeAndNotByItsDetail()
    {
        GameData data = await TestData.LoadAsync();
        SitePlan plan = new() { Seed = 42, Kind = SiteKinds.Tower };

        AdventureSite site = SiteGenerator.Generate(data, plan);
        SiteArea floor = site.Areas.First(area => area.Role != AreaRoles.Top);

        Assert.StartsWith($"{floor.Kind}: ", floor.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(floor.Value, SiteMapper.RenderSvg(site, plan, data.Ui), StringComparison.Ordinal);
    }

    /// <summary>
    /// A name too wide for the gutter is broken across two lines rather than shortened.
    /// </summary>
    /// <remarks>
    /// The d4 names on top of the stack are the long ones, and losing half of one to a trim would
    /// be a drawing quietly telling the reader something the dice did not say.
    /// </remarks>
    [Fact]
    public async Task ALongNameIsBrokenAcrossLinesRatherThanTrimmed()
    {
        GameData data = await TestData.LoadAsync();

        string longest = data.Tower.TopFloorTypes.MaxBy(name => name.Length) ?? "";

        for (uint seed = 1; seed <= 200; seed++)
        {
            SitePlan plan = new() { Seed = seed, Kind = SiteKinds.Tower };
            AdventureSite site = SiteGenerator.Generate(data, plan);

            if (!string.Equals(site.Areas[^1].Kind, longest, StringComparison.Ordinal))
            {
                continue;
            }

            string svg = SiteMapper.RenderSvg(site, plan, data.Ui);

            Assert.Contains(longest, Names(svg));
            Assert.DoesNotContain($">{longest}</tspan>", svg, StringComparison.Ordinal);

            return;
        }

        Assert.Fail($"No seed in range drew the longest top floor name, '{longest}'.");
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
    /// The drawing is laid out from a storey count that varies, inked with a pen that wobbles,
    /// named from tables that can be rewritten, and given a moat on some silhouettes and not
    /// others. Any of those can push a stroke or a word off the paper, and an SVG that overflows
    /// its own viewBox is simply clipped — silently, and only on the seeds that happen to do it.
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
                Assert.InRange(x, 0, 264);
                Assert.InRange(y, 0, 400);
            }
        }
    }

    /// <summary>
    /// Every coordinate the drawing puts on the page: path vertices, circle centres, and both ends
    /// of every line of text.
    /// </summary>
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

        foreach ((_, double x, double y, string text) in NameLines(svg))
        {
            yield return (x, y);
            yield return (x - Span(text), y);
        }
    }

    /// <summary>
    /// How wide a line of text comes out, guessed a shade more generously than the drawing itself
    /// guesses, so that a name which only just fits there still has room to spare here.
    /// </summary>
    private static double Span(string text) => text.Length * 5.8;

    /// <summary>Each floor's name as it reads, in the order the drawing writes them.</summary>
    private static IEnumerable<string> Names(string svg) => NameLines(svg)
        .GroupBy(line => line.Label)
        .Select(label => string.Join(' ', label.Select(line => line.Text)));

    /// <summary>
    /// Every line of every name, with the point it hangs from.
    /// </summary>
    /// <remarks>
    /// Read out of the drawing rather than recalculated, so that a line placed by an offset this
    /// test never heard of is still a line this test checks.
    /// </remarks>
    private static IEnumerable<(int Label, double X, double Y, string Text)> NameLines(string svg)
    {
        Match group = NameGroup().Match(svg);

        if (!group.Success)
        {
            yield break;
        }

        int label = 0;

        foreach (Match name in Label().Matches(group.Groups[1].Value))
        {
            double x = Coordinate(name.Groups[1].Value);
            double y = Coordinate(name.Groups[2].Value);

            foreach (Match line in Line().Matches(name.Groups[3].Value))
            {
                if (line.Groups[1].Success)
                {
                    y += Coordinate(line.Groups[1].Value);
                }

                yield return (label, x, y, line.Groups[2].Value);
            }

            label++;
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

    [GeneratedRegex("<g class=\"tower-names\"[^>]*>(.*?)</g>")]
    private static partial Regex NameGroup();

    [GeneratedRegex("<text x=\"(-?[\\d.]+)\" y=\"(-?[\\d.]+)\">(.*?)</text>")]
    private static partial Regex Label();

    [GeneratedRegex("<tspan x=\"-?[\\d.]+\"(?: dy=\"(-?[\\d.]+)\")?>([^<]*)</tspan>")]
    private static partial Regex Line();
}
