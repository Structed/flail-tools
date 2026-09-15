using System.Text;
using System.Xml;
using System.Xml.Linq;
using FlailTools.Core.Data;
using FlailTools.Core.Model;
using Structed.Inkwell.Mapping;
using Structed.Inkwell.Randomness;
using Structed.Inkwell.Rendering;

namespace FlailTools.Core.Mapping;

internal static class TowerFloorPlans
{
    private const int Width = 320;
    private const int HeaderHeight = 66;
    private const int FloorHeight = 210;
    private const string Ink = "#1c1a17";
    private const string Paper = "#fbf8f1";
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly MapPoint Centre = new(160, 110);

    public static TowerSiteMap Draw(AdventureSite site)
    {
        if (site.Areas.Count == 0)
        {
            throw new ArgumentException("A tower needs at least one floor to draw.", nameof(site));
        }

        MapPolygon boundary = site.Shape switch
        {
            MapShapes.Boxy => new([new(98, 48), new(222, 48), new(222, 172), new(98, 172)]),
            MapShapes.Vessel => new(Enumerable.Range(0, 32)
                .Select(index => Centre + MapPoint.FromAngle(index * Math.Tau / 32, 62))),
            _ => throw new ArgumentException(
                $"Tower floor plans do not support the layout shape '{site.Shape}'.", nameof(site))
        };

        List<TowerFloorPlan> floors = new(site.Areas.Count);

        for (int index = 0; index < site.Areas.Count; index++)
        {
            floors.Add(new TowerFloorPlan(
                site.Areas[index],
                boundary,
                index < site.Areas.Count - 1 ? StairPosition(index) : null,
                index > 0 ? StairPosition(index - 1) : null,
                index == 0 && site.HasWater));
        }

        return new TowerSiteMap(site.Name, floors);
    }

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
                Text(new(16, 22), $"{floor.Area.Number}. {ui.AreaName(SiteKinds.Tower, floor.Area.Role)}", 15));

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
                    new XAttribute("cx", 160),
                    new XAttribute("cy", 81),
                    new XAttribute("r", 12),
                    new XAttribute("fill", Ink)),
                Text(new(160, 86), SvgNumber.Format(floor.Area.Number), 14, Paper, "middle"));

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
        new(lowerFloorIndex % 2 == 0 ? 141 : 179, 125);

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
                new XAttribute("y", 167),
                new XAttribute("width", 16),
                new XAttribute("height", hasMoat ? 28 : 10),
                new XAttribute("fill", Paper)),
            Path(pen.Line(new(152, 172), new(152, 156), 0.4), Ink, 1.2),
            Path(pen.OpenPath([new(168, 172), new(167, 166), new(163, 160), new(152, 156)], 0.3), Ink, 0.7),
            Text(new(184, 192), ui.RoleName(AreaRoles.Entrance), 11));

        if (hasMoat)
        {
            group.Add(
                Path(pen.Line(new(152, 176), new(152, 195), 0.5), Ink, 1),
                Path(pen.Line(new(168, 176), new(168, 195), 0.5), Ink, 1));
        }

        return group;
    }

    private static MapPoint[] Scaled(MapPolygon polygon, double scale) =>
        [.. polygon.Points.Select(point => Centre + ((point - Centre) * scale))];

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
