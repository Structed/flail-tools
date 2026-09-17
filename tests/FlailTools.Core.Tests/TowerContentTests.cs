using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Model;
using Structed.Inkwell.Generation;

namespace FlailTools.Core.Tests;

/// <summary>
/// What the Wizard Towers tables have to look like, beyond what <see cref="GameData.Validate"/>
/// already refuses to load.
/// </summary>
/// <remarks>
/// The four theme axes are the book's d10 tables, so they are pinned at exactly ten entries rather
/// than merely "enough": a table that grew an eleventh row would still roll, but it would no longer
/// be the table on the page, and a pin shared from an older build would point at a different row.
/// </remarks>
public sealed class TowerContentTests
{
    [Fact]
    public async Task EveryAxisHasExactlyTenDistinctSingleLineEntries()
    {
        GameData data = await TestData.LoadAsync();

        foreach ((string path, IReadOnlyList<string> entries) in AxisTables(data))
        {
            Assert.True(entries.Count == 10, $"'{path}' has {entries.Count} entries; the book's d10 needs exactly 10.");
            AssertDistinctSingleLineEntries(entries);
        }
    }

    [Fact]
    public async Task FloorTablesHaveExactlyOneDistinctSingleLineEntryPerFace()
    {
        GameData data = await TestData.LoadAsync();

        Assert.Equal(6, data.Tower.FloorTypes.Count);
        Assert.Equal(4, data.Tower.TopFloorTypes.Count);
        Assert.Equal(6, data.Tower.FloorDetails.Count);
        AssertDistinctSingleLineEntries(data.Tower.FloorTypes);
        AssertDistinctSingleLineEntries(data.Tower.TopFloorTypes);

        foreach (IReadOnlyList<string> details in data.Tower.FloorDetails)
        {
            Assert.Equal(4, details.Count);
            AssertDistinctSingleLineEntries(details);
        }
    }

    [Fact]
    public async Task EveryAxisEntryCanBeRolled()
    {
        GameData data = await TestData.LoadAsync();
        (string Path, IReadOnlyList<string> Entries)[] axes = AxisTables(data);
        Dictionary<string, HashSet<string>> rolled = axes.ToDictionary(
            axis => axis.Path,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        for (uint seed = 1; seed <= 256; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = SiteKinds.Tower });

            foreach (SiteField field in site.Fields)
            {
                rolled[field.Path].Add(field.Value);
            }
        }

        foreach ((string path, IReadOnlyList<string> entries) in axes)
        {
            Assert.Equal(entries.Order(StringComparer.Ordinal), rolled[path].Order(StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// A floor below the top is two rolls that have to agree: the d6 names the kind of room and the
    /// d4 names which one of those, so the detail must come from that kind's own row.
    /// </summary>
    [Fact]
    public async Task EveryFloorEntryIsReachableAndResolvesItsRecordedDieFaces()
    {
        GameData data = await TestData.LoadAsync();
        HashSet<(int Type, int Detail)> ordinaryFloors = [];
        HashSet<int> topFloors = [];

        for (uint seed = 1; seed <= 256; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = SiteKinds.Tower });

            for (int index = 0; index < site.Areas.Count; index++)
            {
                SiteArea floor = site.Areas[index];
                bool isTop = index == site.Areas.Count - 1;

                Assert.True(PinReference.TryGetIndex(site.PinValues[floor.Path], out int face));

                if (isTop)
                {
                    Assert.InRange(face, 0, data.Tower.TopFloorTypes.Count - 1);
                    Assert.Equal(data.Tower.TopFloorTypes[face], floor.Value);
                    topFloors.Add(face);
                    continue;
                }

                Assert.InRange(face, 0, data.Tower.FloorTypes.Count - 1);
                Assert.True(
                    PinReference.TryGetIndex(site.PinValues[FieldPaths.TowerFloorDetail(index)], out int detail),
                    $"Floor {index} rolled a kind but recorded no d4 for which one it is.");
                Assert.InRange(detail, 0, data.Tower.FloorDetails[face].Count - 1);
                Assert.Equal($"{data.Tower.FloorTypes[face]}: {data.Tower.FloorDetails[face][detail]}", floor.Value);
                ordinaryFloors.Add((face, detail));
            }
        }

        Assert.Equal(
            Enumerable.Range(0, data.Tower.TopFloorTypes.Count),
            topFloors.Order());
        Assert.Equal(
            from type in Enumerable.Range(0, data.Tower.FloorTypes.Count)
            from detail in Enumerable.Range(0, data.Tower.FloorDetails[type].Count)
            select (type, detail),
            ordinaryFloors.Order());
    }


    private static (string Path, IReadOnlyList<string> Entries)[] AxisTables(GameData data) =>
    [
        (FieldPaths.TowerShape, data.Tower.Shapes),
        (FieldPaths.TowerOccupant, data.Tower.Occupants),
        (FieldPaths.TowerReaction, data.Tower.Reactions),
        (FieldPaths.TowerGoal, data.Tower.Goals)
    ];

    private static void AssertDistinctSingleLineEntries(IReadOnlyList<string> entries)
    {
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry));
            Assert.Equal(entry.Trim(), entry);
            Assert.False(entry.Any(char.IsControl), $"Entry must fit on one line without control characters: '{entry}'.");
        });

        Assert.Equal(entries.Count, entries.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
