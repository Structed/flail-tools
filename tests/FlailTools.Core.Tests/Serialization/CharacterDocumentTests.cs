using FlailTools.Core.Characters;
using FlailTools.Core.Serialization;
using Structed.Inkwell.Serialization;

namespace FlailTools.Core.Tests.Serialization;

/// <summary>
/// A saved character is a file a reader keeps, so it has to say what it is and be refused when it
/// is something else.
/// </summary>
public sealed class CharacterDocumentTests
{
    [Fact]
    public void AWholeSheetSurvivesBeingWrittenDownAndReadBack()
    {
        Character written = Character.Create() with
        {
            Name = "Bramble",
            Revision = 3,
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

        Character read = CharacterDocuments.Read(CharacterDocuments.Write(written));

        Assert.Equal(CharacterDocuments.Write(written), CharacterDocuments.Write(read));
        Assert.Equal(written.Id, read.Id);
        Assert.Equal(3, read.Revision);
        Assert.Equal("Rope", Assert.Single(read.Satchel).Name);
        Assert.True(Assert.Single(read.Adornments).IsMagical);
        Assert.Equal(PowerKinds.Spell, Assert.Single(read.Powers).Kind);
        Assert.Equal("footsore", Assert.Single(read.Conditions));
    }

    /// <summary>
    /// A site file, as far as telling the two apart is concerned: what it says it is.
    /// </summary>
    /// <remarks>
    /// Written out rather than generated, because what is being tested is the envelope and not the
    /// site. Generating one would drag a whole data load into a test about a header.
    /// </remarks>
    private static readonly string SiteFile =
        $"{{\"format\":\"{SiteDocuments.FormatId}\",\"version\":{SiteDocuments.CurrentVersion}}}";

    [Fact]
    public void ASheetKnowsItselfFromASite()
    {
        string character = CharacterDocuments.Write(Character.Create());

        Assert.True(CharacterDocuments.Matches(character));
        Assert.False(CharacterDocuments.Matches(SiteFile));
        Assert.False(SiteDocuments.Matches(character));
    }

    /// <summary>
    /// Both kinds of file end in <c>.flail.json</c>, so a reader will drop the wrong one in sooner
    /// or later. Saying so is better than reading a site as a character with every field empty.
    /// </summary>
    [Fact]
    public void ASiteDroppedInAsACharacterIsRefused() =>
        Assert.Throws<DocumentFormatException>(() => CharacterDocuments.Read(SiteFile));

    [Fact]
    public void AFileFromAVersionThatDoesNotExistYetIsRefused()
    {
        string tooNew = CharacterDocuments.Write(Character.Create())
            .Replace(
                $"\"version\": {CharacterDocuments.CurrentVersion}",
                $"\"version\": {CharacterDocuments.CurrentVersion + 1}",
                StringComparison.Ordinal);

        Assert.Throws<DocumentFormatException>(() => CharacterDocuments.Read(tooNew));
    }

    [Fact]
    public void AFileIsNamedAfterWhoIsOnIt()
    {
        Assert.Equal(
            "bramble" + CharacterDocuments.FileExtension,
            CharacterDocuments.FileName(Character.Create() with { Name = "Bramble" }));

        Assert.Equal(
            "character" + CharacterDocuments.FileExtension,
            CharacterDocuments.FileName(Character.Create()));
    }

    /// <summary>
    /// This is what decides whether pressing Save counts as a change. A record's own equality is no
    /// use — the lists inside a character compare by reference, so two identical sheets read out of
    /// two places are never equal — and being wrong here is not cosmetic: a sheet that stopped
    /// bumping its revision would be shared as though the other player already had it.
    /// </summary>
    [Fact]
    public void TwoSheetsSayingTheSameThingAreTheSameWhateverTheirRevisions()
    {
        Character left = Character.Create() with
        {
            Name = "Bramble",
            Revision = 1,
            Satchel = [new ItemEntry { Name = "Rope" }]
        };

        Character right = left with { Revision = 12, Satchel = [new ItemEntry { Name = "Rope" }] };

        Assert.NotEqual(left, right);
        Assert.True(CharacterDocuments.SameContent(left, right));
        Assert.False(CharacterDocuments.SameContent(left, right with { Coins = 1 }));
        Assert.False(CharacterDocuments.SameContent(
            left,
            right with { Satchel = [new ItemEntry { Name = "Rope", Slots = 1 }] }));
    }

    [Fact]
    public void AWholePartySurvivesBeingWrittenDownAndReadBack()
    {
        CharacterRoster written = CharacterRoster.Empty
            .Save(Character.Create() with { Name = "Bramble", Revision = 2 })
            .Save(Character.Create() with { Name = "Ash", Coins = 30 });

        CharacterRoster read = PartyDocuments.Read(PartyDocuments.Write(written));

        Assert.Equal(2, read.Count);
        Assert.Equal(["Ash", "Bramble"], read.Characters.Select(character => character.Name));
        Assert.Equal(30, read.Characters[0].Coins);
    }

    /// <summary>
    /// A deliberate difference from reading a file. A file is something a reader chose and can
    /// choose again; this is the contents of their browser, and a tool that will not open because
    /// a storage key was truncated is worse than one that opens holding nobody.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{")]
    [InlineData("{\"format\":\"flail-tools/site\",\"version\":1}")]
    [InlineData("{\"format\":\"flail-tools/party\",\"version\":99}")]
    [InlineData("[]")]
    public void StorageThatMakesNoSenseReadsAsNobodyRatherThanThrowing(string? stored) =>
        Assert.Equal(0, PartyDocuments.Read(stored).Count);

    /// <summary>
    /// Changing the key does not lose anybody's characters so much as hide them: the old key stays
    /// in the browser, holding everything, with nothing left that reads it.
    /// </summary>
    [Fact]
    public void ThePartyIsKeptUnderExactlyThisKey() =>
        Assert.Equal("flail.party.characters", PartyDocuments.StorageKey);

    [Fact]
    public void ADocumentSaysWhichFormatAndVersionItIs()
    {
        Assert.Equal("flail-tools/character", CharacterDocuments.FormatId);
        Assert.Equal("flail-tools/party", PartyDocuments.FormatId);
        Assert.Equal(1, CharacterDocuments.CurrentVersion);
        Assert.Equal(1, PartyDocuments.CurrentVersion);
    }
}
