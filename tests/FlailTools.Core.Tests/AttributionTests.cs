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
