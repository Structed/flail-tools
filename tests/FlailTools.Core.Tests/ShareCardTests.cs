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
/// landing card is written by hand into <c>wwwroot/index.html</c>, and each tool gets a copy of
/// that file under its own address, with the card swapped, written by
/// <c>tools/New-ShareShells.ps1</c> and committed. Neither half is visible from the other, and
/// neither fails when it goes stale: a shell built from a manifest entry that no longer matches
/// the art still deploys, and still looks fine, and is simply wrong.
/// </para>
/// <para>
/// So the manifest is pinned to the things it claims — the images beside it, the routes the app
/// actually serves, and the tags already in <c>index.html</c> — and each shell is pinned to being
/// index.html with nothing but its own card changed.
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

    public static TheoryData<string> ToolRoutes() =>
        [.. Manifest.Cards.Where(card => card.Shell is not null).Select(card => card.Route)];

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
    /// The whole point of a shell is that it is the app, reachable at the tool's own address, with
    /// only the card changed. Rebuilding it here from <c>index.html</c> catches both halves of the
    /// drift: a manifest value that never reached the committed copy, and an edit to index.html
    /// itself — a new stylesheet, a changed loading screen — that the copies never learned about.
    /// </summary>
    [Theory]
    [MemberData(nameof(ToolRoutes))]
    public void EveryShellIsIndexHtmlWithNothingButItsOwnCardChanged(string route)
    {
        ShareCard card = Card(route);
        string shellPath = Path.Combine(WebRoot, card.Shell!, "index.html");

        Assert.True(
            File.Exists(shellPath),
            $"'{route}' has no '{card.Shell}/index.html', so it unfurls as the landing page. "
                + "Run tools/New-ShareShells.ps1 and commit what it writes.");

        string[] index = File.ReadAllLines(Path.Combine(WebRoot, "index.html"));
        string[] expected = [.. index.Select(line => Restamp(line, card))];

        Assert.Equal(expected, File.ReadAllLines(shellPath));
    }

    /// <summary>
    /// A shell is a copy of index.html, so it carries the same relative <c>base href</c> and needs
    /// the same rewriting when Pages serves the app from a subdirectory. Rewriting only the root
    /// index.html would leave every tool loading its assets from the wrong place.
    /// </summary>
    [Fact]
    public void TheDeploymentGivesEveryShellTheHostsBaseHref()
    {
        string workflow = File.ReadAllText(
            Path.Combine(TestData.RepositoryRoot, ".github", "workflows", "deploy.yml"));

        Assert.Contains("find publish/wwwroot -name index.html", workflow, StringComparison.Ordinal);

        // The rewrite is a literal substitution, so the tag has to keep the shape it looks for.
        Assert.Contains(
            "<base href=\"/\" />",
            File.ReadAllText(Path.Combine(WebRoot, "index.html")),
            StringComparison.Ordinal);
    }

    /// <summary>Applies one card to a line of index.html, the way the generator does.</summary>
    private static string Restamp(string line, ShareCard card)
    {
        string image = $"{Manifest.BaseUrl}{card.Image}";

        if (line.Contains("<title>", StringComparison.Ordinal))
        {
            return Replace(line, "<title>", "</title>", card.DocumentTitle);
        }

        // The closing quote matters: og:image must not also match og:image:alt.
        return line switch
        {
            _ when Carries(line, "name=\"description\"") => Content(line, card.Description),
            _ when Carries(line, "property=\"og:title\"") => Content(line, card.Title),
            _ when Carries(line, "property=\"og:description\"") => Content(line, card.Description),
            _ when Carries(line, "property=\"og:image\"") => Content(line, image),
            _ when Carries(line, "property=\"og:image:alt\"") => Content(line, card.ImageAlt),
            _ when Carries(line, "name=\"twitter:image\"") => Content(line, image),
            _ when Carries(line, "name=\"twitter:image:alt\"") => Content(line, card.ImageAlt),
            _ => line,
        };
    }

    private static bool Carries(string line, string attribute) =>
        line.Contains("<meta", StringComparison.Ordinal)
            && line.Contains(attribute, StringComparison.Ordinal);

    private static string Content(string line, string value) =>
        Replace(line, "content=\"", "\"", Escape(value));

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string Replace(string line, string opening, string closing, string value)
    {
        int from = line.IndexOf(opening, StringComparison.Ordinal) + opening.Length;
        int to = line.IndexOf(closing, from, StringComparison.Ordinal);

        return string.Concat(line.AsSpan(0, from), value, line.AsSpan(to));
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
