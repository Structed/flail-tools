using FlailTools.Core.Data;
using Structed.Inkwell.Data;

namespace FlailTools.Core.Tests;

/// <summary>
/// The licence constraint, made executable.
/// </summary>
/// <remarks>
/// FLAIL! has no SRD and no open licence. The Games Omnivorous Third Party Licence lets this tool
/// reuse templates, rules and mechanics; it does not let it copy or translate text. A table's shape
/// is therefore fair game and the entries filling it are not, so no table entry this tool ships may
/// come from the book, and the way that is kept true over time is a test that fails if any data file
/// ever claims an upstream work. <c>NOTICE.md</c> quotes the licence and records that reading.
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
                $"'{file.Path}' names '{file.Source.Work}' as the work its content comes from. FLAIL! " +
                "is not openly licensed and its licence forbids copying or translating text, so every " +
                "entry this tool ships has to be original. If content really was taken from somewhere, " +
                "the answer is to remove it, not to relax this test.");
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
