using System.Text.RegularExpressions;

namespace FlailTools.Core.Tests;

/// <summary>
/// The addresses the pages sit at.
/// </summary>
/// <remarks>
/// <para>
/// A route is not an implementation detail here: it is the thing people paste into chat windows,
/// and the generator's carries a whole site in its query. Moving one breaks every link already
/// shared, and nothing about moving it looks like a mistake — the app still builds, still runs, and
/// still shows a page at the address that used to be somewhere else.
/// </para>
/// <para>
/// So the set of addresses is pinned. Changing one of these is a deliberate act that fails here
/// first, rather than a quiet one that fails in somebody's game.
/// </para>
/// </remarks>
public sealed partial class PageRouteTests
{
    [GeneratedRegex("""^@page\s+"([^"]+)"\s*$""", RegexOptions.Multiline)]
    private static partial Regex PageDirective { get; }

    [Theory]
    [InlineData("/")]
    [InlineData("/site")]
    [InlineData("/dice")]
    [InlineData("/about")]
    public void TheAddressIsServedByExactlyOnePage(string route) =>
        Assert.Single(Routes(), served => served == route);

    [Fact]
    public void NoTwoPagesClaimTheSameAddress()
    {
        string[] routes = Routes();

        Assert.Equal(routes.Length, routes.Distinct(StringComparer.Ordinal).Count());
    }

    private static string[] Routes()
    {
        string root = Path.Combine(TestData.RepositoryRoot, "src", "FlailTools.Web");

        return Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .SelectMany(path => PageDirective.Matches(File.ReadAllText(path)))
            .Select(match => match.Groups[1].Value)
            .ToArray();
    }

    /// <summary>The addresses the app serves, for tests that have to agree with them.</summary>
    internal static IReadOnlyCollection<string> Served() => Routes();
}
