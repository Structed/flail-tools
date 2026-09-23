using System.Text.Json;
using System.Text.Json.Nodes;
using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Model;
using Structed.Inkwell.Data;

namespace FlailTools.Core.Tests;

/// <summary>
/// The two hexcrawl generators, held to the shape the book gives them.
/// </summary>
/// <remarks>
/// <para>
/// These ten tables are the largest slice of reproduced data in the project — two hundred rows, each
/// pinned to a d20 face — and the least self-checking. They are read by <c>RollContext.Text</c>,
/// which picks uniformly over whatever length it is handed, so a table of nineteen rows would
/// generate happily forever: no exception, no blank, nothing on screen. It would only reweight the
/// generator away from the page. <see cref="GameData.Validate"/> now refuses that, and these tests
/// are what keep the refusal honest.
/// </para>
/// <para>
/// Both kinds produce no numbered areas — no rooms, chambers or floors — so the axis text is the
/// entire result, and a wrong row is the entire result being wrong.
/// </para>
/// </remarks>
public sealed class HexcrawlTableTests
{
    /// <summary>The one Occupant row the book prints in both tables, on face 20 of each.</summary>
    private const string CatchAllOccupant = "Random monster";

    /// <summary>
    /// The biome spans the book prints, as face ranges rather than as a set of names.
    /// </summary>
    /// <remarks>
    /// The repeats are the point. The book spans each biome across two or four faces so that a d20
    /// leans towards the commoner ones, which means the Biome column is the one place in this
    /// project where duplicate rows look like a mistake and are not. A tidying pass that
    /// de-duplicated it would leave eight rows, pass every other test, and quietly flatten the
    /// weighting to uniform. Pinned as spans so that such a pass fails on the shape, not on a count.
    /// </remarks>
    private static readonly (string Biome, int Faces)[] BiomeSpans =
    [
        ("Desert", 2),
        ("Steppes", 2),
        ("Mountains", 2),
        ("Pastoral", 4),
        ("Forest", 4),
        ("Swamp", 2),
        ("Jungle", 2),
        ("Tundra", 2)
    ];

    public static TheoryData<string, Func<GameData, IReadOnlyList<string>>> Axes => new()
    {
        { "location.json/locations", data => data.Location.Locations },
        { "location.json/biomes", data => data.Location.Biomes },
        { "location.json/conditions", data => data.Location.Conditions },
        { "location.json/keyFeatures", data => data.Location.KeyFeatures },
        { "location.json/occupants", data => data.Location.Occupants },
        { "landmark.json/landmarks", data => data.Landmark.Landmarks },
        { "landmark.json/biomes", data => data.Landmark.Biomes },
        { "landmark.json/conditions", data => data.Landmark.Conditions },
        { "landmark.json/keyFeatures", data => data.Landmark.KeyFeatures },
        { "landmark.json/occupants", data => data.Landmark.Occupants }
    };

    [Theory]
    [MemberData(nameof(Axes))]
    public async Task EveryHexcrawlAxisHasOneEntryPerFaceOfItsDie(
        string table,
        Func<GameData, IReadOnlyList<string>> axis)
    {
        IReadOnlyList<string> rows = axis(await TestData.LoadAsync());

        Assert.True(
            rows.Count == 20,
            $"'{table}' holds {rows.Count} entries. It is rolled with a d20 and the book prints twenty " +
            "rows, so it needs exactly twenty. Append the missing rows in face order rather than " +
            "reordering what is there: a lock stores a position, not a phrase.");

        Assert.All(rows, row => Assert.False(
            string.IsNullOrWhiteSpace(row),
            $"'{table}' has a blank row, which would roll as an empty result."));
    }

    [Theory]
    [InlineData(DataPaths.Location)]
    [InlineData(DataPaths.Landmark)]
    public async Task TheBiomeColumnsKeepTheBookWeightingRatherThanEightDistinctNames(string file)
    {
        GameData data = await TestData.LoadAsync();

        IReadOnlyList<string> biomes = string.Equals(file, DataPaths.Location, StringComparison.Ordinal)
            ? data.Location.Biomes
            : data.Landmark.Biomes;

        int face = 0;

        foreach ((string biome, int faces) in BiomeSpans)
        {
            for (int step = 0; step < faces; step++, face++)
            {
                Assert.True(
                    face < biomes.Count && string.Equals(biomes[face], biome, StringComparison.Ordinal),
                    $"'{file}' should roll '{biome}' on face {face + 1}. The biome spans are the book's " +
                    "weighting, not accidental repetition.");
            }
        }

        Assert.Equal(20, face);
    }

    /// <summary>
    /// What the two tables really share, which is less than it looks and more than nothing.
    /// </summary>
    /// <remarks>
    /// Confirmed against the rulebook, column by column: Condition and Key Feature have no row in
    /// common; Occupant has exactly one, "Random monster", which the book prints on face 20 of both
    /// as a catch-all; and Biome is identical throughout. Identical content is exactly the case where
    /// merging the two field paths looks like tidying. It is not — a lock stores a table position, so
    /// a shared path would let a Biome locked on a Landmark resolve against the Locations rows on
    /// switching kind, choosing a value nobody picked with nothing on screen to say so. This test
    /// states the finding so the next reader need not rediscover it with the book open.
    /// </remarks>
    [Fact]
    public async Task TheTwoGeneratorsAgreeOnBiomeAndAlmostNothingElse()
    {
        GameData data = await TestData.LoadAsync();

        Assert.Equal(data.Location.Biomes, data.Landmark.Biomes);
        Assert.NotEqual(FieldPaths.LocationBiome, FieldPaths.LandmarkBiome);

        AssertSharedRows("Condition", data.Location.Conditions, data.Landmark.Conditions, []);
        AssertSharedRows("Key Feature", data.Location.KeyFeatures, data.Landmark.KeyFeatures, []);
        AssertSharedRows("Occupant", data.Location.Occupants, data.Landmark.Occupants, [CatchAllOccupant]);

        Assert.Equal(CatchAllOccupant, data.Location.Occupants[19]);
        Assert.Equal(CatchAllOccupant, data.Landmark.Occupants[19]);
    }

    /// <summary>
    /// A landmark is seen from the next hex; a location is walked into. They must not read alike.
    /// </summary>
    [Fact]
    public async Task ALocationAndALandmarkDoNotReadAsTheSameGenerator()
    {
        GameData data = await TestData.LoadAsync();

        for (uint seed = 1; seed <= 40; seed++)
        {
            AdventureSite location = Generate(data, SiteKinds.Location, seed);
            AdventureSite landmark = Generate(data, SiteKinds.Landmark, seed);

            Assert.Empty(location.Areas);
            Assert.Empty(landmark.Areas);

            Assert.All(location.Fields, field =>
                Assert.StartsWith(FieldPaths.LocationPrefix, field.Path, StringComparison.Ordinal));
            Assert.All(landmark.Fields, field =>
                Assert.StartsWith(FieldPaths.LandmarkPrefix, field.Path, StringComparison.Ordinal));

            // The book lets the two coincide on Biome, and on face 20 of Occupant. Anything past that
            // at the same seed means rows have been copied across the files.
            string[] shared = [.. Values(location).Intersect(Values(landmark), StringComparer.Ordinal)];

            Assert.True(
                shared.Length <= 2,
                $"Seed {seed} gives a Location and a Landmark sharing {string.Join(", ", shared)}. " +
                $"Only Biome and '{CatchAllOccupant}' are shared between the two tables in the book, " +
                "so at least three of the five axes should read differently.");
        }
    }

    [Theory]
    [InlineData(DataPaths.Location, "conditions")]
    [InlineData(DataPaths.Landmark, "conditions")]
    public async Task AHexcrawlTableThatIsNotTwentyRowsLongIsRefusedByName(string file, string table)
    {
        GameDataException error = await Assert.ThrowsAsync<GameDataException>(() => GameData.LoadAsync(
            new ShortTableReader(new FileSystemDataFileReader(TestData.DataRoot), file, table)));

        Assert.Contains(file, error.Message, StringComparison.Ordinal);
        Assert.Contains(table, error.Message, StringComparison.Ordinal);
        Assert.Contains("d20", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnwrittenHexcrawlTableIsStillAllowedThrough()
    {
        GameData data = await TestData.LoadEmptyAsync();

        Assert.Empty(data.Location.Locations);
        Assert.Empty(data.Landmark.Landmarks);

        data.Validate();
    }

    private static IEnumerable<string> Values(AdventureSite site) => site.Fields.Select(field => field.Value);

    private static AdventureSite Generate(GameData data, string kind, uint seed) =>
        SiteGenerator.Generate(data, new SitePlan { Seed = seed, Kind = kind });

    private static void AssertSharedRows(
        string axis,
        IReadOnlyList<string> locations,
        IReadOnlyList<string> landmarks,
        string[] expected)
    {
        string[] shared =
        [
            .. locations
                .Intersect(landmarks, StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.Ordinal)
        ];

        Assert.True(
            shared.SequenceEqual(expected, StringComparer.Ordinal),
            $"The {axis} columns share [{string.Join(", ", shared)}], but the book gives them " +
            $"[{string.Join(", ", expected)}] in common. An unexpected overlap means a row was copied " +
            "from one file into the other; a missing one means a row was lost.");
    }

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
