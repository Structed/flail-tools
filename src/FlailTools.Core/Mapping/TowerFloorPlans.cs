using System.Text;
using System.Xml;
using System.Xml.Linq;
using FlailTools.Core.Data;
using FlailTools.Core.Model;
using Structed.Inkwell.Generation;
using Structed.Inkwell.Mapping;
using Structed.Inkwell.Randomness;
using Structed.Inkwell.Rendering;

namespace FlailTools.Core.Mapping;

internal static class TowerFloorPlans
{
    private const int Width = 320;
    private const int HeaderHeight = 66;
    private const int FloorHeight = 228;
    private const string Ink = "#1c1a17";
    private const string Paper = "#fbf8f1";
    private const string Muted = "#6b6257";
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly MapPoint Centre = new(160, 130);

    public static TowerSiteMap Draw(AdventureSite site)
    {
        if (site.Areas.Count == 0)
        {
            throw new ArgumentException("A tower needs at least one floor to draw.", nameof(site));
        }

        MapPolygon boundary = site.Shape switch
        {
            MapShapes.Boxy => new([new(98, 68), new(222, 68), new(222, 192), new(98, 192)]),
            MapShapes.Vessel => new(Enumerable.Range(0, 32)
                .Select(index => Centre + MapPoint.FromAngle(index * Math.Tau / 32, 62))),
            _ => throw new ArgumentException(
                $"Tower floor plans do not support the layout shape '{site.Shape}'.", nameof(site))
        };

        List<TowerFloorPlan> floors = new(site.Areas.Count);

        for (int index = 0; index < site.Areas.Count; index++)
        {
            SiteArea area = site.Areas[index];
            floors.Add(new TowerFloorPlan(
                area,
                boundary,
                index < site.Areas.Count - 1 ? StairPosition(index) : null,
                index > 0 ? StairPosition(index - 1) : null,
                index == 0 && site.HasWater,
                FurnishingFor(site, area)));
        }

        return new TowerSiteMap(site.Name, floors);
    }

    /// <summary>
    /// Which fixtures belong on a floor, as the die face its kind was read from.
    /// </summary>
    /// <remarks>
    /// A floor pinned to somebody's own words has no face, and is drawn bare rather than furnished
    /// from a guess: fixtures that contradict the text would be worse than an empty room.
    /// </remarks>
    private static int? FurnishingFor(AdventureSite site, SiteArea area) =>
        site.PinValues.TryGetValue(area.Path, out string? pin) && PinReference.TryGetIndex(pin, out int face)
            ? face
            : null;

    public static string Render(TowerSiteMap map, uint seed, UiText ui)
    {
        int height = HeaderHeight + (map.Floors.Count * FloorHeight) + 12;
        XElement svg = new(Svg + "svg",
            new XAttribute("viewBox", $"0 0 {Width} {height}"),
            new XAttribute("width", Width),
            new XAttribute("height", height),
            new XAttribute("class", "site-map tower-map"),
            new XAttribute("role", "img"),
            new XAttribute("aria-label", ui.Message("towerMapAlt")),
            new XAttribute("font-family", "Georgia, serif"),
            new XElement(Svg + "title", $"{map.Subject} - {ui.Message("towerMapHeading")}"),
            new XElement(Svg + "desc", string.Join("\n", map.Floors.Select(floor =>
                $"{floor.Area.Number}. {ui.AreaName(SiteKinds.Tower, floor.Area.Role)}: {floor.Area.Value}"))),
            new XElement(Svg + "rect",
                new XAttribute("width", Width),
                new XAttribute("height", height),
                new XAttribute("fill", Paper)),
            Text(new(16, 28), ui.Message("towerMapHeading"), 20),
            Text(new(16, 48), ui.Message("towerMapOrder"), 12));

        for (int index = 0; index < map.Floors.Count; index++)
        {
            TowerFloorPlan floor = map.Floors[index];
            RoughPen pen = new(
                new Pcg32(unchecked(seed + ((uint)floor.Area.Number * 0x9E3779B9u)), Pcg32.DefaultSequence),
                1.1);
            XElement group = new(Svg + "g",
                new XAttribute("class", "tower-floor-plan"),
                new XAttribute("data-floor", floor.Area.Number),
                new XAttribute("data-role", floor.Area.Role),
                new XAttribute("transform", $"translate(0 {HeaderHeight + (index * FloorHeight)})"),
                new XElement(Svg + "title", $"{ui.AreaName(SiteKinds.Tower, floor.Area.Role)}: {floor.Area.Value}"),
                Text(new(16, 22), $"{floor.Area.Number}. {ui.AreaName(SiteKinds.Tower, floor.Area.Role)}", 15),
                Text(new(16, 40), floor.Area.Value, 12, Muted));

            if (floor.HasMoat)
            {
                group.Add(new XElement(Svg + "g",
                    new XAttribute("class", "tower-moat"),
                    new XElement(Svg + "title", ui.Message("towerMoat")),
                    Path(pen.ClosedPath(Scaled(floor.Boundary, 1.25), 1), "#70867b", 1, "#e2ebe5")));
            }

            XElement outline = Path(pen.ClosedPath(floor.Boundary.Points, 1), Ink, 3, Paper);
            outline.SetAttributeValue("class", "floor-outline");
            group.Add(
                outline,
                Path(pen.ClosedPath(Scaled(floor.Boundary, 0.945), 0.5), Ink, 0.8),
                new XElement(Svg + "circle",
                    new XAttribute("cx", 44),
                    new XAttribute("cy", 130),
                    new XAttribute("r", 13),
                    new XAttribute("fill", Ink)),
                Text(new(44, 135), SvgNumber.Format(floor.Area.Number), 14, Paper, "middle"));

            if (Furnishings(pen, floor) is XElement furnishings)
            {
                group.Add(furnishings);
            }

            if (floor.StairsUp is MapPoint up)
            {
                group.Add(Stairs(pen, up, floor.Area.Number + 1, ascending: true, ui));
            }

            if (floor.StairsDown is MapPoint down)
            {
                group.Add(Stairs(pen, down, floor.Area.Number - 1, ascending: false, ui));
            }

            if (index == 0)
            {
                group.Add(Entrance(pen, floor.HasMoat, ui));
            }

            svg.Add(group);
        }

        StringBuilder output = new();
        using (XmlWriter writer = XmlWriter.Create(output, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Entitize
        }))
        {
            svg.WriteTo(writer);
        }

        return output.ToString();
    }

    // Each flight arrives at the same position on the next floor; alternate the two stairwells.
    private static MapPoint StairPosition(int lowerFloorIndex) =>
        new(lowerFloorIndex % 2 == 0 ? 141 : 179, 145);

    private static XElement Stairs(RoughPen pen, MapPoint centre, int destination, bool ascending, UiText ui)
    {
        string label = ui.Message(ascending ? "stairsUp" : "stairsDown");
        XElement group = new(Svg + "g",
            new XAttribute("class", ascending ? "stairs-up" : "stairs-down"),
            new XAttribute("data-to-floor", destination),
            new XAttribute("data-x", SvgNumber.Format(centre.X)),
            new XAttribute("data-y", SvgNumber.Format(centre.Y)),
            new XElement(Svg + "title", $"{label}: {ui.AreaName(SiteKinds.Tower)} {destination}"),
            Path(pen.ClosedPath(
                [centre + new MapPoint(-10, -16), centre + new MapPoint(10, -16),
                 centre + new MapPoint(10, 16), centre + new MapPoint(-10, 16)], 0.4), Ink, 1, Paper));

        for (int step = -10; step <= 10; step += 5)
        {
            group.Add(Path(pen.Line(
                centre + new MapPoint(-10, step), centre + new MapPoint(10, step), 0.3), Ink, 0.7));
        }

        int direction = ascending ? -1 : 1;
        MapPoint start = centre + new MapPoint(0, -12 * direction);
        MapPoint end = centre + new MapPoint(0, 12 * direction);
        string arrow = pen.OpenPath([start, end], 0.2);

        group.Add(
            Path(arrow, Paper, 4),
            Path(arrow, Ink, 1.3),
            Path(pen.OpenPath(
                [end + new MapPoint(-3, -5 * direction), end,
                 end + new MapPoint(3, -5 * direction)], 0.2), Ink, 1.3),
            Text(centre + new MapPoint(0, 28), label, 11, anchor: "middle"));

        return group;
    }

    private static XElement Entrance(RoughPen pen, bool hasMoat, UiText ui)
    {
        XElement group = new(Svg + "g",
            new XAttribute("class", "tower-entrance"),
            new XElement(Svg + "title", ui.RoleName(AreaRoles.Entrance)),
            new XElement(Svg + "rect",
                new XAttribute("x", 152),
                new XAttribute("y", 187),
                new XAttribute("width", 16),
                new XAttribute("height", hasMoat ? 28 : 10),
                new XAttribute("fill", Paper)),
            Path(pen.Line(new(152, 192), new(152, 176), 0.4), Ink, 1.2),
            Path(pen.OpenPath([new(168, 192), new(167, 186), new(163, 180), new(152, 176)], 0.3), Ink, 0.7),
            Text(new(184, 212), ui.RoleName(AreaRoles.Entrance), 11));

        if (hasMoat)
        {
            group.Add(
                Path(pen.Line(new(152, 196), new(152, 215), 0.5), Ink, 1),
                Path(pen.Line(new(168, 196), new(168, 215), 0.5), Ink, 1));
        }

        return group;
    }

    private static MapPoint[] Scaled(MapPolygon polygon, double scale) =>
        [.. polygon.Points.Select(point => Centre + ((point - Centre) * scale))];

    /// <summary>
    /// What is actually in the room, drawn from the die face rather than the words.
    /// </summary>
    /// <remarks>
    /// Fixtures are laid out to clear the stairwells, which are drawn afterwards and over the top so
    /// that a flight always reads as reachable. They hug the walls for the same reason a real plan
    /// does: the middle of a tower floor is the part people walk through.
    /// </remarks>
    private static XElement? Furnishings(RoughPen pen, TowerFloorPlan floor)
    {
        if (floor.Furnishing is not int face)
        {
            return null;
        }

        return floor.Area.Role == AreaRoles.Top ? TopFixtures(pen, face) : RoomFixtures(pen, face);
    }

    private static XElement? RoomFixtures(RoughPen pen, int face) => face switch
    {
        0 => Fixtures("bed", [
            Solid(pen, Rect(new(160, 92), 46, 30)),
            Path(pen.Line(new(141, 86), new(179, 86), 0.3), Ink, 0.8),
            Solid(pen, Rect(new(118, 146), 22, 14))]),
        1 => Fixtures("table", [
            Solid(pen, Rect(new(160, 92), 54, 20)),
            .. Stools(pen, 78),
            .. Stools(pen, 112)]),
        2 => Fixtures("benches", [
            Solid(pen, Rect(new(115, 131), 18, 44)),
            Solid(pen, Rect(new(205, 131), 18, 44)),
            Solid(pen, Ring(new(160, 90), 13)),
            Path(pen.ClosedPath(Ring(new(160, 90), 7), 0.3), Ink, 0.7)]),
        3 => Fixtures("shelves", [
            .. Shelf(pen, new(113, 131), 14, 44, upright: true),
            .. Shelf(pen, new(207, 131), 14, 44, upright: true),
            .. Shelf(pen, new(160, 84), 52, 14, upright: false)]),
        4 => Fixtures("plinths", [
            .. Plinth(pen, new(114, 114)),
            .. Plinth(pen, new(114, 146)),
            .. Plinth(pen, new(206, 114)),
            .. Plinth(pen, new(206, 146)),
            .. Plinth(pen, new(160, 86))]),
        5 => Fixtures("basin", [
            Solid(pen, Ring(new(160, 92), 20)),
            Path(pen.ClosedPath(Ring(new(160, 92), 12), 0.3), Ink, 0.7),
            Solid(pen, Ring(new(114, 118), 6)),
            Solid(pen, Ring(new(114, 146), 6)),
            Solid(pen, Ring(new(206, 118), 6)),
            Solid(pen, Ring(new(206, 146), 6))]),
        _ => null
    };

    /// <summary>
    /// The top floor, which has no flight up and so can use the middle of the room.
    /// </summary>
    private static XElement? TopFixtures(RoughPen pen, int face) => face switch
    {
        0 => Fixtures("telescope", [
            Solid(pen, [new(142, 120), new(186, 84), new(180, 76), new(136, 112)]),
            Path(pen.Line(new(141, 116), new(133, 128), 0.3), Ink, 1),
            Path(pen.Line(new(141, 116), new(149, 128), 0.3), Ink, 1)]),
        1 => Fixtures("cushion", [
            Solid(pen, Ring(new(160, 98), 24)),
            Path(pen.ClosedPath(Ring(new(160, 98), 13), 0.3), Ink, 0.7)]),
        2 => Fixtures("prism", [
            Solid(pen, [new(160, 80), new(176, 110), new(144, 110)]),
            Path(pen.Line(new(160, 76), new(160, 74), 0.2), Ink, 0.9),
            Path(pen.Line(new(146, 94), new(132, 88), 0.2), Ink, 0.9),
            Path(pen.Line(new(174, 94), new(188, 88), 0.2), Ink, 0.9)]),
        3 => Fixtures("oculus", [
            Solid(pen, [
                new(132, 98), new(146, 86), new(160, 82), new(174, 86), new(188, 98),
                new(174, 110), new(160, 114), new(146, 110)]),
            Solid(pen, Ring(new(160, 98), 8))]),
        _ => null
    };

    private static XElement Fixtures(string name, IEnumerable<XElement> parts) =>
        new(Svg + "g",
            new XAttribute("class", "floor-fixtures"),
            new XAttribute("data-fixtures", name),
            parts);

    private static XElement[] Stools(RoughPen pen, double y) =>
        [.. new double[] { 142, 160, 178 }.Select(x => Solid(pen, Ring(new(x, y), 4, 12)))];

    private static XElement[] Shelf(RoughPen pen, MapPoint centre, double width, double height, bool upright)
    {
        List<XElement> parts = [Solid(pen, Rect(centre, width, height))];

        for (int step = -1; step <= 1; step++)
        {
            MapPoint along = upright ? new(0, step * height / 3) : new(step * width / 3, 0);
            MapPoint across = upright ? new(width / 2, 0) : new(0, height / 2);
            parts.Add(Path(pen.Line(centre + along - across, centre + along + across, 0.2), Ink, 0.7));
        }

        return [.. parts];
    }

    private static XElement[] Plinth(RoughPen pen, MapPoint centre) =>
        [Solid(pen, Rect(centre, 16, 16)), Path(pen.ClosedPath(Rect(centre, 9, 9), 0.3), Ink, 0.7)];

    private static XElement Solid(RoughPen pen, IReadOnlyList<MapPoint> points) =>
        Path(pen.ClosedPath(points, 0.4), Ink, 1.1, Paper);

    private static MapPoint[] Rect(MapPoint centre, double width, double height) =>
    [
        centre + new MapPoint(-width / 2, -height / 2),
        centre + new MapPoint(width / 2, -height / 2),
        centre + new MapPoint(width / 2, height / 2),
        centre + new MapPoint(-width / 2, height / 2)
    ];

    private static MapPoint[] Ring(MapPoint centre, double radius, int segments = 20) =>
        [.. Enumerable.Range(0, segments)
            .Select(index => centre + MapPoint.FromAngle(index * Math.Tau / segments, radius))];

    private static XElement Path(string data, string stroke, double width, string fill = "none") =>
        new(Svg + "path",
            new XAttribute("d", data),
            new XAttribute("fill", fill),
            new XAttribute("stroke", stroke),
            new XAttribute("stroke-width", SvgNumber.Format(width)),
            new XAttribute("stroke-linejoin", "round"),
            new XAttribute("stroke-linecap", "round"));

    private static XElement Text(MapPoint point, string value, int size, string fill = Ink, string anchor = "start") =>
        new(Svg + "text",
            new XAttribute("x", SvgNumber.Format(point.X)),
            new XAttribute("y", SvgNumber.Format(point.Y)),
            new XAttribute("font-size", size),
            new XAttribute("text-anchor", anchor),
            new XAttribute("fill", fill),
            value);
}
