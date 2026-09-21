using System.Text.RegularExpressions;
using FlailTools.Core.Data;

namespace FlailTools.Core.Tests;

/// <summary>
/// Every piece of wording the pages ask for by name exists in <c>ui.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// A missing key does not fail. <see cref="UiText.Message"/> falls back to the key itself, so the
/// page renders <c>diceEmptyLog</c> in the middle of the interface and carries on as if nothing
/// were wrong. That is the right behaviour at runtime — half a sentence beats a blank screen — but
/// it means a typo survives every build, every test and every code review, and is found by a player
/// instead.
/// </para>
/// <para>
/// So the join between markup and wording is checked here. Only literal keys can be checked; the
/// ones a page works out at runtime, like a preset's reading, are covered by
/// <c>DiceWordingTests</c> rolling until it has seen them all.
/// </para>
/// </remarks>
public sealed partial class UiWordingTests
{
    [GeneratedRegex("""Ui\.(Action|Message)\("([^"]+)"\)""")]
    private static partial Regex Lookup { get; }

    public static TheoryData<string> Pages
    {
        get
        {
            TheoryData<string> pages = [];
            string root = Path.Combine(TestData.RepositoryRoot, "src", "FlailTools.Web");

            foreach (string path in Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories))
            {
                pages.Add(Path.GetRelativePath(TestData.RepositoryRoot, path));
            }

            return pages;
        }
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task EveryNamedPieceOfWordingExists(string relativePath)
    {
        UiText ui = (await TestData.LoadAsync()).Ui;
        string markup = await File.ReadAllTextAsync(Path.Combine(TestData.RepositoryRoot, relativePath));

        foreach (Match match in Lookup.Matches(markup))
        {
            string kind = match.Groups[1].Value;
            string key = match.Groups[2].Value;

            bool known = kind == "Action" ? ui.Actions.ContainsKey(key) : ui.Messages.ContainsKey(key);

            Assert.True(known, $"'{key}' is asked for by {relativePath} but missing from ui.json.");
        }
    }

    [Fact]
    public void ThereArePagesToCheckInTheFirstPlace()
    {
        Assert.True(Pages.Count > 3);
    }
}
