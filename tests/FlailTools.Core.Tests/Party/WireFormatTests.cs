using FlailTools.Core.Party;

namespace FlailTools.Core.Tests.Party;

/// <summary>
/// The exact bytes two browsers say to each other.
/// </summary>
/// <remarks>
/// <para>
/// Everything else about the table is checked by behaviour: a message round-trips, a ghost carries
/// nothing, a malformed message is refused. None of that notices if the spelling on the wire
/// changes, because both ends of a round trip change together. The people this would hurt are not
/// in the test run at all — they are the player who has not reloaded the page, and the player on
/// yesterday's deployment, sitting at what they both believe is the same table.
/// </para>
/// <para>
/// So the format is pinned as literal text, in both directions: what this build writes, and what
/// this build makes of something an earlier one wrote. A serialiser option, a reordered property, a
/// renamed field or a bumped dependency that changes any of it fails here, loudly, rather than as
/// one player's rolls silently never arriving.
/// </para>
/// <para>
/// Built from fixed numbers rather than from a real roll on purpose. What the dice do with a seed is
/// pinned in <c>dice-baseline.txt</c>; this is about the envelope, and a pin that failed for two
/// unrelated reasons would tell the reader neither.
/// </para>
/// </remarks>
public sealed class WireFormatTests
{
    private const long Noon = 1_700_000_000_000;

    // One line, because that is what it is on the wire. Wrapping it would make the pin a pin on
    // something nobody sends. The `+` in the notation arrives as `\u002B` because that is what the
    // default JSON encoder does with it — which is exactly the sort of detail a reader at the far
    // end has to cope with, and therefore the sort worth writing down.
    private const string RollJson =
        """
        {"version":1,"id":"roll-1","player":"Ada","notation":"4d6kh3\u002B1","faces":[5,6,2,1],"kept":[true,true,true,false],"total":14,"seed":4242,"readingKey":"hit/minor","readingValue":1,"preset":"hit","at":1700000000000}
        """;

    private const string HailJson = """{"version":1,"player":"Ada","at":1700000000000}""";

    private static RollMessage Roll() => new()
    {
        Id = "roll-1",
        Player = "Ada",
        Notation = "4d6kh3+1",
        Faces = [5, 6, 2, 1],
        Kept = [true, true, true, false],
        Total = 14,
        Seed = 4242,
        ReadingKey = "hit/minor",
        ReadingValue = 1,
        Preset = "hit",
        At = Noon
    };

    [Fact]
    public void ARollIsWrittenExactlyLikeThis()
    {
        Assert.Equal(RollJson, Roll().Write());
    }

    [Fact]
    public void AHailIsWrittenExactlyLikeThis()
    {
        Assert.Equal(HailJson, Hail.From("Ada", DateTimeOffset.FromUnixTimeMilliseconds(Noon)).Write());
    }

    /// <summary>A message from a build that is not this one still reads.</summary>
    /// <remarks>
    /// The other direction of the same promise. Written out as text rather than round-tripped,
    /// because a round trip proves only that this build agrees with itself.
    /// </remarks>
    [Fact]
    public void ARollWrittenByAnotherBuildStillReads()
    {
        Assert.True(RollMessage.TryRead(RollJson, out RollMessage read));

        Assert.Equal("roll-1", read.Id);
        Assert.Equal("Ada", read.Player);
        Assert.Equal("4d6kh3+1", read.Notation);
        Assert.Equal([5, 6, 2, 1], read.Faces);
        Assert.Equal([true, true, true, false], read.Kept);
        Assert.Equal(14, read.Total);
        Assert.Equal(4242u, read.Seed);
        Assert.Equal("hit/minor", read.ReadingKey);
        Assert.Equal(1, read.ReadingValue);
        Assert.Equal("hit", read.Preset);
        Assert.Equal(Noon, read.At);
        Assert.False(read.Secret);
    }

    [Fact]
    public void AHailWrittenByAnotherBuildStillReads()
    {
        Assert.True(Hail.TryRead(HailJson, out Hail read));

        Assert.Equal("Ada", read.Player);
        Assert.Equal(Noon, read.At);
    }

    /// <summary>
    /// The version numbers are the handshake, and there is only ever one of them.
    /// </summary>
    /// <remarks>
    /// Both readers refuse anything that is not their own version outright, so raising either of
    /// these numbers does not upgrade a table — it splits one in half, with each side silently
    /// dropping the other's rolls. Worth doing one day, and worth having to mean it.
    /// </remarks>
    [Fact]
    public void TheProtocolVersionsHaveNotMoved()
    {
        Assert.Equal(1, RollMessage.CurrentVersion);
        Assert.Equal(1, Hail.CurrentVersion);
    }

    /// <summary>
    /// The code is how two people find the same table, so its alphabet is part of the protocol.
    /// </summary>
    /// <remarks>
    /// A code is read aloud down a phone line, typed in by hand, and pasted from a link written
    /// weeks ago. Change the alphabet or the length and every code already written down stops
    /// finding the table it was written down for.
    /// </remarks>
    [Fact]
    public void TheTableCodeIsStillSpelledTheSameWay()
    {
        Assert.Equal("0123456789abcdefghjkmnpqrstvwxyz", TableCode.Alphabet);
        Assert.Equal(12, TableCode.Length);

        Assert.True(TableCode.TryParse("K8MP-2QR7-VXZ3", out string read));
        Assert.Equal("k8mp2qr7vxz3", read);
        Assert.Equal("k8mp-2qr7-vxz3", TableCode.Group(read));
    }
}
