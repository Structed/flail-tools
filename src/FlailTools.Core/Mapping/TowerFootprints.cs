using Structed.Inkwell.Mapping;

namespace FlailTools.Core.Mapping;

/// <summary>
/// The outline a tower's floors are drawn inside, taken from the tower's rolled Shape.
/// </summary>
/// <remarks>
/// <para>
/// A Bone Keep and a Glass Turret are not the same building, so they are not drawn the same way.
/// The outline follows the die face the Shape was read from rather than the words, for the reason
/// the fixtures do: a translated table still draws a keep as a keep, and rewording an entry cannot
/// silently change the plan. A Shape pinned to somebody's own words has no face, and gets the plain
/// round outline rather than a guess.
/// </para>
/// <para>
/// Every footprint keeps the same <see cref="Usable"/> disc clear, so one set of fixtures and one
/// pair of stairwells fit inside any of them. Shapes differ in what they add outside that disc, not
/// in how much room they leave inside it, which is also why a floor can be redrawn in another shape
/// without moving its furniture.
/// </para>
/// </remarks>
internal static class TowerFootprints
{
    /// <summary>The disc every footprint leaves clear for stairs and fixtures.</summary>
    public const double Usable = 62;

    /// <summary>What the outlines are actually built to, leaving the sampling error room to fall inside.</summary>
    private const double Clear = 64;

    private const double Down = Math.Tau / 4;
    private const double Left = Math.Tau / 4;
    private static readonly MapPoint Centre = new(160, 130);

    /// <summary>A crystal is faceted rather than smooth, so its walls are flats of uneven width.</summary>
    private static readonly (double Turn, double Distance)[] CrystalFacets =
    [
        (0.00, 64), (0.62, 72), (1.15, 66), (1.80, 76), (2.42, 68),
        (3.05, 74), (3.60, 65), (4.25, 78), (4.80, 67), (5.50, 71)
    ];

    /// <summary>A square monolith with its four edges dressed off, which is an obelisk in plan.</summary>
    private static readonly (double Turn, double Distance)[] ObeliskFacets =
    [
        (0.00, 64), (Math.Tau / 8, 74), (Math.Tau / 4, 64), (3 * Math.Tau / 8, 74),
        (Math.Tau / 2, 64), (5 * Math.Tau / 8, 74), (3 * Math.Tau / 4, 64), (7 * Math.Tau / 8, 74)
    ];

    /// <summary>Courses of stone laid by hand, so the wall is never quite the same thickness twice.</summary>
    private static readonly double[] StoneCourses =
    [
        65, 70, 67, 71, 66, 69, 68, 72, 66, 70, 67, 71,
        65, 69, 68, 72, 66, 70, 67, 71, 65, 69, 68, 72
    ];

    /// <summary>
    /// The outline for a Shape die face, and the name it is drawn under.
    /// </summary>
    /// <param name="face">The face the Shape was rolled on, or <c>null</c> when it was pinned to text.</param>
    /// <param name="floorIndex">Which floor is being drawn, for the shapes that change as they rise.</param>
    public static (string Name, MapPolygon Outline) For(int? face, int floorIndex) => face switch
    {
        0 => ("clocktower", Regular(4)),
        1 => ("crystal", Faceted(CrystalFacets)),
        2 => ("obelisk", Faceted(ObeliskFacets)),
        3 => ("lighthouse", Radial(64, turn => Clear + Swell(turn, Left, 0.85, 18))),
        4 => ("tree", Radial(72, turn => 70 - (6 * Math.Cos(6 * turn)))),
        // A spire that twists has to twist: each floor is set a little further round than the one below.
        5 => ("spire", Regular(8, floorIndex * Math.Tau / 30)),
        6 => ("windmill", Radial(72, turn => Clear + Vanes(turn))),
        7 => ("turret", Regular(12)),
        8 => ("stonework", Courses(StoneCourses)),
        9 => ("keep", Radial(72, turn => Math.Max(WallAt(turn), Bastions(turn)))),
        _ => ("plain", Radial(32, _ => Clear))
    };

    /// <summary>
    /// Where the front door goes: the point at which the wall crosses the middle of the plan, low side.
    /// </summary>
    /// <remarks>
    /// Read off the outline rather than assumed, because the shapes do not all reach the same depth and
    /// a door drawn at a fixed height would hang off the wall of half of them.
    /// </remarks>
    public static MapPoint Doorway(MapPolygon outline)
    {
        ArgumentNullException.ThrowIfNull(outline);

        IReadOnlyList<MapPoint> points = outline.Points;
        double depth = Centre.Y;

        for (int index = 0; index < points.Count; index++)
        {
            MapPoint from = points[index];
            MapPoint to = points[(index + 1) % points.Count];

            if (from.X == to.X || (Centre.X < Math.Min(from.X, to.X)) || (Centre.X > Math.Max(from.X, to.X)))
            {
                continue;
            }

            double along = (Centre.X - from.X) / (to.X - from.X);
            depth = Math.Max(depth, from.Y + (along * (to.Y - from.Y)));
        }

        return new(Centre.X, depth);
    }

    /// <summary>A regular polygon of the given apothem, sitting flat on its lowest edge.</summary>
    private static MapPolygon Regular(int sides, double turn = 0)
    {
        double step = Math.Tau / sides;

        return new(Enumerable.Range(0, sides).Select(index =>
            Centre + MapPoint.FromAngle(Down + (step / 2) + (index * step) + turn, Clear / Math.Cos(step / 2))));
    }

    /// <summary>An outline sampled from a radius that varies as it goes round.</summary>
    private static MapPolygon Radial(int samples, Func<double, double> radius)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 3);

        return new(Enumerable.Range(0, samples).Select(step =>
        {
            double turn = step * Math.Tau / samples;
            return Centre + MapPoint.FromAngle(Down + turn, radius(turn));
        }));
    }

    /// <summary>An outline read straight off a list of wall depths, evenly spaced.</summary>
    private static MapPolygon Courses(IReadOnlyList<double> radii) =>
        new(radii.Select((radius, step) =>
            Centre + MapPoint.FromAngle(Down + (step * Math.Tau / radii.Count), radius)));

    /// <summary>
    /// An outline built from its walls rather than its corners: each facet is a flat at a stated
    /// distance, and the corners fall out of where consecutive flats meet.
    /// </summary>
    /// <remarks>
    /// Built this way a facet can never cut inside the distance it was given, so an outline whose
    /// every facet clears <see cref="Usable"/> is guaranteed to leave that disc intact however
    /// uneven its walls are.
    /// </remarks>
    private static MapPolygon Faceted(IReadOnlyList<(double Turn, double Distance)> facets)
    {
        List<MapPoint> corners = new(facets.Count);

        for (int index = 0; index < facets.Count; index++)
        {
            (double wallTurn, double wall) = facets[index];
            (double nextTurn, double next) = facets[(index + 1) % facets.Count];
            double here = Down + wallTurn;
            double there = Down + nextTurn;
            double span = there - here;

            if (span <= 0)
            {
                span += Math.Tau;
                there = here + span;
            }

            double spread = Math.Sin(span);
            corners.Add(Centre + new MapPoint(
                ((wall * Math.Sin(there)) - (next * Math.Sin(here))) / spread,
                ((next * Math.Cos(here)) - (wall * Math.Cos(there))) / spread));
        }

        return new(corners);
    }

    /// <summary>A stair turret swelling smoothly out of one side and rejoining the wall.</summary>
    private static double Swell(double turn, double at, double width, double height)
    {
        double away = Math.Abs(Math.IEEERemainder(turn - at, Math.Tau));

        return away >= width ? 0 : height * 0.5 * (1 + Math.Cos(Math.PI * away / width));
    }

    /// <summary>Four sail stubs on the diagonals, which leaves the wall plain where the door goes.</summary>
    private static double Vanes(double turn) =>
        16 * Math.Max(0, Math.Cos(4 * (turn - (Math.Tau / 8))));

    /// <summary>The wall of a square of apothem <see cref="Clear"/>, in the direction asked for.</summary>
    private static double WallAt(double turn) =>
        Clear / Math.Max(Math.Abs(Math.Sin(turn)), Math.Abs(Math.Cos(turn)));

    /// <summary>Drum towers at the four corners, which is what makes a keep a keep.</summary>
    private static double Bastions(double turn)
    {
        const double Standing = 78;
        const double Across = 16;
        double reach = 0;

        for (int corner = 0; corner < 4; corner++)
        {
            double away = Math.IEEERemainder(turn - ((Math.Tau / 8) + (corner * Math.Tau / 4)), Math.Tau);
            double offset = Standing * Math.Sin(away);
            double clearance = (Across * Across) - (offset * offset);

            if (clearance >= 0)
            {
                reach = Math.Max(reach, (Standing * Math.Cos(away)) + Math.Sqrt(clearance));
            }
        }

        return reach;
    }
}
