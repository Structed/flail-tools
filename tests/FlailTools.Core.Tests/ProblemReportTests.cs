using System.Text.RegularExpressions;
using FlailTools.Core.Data;
using FlailTools.Core.Reporting;

namespace FlailTools.Core.Tests;

/// <summary>
/// The "Report a problem" link opens the form it means to, fills in what it means to, and publishes
/// nothing it should not.
/// </summary>
/// <remarks>
/// <para>
/// Every way this breaks is silent. A template that has been renamed opens a blank issue with no
/// labels. A field id that has been renamed opens the form with that field empty. A label spelt
/// differently is simply not applied, so reports stop arriving in triage and nobody notices that
/// they have stopped. None of it throws, and all of it is found by whoever wonders why the reports
/// dried up.
/// </para>
/// <para>
/// And the link ends up in a public issue, which makes the one thing this tool keeps secret — a
/// dice table code — the thing most worth a test.
/// </para>
/// </remarks>
public sealed partial class ProblemReportTests
{
    private const string Base = "https://structed.github.io/flail-tools/";

    [GeneratedRegex(@"^\s*id:\s*(\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex FieldId { get; }

    [GeneratedRegex(@"^labels:\s*\[(.*)\]\s*$", RegexOptions.Multiline)]
    private static partial Regex LabelList { get; }

    private static string TemplatePath { get; } = Path.Combine(
        TestData.RepositoryRoot, ".github", "ISSUE_TEMPLATE", ProblemReport.Template);

    [Fact]
    public void TheFormTheLinkOpensExists()
    {
        Assert.True(
            File.Exists(TemplatePath),
            $"The report link opens '{ProblemReport.Template}', which is not under .github/ISSUE_TEMPLATE. " +
            "GitHub would open a blank issue with no labels instead.");
    }

    [Theory]
    [InlineData(ProblemReport.PageField)]
    [InlineData(ProblemReport.EnvironmentField)]
    public async Task EveryFieldTheLinkFillsIsOnTheForm(string field)
    {
        string template = await File.ReadAllTextAsync(TemplatePath);
        HashSet<string> ids = [.. FieldId.Matches(template).Select(match => match.Groups[1].Value)];

        Assert.True(
            ids.Contains(field),
            $"The report link fills in '{field}', but the form has no field with that id. " +
            "Rename them together, or the field arrives empty.");
    }

    [Fact]
    public async Task TheFormLabelsEveryReportForTriage()
    {
        string template = await File.ReadAllTextAsync(TemplatePath);
        Match list = LabelList.Match(template);

        Assert.True(list.Success, "The report form no longer names its labels on one 'labels: [...]' line.");

        string[] labels = [.. list.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(label => label.Trim('"', '\''))];

        Assert.Equal(ProblemReport.Labels.Order(StringComparer.Ordinal), labels.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ADiceTableCodeNeverLeavesTheMachine()
    {
        string page = ProblemReport.Page($"{Base}dice?t=abcdefghjkmn", Base);
        string url = ProblemReport.NewIssueUrl(page, ProblemReport.Environment("0123456", "agent", "800 x 600"));

        Assert.Equal($"{Base}dice", page);
        Assert.DoesNotContain("abcdefghjkmn", url, StringComparison.Ordinal);
    }

    [Fact]
    public void AGeneratorLinkKeepsWhatRebuildsTheSite()
    {
        Assert.Equal($"{Base}site?s=abc123&k=cave", ProblemReport.Page($"{Base}site?s=abc123&k=cave#map", Base));
    }

    [Theory]
    [InlineData("about?anything=at-all", "about")]
    [InlineData("?t=abcdefghjkmn", "")]
    [InlineData("somewhere-new?secret=1", "somewhere-new")]
    [InlineData("dice#t=abcdefghjkmn", "dice")]
    public void EveryOtherPageIsReportedByItsPathAlone(string relative, string expected)
    {
        Assert.Equal(Base + expected, ProblemReport.Page(Base + relative, Base));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://elsewhere.example/dice?t=abcdefghjkmn")]
    public void AnAddressOutsideTheSiteIsReportedAsTheSite(string? page)
    {
        Assert.Equal(Base, ProblemReport.Page(page, Base));
    }

    [Fact]
    public void TheLinkNeverOutgrowsWhatGitHubWillOpen()
    {
        string locks = string.Concat(Enumerable.Repeat("&p=dungeon/rooms/1:3", 1000));
        string page = ProblemReport.Page($"{Base}site?s=abc123{locks}", Base);
        string url = ProblemReport.NewIssueUrl(page, ProblemReport.Environment(null, new string('x', 5000), null));

        Assert.True(url.Length <= ProblemReport.MaximumLength, $"The link is {url.Length} characters long.");
        Assert.Contains($"{ProblemReport.PageField}=", url, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLinkOpensTheFormEvenWithNothingToFillIn()
    {
        Assert.Equal(
            $"{ProblemReport.Repository}/issues/new?template={ProblemReport.Template}",
            ProblemReport.NewIssueUrl("", ""));
    }

    [Theory]
    [InlineData("1.0.0+0123456789abcdef0123456789abcdef01234567", "0123456")]
    [InlineData("1.0.0", null)]
    [InlineData("1.0.0+local", null)]
    [InlineData(null, null)]
    public void TheBuildIsTheCommitItCameFrom(string? version, string? expected)
    {
        Assert.Equal(expected, ProblemReport.Build(version));
    }

    [Fact]
    public void TheEnvironmentReadsTheSameOnEveryPlatform()
    {
        Assert.Equal(
            "Build: unknown\nBrowser: Mozilla/5.0\nWindow: 1280 x 720",
            ProblemReport.Environment(null, "  Mozilla/5.0 ", "1280 x 720"));
    }

    [Fact]
    public async Task EveryWayAScreenshotCanEndHasWords()
    {
        UiText ui = (await TestData.LoadAsync()).Ui;

        foreach (string outcome in ProblemReport.Outcomes.Append("something-new"))
        {
            string key = ProblemReport.Outcome(outcome);

            Assert.True(ui.Messages.ContainsKey(key), $"'{key}' is shown after a screenshot but missing from ui.json.");
        }
    }
}
