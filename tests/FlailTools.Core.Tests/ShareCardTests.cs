using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace FlailTools.Core.Tests;

/// <summary>
/// The card each page unfurls as when somebody pastes its link.
/// </summary>
/// <remarks>
/// <para>
/// A crawler does not run Blazor, so a single-page app has one card unless it is given more. The
/// landing card is written by hand into <c>wwwroot/index.html</c>; the rest are stamped into
/// per-route shells at publish time by <c>tools/New-ShareShells.ps1</c>. Neither half is visible
/// from the other, and neither fails when it goes stale: a shell built from a manifest entry that
/// no longer matches the art still deploys, and still looks fine, and is simply wrong.
/// </para>
/// <para>
/// So the manifest is pinned to the things it claims — the images beside it, the routes the app
/// actually serves, and the tags already in <c>index.html</c>.
/// </para>
/// </remarks>
public sealed class ShareCardTests
{
    private static readonly string WebRoot =
        Path.Combine(TestData.RepositoryRoot, "src", "FlailTools.Web", "wwwroot");

    private static readonly ShareCardManifest Manifest = Load();

    private sealed record ShareCard(
        string Route,
        string? Shell,
        string DocumentTitle,
        string Title,
        string Description,
        string Image,
        string ImageAlt);

    private sealed record ShareCardManifest(string BaseUrl, ShareCard[] Cards);

    private static ShareCardManifest Load()
    {
        string path = Path.Combine(TestData.RepositoryRoot, "tools", "share-cards.json");

        ShareCardManifest? manifest = JsonSerializer.Deserialize<ShareCardManifest>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return manifest ?? throw new InvalidOperationException($"'{path}' is empty.");
    }

    public static TheoryData<string> Routes() => [.. Manifest.Cards.Select(card => card.Route)];

    private static ShareCard Card(string route) =>
        Manifest.Cards.Single(card => card.Route == route);

    [Fact]
    public void BothToolsAndTheLandingPageHaveACard()
    {
        string[] routes = Manifest.Cards.Select(card => card.Route).ToArray();

        Assert.Equal(new[] { "/", "/site", "/dice" }, routes);
    }

    /// <summary>
    /// A card for an address nobody can reach is a card nobody ever sees, and the shell it writes
    /// would sit in the deployment shadowing nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryCardNamesAnAddressTheAppServes(string route) =>
        Assert.Contains(route, PageRouteTests.Served(), StringComparer.Ordinal);

    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryCardIsADistinctPngAtTheSharingSize(string route)
    {
        ShareCard card = Card(route);
        byte[] png = File.ReadAllBytes(Path.Combine(WebRoot, card.Image));

        Assert.Equal("IHDR"u8.ToArray(), png[12..16]);
        Assert.Equal(1200, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(630, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));

        // The point of the exercise: three files, not three copies of one.
        string hash = Convert.ToHexString(SHA256.HashData(png));

        foreach (ShareCard other in Manifest.Cards.Where(other => other.Route != route))
        {
            byte[] theirs = File.ReadAllBytes(Path.Combine(WebRoot, other.Image));

            Assert.NotEqual(hash, Convert.ToHexString(SHA256.HashData(theirs)));
        }
    }

    /// <summary>The PNG is an export; losing the SVG means losing the ability to change the card.</summary>
    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryCardKeepsItsEditableSource(string route)
    {
        string source = Path.ChangeExtension(Card(route).Image, ".svg");

        Assert.True(
            File.Exists(Path.Combine(WebRoot, source)),
            $"'{route}' ships '{Card(route).Image}' with no '{source}' to re-export it from.");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryCardSaysItIsUnofficialAndUnaffiliated(string route)
    {
        ShareCard card = Card(route);

        Assert.Contains("Unofficial", card.Title, StringComparison.Ordinal);
        Assert.Contains("Not affiliated with Games Omnivorous.", card.ImageAlt, StringComparison.Ordinal);
    }

    /// <summary>
    /// The shell is the address with its leading slash taken off, because that is where a static
    /// host looks for the route's <c>index.html</c>. The landing page has none: it is index.html.
    /// </summary>
    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryShellSitsAtItsOwnAddress(string route)
    {
        ShareCard card = Card(route);

        if (route == "/")
        {
            Assert.Null(card.Shell);

            return;
        }

        Assert.Equal(route.TrimStart('/'), card.Shell);
    }

    [Fact]
    public void TheLandingCardIsTheOneAlreadyInTheStaticHead()
    {
        ShareCard landing = Card("/");
        string html = File.ReadAllText(Path.Combine(WebRoot, "index.html"));

        Assert.Equal(landing.Title, MetaContent(html, "property", "og:title"));
        Assert.Equal(landing.Description, MetaContent(html, "property", "og:description"));
        Assert.Equal(landing.Description, MetaContent(html, "name", "description"));
        Assert.Equal(landing.ImageAlt, MetaContent(html, "property", "og:image:alt"));
        Assert.Equal(landing.ImageAlt, MetaContent(html, "name", "twitter:image:alt"));
        Assert.Equal($"{Manifest.BaseUrl}{landing.Image}", MetaContent(html, "property", "og:image"));
        Assert.Equal($"{Manifest.BaseUrl}{landing.Image}", MetaContent(html, "name", "twitter:image"));
        Assert.Contains($"<title>{landing.DocumentTitle}</title>", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The images the head declares are the same size for every card, so the fixed width, height
    /// and type tags in <c>index.html</c> stay true of the shells copied from it.
    /// </summary>
    [Fact]
    public void TheDeclaredImageSizeHoldsForEveryCard()
    {
        string html = File.ReadAllText(Path.Combine(WebRoot, "index.html"));

        Assert.Equal("1200", MetaContent(html, "property", "og:image:width"));
        Assert.Equal("630", MetaContent(html, "property", "og:image:height"));
        Assert.Equal("image/png", MetaContent(html, "property", "og:image:type"));
        Assert.All(Manifest.Cards, card => Assert.EndsWith(".png", card.Image, StringComparison.Ordinal));
    }

    /// <summary>
    /// Everything above is only true of the deployment if the shells are actually written, and
    /// written before the SPA fallback is taken, so the 404 page keeps the generic card.
    /// </summary>
    [Fact]
    public void TheDeploymentWritesTheShellsBeforeTakingTheFallback()
    {
        string workflow = File.ReadAllText(
            Path.Combine(TestData.RepositoryRoot, ".github", "workflows", "deploy.yml"));

        int shells = workflow.IndexOf("New-ShareShells.ps1", StringComparison.Ordinal);
        int fallback = workflow.IndexOf("404.html", StringComparison.Ordinal);

        Assert.True(shells >= 0, "deploy.yml no longer writes the per-route sharing shells.");
        Assert.True(fallback >= 0, "deploy.yml no longer writes the SPA fallback.");
        Assert.True(shells < fallback, "The shells must be written before index.html becomes 404.html.");
    }

    private static string MetaContent(string html, string attribute, string name)
    {
        string opening = $"{attribute}=\"{name}\"";
        int start = html.IndexOf(opening, StringComparison.Ordinal);

        Assert.True(start >= 0, $"index.html has no <meta {opening}>.");
        Assert.Equal(-1, html.IndexOf(opening, start + opening.Length, StringComparison.Ordinal));

        const string marker = "content=\"";
        int from = html.IndexOf(marker, start, StringComparison.Ordinal) + marker.Length;

        return html[from..html.IndexOf('"', from)];
    }
}
