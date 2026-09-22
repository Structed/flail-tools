using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FlailTools.Core.Tests;

public sealed class ArtworkTests
{
    private static readonly string WebRoot =
        Path.Combine(TestData.RepositoryRoot, "src", "FlailTools.Web", "wwwroot");

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
}
