using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Model;
using Structed.Inkwell.Generation;

namespace FlailTools.Core.Tests;

public sealed class TowerContentTests
{
    [Fact]
    public async Task EveryAxisHasAtLeastEighteenDistinctSingleLineEntries()
    {
        GameData data = await TestData.LoadAsync();

        foreach ((string path, IReadOnlyList<string> entries) in AxisTables(data))
        {
            Assert.True(entries.Count >= 18, $"'{path}' has only {entries.Count} entries; it needs at least 18.");
            AssertDistinctSingleLineEntries(entries);
        }
    }

    [Fact]
    public async Task FloorTablesHaveExactlyOneDistinctSingleLineEntryPerFace()
    {
        GameData data = await TestData.LoadAsync();

        Assert.Equal(6, data.Tower.FloorTypes.Count);
        Assert.Equal(4, data.Tower.TopFloorTypes.Count);
        AssertDistinctSingleLineEntries(data.Tower.FloorTypes);
        AssertDistinctSingleLineEntries(data.Tower.TopFloorTypes);
    }

    [Fact]
    public async Task EveryAxisEntryCanBeRolledIncludingThoseBeyondTheTenth()
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

    [Fact]
    public async Task EveryFloorEntryIsReachableAndResolvesItsRecordedDieFace()
    {
        GameData data = await TestData.LoadAsync();
        HashSet<string> ordinaryFloors = new(StringComparer.Ordinal);
        HashSet<string> topFloors = new(StringComparer.Ordinal);

        for (uint seed = 1; seed <= 64; seed++)
        {
            AdventureSite site = SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = SiteKinds.Tower });

            for (int index = 0; index < site.Areas.Count; index++)
            {
                SiteArea floor = site.Areas[index];
                bool isTop = index == site.Areas.Count - 1;
                IReadOnlyList<string> entries = isTop ? data.Tower.TopFloorTypes : data.Tower.FloorTypes;

                Assert.True(PinReference.TryGetIndex(site.PinValues[floor.Path], out int face));
                Assert.InRange(face, 0, (isTop ? 4 : 6) - 1);
                Assert.Equal(entries[face], floor.Value);
                (isTop ? topFloors : ordinaryFloors).Add(floor.Value);
            }
        }

        Assert.Equal(data.Tower.FloorTypes.Order(StringComparer.Ordinal), ordinaryFloors.Order(StringComparer.Ordinal));
        Assert.Equal(data.Tower.TopFloorTypes.Order(StringComparer.Ordinal), topFloors.Order(StringComparer.Ordinal));
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
