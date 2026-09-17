using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Mapping;
using FlailTools.Core.Model;
using FlailTools.Core.Serialization;
using Structed.Inkwell.Generation;
using Structed.Inkwell.Mapping;
using Structed.Inkwell.Rendering;

namespace FlailTools.Core.Tests;

public sealed partial class TowerMapTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    [Theory]
    [InlineData("boxy-compact", 2, 5)]
    [InlineData("vessel-tall", 4, 7)]
    [InlineData("vessel-moated", 4, 7)]
    public async Task FloorsShareAFootprintAndTheirStairsConnectInOrder(string silhouette, int shortest, int tallest)
    {
        GameData data = await TestData.LoadAsync();
        HashSet<int> counts = [];

        for (uint seed = 1; seed <= 40; seed++)
        {
            SitePlan plan = PlanFor(data, silhouette, seed);
            AdventureSite site = SiteGenerator.Generate(data, plan);
            TowerSiteMap map = Assert.IsType<TowerSiteMap>(SiteMapper.Draw(site, plan, data.Ui));
            counts.Add(map.Floors.Count);

            Assert.Equal(site.Areas, map.Floors.Select(floor => floor.Area));

            for (int index = 0; index < map.Floors.Count; index++)
            {
                TowerFloorPlan floor = map.Floors[index];

                // A spire twists as it rises, so the outline is shared by name rather than by point.
                Assert.Equal(map.Floors[0].Footprint, floor.Footprint);

                if (floor.Footprint == "spire")
                {
                    Assert.Equal(index == 0, map.Floors[0].Boundary.Points.SequenceEqual(floor.Boundary.Points));
                }
                else
                {
                    Assert.Equal(map.Floors[0].Boundary.Points, floor.Boundary.Points);
                }

                Assert.Equal(index == 0 && site.HasWater, floor.HasMoat);
                Assert.Equal(index > 0, floor.StairsDown.HasValue);
                Assert.Equal(index < map.Floors.Count - 1, floor.StairsUp.HasValue);

                if (floor.StairsUp is MapPoint up)
                {
                    Assert.Equal(up, map.Floors[index + 1].StairsDown);
                    AssertStairsInside(floor.Boundary, up);
                }

                if (floor.StairsDown is MapPoint down)
                {
                    Assert.NotEqual(floor.StairsUp, floor.StairsDown);
                    AssertStairsInside(floor.Boundary, down);
                }
            }
        }

        Assert.Contains(shortest, counts);
        Assert.Contains(tallest, counts);
    }

    [Theory]
    [InlineData("boxy-compact", 1u)]
    [InlineData("vessel-tall", 7u)]
    [InlineData("vessel-moated", 1337u)]
    public async Task SvgShowsOneOrderedPlanPerFloorAndOnlyOneEntrance(string silhouette, uint seed)
    {
        GameData data = await TestData.LoadAsync();
        SitePlan plan = PlanFor(data, silhouette, seed);
        AdventureSite site = SiteGenerator.Generate(data, plan);
        TowerSiteMap map = Assert.IsType<TowerSiteMap>(SiteMapper.Draw(site, plan, data.Ui));
        string svg = SiteMapper.RenderSvg(site, plan, data.Ui);
        XElement root = XElement.Parse(svg);
        XElement[] floors = [.. Groups(root, "tower-floor-plan")];

        Assert.Equal(site.Areas.Count, floors.Length);
        Assert.Equal(site.Areas.Select(area => area.Number), floors.Select(floor => (int)floor.Attribute("data-floor")!));
        Assert.Equal("img", (string?)root.Attribute("role"));
        Assert.Equal(data.Ui.Message("towerMapAlt"), (string?)root.Attribute("aria-label"));
        Assert.DoesNotContain('\r', svg);

        double width = (double)root.Attribute("width")!;
        double height = (double)root.Attribute("height")!;
        double previousY = -1;

        for (int index = 0; index < floors.Length; index++)
        {
            XElement floor = floors[index];
            double y = double.Parse(
                floor.Attribute("transform")!.Value.Split(' ')[1].TrimEnd(')'), CultureInfo.InvariantCulture);

            Assert.True(y > previousY);
            previousY = y;
            Assert.Equal(site.Areas[index].Role, (string?)floor.Attribute("data-role"));
            Assert.Equal(
                $"{site.Areas[index].Number}. {data.Ui.AreaName(SiteKinds.Tower, site.Areas[index].Role)}",
                floor.Element(Svg + "text")!.Value);
            Assert.Equal(site.Areas[index].Value, floor.Elements(Svg + "text").ElementAt(1).Value);
            Assert.Contains(site.Areas[index].Value, floor.Element(Svg + "title")!.Value, StringComparison.Ordinal);
            Assert.Single(floor.Elements(Svg + "path"), path => (string?)path.Attribute("class") == "floor-outline");
            Assert.Equal(index == 0 ? 1 : 0, Groups(floor, "tower-entrance").Count());
            Assert.Equal(index == 0 && site.HasWater ? 1 : 0, Groups(floor, "tower-moat").Count());

            Assert.All(map.Floors[index].Boundary.Points, point =>
            {
                Assert.InRange(point.X, 0, width);
                Assert.InRange(y + point.Y, 0, height);
            });

            if (index < floors.Length - 1)
            {
                XElement up = Assert.Single(Groups(floor, "stairs-up"));
                XElement down = Assert.Single(Groups(floors[index + 1], "stairs-down"));

                Assert.Equal(site.Areas[index + 1].Number, (int)up.Attribute("data-to-floor")!);
                Assert.Equal(site.Areas[index].Number, (int)down.Attribute("data-to-floor")!);
                Assert.Equal((string?)up.Attribute("data-x"), (string?)down.Attribute("data-x"));
                Assert.Equal((string?)up.Attribute("data-y"), (string?)down.Attribute("data-y"));
            }
        }

        Assert.Empty(Groups(floors[0], "stairs-down"));
        Assert.Empty(Groups(floors[^1], "stairs-up"));
        Assert.Equal(site.Areas.Count - 1, Groups(root, "stairs-up").Count());
        Assert.Equal(site.Areas.Count - 1, Groups(root, "stairs-down").Count());
    }

    [Theory]
    [InlineData("boxy-compact")]
    [InlineData("vessel-moated")]
    public async Task EachFloorIsFurnishedFromItsDieFaceAndCaptionedWithWhatWasRolled(string silhouette)
    {
        GameData data = await TestData.LoadAsync();
        Dictionary<(string Role, int Face), string> furnishings = [];

        for (uint seed = 1; seed <= 60; seed++)
        {
            SitePlan plan = PlanFor(data, silhouette, seed);
            AdventureSite site = SiteGenerator.Generate(data, plan);
            XElement root = XElement.Parse(SiteMapper.RenderSvg(site, plan, data.Ui));
            XElement[] floors = [.. Groups(root, "tower-floor-plan")];

            for (int index = 0; index < floors.Length; index++)
            {
                SiteArea area = site.Areas[index];
                Assert.Equal(area.Value, floors[index].Elements(Svg + "text").ElementAt(1).Value);
                Assert.True(PinReference.TryGetIndex(site.PinValues[area.Path], out int face));

                string name = (string)Assert.Single(Groups(floors[index], "floor-fixtures"))
                    .Attribute("data-fixtures")!;

                if (furnishings.TryGetValue((area.Role, face), out string? drawnEarlier))
                {
                    Assert.Equal(drawnEarlier, name);
                }

                furnishings[(area.Role, face)] = name;
            }
        }

        // The words may be retranslated or reworded; the face is what the drawing follows.
        Assert.Equal(furnishings.Count, furnishings.Values.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(furnishings, entry => entry.Key.Role == AreaRoles.Top);
        Assert.Contains(furnishings, entry => entry.Key.Role != AreaRoles.Top);
    }

    [Fact]
    public async Task EveryFloorKindHasItsOwnFixtures()
    {
        GameData data = await TestData.LoadAsync();
        HashSet<string> drawn = [];

        for (uint seed = 1; seed <= 400 && drawn.Count < 10; seed++)
        {
            SitePlan plan = new() { Seed = seed, Kind = SiteKinds.Tower };
            AdventureSite site = SiteGenerator.Generate(data, plan);

            foreach (XElement set in Groups(XElement.Parse(SiteMapper.RenderSvg(site, plan, data.Ui)), "floor-fixtures"))
            {
                drawn.Add((string)set.Attribute("data-fixtures")!);
            }
        }

        Assert.Equal(data.Tower.FloorTypes.Count + data.Tower.TopFloorTypes.Count, drawn.Count);
    }

    [Theory]
    [InlineData("boxy-compact")]
    [InlineData("vessel-moated")]
    public async Task FixturesStayInsideTheWallsAndLetTheStairsCoverThem(string silhouette)
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 25; seed++)
        {
            // Cycle the shapes too: one set of fixtures has to sit inside all ten of them.
            SitePlan plan = PlanFor(data, silhouette, seed)
                .WithPin(FieldPaths.TowerShape, PinReference.ForIndex((int)(seed % 10)));
            AdventureSite site = SiteGenerator.Generate(data, plan);
            TowerSiteMap map = Assert.IsType<TowerSiteMap>(SiteMapper.Draw(site, plan, data.Ui));
            XElement[] floors =
                [.. Groups(XElement.Parse(SiteMapper.RenderSvg(site, plan, data.Ui)), "tower-floor-plan")];

            for (int index = 0; index < floors.Length; index++)
            {
                List<XElement> layers = [.. floors[index].Elements(Svg + "g")];
                int fixtures = layers.FindIndex(layer => (string?)layer.Attribute("class") == "floor-fixtures");
                int stairs = layers.FindIndex(layer =>
                    ((string?)layer.Attribute("class"))?.StartsWith("stairs-", StringComparison.Ordinal) == true);

                Assert.True(fixtures >= 0, $"Floor {index + 1} of seed {seed} was left unfurnished.");
                Assert.True(
                    stairs < 0 || fixtures < stairs,
                    $"Floor {index + 1} draws its fixtures over the stairs, hiding a way out.");

                Assert.All(Corners(layers[fixtures]), point => Assert.True(
                    map.Floors[index].Boundary.ContainsWithMargin(point, 1),
                    FormattableString.Invariant(
                        $"A fixture reaches {point.X},{point.Y}, outside floor {index + 1} of seed {seed}.")));
            }
        }
    }

    [Fact]
    public async Task AFloorGivenSomebodysOwnWordsIsDrawnBare()
    {
        GameData data = await TestData.LoadAsync();
        SiteWorkspace workspace = new(data, PlanFor(data, "vessel-tall", 3));
        const string typed = "A room the referee made up";
        workspace.SetText(FieldPaths.TowerFloor(0), typed);
        XElement[] floors = [.. Groups(XElement.Parse(workspace.MapSvg), "tower-floor-plan")];

        Assert.Empty(Groups(floors[0], "floor-fixtures"));
        Assert.Equal(typed, floors[0].Elements(Svg + "text").ElementAt(1).Value);
        Assert.All(floors.Skip(1), floor => Assert.Single(Groups(floor, "floor-fixtures")));
    }

    [Fact]
    public async Task ThePlanIsDrawnInTheShapeTheTowerRolled()
    {
        GameData data = await TestData.LoadAsync();
        Dictionary<int, string> outlines = [];

        for (int face = 0; face < data.Tower.Shapes.Count; face++)
        {
            SitePlan plan = new SitePlan { Seed = 11, Kind = SiteKinds.Tower }
                .WithPin(FieldPaths.TowerShape, PinReference.ForIndex(face));
            AdventureSite site = SiteGenerator.Generate(data, plan);
            XElement[] floors =
                [.. Groups(XElement.Parse(SiteMapper.RenderSvg(site, plan, data.Ui)), "tower-floor-plan")];

            outlines[face] = (string)floors[0].Attribute("data-footprint")!;
            Assert.All(floors, floor => Assert.Equal(outlines[face], (string?)floor.Attribute("data-footprint")));
        }

        Assert.Equal(data.Tower.Shapes.Count, outlines.Values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task AShapePinnedToSomebodysOwnWordsFallsBackToAPlainOutline()
    {
        GameData data = await TestData.LoadAsync();
        SiteWorkspace workspace = new(data, new SitePlan { Seed = 11, Kind = SiteKinds.Tower });
        workspace.SetText(FieldPaths.TowerShape, "A tower of no describable shape");
        XElement[] floors = [.. Groups(XElement.Parse(workspace.MapSvg), "tower-floor-plan")];

        Assert.All(floors, floor => Assert.Equal("plain", (string?)floor.Attribute("data-footprint")));
    }

    [Fact]
    public async Task TheWayInIsCutIntoWhicheverWallTheShapeGives()
    {
        GameData data = await TestData.LoadAsync();

        for (int face = 0; face < data.Tower.Shapes.Count; face++)
        {
            SitePlan plan = new SitePlan { Seed = 11, Kind = SiteKinds.Tower }
                .WithPin(FieldPaths.TowerShape, PinReference.ForIndex(face))
                .WithPin(FieldPaths.Silhouette, PinReference.ForIndex(SilhouetteIndex(data, "vessel-moated")));
            AdventureSite site = SiteGenerator.Generate(data, plan);
            TowerSiteMap map = Assert.IsType<TowerSiteMap>(SiteMapper.Draw(site, plan, data.Ui));
            XElement[] floors =
                [.. Groups(XElement.Parse(SiteMapper.RenderSvg(site, plan, data.Ui)), "tower-floor-plan")];
            XElement way = Assert.Single(Groups(floors[0], "tower-entrance"));

            double sill = (double)way.Element(Svg + "rect")!.Attribute("y")! + 5;
            MapPoint door = new(160, sill);

            Assert.True(
                map.Floors[0].Boundary.DistanceToEdge(door) < 1,
                FormattableString.Invariant(
                    $"The {data.Tower.Shapes[face]} has its door at {sill}, which is not on its wall."));
            Assert.True(sill > 130 + TowerFootprints.Usable, "The door should be outside the usable floor.");
            Assert.All(floors.Skip(1), floor => Assert.Empty(Groups(floor, "tower-entrance")));
        }
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public async Task TheSamePlanRendersIdenticallyAcrossCultures(string culture)
    {
        GameData data = await TestData.LoadAsync();
        SitePlan plan = PlanFor(data, "vessel-moated", 1337);
        AdventureSite site = SiteGenerator.Generate(data, plan);
        string expected = SiteMapper.RenderSvg(site, plan, data.Ui);
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

            Assert.Equal(expected, SiteMapper.RenderSvg(site, plan, data.Ui));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task LabelsAndDescriptionsStayLocalisedAndEscaped()
    {
        GameData data = await TestData.LoadAsync();
        SitePlan plan = PlanFor(data, "vessel-tall", 1);
        const string typed = "<script>alert('example')</script> & \"notes\"\r\nmore";
        AdventureSite site = SiteGenerator.Generate(data, plan) with { Name = typed };
        site = site with { Areas = [.. site.Areas.Select(area => area with { Value = typed })] };
        UiText ui = data.Ui with
        {
            AreaNames = new Dictionary<string, string> { [SiteKinds.Tower] = "Level & room" },
            Roles = new Dictionary<string, string> { [AreaRoles.Top] = "Upper <level>" },
            Messages = new Dictionary<string, string>(data.Ui.Messages)
            {
                ["towerMapHeading"] = "Plans & levels",
                ["towerMapOrder"] = "Lower < upper",
                ["towerMapAlt"] = "A \"tower\" & its levels",
                ["stairsUp"] = "Ascend",
                ["stairsDown"] = "Descend"
            }
        };
        string svg = SiteMapper.RenderSvg(site, plan, ui);
        XElement root = XElement.Parse(svg);

        Assert.DoesNotContain(root.Descendants(), element => element.Name.LocalName == "script");
        Assert.Equal(ui.Message("towerMapAlt"), (string?)root.Attribute("aria-label"));
        Assert.Contains(typed, root.Element(Svg + "title")!.Value, StringComparison.Ordinal);
        Assert.Equal(typed, Groups(root, "tower-floor-plan").First().Elements(Svg + "text").ElementAt(1).Value);
        Assert.Contains("Plans & levels", root.Value, StringComparison.Ordinal);
        Assert.Contains("Lower < upper", root.Value, StringComparison.Ordinal);
        Assert.Contains("1. Level & room", root.Value, StringComparison.Ordinal);
        Assert.Contains("Upper <level>", root.Value, StringComparison.Ordinal);
        Assert.Contains("Ascend", root.Value, StringComparison.Ordinal);
        Assert.Contains("Descend", root.Value, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', svg);
    }

    [Fact]
    public async Task LinksAndSavedFilesRebuildAllFloorPlans()
    {
        GameData data = await TestData.LoadAsync();
        SiteWorkspace workspace = new(data, PlanFor(data, "boxy-compact", 7));
        workspace.SetText(FieldPaths.TowerFloor(0), "A room with a newly fitted shelf");
        workspace.Reroll(FieldPaths.TowerGoal);

        SiteWorkspace linked = new(data, SiteUrl.FromQuery(workspace.Query));
        SiteWorkspace loaded = new(data, SiteDocuments.Read(workspace.ToDocument()));

        Assert.Equal(workspace.MapSvg, linked.MapSvg);
        Assert.Equal(workspace.MapSvg, loaded.MapSvg);
        Assert.Contains("A room with a newly fitted shelf", workspace.MapSvg, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SiteKinds.Dungeon)]
    [InlineData(SiteKinds.Cave)]
    [InlineData(SiteKinds.Location)]
    [InlineData(SiteKinds.Landmark)]
    public async Task OtherKindsStillUseTheSameOverviewRenderer(string kind)
    {
        GameData data = await TestData.LoadAsync();
        SitePlan plan = new() { Seed = 42, Kind = kind };
        AdventureSite site = SiteGenerator.Generate(data, plan);
        uint seed = MapGenerator.SeedFor(plan);
        PlaceMap expected = MapGenerator.Generate(SiteMapper.BriefFor(site, data.Ui), seed);
        string svg = SvgMapRenderer.Render(expected, seed, new MapRenderOptions
        {
            AriaLabel = data.Ui.Message("mapAlt"),
            CssClass = "site-map",
            IntrinsicSize = true
        });

        Assert.IsType<OverviewSiteMap>(SiteMapper.Draw(site, plan, data.Ui));
        Assert.Equal(svg, SiteMapper.RenderSvg(site, plan, data.Ui));
    }

    [Fact]
    public async Task AnEmptyTowerIsRejectedRatherThanDrawnAsAnOverview()
    {
        GameData data = await TestData.LoadAsync();
        AdventureSite site = new() { Kind = SiteKinds.Tower, Shape = MapShapes.Vessel };

        Assert.Throws<ArgumentException>(() => SiteMapper.Draw(site, new SitePlan(), data.Ui));
    }

    private static SitePlan PlanFor(GameData data, string silhouette, uint seed) =>
        new SitePlan { Seed = seed, Kind = SiteKinds.Tower }
            .WithPin(FieldPaths.Silhouette, PinReference.ForIndex(SilhouetteIndex(data, silhouette)));

    private static int SilhouetteIndex(GameData data, string silhouette)
    {
        List<SilhouetteRow> candidates = [.. data.Silhouettes.Silhouettes.Where(row => row.Allows(SiteKinds.Tower))];
        int index = candidates.FindIndex(row => row.Id == silhouette);
        Assert.True(index >= 0, $"No tower silhouette named '{silhouette}'.");

        return index;
    }

    private static IEnumerable<XElement> Groups(XElement parent, string cssClass) =>
        parent.Descendants(Svg + "g").Where(group => (string?)group.Attribute("class") == cssClass);

    // Every number in an SVG path is half of a coordinate pair, whichever command it belongs to.
    private static IEnumerable<MapPoint> Corners(XElement group)
    {
        foreach (XElement path in group.Descendants(Svg + "path"))
        {
            double[] numbers =
                [.. Numbers().Matches((string)path.Attribute("d")!)
                    .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture))];

            for (int index = 0; index + 1 < numbers.Length; index += 2)
            {
                yield return new MapPoint(numbers[index], numbers[index + 1]);
            }
        }
    }

    [GeneratedRegex(@"-?\d+(?:\.\d+)?")]
    private static partial Regex Numbers();

    private static void AssertStairsInside(MapPolygon boundary, MapPoint centre)
    {
        foreach (int x in new[] { -10, 10 })
        {
            foreach (int y in new[] { -16, 16 })
            {
                Assert.True(boundary.ContainsWithMargin(centre + new MapPoint(x, y), 3));
            }
        }
    }
}
