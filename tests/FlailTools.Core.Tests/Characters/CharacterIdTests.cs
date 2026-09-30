using FlailTools.Core.Characters;

namespace FlailTools.Core.Tests.Characters;

/// <summary>
/// The id is what makes re-sharing overwrite instead of duplicate, so its shape is pinned.
/// </summary>
public sealed class CharacterIdTests
{
    /// <summary>
    /// An id is the storage key and the thing a re-shared link lands on. Widening the alphabet or
    /// changing the length is fine for characters made afterwards and orphans every one already
    /// saved, because the ids they carry stop passing <see cref="CharacterId.IsValid"/> and are
    /// replaced with fresh ones on the way in.
    /// </summary>
    [Fact]
    public void AnIdIsBuiltExactlyLikeThis()
    {
        Assert.Equal("23456789abcdefghjkmnpqrstuvwxyz", CharacterId.Alphabet);
        Assert.Equal(10, CharacterId.Length);
    }

    [Fact]
    public void TheAlphabetAvoidsTheLettersPeopleMisreadAloud()
    {
        Assert.DoesNotContain('i', CharacterId.Alphabet);
        Assert.DoesNotContain('l', CharacterId.Alphabet);
        Assert.DoesNotContain('o', CharacterId.Alphabet);
        Assert.DoesNotContain('0', CharacterId.Alphabet);
        Assert.DoesNotContain('1', CharacterId.Alphabet);
        Assert.Equal(CharacterId.Alphabet, CharacterId.Alphabet.ToLowerInvariant());
        Assert.Equal(CharacterId.Alphabet.Distinct().Count(), CharacterId.Alphabet.Length);
    }

    [Fact]
    public void AFreshIdIsValidAndNotTheLastOne()
    {
        HashSet<string> minted = [.. Enumerable.Range(0, 500).Select(_ => CharacterId.Create())];

        Assert.Equal(500, minted.Count);
        Assert.All(minted, id => Assert.True(CharacterId.IsValid(id)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abcdefghj")]
    [InlineData("abcdefghjkm2")]
    [InlineData("abcdefghji")]
    [InlineData("ABCDEFGHJK")]
    [InlineData("abcdefgh-k")]
    [InlineData("abcdefgh k")]
    public void AnythingElseIsNotAnId(string? id) => Assert.False(CharacterId.IsValid(id));
}
