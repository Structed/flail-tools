using FlailTools.Core.Characters;

namespace FlailTools.Core.Tests.Characters;

/// <summary>
/// A sheet arrives from a stranger's link, so nothing on it is believed until it has been clamped.
/// </summary>
public sealed class CharacterTests
{
    [Fact]
    public void ABlankSheetOpensReadyToPlayRatherThanAtZero()
    {
        Character character = Character.Create();

        Assert.True(CharacterId.IsValid(character.Id));
        Assert.Equal(0, character.Revision);
        Assert.Equal(1, character.Level);
        Assert.Equal(10, character.Strength);
        Assert.Equal(10, character.Dexterity);
        Assert.Equal(10, character.Luck);
    }

    [Fact]
    public void EveryClassTheBookNamesIsOfferedAndNothingElseIs()
    {
        Assert.Equal(8, CharacterClasses.All.Count);
        Assert.All(CharacterClasses.All, id => Assert.True(CharacterClasses.IsKnown(id)));
        Assert.False(CharacterClasses.IsKnown("paladin"));
        Assert.False(CharacterClasses.IsKnown(null));
    }

    /// <summary>
    /// A sheet that names a class this tool has not heard of is kept as written rather than blanked.
    /// The eight are what the picker offers, not what a sheet is allowed to say.
    /// </summary>
    [Fact]
    public void AnUnknownClassSurvivesClamping()
    {
        Character clamped = Character.Create() with { Class = "hedge-knight" };

        Assert.Equal("hedge-knight", clamped.Clamped().Class);
    }

    [Fact]
    public void AnUnreadableIdIsReplacedRatherThanKept()
    {
        Character clamped = (Character.Create() with { Id = "NOT AN ID" }).Clamped();

        Assert.True(CharacterId.IsValid(clamped.Id));
        Assert.NotEqual("NOT AN ID", clamped.Id);
    }

    [Fact]
    public void NumbersFromOutsideAreBroughtInsideTheirLimits()
    {
        Character clamped = (Character.Create() with
        {
            Revision = -4,
            Level = 5000,
            Strength = -1,
            Dexterity = int.MaxValue,
            Luck = 11,
            HitPoints = -20,
            MaxHitPoints = 4000,
            Defence = 10000,
            Coins = int.MinValue
        }).Clamped();

        Assert.Equal(0, clamped.Revision);
        Assert.Equal(CharacterLimits.MostLevel, clamped.Level);
        Assert.Equal(0, clamped.Strength);
        Assert.Equal(CharacterLimits.MostScore, clamped.Dexterity);
        Assert.Equal(11, clamped.Luck);
        Assert.Equal(0, clamped.HitPoints);
        Assert.Equal(CharacterLimits.MostHitPoints, clamped.MaxHitPoints);
        Assert.Equal(CharacterLimits.MostDefence, clamped.Defence);
        Assert.Equal(0, clamped.Coins);
    }

    [Fact]
    public void TextFromOutsideIsCutToLengthAndTrimmed()
    {
        Character clamped = (Character.Create() with
        {
            Name = "  " + new string('n', 400) + "  ",
            Notes = new string('p', CharacterLimits.ProseLength + 50)
        }).Clamped();

        Assert.Equal(CharacterLimits.NameLength, clamped.Name.Length);
        Assert.Equal(CharacterLimits.ProseLength, clamped.Notes.Length);
    }

    /// <summary>
    /// A link with forty thousand items in it is a denial of service on somebody's browser, not a
    /// character, and the cap is what stops the page trying to draw it.
    /// </summary>
    [Fact]
    public void AZoneCannotHoldMoreRowsThanTheLimitAllows()
    {
        ItemEntry[] far = [.. Enumerable.Range(0, 5000).Select(at => new ItemEntry { Name = $"item {at}" })];

        Character clamped = (Character.Create() with { Satchel = far }).Clamped();

        Assert.Equal(CharacterLimits.Entries, clamped.Satchel.Count);
    }

    [Fact]
    public void EachZoneIsReachableByItsIdAndAnUnknownZoneIsEmpty()
    {
        Character character = Character.Create();

        foreach (string zone in CharacterZones.All)
        {
            Character held = character.WithZone(zone, [new ItemEntry { Name = zone }]);

            Assert.Equal(zone, Assert.Single(held.Zone(zone)).Name);
        }

        Assert.Empty(character.Zone("pockets"));
        Assert.Equal(character, character.WithZone("pockets", [new ItemEntry { Name = "lint" }]));
    }

    [Fact]
    public void EachScoreIsReachableByItsIdAndAnUnknownScoreIsIgnored()
    {
        Character character = Character.Create();

        Assert.Equal(13, character.WithScore(CharacterAttributes.Strength, 13).Strength);
        Assert.Equal(14, character.WithScore(CharacterAttributes.Dexterity, 14).Dexterity);
        Assert.Equal(15, character.WithScore(CharacterAttributes.Luck, 15).Luck);
        Assert.Equal(13, character.WithScore(CharacterAttributes.Strength, 13).Score(CharacterAttributes.Strength));
        Assert.Equal(0, character.Score("charisma"));
        Assert.Equal(character, character.WithScore("charisma", 18));
    }

    [Fact]
    public void ARowNobodyTypedInIsDroppedOnTheWayOut()
    {
        Character character = Character.Create() with
        {
            Satchel = [new ItemEntry { Name = "rope" }, new ItemEntry(), new ItemEntry { Name = " " }],
            Powers = [new PowerEntry { Kind = PowerKinds.Spell }, new PowerEntry { Name = "Mend" }],
            Conditions = ["cursed", "", "   "]
        };

        Character tidied = character.Tidied();

        Assert.Equal("rope", Assert.Single(tidied.Satchel).Name);
        Assert.Equal("Mend", Assert.Single(tidied.Powers).Name);
        Assert.Equal("cursed", Assert.Single(tidied.Conditions));
    }

    /// <summary>
    /// A row carrying only a note is still something somebody typed, and dropping it would delete
    /// their work without saying so. A row carrying only a tick or a slot count is not: nobody
    /// means anything by a nameless magical item, and keeping it would put an empty line on the
    /// recipient's sheet.
    /// </summary>
    [Fact]
    public void ARowWithWordsInItIsKeptAndOneWithoutIsNot()
    {
        Assert.False(new ItemEntry { Note = "bent" }.IsBlank);
        Assert.False(new PowerEntry { Note = "once a day" }.IsBlank);
        Assert.True(new ItemEntry().IsBlank);
        Assert.True(new ItemEntry { IsMagical = true, Slots = 2 }.IsBlank);
        Assert.True(new PowerEntry { Kind = PowerKinds.Spell }.IsBlank);
    }
}
