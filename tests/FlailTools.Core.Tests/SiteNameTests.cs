using FlailTools.Core.Data;
using Structed.Inkwell.Generation;

namespace FlailTools.Core.Tests;

public sealed class SiteNameTests
{
    [Fact]
    public async Task BothNameTablesHaveAtLeastFortyDistinctNonblankEntries()
    {
        GameData data = await TestData.LoadAsync();

        AssertNameParts("nameStems", data.Site.NameStems);
        AssertNameParts("nameTails", data.Site.NameTails);
    }

    [Fact]
    public async Task EveryPairMakesAShortDistinctNameWithOneJoiningSpace()
    {
        GameData data = await TestData.LoadAsync();
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

        Assert.All(data.Site.NameStems, stem => Assert.Equal(stem.Trim(), stem));
        Assert.All(data.Site.NameTails, tail => Assert.Equal($" {tail.Trim()}", tail));

        foreach (string stem in data.Site.NameStems)
        {
            foreach (string tail in data.Site.NameTails)
            {
                string name = NameAssembler.Join(stem, tail);

                Assert.Equal($"{stem} {tail.Trim()}", name);
                Assert.InRange(name.Length, 1, 32);
                Assert.True(names.Add(name), $"More than one pairing produces '{name}'.");
            }
        }

        Assert.True(names.Count >= 1600, $"Only {names.Count} distinct names are available.");
    }

    private static void AssertNameParts(string table, IReadOnlyList<string> entries)
    {
        Assert.True(entries.Count >= 40, $"'{table}' has only {entries.Count} entries; it needs at least 40.");
        Assert.All(entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry)));
        Assert.Equal(
            entries.Count,
            entries.Select(entry => entry.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
