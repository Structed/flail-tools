namespace FlailTools.Core.Tests;

/// <summary>
/// The dice and party code is meant to move house one day, so it must not put down roots.
/// </summary>
/// <remarks>
/// <para>
/// This tool and the Mausritter one both want a shared dice table, and the plan is to build it here,
/// prove it at a real table, and then lift it into the shared engine. That only stays cheap if the
/// lift is a move rather than a rewrite — and the thing that quietly makes it a rewrite is one
/// innocent <c>using FlailTools.Core.Data;</c> added months from now by somebody who just needed a
/// bit of wording.
/// </para>
/// <para>
/// So the rule is written down here, where it fails loudly, instead of in a document nobody rereads.
/// Reading the source text rather than the compiled types is deliberate: a <c>using</c> that is
/// there but unused still signals the intent to reach for this tool's furniture, and catching it at
/// that point is kinder than catching it after the dependency is load-bearing.
/// </para>
/// </remarks>
public sealed class PortabilityTests
{
    /// <summary>The namespaces the portable code may not reach into.</summary>
    private static readonly string[] Forbidden =
    [
        "FlailTools.Core.Data",
        "FlailTools.Core.Generation",
        "FlailTools.Core.Mapping",
        "FlailTools.Core.Model",
        "FlailTools.Core.Serialization"
    ];

    /// <summary>
    /// The one file that is allowed to know which game it is playing.
    /// </summary>
    /// <remarks>
    /// Named rather than pattern-matched, so that adding a second game-specific file is a decision
    /// somebody has to make in this list rather than a side effect of naming a file well.
    /// </remarks>
    private const string StaysBehind = "FlailRolls.cs";

    public static TheoryData<string> PortableSources
    {
        get
        {
            TheoryData<string> sources = [];

            foreach (string folder in new[] { "Dice", "Party" })
            {
                string root = Path.Combine(TestData.RepositoryRoot, "src", "FlailTools.Core", folder);

                foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    sources.Add(Path.GetRelativePath(TestData.RepositoryRoot, path));
                }
            }

            return sources;
        }
    }

    [Theory]
    [MemberData(nameof(PortableSources))]
    public void TheDiceAndPartyCodeKnowsNothingAboutThisGame(string relativePath)
    {
        if (Path.GetFileName(relativePath) == StaysBehind)
        {
            return;
        }

        string source = File.ReadAllText(Path.Combine(TestData.RepositoryRoot, relativePath));

        foreach (string forbidden in Forbidden)
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The game-specific file has to stay small enough to be worth leaving behind.
    /// </summary>
    /// <remarks>
    /// Not a style rule. If the FLAIL! presets grow into a rules engine, the extraction stops being
    /// a move and the plan needs revisiting — this is where that conversation gets scheduled.
    /// </remarks>
    [Fact]
    public void TheGameSpecificFileIsStillTheSmallOne()
    {
        string path = Path.Combine(TestData.RepositoryRoot, "src", "FlailTools.Core", "Dice", StaysBehind);

        Assert.True(File.Exists(path), $"'{StaysBehind}' has moved; the extraction rule needs updating.");
        Assert.True(File.ReadAllLines(path).Length < 150, $"'{StaysBehind}' is growing into a rules engine.");
    }

    [Fact]
    public void ThereIsPortableCodeToCheckInTheFirstPlace()
    {
        Assert.True(PortableSources.Count > 4);
    }
}
