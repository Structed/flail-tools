using FlailTools.Core.Data;
using FlailTools.Core.Model;
using Structed.Inkwell.Data;
using Structed.Inkwell.Generation;
using Structed.Inkwell.Randomness;

namespace FlailTools.Core.Generation;

/// <summary>
/// Builds an adventure site from a plan.
/// </summary>
/// <remarks>
/// <para>
/// Generation is a pure function of <see cref="SitePlan"/>. Nothing here reads a clock, a culture or
/// an ambient random source, because a shared link is only worth sharing if it rebuilds exactly what
/// the sender saw.
/// </para>
/// <para>
/// The procedures are FLAIL!'s and are implemented faithfully — the d6 room stocking, the stack of
/// dice that makes a tower's floors, the handful of dice dropped on a page that makes a cave. The
/// licence permits reusing rules and mechanics; it does not permit reusing the words, so every table
/// these read from is original to this repository.
/// </para>
/// </remarks>
public static class SiteGenerator
{
    public static AdventureSite Generate(GameData data, SitePlan plan)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(plan);

        RollContext roll = new(plan);

        string kind = ChooseKind(data, plan, roll);
        SilhouetteRow silhouette = ChooseSilhouette(data, roll, kind);

        string name = NameAssembler.Join(
            roll.Text(FieldPaths.NameStem, data.Site.NameStems),
            roll.Text(FieldPaths.NameTail, data.Site.NameTails));

        List<SiteField> fields = [];
        foreach (string path in FieldPaths.AxesFor(kind))
        {
            fields.Add(new SiteField { Path = path, Value = roll.Text(path, TableFor(data, path)) });
        }

        IReadOnlyList<SiteArea> areas = kind switch
        {
            SiteKinds.Dungeon => BuildDungeonRooms(data, roll),
            SiteKinds.Cave => BuildCaveChambers(data, roll),
            SiteKinds.Tower => BuildTowerFloors(data, roll),
            _ => []
        };

        return new AdventureSite
        {
            Name = name,
            Kind = kind,
            Silhouette = silhouette.Id,
            Shape = silhouette.Shape,
            HasWater = silhouette.HasWater,
            Scale = ScaleFor(kind, areas.Count),
            Fields = fields,
            Areas = areas,
            PinValues = new Dictionary<string, string>(roll.PinValues, StringComparer.Ordinal)
        };
    }

    private static string ChooseKind(GameData data, SitePlan plan, RollContext roll)
    {
        if (SiteKinds.IsKnown(plan.Kind))
        {
            return plan.Kind!;
        }

        return Rolls.Weighted(roll, FieldPaths.Kind, data.Site.Kinds, row => row.Weight).Id;
    }

    private static SilhouetteRow ChooseSilhouette(GameData data, RollContext roll, string kind)
    {
        List<SilhouetteRow> candidates = [.. data.Silhouettes.Silhouettes.Where(row => row.Allows(kind))];

        if (candidates.Count == 0)
        {
            throw new GameDataException(
                $"No silhouette in '{DataPaths.Silhouettes}' admits kind '{kind}', so it cannot be drawn.");
        }

        return Rolls.Weighted(roll, FieldPaths.Silhouette, candidates, row => row.Weight);
    }

    /// <summary>
    /// Eight to twelve rooms, each stocked on a d6, keyed from the way in to the way it ends.
    /// </summary>
    /// <remarks>
    /// Area 1 is the entrance and area 10 is the finale, so the rooms are keyed in the order they
    /// are rolled rather than sorted afterwards: the number is the reader's route through the place,
    /// not a ranking. A dungeon that rolled fewer than ten rooms ends at its last one — the point of
    /// the finale is that the place has somewhere to end, not that it is the tenth door.
    /// </remarks>
    private static IReadOnlyList<SiteArea> BuildDungeonRooms(GameData data, RollContext roll)
    {
        int count = 7 + roll.Dice(FieldPaths.DungeonRoomCount).Roll(5);
        int finale = Math.Min(9, count - 1);
        List<SiteArea> rooms = new(count);

        for (int index = 0; index < count; index++)
        {
            string path = FieldPaths.DungeonRoom(index);

            rooms.Add(new SiteArea
            {
                Number = index + 1,
                Path = path,
                Role = index == 0
                    ? AreaRoles.Entrance
                    : index == finale ? AreaRoles.Finale : AreaRoles.Plain,
                Value = Rolls.Face(roll, path, data.Dungeon.Stocking, sides: 6).Value
            });
        }

        return rooms;
    }

    /// <summary>
    /// A handful of d6s dropped on a page.
    /// </summary>
    /// <remarks>
    /// The die nearest the edge is the way in, the one nearest the middle is the heart of the place,
    /// anything that bounced off the paper is reached some other way, and dice showing the same
    /// number near each other are one connected group. The drop is simulated rather than asked for,
    /// but it is drawn from the same seeded streams as everything else, so a given seed lands the
    /// same dice on the same part of the page on every machine.
    /// </remarks>
    private static IReadOnlyList<SiteArea> BuildCaveChambers(GameData data, RollContext roll)
    {
        int count = 4 + roll.Dice(FieldPaths.CaveChamberCount).Roll(5);

        double[] x = new double[count];
        double[] y = new double[count];
        int[] face = new int[count];
        bool[] offPaper = new bool[count];
        string[] values = new string[count];
        string[] roles = new string[count];

        for (int index = 0; index < count; index++)
        {
            DiceRoller drop = roll.Dice(FieldPaths.CaveDrop(index));

            // Slightly wider than the page, so a die can miss it the way a thrown one does.
            x[index] = -Overshoot + (Rolls.Unit(drop) * (1 + (2 * Overshoot)));
            y[index] = -Overshoot + (Rolls.Unit(drop) * (1 + (2 * Overshoot)));

            offPaper[index] = x[index] is < 0 or > 1 || y[index] is < 0 or > 1;
            roles[index] = offPaper[index] ? AreaRoles.Hidden : AreaRoles.Plain;

            string path = FieldPaths.CaveChamber(index);
            (face[index], values[index]) = Rolls.Face(roll, path, data.Cave.Chambers, sides: 6);
        }

        int entry = NearestOnPaper(count, offPaper, index => EdgeDistance(x[index], y[index]), exclude: -1);
        if (entry >= 0)
        {
            roles[entry] = AreaRoles.Entry;
        }

        int core = NearestOnPaper(count, offPaper, index => CentreDistance(x[index], y[index]), exclude: entry);
        if (core >= 0)
        {
            roles[core] = AreaRoles.Core;
        }

        for (int index = 0; index < count; index++)
        {
            if (roles[index] != AreaRoles.Plain)
            {
                continue;
            }

            for (int other = 0; other < count; other++)
            {
                if (other == index || offPaper[other] || face[index] < 0 || face[other] != face[index])
                {
                    continue;
                }

                if (Distance(x[index], y[index], x[other], y[other]) <= ClusterRadius)
                {
                    roles[index] = AreaRoles.Cluster;
                    break;
                }
            }
        }

        List<SiteArea> chambers = new(count);
        for (int index = 0; index < count; index++)
        {
            chambers.Add(new SiteArea
            {
                Number = index + 1,
                Path = FieldPaths.CaveChamber(index),
                Role = roles[index],
                Value = values[index]
            });
        }

        return chambers;
    }

    /// <summary>
    /// A stack of d6s with a d4 balanced on top, each face the floor it stands for.
    /// </summary>
    /// <remarks>
    /// The d4 is the top floor and reads from its own four-row table, which is the whole reason the
    /// top floor of a wizard's tower is never merely another storey.
    /// </remarks>
    private static IReadOnlyList<SiteArea> BuildTowerFloors(GameData data, RollContext roll)
    {
        int count = 2 + roll.Dice(FieldPaths.TowerFloorCount).Roll(4);
        List<SiteArea> floors = new(count);

        for (int index = 0; index < count; index++)
        {
            bool isTop = index == count - 1;
            string path = FieldPaths.TowerFloor(index);

            floors.Add(new SiteArea
            {
                Number = index + 1,
                Path = path,
                Role = isTop ? AreaRoles.Top : AreaRoles.Plain,
                Value = isTop
                    ? Rolls.Face(roll, path, data.Tower.TopFloorTypes, sides: 4).Value
                    : Rolls.Face(roll, path, data.Tower.FloorTypes, sides: 6).Value
            });
        }

        return floors;
    }

    /// <summary>How big to draw the place, from what its own procedure already decided.</summary>
    private static int ScaleFor(string kind, int areaCount) => kind switch
    {
        SiteKinds.Dungeon => Math.Clamp((areaCount / 3) + 1, 2, 6),
        SiteKinds.Cave => Math.Clamp((areaCount / 2) + 1, 2, 6),
        SiteKinds.Tower => Math.Clamp(areaCount, 2, 6),
        SiteKinds.Location => 3,
        SiteKinds.Landmark => 2,
        _ => 3
    };

    private static IReadOnlyList<string> TableFor(GameData data, string path) => path switch
    {
        FieldPaths.DungeonFlavour => data.Dungeon.Flavours,
        FieldPaths.DungeonType => data.Dungeon.Types,
        FieldPaths.DungeonLocation => data.Dungeon.Locations,
        FieldPaths.DungeonKeyFeature => data.Dungeon.KeyFeatures,
        FieldPaths.DungeonCreatures => data.Dungeon.Creatures,

        FieldPaths.CaveFlavour => data.Cave.Flavours,
        FieldPaths.CaveType => data.Cave.Types,
        FieldPaths.CaveLocation => data.Cave.Locations,
        FieldPaths.CaveKeyFeature => data.Cave.KeyFeatures,
        FieldPaths.CaveCreatures => data.Cave.Creatures,

        FieldPaths.TowerShape => data.Tower.Shapes,
        FieldPaths.TowerOccupant => data.Tower.Occupants,
        FieldPaths.TowerReaction => data.Tower.Reactions,
        FieldPaths.TowerGoal => data.Tower.Goals,

        FieldPaths.LocationLocation => data.Location.Locations,
        FieldPaths.LocationBiome => data.Location.Biomes,
        FieldPaths.LocationCondition => data.Location.Conditions,
        FieldPaths.LocationKeyFeature => data.Location.KeyFeatures,
        FieldPaths.LocationOccupant => data.Location.Occupants,

        FieldPaths.LandmarkLandmark => data.Landmark.Landmarks,
        FieldPaths.LandmarkBiome => data.Landmark.Biomes,
        FieldPaths.LandmarkCondition => data.Landmark.Conditions,
        FieldPaths.LandmarkKeyFeature => data.Landmark.KeyFeatures,
        FieldPaths.LandmarkOccupant => data.Landmark.Occupants,

        _ => []
    };

    private static int NearestOnPaper(int count, bool[] offPaper, Func<int, double> distance, int exclude)
    {
        int best = -1;
        double bestDistance = double.MaxValue;

        for (int index = 0; index < count; index++)
        {
            if (offPaper[index] || index == exclude)
            {
                continue;
            }

            double candidate = distance(index);
            if (candidate < bestDistance)
            {
                bestDistance = candidate;
                best = index;
            }
        }

        return best;
    }

    private static double EdgeDistance(double x, double y) =>
        Math.Min(Math.Min(x, 1 - x), Math.Min(y, 1 - y));

    private static double CentreDistance(double x, double y) => Distance(x, y, 0.5, 0.5);

    private static double Distance(double x1, double y1, double x2, double y2) =>
        Math.Sqrt(((x1 - x2) * (x1 - x2)) + ((y1 - y2) * (y1 - y2)));

    private const double Overshoot = 0.12;

    private const double ClusterRadius = 0.28;
}
