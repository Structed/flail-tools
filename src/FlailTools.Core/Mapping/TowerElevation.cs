using System.Globalization;
using System.Text;
using FlailTools.Core.Data;
using FlailTools.Core.Model;
using Structed.Inkwell.Mapping;
using Structed.Inkwell.Randomness;
using Structed.Inkwell.Rendering;

namespace FlailTools.Core.Mapping;

/// <summary>
/// Draws a wizard's tower as the stack of dice FLAIL! builds it from.
/// </summary>
/// <remarks>
/// <para>
/// The map engine draws places seen from above, which is the wrong view for the one kind of site
/// whose procedure is explicitly vertical. FLAIL! gives no cartography for towers at all — no
/// floor plans, no connections, not even an entrance — so a plan view would have to invent
/// everything it drew. What the book does give is the stack itself: four to six d6s with a d4
/// balanced on top, read down one chosen face. That is a picture already, and it is this one.
/// </para>
/// <para>
/// So each storey shows the number its die presents to the chosen façade, drawn as pips. A reader
/// holding the book can read the tower off the drawing exactly as they would read it off their own
/// dice, and walking round it — re-rolling the façade — visibly changes every floor at once.
/// </para>
/// <para>
/// Written here rather than in the engine because none of it is general: the engine has never heard
/// of wizards, and a stack of dice is not a shape another game's generator would ask for. It ports
/// the engine's own ink, palette and keying conventions so the two drawings sit on the same page
/// without looking like they came from different tools.
/// </para>
/// </remarks>
internal static class TowerElevation
{
    public static string Render(AdventureSite site, uint seed, UiText ui)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(ui);

        IReadOnlyList<SiteArea> storeys = [.. site.Areas.Where(area => area.Role != AreaRoles.Top)];
        SiteArea? top = site.Areas.FirstOrDefault(area => area.Role == AreaRoles.Top);

        Pcg32 random = SeedDerivation.CreateStream(seed, InkStream);
        RoughPen pen = new(random, 1.0);

        double bodyTop = GroundY - (StoreyHeight * Math.Max(storeys.Count, 1));
        bool isRound = string.Equals(site.Shape, MapShapes.Vessel, StringComparison.Ordinal);

        StringBuilder svg = new();

        Open(svg, ui);
        Ground(svg, pen, site.HasWater);
        Body(svg, pen, storeys, bodyTop, isRound);
        Roof(svg, pen, top, bodyTop, isRound);
        Keys(svg, ui, storeys, top, bodyTop);

        svg.Append("</svg>");

        return svg.ToString();
    }

    private static void Open(StringBuilder svg, UiText ui)
    {
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
            .Append(N(Width)).Append(' ').Append(N(Height))
            .Append("\" width=\"").Append(N(Width))
            .Append("\" height=\"").Append(N(Height))
            .Append("\" role=\"img\" aria-label=\"").Append(Escape(ui.Message("towerMapAlt")))
            .Append("\" class=\"site-map\">\n")
            .Append("<defs>\n")
            .Append("  <filter id=\"paper-grain\" x=\"0\" y=\"0\" width=\"100%\" height=\"100%\">\n")
            .Append("    <feTurbulence type=\"fractalNoise\" baseFrequency=\"0.8\" numOctaves=\"3\" seed=\"7\" />\n")
            .Append("    <feColorMatrix type=\"saturate\" values=\"0\" />\n")
            .Append("  </filter>\n")
            .Append("</defs>")
            .Append("<rect width=\"100%\" height=\"100%\" fill=\"var(--paper, ").Append(Paper).Append(")\" />")
            .Append("<rect width=\"100%\" height=\"100%\" fill=\"#000\" opacity=\"0.05\" filter=\"url(#paper-grain)\" />");
    }

    /// <summary>
    /// The ground the tower stands on — or the moat it stands in, when the silhouette asked for
    /// water, which is its own horizon and so replaces the line rather than being crossed by it.
    /// </summary>
    private static void Ground(StringBuilder svg, RoughPen pen, bool hasWater)
    {
        svg.Append("<g class=\"tower-ground\">");

        if (hasWater)
        {
            svg.Append(Path(pen.ClosedPath(Ellipse(CentreX, GroundY, MoatWidth, MoatDepth), 1.0), Water, Muted, 1.1));

            for (int ring = 1; ring <= 2; ring++)
            {
                double inset = ring * 16;

                svg.Append(Path(
                    pen.ClosedPath(Ellipse(CentreX, GroundY, MoatWidth - inset, MoatDepth - (ring * 3.5)), 0.8),
                    "none",
                    Muted,
                    0.7,
                    opacity: 0.5));
            }
        }
        else
        {
            svg.Append(Stroke(
                pen.Line(new MapPoint(Margin, GroundY), new MapPoint(Width - Margin, GroundY), 1.4), Ink, 1.6));
        }

        svg.Append("</g>");
    }

    /// <summary>The storeys, each showing the number its die turns towards the chosen façade.</summary>
    private static void Body(
        StringBuilder svg,
        RoughPen pen,
        IReadOnlyList<SiteArea> storeys,
        double bodyTop,
        bool isRound)
    {
        svg.Append("<g class=\"tower-body\">");

        svg.Append(Path(
            pen.ClosedPath(
                [
                    new MapPoint(CentreX - HalfWidth, GroundY),
                    new MapPoint(CentreX - HalfWidth, bodyTop),
                    new MapPoint(CentreX + HalfWidth, bodyTop),
                    new MapPoint(CentreX + HalfWidth, GroundY)
                ],
                1.0),
            Stonework,
            Ink,
            1.8));

        for (int index = 0; index < storeys.Count; index++)
        {
            double storeyTop = GroundY - (StoreyHeight * (index + 1));

            // The top of the topmost storey is the body's own outline, which is already drawn.
            if (index + 1 < storeys.Count)
            {
                svg.Append(Course(pen, storeyTop, isRound));
            }

            Pips(svg, storeys[index].Face, CentreX, storeyTop + (StoreyHeight / 2));
        }

        svg.Append("</g>");
    }

    /// <summary>
    /// The line between two storeys.
    /// </summary>
    /// <remarks>
    /// Bowed on a round tower and flat on a square one, which is the whole of the difference between
    /// the two silhouettes a tower may be drawn as. It is a small thing, but it is the only thing in
    /// the drawing that answers to the Layout field, and a field that changed nothing visible would
    /// be a field worth deleting.
    /// </remarks>
    private static string Course(RoughPen pen, double y, bool isRound)
    {
        if (!isRound)
        {
            return Stroke(
                pen.Line(new MapPoint(CentreX - HalfWidth, y), new MapPoint(CentreX + HalfWidth, y), 0.9),
                Muted,
                1.0);
        }

        List<MapPoint> bow = [];

        for (int step = 0; step <= BowSteps; step++)
        {
            double across = (double)step / BowSteps;

            bow.Add(new MapPoint(
                CentreX - HalfWidth + (across * HalfWidth * 2),
                y + (Math.Sin(across * Math.PI) * BowDepth)));
        }

        return Stroke(pen.OpenPath(bow, 0.9), Muted, 1.0);
    }

    /// <summary>The d4 balanced on top, drawn as the tetrahedron it is and showing its number.</summary>
    private static void Roof(StringBuilder svg, RoughPen pen, SiteArea? top, double bodyTop, bool isRound)
    {
        double apex = bodyTop - RoofHeight;
        double eave = isRound ? Eaves : Eaves * 0.6;

        svg.Append("<g class=\"tower-roof\">")
            .Append(Path(
                pen.ClosedPath(
                    [
                        new MapPoint(CentreX - HalfWidth - eave, bodyTop),
                        new MapPoint(CentreX, apex),
                        new MapPoint(CentreX + HalfWidth + eave, bodyTop)
                    ],
                    1.0),
                Roofing,
                Ink,
                1.8));

        if (top is { Face: > 0 })
        {
            svg.Append("<text x=\"").Append(N(CentreX))
                .Append("\" y=\"").Append(N(bodyTop - (RoofHeight * 0.28)))
                .Append("\" fill=\"").Append(Ink)
                .Append("\" font-family=\"Helvetica, Arial, sans-serif\" font-size=\"16\" font-weight=\"700\"")
                .Append(" text-anchor=\"middle\">")
                .Append(top.Face.ToString(CultureInfo.InvariantCulture))
                .Append("</text>");
        }

        svg.Append("</g>");
    }

    /// <summary>The numbers beside the tower, in the engine's own keying style.</summary>
    private static void Keys(
        StringBuilder svg,
        UiText ui,
        IReadOnlyList<SiteArea> storeys,
        SiteArea? top,
        double bodyTop)
    {
        svg.Append("<g class=\"map-keys\" font-family=\"Helvetica, Arial, sans-serif\" font-size=\"9\"")
            .Append(" font-weight=\"700\" text-anchor=\"middle\">");

        foreach (SiteArea storey in storeys)
        {
            Key(svg, ui, storey, GroundY - (StoreyHeight * (storey.Number - 0.5)));
        }

        if (top is not null)
        {
            Key(svg, ui, top, bodyTop - (RoofHeight * 0.45));
        }

        svg.Append("</g>");
    }

    private static void Key(StringBuilder svg, UiText ui, SiteArea area, double y)
    {
        string name = ui.RoleName(area.Role);
        name = name.Length > 0 ? name : ui.AreaName(SiteKinds.Tower);

        // Each badge is wrapped so its <title> is that badge's first child. A flat run of titles in
        // one group names the group once and drops the rest, which is a tooltip nobody ever sees.
        svg.Append("<g><title>").Append(Escape(name)).Append("</title>")
            .Append("<circle cx=\"").Append(N(KeyX)).Append("\" cy=\"").Append(N(y))
            .Append("\" r=\"7\" fill=\"").Append(Ink).Append("\" />")
            .Append("<text x=\"").Append(N(KeyX)).Append("\" y=\"").Append(N(y + 3.2))
            .Append("\" fill=\"").Append(Paper).Append("\">")
            .Append(area.Number.ToString(CultureInfo.InvariantCulture))
            .Append("</text></g>");
    }

    /// <summary>One die face, as the pips a reader would see on the die itself.</summary>
    private static void Pips(StringBuilder svg, int face, double centreX, double centreY)
    {
        if (face is < 1 or > 6)
        {
            return;
        }

        foreach ((int across, int down) in PipLayout[face])
        {
            svg.Append("<circle cx=\"").Append(N(centreX + (across * PipSpacing)))
                .Append("\" cy=\"").Append(N(centreY + (down * PipSpacing)))
                .Append("\" r=\"").Append(N(PipRadius))
                .Append("\" fill=\"").Append(Ink).Append("\" />");
        }
    }

    /// <summary>Where the pips sit on each face, in thirds of the cluster.</summary>
    private static readonly (int Across, int Down)[][] PipLayout =
    [
        [],
        [(0, 0)],
        [(-1, -1), (1, 1)],
        [(-1, -1), (0, 0), (1, 1)],
        [(-1, -1), (1, -1), (-1, 1), (1, 1)],
        [(-1, -1), (1, -1), (0, 0), (-1, 1), (1, 1)],
        [(-1, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (1, 1)]
    ];

    private static IReadOnlyList<MapPoint> Ellipse(double centreX, double centreY, double across, double down)
    {
        List<MapPoint> points = new(EllipseSteps);

        for (int step = 0; step < EllipseSteps; step++)
        {
            double angle = step * 2 * Math.PI / EllipseSteps;

            points.Add(new MapPoint(centreX + (Math.Cos(angle) * across), centreY + (Math.Sin(angle) * down)));
        }

        return points;
    }

    private static string Path(string data, string fill, string stroke, double width, double opacity = 1) =>
        $"<path d=\"{data}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{N(width)}\"" +
        (opacity < 1 ? $" opacity=\"{N(opacity)}\"" : "") +
        " stroke-linejoin=\"round\" />";

    private static string Stroke(string data, string stroke, double width) =>
        $"<path d=\"{data}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{N(width)}\" stroke-linecap=\"round\" />";

    private static string N(double value) => SvgNumber.Format(value);

    private static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    /// <summary>The stream the ink's wobble is drawn from, kept apart from anything rolled.</summary>
    private const string InkStream = "tower/elevation";

    private const double Width = 300;
    private const double Height = 400;
    private const double Margin = 24;

    private const double CentreX = 140;
    private const double HalfWidth = 56;
    private const double GroundY = 344;

    private const double StoreyHeight = 44;
    private const double RoofHeight = 54;
    private const double Eaves = 12;

    private const double KeyX = 214;

    private const double PipSpacing = 11;
    private const double PipRadius = 3.2;

    private const double MoatWidth = 112;
    private const double MoatDepth = 16;

    private const int EllipseSteps = 40;
    private const int BowSteps = 10;
    private const double BowDepth = 5;

    private const string Ink = "#1a1713";
    private const string Muted = "#6b6355";
    private const string Paper = "#f6f1e4";
    private const string Stonework = "#fbf6ea";
    private const string Roofing = "#e6e1d4";
    private const string Water = "#dbe5e6";
}
