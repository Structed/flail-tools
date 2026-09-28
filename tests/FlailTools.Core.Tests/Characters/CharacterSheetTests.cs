using FlailTools.Core.Characters;

namespace FlailTools.Core.Tests.Characters;

/// <summary>
/// The sheet is the whole of the page's behaviour, so the page can be thin and this can be tested.
/// </summary>
public sealed class CharacterSheetTests
{
    [Fact]
    public void ANewSheetHasAnIdAlready()
    {
        CharacterSheet sheet = new();

        Assert.True(CharacterId.IsValid(sheet.Character.Id));
    }

    [Fact]
    public void EveryFieldIsSetByOneCall()
    {
        CharacterSheet sheet = new();

        sheet.Rename("Bramble");
        sheet.SetClass(CharacterClasses.Druid);
        sheet.SetLevel(3);
        sheet.SetBackground("Hedge witch");
        sheet.SetBackgroundPerk("Knows which mushrooms are which.");
        sheet.SetScore(CharacterAttributes.Strength, 9);
        sheet.SetScore(CharacterAttributes.Dexterity, 14);
        sheet.SetScore(CharacterAttributes.Luck, 12);
        sheet.SetHitPoints(5);
        sheet.SetMaxHitPoints(8);
        sheet.SetDefence(2);
        sheet.SetCoins(17);
        sheet.SetNotes("Owes the miller a favour.");

        Character character = sheet.Character;

        Assert.Equal("Bramble", character.Name);
        Assert.Equal(CharacterClasses.Druid, character.Class);
        Assert.Equal(3, character.Level);
        Assert.Equal("Hedge witch", character.Background);
        Assert.Equal("Knows which mushrooms are which.", character.BackgroundPerk);
        Assert.Equal(9, character.Strength);
        Assert.Equal(14, character.Dexterity);
        Assert.Equal(12, character.Luck);
        Assert.Equal(5, character.HitPoints);
        Assert.Equal(8, character.MaxHitPoints);
        Assert.Equal(2, character.Defence);
        Assert.Equal(17, character.Coins);
        Assert.Equal("Owes the miller a favour.", character.Notes);
    }

    /// <summary>
    /// Trimming as somebody types deletes the space they just pressed in the middle of a name, so
    /// it waits until the sheet is written down.
    /// </summary>
    [Fact]
    public void ASpaceBeingTypedSurvivesUntilTheSheetIsWrittenDown()
    {
        CharacterSheet sheet = new();

        sheet.Rename("Bramble ");

        Assert.Equal("Bramble ", sheet.Character.Name);
        Assert.Equal("Bramble", sheet.Tidied.Name);
    }

    [Fact]
    public void ARowIsAddedEditedAndTakenAwayAgain()
    {
        CharacterSheet sheet = new();

        sheet.AddItem(CharacterZones.Satchel);
        sheet.SetItem(
            CharacterZones.Satchel,
            0,
            sheet.Items(CharacterZones.Satchel)[0] with { Name = "Rope", Slots = 1, IsMagical = true });

        ItemEntry item = Assert.Single(sheet.Items(CharacterZones.Satchel));

        Assert.Equal("Rope", item.Name);
        Assert.Equal(1, item.Slots);
        Assert.True(item.IsMagical);

        sheet.RemoveItem(CharacterZones.Satchel, 0);

        Assert.Empty(sheet.Items(CharacterZones.Satchel));
    }

    /// <summary>
    /// A stale index is what a second browser tab, or a fast double click on a remove button,
    /// produces. It has to be a no-op rather than an exception, because there is nobody to catch it.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(99)]
    public void AnIndexThatIsNotThereChangesNothing(int index)
    {
        CharacterSheet sheet = new();

        sheet.AddItem(CharacterZones.Hands);
        sheet.AddEntry(CharacterLists.Powers);
        sheet.AddCondition();

        Character before = sheet.Character;

        sheet.SetItem(CharacterZones.Hands, index, new ItemEntry { Name = "Sword" });
        sheet.RemoveItem(CharacterZones.Hands, index);
        sheet.SetEntry(CharacterLists.Powers, index, new PowerEntry { Name = "Mend" });
        sheet.RemoveEntry(CharacterLists.Powers, index);
        sheet.SetCondition(index, "cursed");
        sheet.RemoveCondition(index);

        Assert.Equal(before, sheet.Character);
    }

    [Fact]
    public void AnUnknownZoneOrListIsIgnoredRatherThanThrowing()
    {
        CharacterSheet sheet = new();
        Character before = sheet.Character;

        sheet.AddItem("pockets");
        sheet.AddEntry("wishes");

        Assert.Equal(before, sheet.Character);
        Assert.Empty(sheet.Items("pockets"));
        Assert.Empty(sheet.Entries("wishes"));
    }

    [Fact]
    public void NoListGrowsPastTheLimit()
    {
        CharacterSheet sheet = new();

        for (int at = 0; at < CharacterLimits.Entries + 20; at++)
        {
            sheet.AddItem(CharacterZones.Satchel);
            sheet.AddEntry(CharacterLists.Powers, PowerKinds.Spell);
            sheet.AddCondition();
        }

        Assert.Equal(CharacterLimits.Entries, sheet.Items(CharacterZones.Satchel).Count);
        Assert.Equal(CharacterLimits.Entries, sheet.Entries(CharacterLists.Powers).Count);
        Assert.Equal(CharacterLimits.Entries, sheet.Character.Conditions.Count);
    }

    [Fact]
    public void TalentsAndPowersAreKeptApart()
    {
        CharacterSheet sheet = new();

        sheet.AddEntry(CharacterLists.Talents);
        sheet.SetEntry(CharacterLists.Talents, 0, new PowerEntry { Name = "Cleave" });
        sheet.AddEntry(CharacterLists.Powers, PowerKinds.Spell);
        sheet.SetEntry(CharacterLists.Powers, 0, new PowerEntry { Kind = PowerKinds.Spell, Name = "Mend" });

        Assert.Equal("Cleave", Assert.Single(sheet.Character.Talents).Name);
        Assert.Equal("Mend", Assert.Single(sheet.Character.Powers).Name);
    }

    /// <summary>
    /// An empty row added to be typed into has to stay put while the sheet is open, and go away
    /// when it is written down. Both halves matter: without the first, a row vanishes from under
    /// the cursor; without the second, every link ever shared carries the rows somebody thought
    /// better of.
    /// </summary>
    [Fact]
    public void AnEmptyRowStaysWhileTypingAndGoesOnTheWayOut()
    {
        CharacterSheet sheet = new();

        sheet.AddItem(CharacterZones.Satchel);

        Assert.Single(sheet.Items(CharacterZones.Satchel));
        Assert.Empty(sheet.Tidied.Satchel);
    }

    [Fact]
    public void AFirstSaveIsRevisionOne()
    {
        CharacterSheet sheet = new();

        sheet.Rename("Bramble");

        Assert.Equal(1, sheet.ForSaving(stored: null).Revision);
    }

    /// <summary>
    /// Pressing Save on a sheet nobody has touched must not move the revision. If it did, opening
    /// somebody's shared character and pressing Save out of habit would make this browser look, to
    /// everybody it was sent to, like the one holding newer work — and the stale warning that
    /// protects a party's sheets would start crying wolf.
    /// </summary>
    [Fact]
    public void SavingAnUntouchedSheetDoesNotMoveTheRevision()
    {
        Character stored = (Character.Create() with { Name = "Bramble", Revision = 4 }).Tidied();
        CharacterSheet sheet = new(stored);

        Character saved = sheet.ForSaving(stored);

        Assert.Equal(4, saved.Revision);
        Assert.True(sheet.Matches(stored));
    }

    [Fact]
    public void SavingAChangedSheetMovesTheRevisionOnce()
    {
        Character stored = Character.Create() with { Name = "Bramble", Revision = 4 };
        CharacterSheet sheet = new(stored);

        sheet.SetHitPoints(3);

        Assert.False(sheet.Matches(stored));
        Assert.Equal(5, sheet.ForSaving(stored).Revision);
    }

    /// <summary>
    /// A sheet opened from an old link and then edited has to land above the copy already stored.
    /// One past its own revision would produce a character claiming to be older than the thing it
    /// just overwrote, and the next comparison would offer to overwrite it back again.
    /// </summary>
    [Fact]
    public void AnEditedOldLinkLandsAboveWhatIsAlreadyStored()
    {
        string id = CharacterId.Create();
        Character stored = Character.Create() with { Id = id, Name = "Bramble", Revision = 9 };
        CharacterSheet sheet = new(Character.Create() with { Id = id, Name = "Bramble", Revision = 2 });

        sheet.SetCoins(3);

        Assert.Equal(10, sheet.ForSaving(stored).Revision);
    }

    [Fact]
    public void TheIdNeverChangesHoweverMuchIsEdited()
    {
        CharacterSheet sheet = new();
        string id = sheet.Character.Id;

        sheet.Rename("Bramble");
        sheet.SetClass(CharacterClasses.Wizard);
        sheet.AddItem(CharacterZones.Body);
        sheet.SetLevel(4);

        Assert.Equal(id, sheet.Character.Id);
        Assert.Equal(id, sheet.ForSaving(stored: null).Id);
    }

    [Fact]
    public void ApplyingASheetReplacesEverythingIncludingTheId()
    {
        CharacterSheet sheet = new();
        Character arriving = Character.Create() with { Name = "Odd", Revision = 7 };

        sheet.Apply(arriving);

        Assert.Equal(arriving.Id, sheet.Character.Id);
        Assert.Equal("Odd", sheet.Character.Name);
        Assert.Equal(7, sheet.Character.Revision);
    }

    [Fact]
    public void TheShortQueryNamesTheCharacterAndTheLongOneCarriesIt()
    {
        CharacterSheet sheet = new();

        sheet.Rename("Bramble");

        Assert.Equal($"?id={sheet.Character.Id}", sheet.IdQuery);
        Assert.StartsWith("?c=", sheet.Query, StringComparison.Ordinal);
        Assert.EndsWith(".flail.json", sheet.FileName, StringComparison.Ordinal);
    }
}
