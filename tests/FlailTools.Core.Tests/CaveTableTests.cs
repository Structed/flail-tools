using System.Text.Json;
using System.Text.Json.Nodes;
using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Model;
using Structed.Inkwell.Data;

namespace FlailTools.Core.Tests;

/// <summary>
/// The Caves generator, read back against the page it came from.
/// </summary>
/// <remarks>
/// <para>
/// A cave is the one generator where the dice are the procedure: a handful of d6s is dropped on a
/// page, where each die <em>lands</em> assigns its role and what each die <em>shows</em> reads the
/// chambers table. The two are independent, and a cave has five to nine chambers, so those six rows
/// are read over and over inside a single result. A row sitting on the wrong face is therefore not
/// a typo that shows up once — it is a cave that is internally consistent and wrong throughout.
/// </para>
/// <para>
/// Nothing in the file's shape catches that, so the book's pairings are written down here.
/// </para>
/// </remarks>
public sealed class CaveTableTests
{
    /// <summary>
    /// The two chamber faces the book rolls twice for, with the d4 each carries.
    /// </summary>
    /// <remarks>
    /// The book prints these as <c>1 Treasure: (1) buried chest, (2) weapons cache, (3) spellbook
    /// (4) Unique Item.</c> — one row of the d6 table, holding a d4 table of its own. Flattening
    /// that into a single phrase listing all four reads plausibly and is a different procedure: it
    /// hands a reader four things at once where the book hands them one. The pairing is pinned here
    /// rather than derived from the file, because deriving it from the file would pass just as
    /// happily with the two rows swapped.
    /// </remarks>
    private static readonly (int Face, string Chamber, string[] Details)[] FacesRolledTwice =
    [
        (1, "Treasure", ["buried chest", "weapons cache", "spellbook", "Unique Item"]),
        (2, "Vestiges", ["bone altar", "makeshift shelter", "burial mound", "warding totem"])
    ];

    /// <summary>The four faces the book resolves in one roll, by the word each opens with.</summary>
    private static readonly (int Face, string Opening)[] FacesRolledOnce =
    [
        (3, "Obstacle"),
        (4, "Weak encounter"),
        (5, "Average encounter"),
        (6, "Strong encounter")
    ];

    public static TheoryData<string, Func<GameData, IReadOnlyList<string>>> Axes => new()
    {
        { "cave.json/flavours", data => data.Cave.Flavours },
        { "cave.json/types", data => data.Cave.Types },
        { "cave.json/locations", data => data.Cave.Locations },
        { "cave.json/keyFeatures", data => data.Cave.KeyFeatures },
        { "cave.json/creatures", data => data.Cave.Creatures },
        { "cave.json/chambers", data => data.Cave.Chambers }
    };

    [Theory]
    [MemberData(nameof(Axes))]
    public async Task EveryCaveTableHasOneEntryPerFaceOfItsD6(
        string table,
        Func<GameData, IReadOnlyList<string>> rows)
    {
        IReadOnlyList<string> entries = rows(await TestData.LoadAsync());

        Assert.True(
            entries.Count == 6,
            $"'{table}' holds {entries.Count} entries. The book prints six and it is read by a d6, so " +
            "it needs exactly six. Append in face order rather than reordering what is there: a lock " +
            "stores a position, not a phrase.");

        Assert.All(entries, entry => Assert.False(
            string.IsNullOrWhiteSpace(entry),
            $"'{table}' has a blank row, which would roll as an empty result."));
    }

    /// <summary>
    /// Faces 1 and 2 keep the second roll the book gives them, on the face that owns it.
    /// </summary>
    [Fact]
    public async Task TheTwoChambersTheBookRollsTwiceForStillCarryTheirOwnFourOptions()
    {
        GameData data = await TestData.LoadAsync();

        Assert.Equal(6, data.Cave.ChamberDetails.Count);

        foreach ((int face, string chamber, string[] details) in FacesRolledTwice)
        {
            Assert.Equal(chamber, data.Cave.Chambers[face - 1]);
            Assert.Equal(details, data.Cave.ChamberDetails[face - 1]);
        }
    }

    /// <summary>
    /// The other four faces are one roll in the book, and stay that way.
    /// </summary>
    /// <remarks>
    /// Their empty rows are the absence of a die rather than a table nobody has written yet, which
    /// is the one thing about this file that looks unfinished and is not. A tidying pass that
    /// dropped them would leave two rows, unpair the two faces that do roll twice, and make a
    /// chamber of vestiges hold buried treasure.
    /// </remarks>
    [Fact]
    public async Task TheFourChambersTheBookResolvesInOneRollAreGivenNoSecondDie()
    {
        GameData data = await TestData.LoadAsync();

        foreach ((int face, string opening) in FacesRolledOnce)
        {
            Assert.StartsWith($"{opening}:", data.Cave.Chambers[face - 1], StringComparison.Ordinal);

            Assert.Empty(data.Cave.ChamberDetails[face - 1]);
        }
    }

    /// <summary>
    /// A generated chamber only ever reads as a pairing the book actually prints.
    /// </summary>
    /// <remarks>
    /// The set of legal results is built from the data rather than listed, so this survives the
    /// tables being extended; what it refuses is a detail crossing to a chamber it does not belong
    /// to. "Vestiges: buried chest" is a perfectly readable cave and is not in the book, and no
    /// other test in this repository would notice it.
    /// </remarks>
    [Fact]
    public async Task NoChamberEverBorrowsAnotherChambersDetail()
    {
        GameData data = await TestData.LoadAsync();
        HashSet<string> legal = new(StringComparer.Ordinal);

        for (int face = 0; face < data.Cave.Chambers.Count; face++)
        {
            IReadOnlyList<string> details = data.Cave.ChamberDetails[face];

            if (details.Count == 0)
            {
                legal.Add(data.Cave.Chambers[face]);
                continue;
            }

            foreach (string detail in details)
            {
                legal.Add($"{data.Cave.Chambers[face]}: {detail}");
            }
        }

        for (uint seed = 1; seed <= 120; seed++)
        {
            AdventureSite site = Generate(data, SiteKinds.Cave, seed);

            Assert.All(site.Areas, chamber => Assert.Contains(chamber.Value, legal, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// The second die is really rolled, rather than one option standing in for four.
    /// </summary>
    /// <remarks>
    /// Checked as a spread across seeds because a bug that always returned the first option would
    /// leave every assertion above passing: every chamber would still be a pairing the book prints,
    /// just always the same one.
    /// </remarks>
    [Fact]
    public async Task EveryOptionOnASecondDieIsReachable()
    {
        GameData data = await TestData.LoadAsync();
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (uint seed = 1; seed <= 400; seed++)
        {
            foreach (SiteArea chamber in Generate(data, SiteKinds.Cave, seed).Areas)
            {
                seen.Add(chamber.Value);
            }
        }

        foreach ((int face, string chamber, string[] details) in FacesRolledTwice)
        {
            foreach (string detail in details)
            {
                Assert.Contains($"{chamber}: {detail}", seen, StringComparer.Ordinal);
            }

            Assert.DoesNotContain(data.Cave.Chambers[face - 1], seen, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Two chambers showing the same number are still two dice, and roll their own second die.
    /// </summary>
    /// <remarks>
    /// A cave reads its six rows five to nine times over, so the cheap mistake is to key the second
    /// roll off the face rather than off the chamber — at which point every treasure in a cave is
    /// the same treasure. That is a readable cave, it is stable across runs, and every other
    /// assertion here passes. The clustering rule makes it worse: dice sharing a number are already
    /// meant to form one area, so repeated treasure looks like the procedure working.
    /// </remarks>
    [Fact]
    public async Task TwoChambersOnTheSameFaceStillRollTheirOwnSecondDie()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 400; seed++)
        {
            IEnumerable<IGrouping<string, string>> byChamber = Generate(data, SiteKinds.Cave, seed)
                .Areas
                .Select(area => area.Value)
                .Where(value => value.Contains(": ", StringComparison.Ordinal))
                .GroupBy(
                    value => value.Split(": ", 2, StringSplitOptions.None)[0],
                    value => value.Split(": ", 2, StringSplitOptions.None)[1],
                    StringComparer.Ordinal);

            foreach (IGrouping<string, string> chamber in byChamber)
            {
                if (FacesRolledTwice.Any(row => string.Equals(row.Chamber, chamber.Key, StringComparison.Ordinal)) &&
                    chamber.Distinct(StringComparer.Ordinal).Count() > 1)
                {
                    return;
                }
            }
        }

        Assert.Fail(
            "In 400 caves, two chambers showing the same number never held different contents. The " +
            "second roll is keyed to the face rather than to the chamber, so every treasure in a " +
            "cave is the same treasure.");
    }

    /// <summary>
    /// Locking what is in a chamber does not also decide which one it is.
    /// </summary>
    /// <remarks>
    /// The two rolls are on separate paths for the same reason a tower's floor and its detail are:
    /// a reader who liked a treasure chamber should be able to keep it and still ask for a different
    /// treasure. Sharing one path would make the pair inseparable and would silently orphan the
    /// detail the moment anybody pinned the chamber.
    /// </remarks>
    [Fact]
    public async Task ChamberAndDetailAreRolledAndLockedSeparately()
    {
        GameData data = await TestData.LoadAsync();

        Assert.NotEqual(FieldPaths.CaveChamber(0), FieldPaths.CaveChamberDetail(0));

        for (uint seed = 1; seed <= 120; seed++)
        {
            SitePlan plan = new() { Seed = seed, Kind = SiteKinds.Cave };
            AdventureSite site = Generate(data, SiteKinds.Cave, seed);

            for (int index = 0; index < site.Areas.Count; index++)
            {
                string value = site.Areas[index].Value;

                if (!FacesRolledTwice.Any(row => value.StartsWith($"{row.Chamber}: ", StringComparison.Ordinal)))
                {
                    continue;
                }

                string chamber = value.Split(": ", 2, StringSplitOptions.None)[0];

                AdventureSite rerolled = SiteGenerator.Generate(
                    data,
                    plan.WithPin(FieldPaths.CaveChamber(index), site.PinValues[FieldPaths.CaveChamber(index)])
                        .WithReroll(FieldPaths.CaveChamberDetail(index)));

                Assert.StartsWith($"{chamber}: ", rerolled.Areas[index].Value, StringComparison.Ordinal);

                return;
            }
        }

        Assert.Fail("No cave in 120 seeds ever rolled a chamber with a second die.");
    }

    /// <summary>
    /// A cave and a dungeon at the same seed must not read as the same place.
    /// </summary>
    /// <remarks>
    /// The book gives the two generators the same five axis names over entirely different tables,
    /// which is exactly the shape that invites somebody to merge them. They overlap only where the
    /// book overlaps: "Haunted" appears as a Flavour in both, and Reptiles, Giants and Insects all
    /// appear as Creatures in both. Anything past those two axes at one seed means rows have been
    /// copied across the files.
    /// </remarks>
    [Fact]
    public async Task ACaveAndADungeonDoNotReadAsTheSamePlace()
    {
        GameData data = await TestData.LoadAsync();

        Assert.Empty(data.Cave.Chambers.Intersect(data.Dungeon.Stocking, StringComparer.OrdinalIgnoreCase));

        for (uint seed = 1; seed <= 60; seed++)
        {
            AdventureSite cave = Generate(data, SiteKinds.Cave, seed);
            AdventureSite dungeon = Generate(data, SiteKinds.Dungeon, seed);

            Assert.All(cave.Fields, field =>
                Assert.StartsWith(FieldPaths.CavePrefix, field.Path, StringComparison.Ordinal));
            Assert.All(dungeon.Fields, field =>
                Assert.StartsWith(FieldPaths.DungeonPrefix, field.Path, StringComparison.Ordinal));

            string[] shared =
            [
                .. cave.Fields.Select(field => field.Value)
                    .Intersect(dungeon.Fields.Select(field => field.Value), StringComparer.Ordinal)
            ];

            Assert.True(
                shared.Length <= 2,
                $"Seed {seed} gives a cave and a dungeon sharing {string.Join(", ", shared)}. Only " +
                "Flavour and Creatures have any row in common between the two tables in the book, so " +
                "at least three of the five axes should read differently.");
        }
    }

    [Theory]
    [InlineData("chambers", 6)]
    [InlineData("chamberDetails", 6)]
    public async Task ACaveTableThatIsNotSixRowsLongIsRefusedByName(string table, int faces)
    {
        GameDataException error = await Assert.ThrowsAsync<GameDataException>(() => GameData.LoadAsync(
            new ShortTableReader(new FileSystemDataFileReader(TestData.DataRoot), DataPaths.Cave, table)));

        Assert.Contains(DataPaths.Cave, error.Message, StringComparison.Ordinal);
        Assert.Contains(table, error.Message, StringComparison.Ordinal);
        Assert.Contains($"d{faces}", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A second die with three options is refused, naming the face it belongs to.
    /// </summary>
    /// <remarks>
    /// Worth its own case because the outer count would still be six: the file would look right and
    /// the missing option would simply never come up.
    /// </remarks>
    [Fact]
    public async Task AChamberDetailRowThatIsNotFourOptionsLongIsRefusedByFace()
    {
        GameDataException error = await Assert.ThrowsAsync<GameDataException>(() => GameData.LoadAsync(
            new ShortDetailRowReader(new FileSystemDataFileReader(TestData.DataRoot), face: 0)));

        Assert.Contains(DataPaths.Cave, error.Message, StringComparison.Ordinal);
        Assert.Contains("chamberDetails[0]", error.Message, StringComparison.Ordinal);
        Assert.Contains("d4", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnwrittenCaveTableIsStillAllowedThrough()
    {
        GameData data = await TestData.LoadEmptyAsync();

        Assert.Empty(data.Cave.Chambers);
        Assert.Empty(data.Cave.ChamberDetails);

        data.Validate();
    }

    private static AdventureSite Generate(GameData data, string kind, uint seed) =>
        SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = kind });

    /// <summary>Serves the shipped files, with one named table a row short.</summary>
    private sealed class ShortTableReader(IDataFileReader inner, string file, string table) : IDataFileReader
    {
        public async Task<Stream> OpenAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(relativePath, file, StringComparison.Ordinal))
            {
                return await inner.OpenAsync(relativePath, cancellationToken);
            }

            JsonObject document = await ReadAsync(inner, relativePath, cancellationToken);
            JsonArray rows = Assert.IsType<JsonArray>(document[table]);
            rows.RemoveAt(rows.Count - 1);

            return new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(document));
        }
    }

    /// <summary>Serves the shipped files, with one chamber's second die one option short.</summary>
    private sealed class ShortDetailRowReader(IDataFileReader inner, int face) : IDataFileReader
    {
        public async Task<Stream> OpenAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(relativePath, DataPaths.Cave, StringComparison.Ordinal))
            {
                return await inner.OpenAsync(relativePath, cancellationToken);
            }

            JsonObject document = await ReadAsync(inner, relativePath, cancellationToken);
            JsonArray details = Assert.IsType<JsonArray>(document["chamberDetails"]);
            JsonArray options = Assert.IsType<JsonArray>(details[face]);
            options.RemoveAt(options.Count - 1);

            return new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(document));
        }
    }

    private static async Task<JsonObject> ReadAsync(
        IDataFileReader inner,
        string relativePath,
        CancellationToken cancellationToken)
    {
        await using Stream stream = await inner.OpenAsync(relativePath, cancellationToken);

        return Assert.IsType<JsonObject>(await JsonNode.ParseAsync(
            stream,
            documentOptions: LocalisingDataFileReader.DocumentOptions,
            cancellationToken: cancellationToken));
    }
}
