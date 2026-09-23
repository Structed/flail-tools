using System.Text.Json;
using System.Text.Json.Nodes;
using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Model;
using Structed.Inkwell.Data;

namespace FlailTools.Core.Tests;

/// <summary>
/// The Dungeons generator, read back against the page it came from.
/// </summary>
/// <remarks>
/// <para>
/// The book prints the five theme axes as one table of six rows across five columns, and says to
/// roll d6 on each column. Face order <em>is</em> the data: a row sitting in the wrong position is
/// not a typo anything can catch, because every result it produces is a perfectly plausible dungeon
/// — just not the one the book's dice describe. So the grid is transcribed here and checked face by
/// face, rather than the file being checked against itself.
/// </para>
/// <para>
/// The keying checklist is the other half, and the more fragile one. It holds nine concepts, not
/// six, because FLAIL! asks a reader to stock each area with one of nine rather than to roll a die.
/// It sits in a file whose other six tables are all d6 tables, which makes "nine looks wrong, trim
/// it" the obvious and wholly silent mistake: the first six concepts would still read perfectly and
/// the last three would simply stop appearing. Nothing on screen would say so, which is why
/// <see cref="EveryKeyingConceptIsReachableInAGeneratedDungeon"/> goes looking for all nine in real
/// dungeons instead of trusting the count.
/// </para>
/// <para>
/// That checklist is credited in FLAIL! itself to the Goblin Punch blog, and the credit is passed
/// on in <c>NOTICE.md</c> rather than stopping at Games Omnivorous.
/// </para>
/// </remarks>
public sealed class DungeonTableTests
{
    /// <summary>
    /// The theme table as the book prints it: one row per face, five columns across.
    /// </summary>
    /// <remarks>
    /// Written down as rows rather than as five separate columns because that is the shape of the
    /// mistake being guarded against. A column transcribed correctly but pasted one row out reads
    /// as five sound tables, and only lines up wrongly against the faces beside it. Pinned rather
    /// than derived from the file, for the reason <c>CaveTableTests</c> pins its chamber pairings:
    /// anything derived from the file would pass just as happily with two rows swapped.
    /// <para>
    /// Reproducing the entries is what the Third-Party Licence permits. The prose the book wraps
    /// around them is not, and stays in the book.
    /// </para>
    /// </remarks>
    private static readonly (int Face, string Flavour, string Type, string Location, string KeyFeature, string Creatures)[] Theme =
    [
        (1, "Enchanted", "Crypts", "Beneath a busy city", "Arcane glyphs", "Undead"),
        (2, "Festering", "Vaults", "Hidden in old temple", "Bone-coated walls", "Oozes"),
        (3, "Haunted", "Tombs", "Inside a deep well", "Toxic vapours", "Reptiles"),
        (4, "Misty", "Catacombs", "Within a black fortress", "Cobweb shroud", "Giants"),
        (5, "Broken", "Sewers", "Under a ruined castle", "Dripping ceilings", "Insects"),
        (6, "Cursed", "Cellar", "Below an ancient ruin", "Hanging chains", "Demons")
    ];

    /// <summary>The nine concepts an area is stocked with, in the order the book lists them.</summary>
    private static readonly string[] Checklist =
    [
        "Something to steal",
        "Something that probably won't be found",
        "Something the characters can kill",
        "Something that can kill the characters",
        "Someone to meet",
        "Puzzle to play with",
        "Obstacle to overcome",
        "Trap to avoid",
        "Odd anomaly"
    ];

    public static TheoryData<string, Func<GameData, IReadOnlyList<string>>> Axes => new()
    {
        { "dungeon.json/flavours", data => data.Dungeon.Flavours },
        { "dungeon.json/types", data => data.Dungeon.Types },
        { "dungeon.json/locations", data => data.Dungeon.Locations },
        { "dungeon.json/keyFeatures", data => data.Dungeon.KeyFeatures },
        { "dungeon.json/creatures", data => data.Dungeon.Creatures }
    };

    [Theory]
    [MemberData(nameof(Axes))]
    public async Task EveryDungeonAxisHasOneEntryPerFaceOfItsD6(
        string table,
        Func<GameData, IReadOnlyList<string>> axis)
    {
        IReadOnlyList<string> rows = axis(await TestData.LoadAsync());

        Assert.True(
            rows.Count == 6,
            $"'{table}' holds {rows.Count} entries. The book prints six and says to roll d6 on the " +
            "column, so it needs exactly six. Append in face order rather than reordering what is " +
            "there: a lock stores a position, not a phrase.");

        Assert.All(rows, row => Assert.False(
            string.IsNullOrWhiteSpace(row),
            $"'{table}' has a blank row, which would roll as an empty result."));
    }

    /// <summary>
    /// Every row sits on the face the book prints it on, across all five columns at once.
    /// </summary>
    /// <remarks>
    /// The assertion is per face rather than per column so that a failure names the face, which is
    /// the thing a reader has to go and look up. A table holding all the right entries in the wrong
    /// order passes every other test in this repository.
    /// </remarks>
    [Fact]
    public async Task EveryDungeonAxisSitsOnTheFaceTheBookPrintsItOn()
    {
        GameData data = await TestData.LoadAsync();

        foreach ((int face, string flavour, string type, string location, string keyFeature, string creatures) in Theme)
        {
            AssertFace(face, "flavours", flavour, data.Dungeon.Flavours);
            AssertFace(face, "types", type, data.Dungeon.Types);
            AssertFace(face, "locations", location, data.Dungeon.Locations);
            AssertFace(face, "keyFeatures", keyFeature, data.Dungeon.KeyFeatures);
            AssertFace(face, "creatures", creatures, data.Dungeon.Creatures);
        }
    }

    /// <summary>
    /// The checklist holds all nine concepts, in the book's order, none folded into another.
    /// </summary>
    /// <remarks>
    /// Order matters here for the same reason it matters on a die table, even though nothing rolls
    /// a face against it: a pin stores the position it picked, so moving a concept rewrites what
    /// every already-shared link resolves to in that room.
    /// </remarks>
    [Fact]
    public async Task TheKeyingChecklistHoldsAllNineConceptsInTheBooksOrder()
    {
        GameData data = await TestData.LoadAsync();

        Assert.Equal(Checklist, data.Dungeon.Stocking);
    }

    /// <summary>
    /// All nine concepts turn up in real dungeons, not just in the file.
    /// </summary>
    /// <remarks>
    /// This is the test that catches a d6 being put over a nine-row checklist. Such a dungeon reads
    /// correctly, generates forever, and never once shows a trap, an obstacle or an odd anomaly.
    /// Counting the rows would not notice, because the rows would all still be there.
    /// </remarks>
    [Fact]
    public async Task EveryKeyingConceptIsReachableInAGeneratedDungeon()
    {
        GameData data = await TestData.LoadAsync();
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (uint seed = 1; seed <= 200; seed++)
        {
            foreach (SiteArea room in Generate(data, seed).Areas)
            {
                seen.Add(room.Value);
            }
        }

        string[] missing = [.. Checklist.Where(concept => !seen.Contains(concept))];

        Assert.True(
            missing.Length == 0,
            $"In 200 dungeons, [{string.Join(", ", missing)}] never came up. The checklist is picked " +
            "across all nine concepts; a die rolled over it would strand whichever ones sit past its " +
            "last face, and every dungeon would still read perfectly.");
    }

    /// <summary>
    /// A dungeon runs from an entrance to somewhere it is meant to end.
    /// </summary>
    /// <remarks>
    /// The book keys ten areas, one the entrance and ten the big finale, over a map of eight to
    /// twelve rooms. A dungeon that rolled fewer than ten ends at its last room, because the point
    /// of the finale is that the place has somewhere to end, not that it is the tenth door. Read
    /// across seeds because the short dungeons are the ones where that rule is doing any work.
    /// </remarks>
    [Fact]
    public async Task EveryDungeonRunsFromAnEntranceToAFinale()
    {
        GameData data = await TestData.LoadAsync();
        bool sawShortDungeon = false;

        for (uint seed = 1; seed <= 200; seed++)
        {
            IReadOnlyList<SiteArea> rooms = Generate(data, seed).Areas;

            Assert.InRange(rooms.Count, 8, 12);
            Assert.Equal(AreaRoles.Entrance, rooms[0].Role);
            Assert.Equal(1, rooms[0].Number);

            SiteArea finale = Assert.Single(rooms, room =>
                string.Equals(room.Role, AreaRoles.Finale, StringComparison.Ordinal));

            Assert.Equal(Math.Min(10, rooms.Count), finale.Number);

            sawShortDungeon |= rooms.Count < 10;
        }

        Assert.True(
            sawShortDungeon,
            "No dungeon in 200 seeds held fewer than ten rooms, so the rule that moves the finale " +
            "to the last room of a short dungeon was never exercised.");
    }

    [Theory]
    [InlineData("flavours")]
    [InlineData("types")]
    [InlineData("locations")]
    [InlineData("keyFeatures")]
    [InlineData("creatures")]
    public async Task ADungeonAxisThatIsNotSixRowsLongIsRefusedByName(string table)
    {
        GameDataException error = await Assert.ThrowsAsync<GameDataException>(() => GameData.LoadAsync(
            new ShortTableReader(new FileSystemDataFileReader(TestData.DataRoot), DataPaths.Dungeon, table)));

        Assert.Contains(DataPaths.Dungeon, error.Message, StringComparison.Ordinal);
        Assert.Contains(table, error.Message, StringComparison.Ordinal);
        Assert.Contains("d6", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A short checklist is refused as a checklist, and the refusal names no die.
    /// </summary>
    /// <remarks>
    /// The wording is the point, so it is asserted rather than left to a reviewer. A message reading
    /// "it is read by a d9" would be untrue, and would send the next reader looking for the die that
    /// does not exist — or, worse, agreeing that nine is the wrong number and cutting it to six.
    /// </remarks>
    [Fact]
    public async Task AShortKeyingChecklistIsRefusedAsAChecklistRatherThanAsADie()
    {
        GameDataException error = await Assert.ThrowsAsync<GameDataException>(() => GameData.LoadAsync(
            new ShortTableReader(new FileSystemDataFileReader(TestData.DataRoot), DataPaths.Dungeon, "stocking")));

        Assert.Contains(DataPaths.Dungeon, error.Message, StringComparison.Ordinal);
        Assert.Contains("stocking", error.Message, StringComparison.Ordinal);
        Assert.Contains("checklist", error.Message, StringComparison.Ordinal);
        Assert.Contains("9", error.Message, StringComparison.Ordinal);

        Assert.DoesNotContain("d6", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("d9", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nine concepts are not mistaken for a six-face table that has grown three rows too long.
    /// </summary>
    [Fact]
    public async Task TheKeyingChecklistIsNotHeldToTheSixFacesOfTheColumnsBesideIt()
    {
        GameData data = await TestData.LoadAsync();

        Assert.Equal(9, data.Dungeon.Stocking.Count);

        data.Validate();
    }

    [Fact]
    public async Task AnUnwrittenDungeonTableIsStillAllowedThrough()
    {
        GameData data = await TestData.LoadEmptyAsync();

        Assert.Empty(data.Dungeon.Flavours);
        Assert.Empty(data.Dungeon.Stocking);

        data.Validate();
    }

    private static void AssertFace(int face, string table, string expected, IReadOnlyList<string> rows)
    {
        Assert.True(
            rows.Count >= face,
            $"'dungeon.json/{table}' has no face {face}, but the book prints '{expected}' there.");

        Assert.True(
            string.Equals(rows[face - 1], expected, StringComparison.Ordinal),
            $"Face {face} of 'dungeon.json/{table}' reads '{rows[face - 1]}', but the book prints " +
            $"'{expected}'. Rolls.Face indexes the row directly, so a row in the wrong position " +
            "generates a plausible dungeon that is not the one the dice describe.");
    }

    private static AdventureSite Generate(GameData data, uint seed) =>
        SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = SiteKinds.Dungeon });

    /// <summary>Serves the shipped files, with one named table a row short.</summary>
    /// <remarks>
    /// Shortens the real file rather than building a fixture, so the test still proves something
    /// once the tables are edited. <see cref="GameData"/> has a private constructor by design — the
    /// only way to hold one is to have loaded and validated it — so a bad file is the only way to
    /// reach the failure this is checking.
    /// </remarks>
    private sealed class ShortTableReader(IDataFileReader inner, string file, string table) : IDataFileReader
    {
        public async Task<Stream> OpenAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(relativePath, file, StringComparison.Ordinal))
            {
                return await inner.OpenAsync(relativePath, cancellationToken);
            }

            await using Stream stream = await inner.OpenAsync(relativePath, cancellationToken);
            JsonObject document = Assert.IsType<JsonObject>(await JsonNode.ParseAsync(
                stream,
                documentOptions: LocalisingDataFileReader.DocumentOptions,
                cancellationToken: cancellationToken));

            JsonArray rows = Assert.IsType<JsonArray>(document[table]);
            rows.RemoveAt(rows.Count - 1);

            return new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(document));
        }
    }
}
