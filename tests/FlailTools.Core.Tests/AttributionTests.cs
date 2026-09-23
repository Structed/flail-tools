using FlailTools.Core.Data;

namespace FlailTools.Core.Tests;

/// <summary>
/// The two notices the licence requires must survive whatever is done to the interface.
/// </summary>
/// <remarks>
/// They are required to appear in the product, not merely in the repository. Holding them only in
/// <c>ui.json</c> would make them look like ordinary interface strings that a tidy-up, or a locale
/// file, could quietly replace; so the wording lives in <see cref="Attribution"/> and these tests
/// assert that what the interface renders still reads exactly that once the markup comes off.
/// </remarks>
public sealed class AttributionTests
{
    [Fact]
    public async Task TheIndependenceNoticeIsRenderedVerbatim()
    {
        GameData data = await TestData.LoadAsync();

        Assert.True(
            Attribution.Preserves(data.Ui.Licence.IndependentHtml, Attribution.IndependentNotice),
            $"The rendered notice reads:\n  {Attribution.PlainText(data.Ui.Licence.IndependentHtml)}\n" +
            $"and the licence requires:\n  {Attribution.IndependentNotice}");
    }

    [Fact]
    public async Task TheCopyrightNoticeIsRenderedVerbatim()
    {
        GameData data = await TestData.LoadAsync();

        Assert.True(
            Attribution.Preserves(data.Ui.Licence.CopyrightHtml, Attribution.CopyrightNotice),
            $"The rendered notice reads:\n  {Attribution.PlainText(data.Ui.Licence.CopyrightHtml)}\n" +
            $"and the licence requires:\n  {Attribution.CopyrightNotice}");
    }

    [Fact]
    public async Task BothNoticesAreShownEvenIfTheDataFilesForgetThem()
    {
        GameData data = await TestData.LoadAsync();

        foreach (string notice in Attribution.RequiredNotices)
        {
            Assert.Contains(notice, data.Notices, StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task TheToolIsLabelledUnofficial()
    {
        GameData data = await TestData.LoadAsync();

        Assert.False(string.IsNullOrWhiteSpace(data.Ui.Licence.Unofficial));
    }

    [Fact]
    public async Task TheLicenceIsLinkedFromTheInterface()
    {
        GameData data = await TestData.LoadAsync();

        Assert.Contains(Attribution.LicenceUrl, data.Ui.Licence.IndependentHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void BothNoticesAppearInTheReadme()
    {
        string readme = File.ReadAllText(Path.Combine(TestData.RepositoryRoot, "README.md"));

        foreach (string notice in Attribution.RequiredNotices)
        {
            Assert.Contains(
                notice,
                Attribution.PlainText(readme),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BothNoticesAppearInTheNoticeFile()
    {
        string notices = File.ReadAllText(Path.Combine(TestData.RepositoryRoot, "NOTICE.md"));

        foreach (string notice in Attribution.RequiredNotices)
        {
            Assert.Contains(notice, Attribution.PlainText(notices), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The compatibility badge must say what it is for and nothing more.
    /// </summary>
    /// <remarks>
    /// Section 1 of the licence requires the logo and forbids using it "to suggest that your work is
    /// official, approved or endorsed". The artwork itself cannot break that rule; the words set
    /// beside it can, and they are the part somebody will one day rewrite to sound more impressive.
    /// The masthead already says <em>Unofficial</em> and the notice beneath the badge already
    /// disclaims affiliation, so a badge that hinted otherwise would contradict its own footer.
    /// </remarks>
    [Theory]
    [InlineData("official")]
    [InlineData("approved")]
    [InlineData("endorsed")]
    [InlineData("partner")]
    public async Task TheCompatibilityBadgeNeverClaimsOfficialStatus(string forbidden)
    {
        GameData data = await TestData.LoadAsync();

        foreach (string wording in new[] { data.Ui.Licence.CompatibleAlt, data.Ui.Licence.CompatibleWith })
        {
            Assert.DoesNotContain(forbidden, wording, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The logo is a lead-in, so both halves of its sentence have to be there.
    /// </summary>
    /// <remarks>
    /// The artwork ends in the words "swings hard with" and the product name finishes it. Lose
    /// either half and the badge reads as a dangling phrase rather than the compatibility statement
    /// the licence asks for — and because a missing interface string renders as nothing at all
    /// rather than throwing, it would survive every build and review.
    /// </remarks>
    [Fact]
    public async Task TheCompatibilityBadgeStillReadsAsASentence()
    {
        GameData data = await TestData.LoadAsync();

        Assert.False(string.IsNullOrWhiteSpace(data.Ui.Licence.CompatibleAlt));
        Assert.False(string.IsNullOrWhiteSpace(data.Ui.Licence.CompatibleWith));
    }

    /// <summary>
    /// The licence requires the badge wherever the notices go, so the README carries it too.
    /// </summary>
    [Fact]
    public void TheCompatibilityLogoAppearsInTheReadme()
    {
        string readme = File.ReadAllText(Path.Combine(TestData.RepositoryRoot, "README.md"));

        Assert.Contains("flail-compatible-logo.png", readme, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<p>Hello  world.</p>", "Hello world.")]
    [InlineData("A <a href=\"x\">link</a>.", "A link.")]
    [InlineData("  spaced\n  out  ", "spaced out")]
    [InlineData("Tom &amp; Jerry", "Tom & Jerry")]
    [InlineData(null, "")]
    public void MarkupAndWhitespaceAreIgnoredButWordsAreNot(string? html, string expected) =>
        Assert.Equal(expected, Attribution.PlainText(html));

    [Fact]
    public void AMissingWordIsNotForgiven() =>
        Assert.False(Attribution.Preserves("FLAIL is copyright.", Attribution.CopyrightNotice));
}
