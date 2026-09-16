using FlailTools.Core.Data;
using Structed.Inkwell.Data;

namespace FlailTools.Core.Tests;

/// <summary>
/// The provenance claim, made executable.
/// </summary>
/// <remarks>
/// The Games Omnivorous Third-Party Licence lets this tool reuse rules, mechanics, terminology and
/// random tables, table entries included; it does not let it reproduce the book's artwork or prose.
/// Every table shipped here is original anyway, which is now a choice rather than a requirement.
/// This test is what keeps that claim honest over time: it fails if any data file ever declares an
/// upstream work without that being a deliberate, reviewed decision.
/// </remarks>
public sealed class ProvenanceTests
{
    [Fact]
    public async Task NoDataFileIsDerivedFromAnUpstreamWork()
    {
        GameData data = await TestData.LoadAsync();

        foreach (DataFileProvenance file in data.Provenance)
        {
            Assert.False(
                file.Source.IsDerived,
                $"'{file.Path}' names '{file.Source.Work}' as the work its content comes from. Every " +
                "entry this tool ships is original, and the _source headers say so. The licence does " +
                "now permit reusing FLAIL!'s random tables, so this is a claim about what is actually " +
                "in the file rather than about what is allowed: if content really was taken from " +
                "somewhere, say so here deliberately and update the notices to match.");
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
