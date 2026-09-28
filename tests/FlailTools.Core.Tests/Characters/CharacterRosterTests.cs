using FlailTools.Core.Characters;

namespace FlailTools.Core.Tests.Characters;

/// <summary>
/// The roster is what makes a re-shared link overwrite a character rather than pile up beside it.
/// </summary>
public sealed class CharacterRosterTests
{
    [Fact]
    public void ABrowserThatIsKeepingNobodyIsEmpty()
    {
        Assert.Equal(0, CharacterRoster.Empty.Count);
        Assert.Null(CharacterRoster.Empty.Find(CharacterId.Create()));
        Assert.Null(CharacterRoster.Empty.Find(null));
    }

    /// <summary>
    /// The whole point of the id. Saving the same character again replaces it, so re-sharing a
    /// sheet updates the copy in the party list instead of leaving two at two revisions with no
    /// way to tell which is current.
    /// </summary>
    [Fact]
    public void SavingTheSameCharacterAgainReplacesItRatherThanAddingOne()
    {
        Character first = Character.Create() with { Name = "Bramble", Revision = 1 };
        Character second = first with { Name = "Bramble", Revision = 2, Coins = 40 };

        CharacterRoster roster = CharacterRoster.Empty.Save(first).Save(second);

        Assert.Equal(1, roster.Count);
        Assert.Equal(40, roster.Find(first.Id)!.Coins);
        Assert.Equal(2, roster.Find(first.Id)!.Revision);
    }

    [Fact]
    public void TwoDifferentCharactersBothStay()
    {
        CharacterRoster roster = CharacterRoster.Empty
            .Save(Character.Create() with { Name = "Bramble" })
            .Save(Character.Create() with { Name = "Ash" });

        Assert.Equal(2, roster.Count);
    }

    [Fact]
    public void TheListIsInNameOrderSoItDoesNotRearrangeItself()
    {
        CharacterRoster roster = CharacterRoster.Empty
            .Save(Character.Create() with { Name = "Wren" })
            .Save(Character.Create() with { Name = "ash" })
            .Save(Character.Create() with { Name = "Bramble" });

        Assert.Equal(["ash", "Bramble", "Wren"], roster.Characters.Select(character => character.Name));
    }

    /// <summary>
    /// Saving replaces by id, so two characters sharing one should be impossible — but this reads
    /// storage that anything may have written into, and of the two the one saved more recently is
    /// the better guess at what its owner wanted.
    /// </summary>
    [Fact]
    public void TwoCharactersSharingAnIdCollapseToTheNewer()
    {
        Character older = Character.Create() with { Name = "Bramble", Revision = 2 };
        Character newer = older with { Revision = 5, Coins = 9 };

        CharacterRoster roster = CharacterRoster.Of([older, newer]);

        Assert.Equal(1, roster.Count);
        Assert.Equal(5, roster.Characters[0].Revision);
        Assert.Equal(9, roster.Characters[0].Coins);
    }

    [Fact]
    public void RemovingTakesOneOutAndLeavesTheRest()
    {
        Character going = Character.Create() with { Name = "Bramble" };
        CharacterRoster roster = CharacterRoster.Empty
            .Save(going)
            .Save(Character.Create() with { Name = "Ash" });

        Assert.Equal(1, roster.Remove(going.Id).Count);
        Assert.Equal(2, roster.Remove("not an id").Count);
        Assert.Equal(2, roster.Remove(null).Count);
    }

    /// <summary>
    /// What the warning on the receiving page is made of. Saving an older sheet over a newer one
    /// loses work, and the only way to know that has happened is to compare the two revisions
    /// before writing anything.
    /// </summary>
    [Fact]
    public void AnArrivingSheetIsComparedWithTheCopyAlreadyHeld()
    {
        Character stored = Character.Create() with { Name = "Bramble", Revision = 4 };
        CharacterRoster roster = CharacterRoster.Empty.Save(stored);

        Assert.Equal(RosterMatch.Unknown, roster.Compare(Character.Create() with { Name = "Ash" }));
        Assert.Equal(RosterMatch.Older, roster.Compare(stored with { Revision = 3 }));
        Assert.Equal(RosterMatch.Same, roster.Compare(stored));
        Assert.Equal(RosterMatch.Newer, roster.Compare(stored with { Revision = 5 }));
    }

    /// <summary>
    /// Bumping the revision here would be the obvious place for it and the wrong one: saving a
    /// received sheet unaltered would make this browser's copy look newer than the one its author
    /// is still editing, and sending it back would offer to overwrite their work with their own
    /// sheet.
    /// </summary>
    [Fact]
    public void SavingStoresTheRevisionItWasHandedAndDoesNotMoveIt()
    {
        Character arriving = Character.Create() with { Name = "Bramble", Revision = 6 };

        Assert.Equal(6, CharacterRoster.Empty.Save(arriving).Find(arriving.Id)!.Revision);
    }

    [Fact]
    public void ARosterIsNeverChangedInPlace()
    {
        CharacterRoster roster = CharacterRoster.Empty;

        _ = roster.Save(Character.Create() with { Name = "Bramble" });

        Assert.Equal(0, roster.Count);
    }

    [Fact]
    public void SomethingUnreadableInStorageIsStillGivenAnIdRatherThanDropped()
    {
        CharacterRoster roster = CharacterRoster.Of([new Character { Name = "Bramble" }]);

        Assert.Equal(1, roster.Count);
        Assert.True(CharacterId.IsValid(roster.Characters[0].Id));
    }
}
