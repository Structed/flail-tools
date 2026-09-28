using FlailTools.Core.Characters;
using FlailTools.Core.Serialization;

namespace FlailTools.Core.Tests.Serialization;

/// <summary>
/// The link is the whole sheet, and it arrives having been through a chat client, an email wrapper
/// and somebody's clipboard.
/// </summary>
public sealed class CharacterLinkTests
{
    /// <summary>
    /// A payload made by this tool, kept verbatim.
    /// </summary>
    /// <remarks>
    /// Pinned as something to <em>read</em> rather than as something to reproduce. What matters is
    /// that links already sent keep opening, and deflate is not promised to emit identical bytes
    /// across runtimes, so asserting the encoder still produces this string would fail on a .NET
    /// upgrade that broke nothing at all. If this one stops decoding, every link anybody has
    /// shared has stopped with it.
    /// </remarks>
    private const string Pinned =
        "1xZRNb9swDIbvA_ofBJ-7bkk3dOut3aXAVqzojsMOjMTEgiUxkGQ7QdH_XsqenHgzGjQo0Jv98iX5iPp4OHknRLEkbyEWl_x"
        + "lQJv3kciED7IEDzKiL047V4M-aHJsm_XCznApHpLCmlapzMLP7eJ8s72o-lwOeGz03_TzrDmwmOzXHuzC4OCVBkJIAeVrLph"
        + "lgw0aludZWICsVp5q1zW9QbVC0eooy-J_xx36Krm-O2qDaEstS2HrUHoiGwR47LWzITVEj24VS076mjWFG16ujts0hE8DVy1"
        + "T6dnAVep4R9rFtIbPWbSwudnTv-xqLtFJ3F-XZFPyzC6GiuBUUn73_yLPezTGX1pWuyn2izDUdZvti45i5x85dbiFlZaQBrw"
        + "EE3AcvDbgqhzKkcf-488wbVLbQ5A_EGLJJ2kScz6FuQbeUVSvTguKvLPYb8ezzFeSnSIddzuJ_fFF042-PgY3dGMwh1jvaf2"
        + "WRyCCeW6ile7v6qhvRv9mEJoxfOaEKLrbL-bFEVBratOJO8AU1mjMJNgtOjWJRXxxBQgF22OwJDmlI7-JIzR-jXnDyGPxjz0"
        + "17V7Fny0GwZdIWG0MegZYQkO1P-syuMvjEw";

    private static Character Pinnable() => new()
    {
        Id = "br2mb3xy7k",
        Revision = 3,
        Name = "Bramble",
        Class = CharacterClasses.Druid,
        Level = 2,
        Background = "Hedge witch",
        BackgroundPerk = "Knows which mushrooms are which.",
        Strength = 9,
        Dexterity = 14,
        Luck = 12,
        HitPoints = 5,
        MaxHitPoints = 8,
        Defence = 2,
        Coins = 17,
        Hands = [new ItemEntry { Name = "Sickle", Slots = 1 }],
        Body = [new ItemEntry { Name = "Leathers", Slots = 2, Note = "patched" }],
        Adornments = [new ItemEntry { Name = "Acorn charm", IsMagical = true }],
        Satchel = [new ItemEntry { Name = "Rope", Slots = 1 }],
        Talents = [new PowerEntry { Name = "Cleave", Note = "at level 2" }],
        Powers = [new PowerEntry { Kind = PowerKinds.Spell, Name = "Mend", Note = "once a day" }],
        Conditions = ["footsore"],
        Notes = "Owes the miller a favour."
    };

    [Fact]
    public void ALinkAlreadySharedStillOpens()
    {
        Character read = Assert.IsType<Character>(CharacterLink.Decode(Pinned));

        Assert.Equal(
            CharacterDocuments.Write(Pinnable()),
            CharacterDocuments.Write(read));
    }

    [Fact]
    public void AWholeSheetSurvivesTheRoundTrip()
    {
        Character read = Assert.IsType<Character>(CharacterLink.FromQuery(CharacterLink.ToQuery(Pinnable())));

        Assert.Equal(CharacterDocuments.Write(Pinnable()), CharacterDocuments.Write(read));
    }

    [Theory]
    [InlineData("plain words")]
    [InlineData("a tilde ~ in the middle")]
    [InlineData("an ampersand & and a question ?")]
    [InlineData("a slash / and a percent % and a plus +")]
    [InlineData("a hash # and an equals =")]
    [InlineData("nôn-âscii ünd emoji 🜃")]
    [InlineData("")]
    public void TypedWordsSurviveTheRoundTrip(string typed)
    {
        Character written = Character.Create() with
        {
            Name = typed,
            Notes = typed,
            Satchel = [new ItemEntry { Name = typed, Note = typed }]
        };

        Character read = Assert.IsType<Character>(CharacterLink.FromQuery(CharacterLink.ToQuery(written)));

        Assert.Equal(written.Name.Trim(), read.Name);
        Assert.Equal(written.Notes.Trim(), read.Notes);
    }

    [Fact]
    public void TheIdSurvivesTheRoundTripBecauseItIsTheWholePoint()
    {
        Character written = Character.Create() with { Name = "Bramble", Revision = 4 };

        Character read = Assert.IsType<Character>(CharacterLink.FromQuery(CharacterLink.ToQuery(written)));

        Assert.Equal(written.Id, read.Id);
        Assert.Equal(4, read.Revision);
    }

    [Fact]
    public void APayloadSaysWhichFormatItIs() =>
        Assert.StartsWith("1", CharacterLink.Encode(Character.Create()), StringComparison.Ordinal);

    /// <summary>
    /// The reason a second format is possible later without orphaning every link already sent. A
    /// reader that meets a version it does not know says so, rather than confidently decoding
    /// nonsense.
    /// </summary>
    [Fact]
    public void APayloadFromAFormatThisDoesNotKnowIsNotGuessedAt()
    {
        string payload = CharacterLink.Encode(Character.Create());

        Assert.Null(CharacterLink.Decode("2" + payload[1..]));
    }

    /// <summary>
    /// Every failure is the same answer on purpose. A reader cannot act on the difference between a
    /// truncated paste and a link that was never one, and saying which would only invite trying to
    /// repair it.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("not a payload at all")]
    [InlineData("1not base64url !!")]
    [InlineData("1AAAAAAAAAAAAAAAAAAA")]
    public void AnythingThatIsNotAPayloadIsNoCharacterRatherThanAnError(string? payload) =>
        Assert.Null(CharacterLink.Decode(payload));

    [Fact]
    public void ATruncatedPasteBringsNoCharacterRatherThanHalfAnOne()
    {
        string payload = CharacterLink.Encode(Pinnable());

        Assert.Null(CharacterLink.Decode(payload[..(payload.Length / 2)]));
    }

    [Fact]
    public void ALinkCarryingNoSheetBringsNoCharacter()
    {
        Assert.Null(CharacterLink.FromQuery(null));
        Assert.Null(CharacterLink.FromQuery(""));
        Assert.Null(CharacterLink.FromQuery("?seed=1234"));
        Assert.Null(CharacterLink.FromQuery("?c="));
    }

    [Fact]
    public void TheShortLinkNamesACharacterThisBrowserAlreadyHolds()
    {
        string id = CharacterId.Create();

        Assert.Equal($"?id={id}", CharacterLink.ToIdQuery(id));
        Assert.Equal(id, CharacterLink.IdFromQuery(CharacterLink.ToIdQuery(id)));
    }

    /// <summary>
    /// An id out of a link is used as a storage key and as the thing a save overwrites on, so
    /// something that could not have been issued here is refused rather than looked up.
    /// </summary>
    [Theory]
    [InlineData("?id=")]
    [InlineData("?id=NOT-AN-ID")]
    [InlineData("?id=abcdefghji")]
    [InlineData("?c=1abc")]
    [InlineData("")]
    [InlineData(null)]
    public void AnIdThatCouldNotHaveComeFromHereIsRefused(string? query)
    {
        Assert.Null(CharacterLink.IdFromQuery(query));
        Assert.Equal("", CharacterLink.ToIdQuery(query));
    }

    /// <summary>
    /// A few hundred bytes of deflate can name many megabytes of output, and this runs in the
    /// reader's own browser with no server between them and whoever sent the link.
    /// </summary>
    [Fact]
    public void APayloadThatUnpacksIntoSomethingEnormousIsRefused()
    {
        byte[] huge = new byte[2 * 1024 * 1024];

        using MemoryStream packed = new();

        using (System.IO.Compression.DeflateStream deflate = new(
            packed,
            System.IO.Compression.CompressionLevel.SmallestSize,
            leaveOpen: true))
        {
            deflate.Write(huge, 0, huge.Length);
        }

        string payload = "1" + System.Buffers.Text.Base64Url.EncodeToString(packed.ToArray());

        Assert.Null(CharacterLink.Decode(payload));
    }

    /// <summary>
    /// The link has to be paste-able into a chat window, which is the real reason
    /// <see cref="CharacterLimits"/> exists. A sheet filled to every one of those limits is the
    /// worst case anybody can actually produce.
    /// </summary>
    [Fact]
    public void EvenASheetFilledToEveryLimitFitsInALink()
    {
        Character full = Character.Create() with
        {
            Name = new string('n', CharacterLimits.NameLength),
            Class = new string('c', CharacterLimits.NameLength),
            Background = new string('b', CharacterLimits.NameLength),
            BackgroundPerk = new string('p', CharacterLimits.ProseLength),
            Notes = new string('q', CharacterLimits.ProseLength),
            Hands = Items(),
            Body = Items(),
            Adornments = Items(),
            Satchel = Items(),
            Talents = Powers(),
            Powers = Powers(),
            Conditions = [.. Enumerable.Range(0, CharacterLimits.Entries)
                .Select(at => $"condition {at} that somebody has written out at length")]
        };

        string query = CharacterLink.ToQuery(full);

        Assert.NotNull(CharacterLink.FromQuery(query));
        Assert.True(
            query.Length < 8000,
            $"A full sheet's link is {query.Length} characters. Anything past about 8000 stops "
            + "being paste-able, and the limits in CharacterLimits are what keep it under.");
    }

    private static IReadOnlyList<ItemEntry> Items() =>
    [
        .. Enumerable.Range(0, CharacterLimits.Entries).Select(at => new ItemEntry
        {
            Name = $"item {at} with a long enough name to be realistic",
            Note = new string('z', CharacterLimits.NoteLength),
            Slots = 2,
            IsMagical = true
        })
    ];

    private static IReadOnlyList<PowerEntry> Powers() =>
    [
        .. Enumerable.Range(0, CharacterLimits.Entries).Select(at => new PowerEntry
        {
            Kind = PowerKinds.Spell,
            Name = $"power {at} with a long enough name to be realistic",
            Note = new string('z', CharacterLimits.NoteLength)
        })
    ];
}
