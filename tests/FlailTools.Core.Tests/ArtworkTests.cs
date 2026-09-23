using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FlailTools.Core.Tests;

public sealed class ArtworkTests
{
    private static readonly string WebRoot =
        Path.Combine(TestData.RepositoryRoot, "src", "FlailTools.Web", "wwwroot");

    /// <summary>The compatibility logo, named once so the tests and the markup cannot drift apart.</summary>
    private const string CompatibilityLogo = "flail-compatible-logo.png";

    [Theory]
    [InlineData("favicon.png", 32, 32)]
    [InlineData("icon-192.png", 192, 192)]
    [InlineData("apple-touch-icon.png", 180, 180)]
    [InlineData("open-graph.png", 1200, 630)]
    [InlineData("open-graph-site.png", 1200, 630)]
    [InlineData("open-graph-dice.png", 1200, 630)]
    public void ArtworkIsPngAtItsDeclaredSize(string file, int width, int height)
    {
        byte[] png = File.ReadAllBytes(Path.Combine(WebRoot, file));

        Assert.True(png.Length > 33, $"{file} must contain more than a PNG header.");
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal(13, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(8, 4)));
        Assert.Equal("IHDR"u8.ToArray(), png[12..16]);
        Assert.Equal(width, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(height, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));

        // Reject both stock Blazor icons; originality still needs human review.
        string hash = Convert.ToHexString(SHA256.HashData(png));
        Assert.NotEqual("E265AC0F2DDA1E5DFA65B1ADF330722BB3EF7789115283604D8CD19F098F1F08", hash);
        Assert.NotEqual("0DBA506AAEBC6526F92283E8B0112B33541605FB1B4F1A49AA15344448BAC0FE", hash);
    }

    [Theory]
    [InlineData("icon", "favicon.png", "32x32")]
    [InlineData("icon", "icon-192.png", "192x192")]
    [InlineData("apple-touch-icon", "apple-touch-icon.png", "180x180")]
    public void IconsAreLinkedRelativeToTheDeploymentBase(string rel, string file, string sizes)
    {
        string html = File.ReadAllText(Path.Combine(WebRoot, "index.html"));
        IEnumerable<XElement> links = Regex.Matches(html, @"<link\b[^>]*>", RegexOptions.IgnoreCase)
            .Select(match => XElement.Parse(match.Value));

        XElement link = Assert.Single(links, link =>
            (string?)link.Attribute("rel") == rel && (string?)link.Attribute("sizes") == sizes);

        Assert.Equal(file, (string?)link.Attribute("href"));
        Assert.True(File.Exists(Path.Combine(WebRoot, file)));
        if (rel == "icon")
        {
            Assert.Equal("image/png", (string?)link.Attribute("type"));
        }
    }

    [Fact]
    public void SharingMetadataIsInTheStaticHeadWithAbsolutePublicUrls()
    {
        string html = File.ReadAllText(Path.Combine(WebRoot, "index.html"));
        string head = Regex.Match(html, @"<head>(.*?)</head>", RegexOptions.Singleline).Groups[1].Value;
        XElement[] tags = Regex.Matches(head, @"<meta\b[^>]*>", RegexOptions.IgnoreCase)
            .Select(match => XElement.Parse(match.Value))
            .ToArray();

        string Content(string attribute, string name)
        {
            XElement tag = Assert.Single(tags, tag => (string?)tag.Attribute(attribute) == name);
            return Assert.IsType<XAttribute>(tag.Attribute("content")).Value;
        }

        Assert.Equal("website", Content("property", "og:type"));
        Assert.Contains("Unofficial", Content("property", "og:title"), StringComparison.Ordinal);
        Assert.Equal(Content("name", "description"), Content("property", "og:description"));
        Assert.DoesNotContain(tags, tag => (string?)tag.Attribute("property") == "og:url");
        IEnumerable<XElement> links = Regex.Matches(head, @"<link\b[^>]*>", RegexOptions.IgnoreCase)
            .Select(match => XElement.Parse(match.Value));
        Assert.DoesNotContain(links, link => (string?)link.Attribute("rel") == "canonical");
        Assert.Equal("https://flail-tools.pages.dev/open-graph.png", Content("property", "og:image"));
        Assert.Equal("image/png", Content("property", "og:image:type"));
        Assert.Equal("1200", Content("property", "og:image:width"));
        Assert.Equal("630", Content("property", "og:image:height"));
        Assert.Equal("summary_large_image", Content("name", "twitter:card"));
        Assert.Equal(Content("property", "og:image"), Content("name", "twitter:image"));
        Assert.Equal(Content("property", "og:image:alt"), Content("name", "twitter:image:alt"));
        Assert.Contains("Unofficial", Content("property", "og:image:alt"), StringComparison.Ordinal);
        Assert.Contains("Not affiliated with Games Omnivorous.",
            Content("property", "og:image:alt"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("open-graph.svg")]
    [InlineData("open-graph-site.svg")]
    [InlineData("open-graph-dice.svg")]
    public void EverySharingCardCarriesItsOwnUnofficialNotice(string file)
    {
        XElement svg = XElement.Load(Path.Combine(WebRoot, file));
        XNamespace ns = "http://www.w3.org/2000/svg";

        Assert.Equal("1200", (string?)svg.Attribute("width"));
        Assert.Equal("630", (string?)svg.Attribute("height"));

        string[] visibleText = svg.Descendants(ns + "text").Select(text => text.Value).ToArray();
        Assert.Contains("UNOFFICIAL TOOL", visibleText);
        Assert.Contains("Independent production. Not affiliated with Games Omnivorous.", visibleText);
        Assert.Contains("flail-tools.pages.dev", visibleText);
    }

    [Theory]
    [InlineData("open-graph.svg")]
    [InlineData("open-graph-site.svg")]
    public void TheMapCardsReuseOurOwnIcon(string file)
    {
        XElement svg = XElement.Load(Path.Combine(WebRoot, file));
        XNamespace ns = "http://www.w3.org/2000/svg";

        XElement map = Assert.Single(svg.Descendants(ns + "image"));
        Assert.Equal("icon.svg", (string?)map.Attribute("href"));
        Assert.True(File.Exists(Path.Combine(WebRoot, "icon.svg")));
    }

    /// <summary>
    /// The dice are drawn in the card rather than borrowed from anywhere, which is the whole point
    /// of them: a die is the one picture a game tool is most tempted to lift.
    /// </summary>
    [Fact]
    public void TheDiceCardDrawsItsOwnArtwork()
    {
        XElement svg = XElement.Load(Path.Combine(WebRoot, "open-graph-dice.svg"));
        XNamespace ns = "http://www.w3.org/2000/svg";

        Assert.Empty(svg.Descendants(ns + "image"));
        Assert.NotEmpty(svg.Descendants(ns + "path"));
    }

    /// <summary>
    /// The one file here that is not ours, and the one the licence forbids changing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Section 1 of the Games Omnivorous Third-Party Licence requires this tool to carry the
    /// compatibility logo Games Omnivorous publish, and says it "may not be altered". Every other
    /// image in <c>wwwroot</c> is ours to re-export at will, so the day somebody runs the folder
    /// through an optimiser, or regenerates the icons at a new size and sweeps this up with them,
    /// nothing would look wrong: a re-encoded PNG renders identically and reviews clean.
    /// </para>
    /// <para>
    /// So the bytes are pinned, not the appearance. The hash is of
    /// <c>FLAIL compatible logo black.png</c> as downloaded from the folder Games Omnivorous link
    /// from the licence page. If this fails, the fix is to restore the published file — never to
    /// update the hash to match whatever is now on disk.
    /// </para>
    /// <para>
    /// Note the copy embedded in the licence page's own HTML is a storefront re-encode: same
    /// picture, same 564 x 511, different bytes. The download is the one to ship.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheCompatibilityLogoIsTheOfficialFileUnaltered()
    {
        const string Official = "88149DAAFF1D7C37E2C3435F009BDA6BF8E702CC062C7DB0A1B35FDC22B1794E";

        string path = Path.Combine(WebRoot, CompatibilityLogo);
        Assert.True(File.Exists(path), $"{CompatibilityLogo} is required on every page by the licence.");

        byte[] png = File.ReadAllBytes(path);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal("IHDR"u8.ToArray(), png[12..16]);
        Assert.Equal(564, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(511, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));

        Assert.True(
            Official.Equals(Convert.ToHexString(SHA256.HashData(png)), StringComparison.Ordinal),
            $"{CompatibilityLogo} no longer matches the file Games Omnivorous publish, which the " +
            "licence forbids altering. Restore the published download rather than updating this hash.");
    }

    /// <summary>
    /// The logo has to be on the page, not merely in the repository.
    /// </summary>
    /// <remarks>
    /// It lives in the footer component for the same reason the two notices do: that is the one
    /// piece of markup every route renders. Asserting the reference here means deleting the badge
    /// while tidying the footer fails the build rather than quietly putting the tool back in breach.
    /// </remarks>
    [Fact]
    public void TheCompatibilityLogoIsRenderedByTheFooterOnEveryPage()
    {
        string footer = File.ReadAllText(Path.Combine(
            TestData.RepositoryRoot, "src", "FlailTools.Web", "Components", "LicenceFooter.razor"));

        Assert.Contains(CompatibilityLogo, footer, StringComparison.Ordinal);
    }

    /// <summary>
    /// The stylesheet may set one dimension of the logo, never two.
    /// </summary>
    /// <remarks>
    /// Scaling a picture is not altering it; stretching one is. A <c>width</c> beside the
    /// <c>height</c> on <c>.compat-logo</c> would squash a mark the licence says may not be altered,
    /// and it would read as a harmless layout tweak in review — so <c>width</c> is required to stay
    /// <c>auto</c>, letting the file's own proportions decide.
    /// </remarks>
    [Fact]
    public void TheLogoIsScaledByHeightAloneSoItCannotBeStretched()
    {
        string css = File.ReadAllText(Path.Combine(WebRoot, "css", "app.css"));
        string rule = Regex.Match(css, @"\.compat-logo\s*\{(?<body>[^}]*)\}").Groups["body"].Value;

        Assert.False(string.IsNullOrWhiteSpace(rule), "The compatibility logo needs a .compat-logo rule.");
        Assert.Matches(@"(?<!max-)width\s*:\s*auto\s*;", rule);
    }

    /// <summary>
    /// The badge must be big enough to read the half of its sentence that is inside the picture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The logo is a lead-in ending in "SWINGS HARD WITH", and the product name beside it finishes
    /// the thought. That only works if both halves can be read as one line — but the phrase is a
    /// small part of a picture that is mostly flail. Its capitals occupy rows 402-445 of the file's
    /// 511, measured off the PNG, so they come out at 8.61% of whatever height the stylesheet sets.
    /// </para>
    /// <para>
    /// At the 3rem this badge first shipped at, that is 4.1px of capital standing next to an 11px
    /// product name: the logo's own words were a smudge, the sentence could not be read, and no
    /// test minded. Shrinking a logo is exactly the sort of tidy-up that looks harmless in a diff,
    /// so the floor is held here instead — the words inside the picture may not come out smaller
    /// than the capitals of the name they run into.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheLogoIsLargeEnoughToReadTheWordsInsideIt()
    {
        const double PhraseShareOfHeight = 44.0 / 511.0;
        const double CapHeightOfFontSize = 0.7;

        string css = File.ReadAllText(Path.Combine(WebRoot, "css", "app.css"));
        string rule = Regex.Match(css, @"\.compat-logo\s*\{(?<body>[^}]*)\}").Groups["body"].Value;

        Assert.Contains("var(--compat-logo-height)", rule, StringComparison.Ordinal);

        double phraseCaps = RemValue(css, @"--compat-logo-height\s*:\s*(?<rem>[\d.]+)rem") * PhraseShareOfHeight;
        double nameCaps = RemValue(css, @"\.compat-name\s*\{[^}]*?font-size\s*:\s*(?<rem>[\d.]+)rem") * CapHeightOfFontSize;

        Assert.True(
            phraseCaps >= nameCaps * 0.9,
            string.Create(
                CultureInfo.InvariantCulture,
                $"The logo would render its own words at {phraseCaps * 16:0.0}px beside a " +
                $"{nameCaps * 16:0.0}px product name, so the badge no longer reads as one sentence. " +
                $"Raise --compat-logo-height rather than relaxing this."));
    }

    /// <summary>Reads a single <c>rem</c> measurement out of the stylesheet.</summary>
    private static double RemValue(string css, string pattern)
    {
        Match match = Regex.Match(css, pattern, RegexOptions.Singleline);

        Assert.True(match.Success, $"app.css no longer declares a value matching {pattern}.");

        return double.Parse(match.Groups["rem"].Value, CultureInfo.InvariantCulture);
    }
}
