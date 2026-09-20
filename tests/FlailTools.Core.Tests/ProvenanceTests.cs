using FlailTools.Core.Data;
using Structed.Inkwell.Data;

namespace FlailTools.Core.Tests;

/// <summary>
/// The provenance claim, made executable.
/// </summary>
/// <remarks>
/// The Games Omnivorous Third-Party Licence lets this tool reuse rules, mechanics, terminology and
/// random tables, table entries included; it does not let it reproduce the book's artwork or its
/// prose. The five generator tables are FLAIL!'s own and say so. Everything else — the name halves,
/// the silhouettes, the interface copy — is written for this tool and says that instead. This test
/// is what keeps the split honest: a file cannot quietly change sides.
/// </remarks>
public sealed class ProvenanceTests
{
    /// <summary>The files whose entries are reproduced from FLAIL!, and may therefore name it.</summary>
    private static readonly string[] DerivedFromFlail =
    [
        DataPaths.Dungeon,
        DataPaths.Cave,
        DataPaths.Tower,
        DataPaths.Location,
        DataPaths.Landmark
    ];

    [Fact]
    public async Task OnlyTheGeneratorTablesAreDerivedFromAnUpstreamWork()
    {
        GameData data = await TestData.LoadAsync();

        foreach (DataFileProvenance file in data.Provenance)
        {
            bool mayBeDerived = DerivedFromFlail.Contains(file.Path, StringComparer.Ordinal);

            if (mayBeDerived)
            {
                Assert.True(
                    file.Source.IsDerived,
                    $"'{file.Path}' holds FLAIL!'s own table entries but does not name the work they " +
                    "come from. Set _source.work so the About page and this test both say where it " +
                    "came from.");

                continue;
            }

            Assert.False(
                file.Source.IsDerived,
                $"'{file.Path}' names '{file.Source.Work}' as the work its content comes from, but it " +
                "is not one of the generator tables. The licence does permit reusing FLAIL!'s random " +
                "tables, so this is a claim about what is actually in the file rather than about what " +
                "is allowed: if content really was taken from somewhere, add the file to " +
                $"{nameof(DerivedFromFlail)} deliberately and update the notices to match.");
        }
    }

    [Fact]
    public async Task EveryDataFileLivesUnderTheHouseDirectory()
    {
        GameData data = await TestData.LoadAsync();

        foreach (DataFileProvenance file in data.Provenance)
        {
            Assert.StartsWith($"{DataPaths.HouseRoot}/", file.Path, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task EveryDataFileSaysWhatItIsAndUnderWhatTerms()
    {
        GameData data = await TestData.LoadAsync();

        foreach (DataFileProvenance file in data.Provenance)
        {
            DataProvenance source = file.Source;

            Assert.False(string.IsNullOrWhiteSpace(source.Describes), $"'{file.Path}' does not say what it is.");
            Assert.False(string.IsNullOrWhiteSpace(source.Rationale), $"'{file.Path}' does not say why it is as it is.");
            Assert.False(string.IsNullOrWhiteSpace(source.Status), $"'{file.Path}' does not mark itself unofficial.");
            Assert.Equal(Attribution.LicenceUrl, source.LicenceUrl);
        }
    }

    [Fact]
    public async Task EveryDataFileCarriesBothRequiredNotices()
    {
        GameData data = await TestData.LoadAsync();

        foreach (DataFileProvenance file in data.Provenance)
        {
            Assert.Equal(Attribution.IndependentNotice, file.Source.Attribution);

            Assert.Contains(
                Attribution.CopyrightNotice,
                file.Source.Notices,
                StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task EveryTableFileOnDiskIsOneTheAppLoads()
    {
        GameData data = await TestData.LoadAsync();

        string houseRoot = Path.Combine(TestData.DataRoot, DataPaths.HouseRoot);
        IEnumerable<string> onDisk = Directory
            .EnumerateFiles(houseRoot, "*.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(TestData.DataRoot, path).Replace('\\', '/'));

        foreach (string path in onDisk)
        {
            Assert.Contains(
                path,
                data.Provenance.Select(file => file.Path),
                StringComparer.Ordinal);
        }
    }
}
