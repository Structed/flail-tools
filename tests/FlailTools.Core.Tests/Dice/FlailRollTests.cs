using FlailTools.Core.Dice;

namespace FlailTools.Core.Tests.Dice;

/// <summary>
/// The two rolls FLAIL! actually asks for, read the way the rules read them.
/// </summary>
public sealed class FlailRollTests
{
    [Fact]
    public void ToHitIsThatManySixSidedDice()
    {
        for (int dice = 1; dice <= 12; dice++)
        {
            RollOutcome outcome = FlailRolls.Hit.Roll(dice);

            Assert.Equal(dice, outcome.Dice.Count);
            Assert.Equal(6, outcome.Notation.Sides);
            Assert.All(outcome.Dice, die => Assert.True(die.IsKept));
        }
    }

    /// <summary>
    /// To Hit is read by counting 1s, which is nothing at all like summing it.
    /// </summary>
    /// <remarks>
    /// Written as a table of faces rather than seeds so the rule being asserted is legible without
    /// running anything. Finding a seed that rolls three 1s is not the point of this test.
    /// </remarks>
    [Theory]
    [InlineData(new[] { 2, 3 }, "hit/miss", 0)]
    [InlineData(new[] { 1, 4 }, "hit/minor", 1)]
    [InlineData(new[] { 1, 1 }, "hit/major", 2)]
    [InlineData(new[] { 1, 1, 1 }, "hit/death", 3)]
    [InlineData(new[] { 1, 1, 1, 1, 6 }, "hit/death", 4)]
    public void ToHitIsReadByCountingOnes(int[] faces, string expectedKey, int expectedCount)
    {
        RollReading? reading = FlailRolls.Hit.Read(Fake("2d6", faces), faces.Length);

        Assert.NotNull(reading);
        Assert.Equal(expectedKey, reading.Key);
        Assert.Equal(expectedCount, reading.Value);
    }

    /// <summary>
    /// Two 6s and no 1s is a fumble; a single 6 is only a miss.
    /// </summary>
    /// <remarks>
    /// The "no 1s" half is the part worth pinning down. A roll of three 6s and a 1 hits, and the
    /// sixes are simply nothing — a rule that checked them first would turn a hit into a fumble.
    /// </remarks>
    [Theory]
    [InlineData(new[] { 6, 6 }, "hit/fumble", 2)]
    [InlineData(new[] { 6, 6, 6 }, "hit/fumble", 3)]
    [InlineData(new[] { 6, 2, 3 }, "hit/miss", 0)]
    [InlineData(new[] { 6, 6, 1 }, "hit/minor", 1)]
    [InlineData(new[] { 6, 6, 6, 1, 1 }, "hit/major", 2)]
    public void TwoSixesWithoutAOneIsAFumble(int[] faces, string expectedKey, int expectedValue)
    {
        RollReading? reading = FlailRolls.Hit.Read(Fake("2d6", faces), faces.Length);

        Assert.NotNull(reading);
        Assert.Equal(expectedKey, reading.Key);
        Assert.Equal(expectedValue, reading.Value);
    }

    /// <summary>An edge on a pool is a die, and the pool never empties however bad it gets.</summary>
    [Theory]
    [InlineData(4, 0, 4)]
    [InlineData(4, 1, 5)]
    [InlineData(4, 2, 6)]
    [InlineData(4, -2, 2)]
    [InlineData(1, -2, 1)]
    public void AnEdgeOnToHitIsADie(int dice, int edge, int expected)
    {
        Assert.Equal(expected, FlailRolls.Hit.Dice(dice, edge).Count);
    }

    [Fact]
    public void TheSaveIsOneTwentySidedDie()
    {
        RollOutcome outcome = FlailRolls.Save.Roll(12);

        Assert.Single(outcome.Dice);
        Assert.Equal(20, outcome.Notation.Sides);
        Assert.Equal("1d20", outcome.Notation.Text);
    }

    /// <summary>Rolling your score exactly succeeds, which is the part that is easy to get wrong.</summary>
    [Theory]
    [InlineData(9, 10, "save/pass")]
    [InlineData(10, 10, "save/pass")]
    [InlineData(11, 10, "save/fail")]
    [InlineData(19, 10, "save/fail")]
    [InlineData(2, 1, "save/fail")]
    public void TheSaveIsRolledAtOrUnderTheScore(int face, int score, string expectedKey)
    {
        RollReading? reading = FlailRolls.Save.Read(Fake("1d20", [face]), score);

        Assert.NotNull(reading);
        Assert.Equal(expectedKey, reading.Key);
        Assert.Equal(score, reading.Value);
    }

    /// <summary>A natural 1 and a natural 20 outrank the score entirely.</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 20)]
    public void ANaturalOneIsACritical(int face, int score)
    {
        RollReading? reading = FlailRolls.Save.Read(Fake("1d20", [face]), score);

        Assert.NotNull(reading);
        Assert.Equal("save/critical", reading.Key);
        Assert.Equal(face, reading.Value);
    }

    [Theory]
    [InlineData(20, 1)]
    [InlineData(20, 20)]
    public void ANaturalTwentyIsAFumble(int face, int score)
    {
        RollReading? reading = FlailRolls.Save.Read(Fake("1d20", [face]), score);

        Assert.NotNull(reading);
        Assert.Equal("save/fumble", reading.Key);
        Assert.Equal(face, reading.Value);
    }

    /// <summary>An edge on a save is another d20 with one of them kept.</summary>
    [Theory]
    [InlineData(0, "1d20")]
    [InlineData(1, "2d20kl1")]
    [InlineData(2, "3d20kl1")]
    [InlineData(-1, "2d20kh1")]
    [InlineData(-2, "3d20kh1")]
    public void AnEdgeOnASaveIsASecondDie(int edge, string expected)
    {
        Assert.Equal(expected, FlailRolls.Save.Dice(10, edge).Text);
    }

    /// <summary>
    /// A save is read from the die that counted, not from the total of the dice rolled.
    /// </summary>
    /// <remarks>
    /// With advantage there are two or three d20s on the table and only one of them is the save.
    /// Summing them would put every advantaged save above twenty and fail the lot.
    /// </remarks>
    [Fact]
    public void ASaveReadsTheDieThatCounted()
    {
        RollOutcome outcome = Fake("2d20kl1", [4, 18], kept: [true, false]);
        RollReading? reading = FlailRolls.Save.Read(outcome, 10);

        Assert.NotNull(reading);
        Assert.Equal("save/pass", reading.Key);
    }

    /// <summary>A 1 thrown away by disadvantage is not a critical.</summary>
    [Fact]
    public void ADiscardedOneIsNotACritical()
    {
        RollOutcome outcome = Fake("2d20kh1", [1, 17], kept: [false, true]);
        RollReading? reading = FlailRolls.Save.Read(outcome, 10);

        Assert.NotNull(reading);
        Assert.Equal("save/fail", reading.Key);
    }

    [Fact]
    public void NoAmountOfStackingBendsThingsFurtherThanThreeDice()
    {
        Assert.Equal(6, FlailRolls.Hit.Dice(4, 9).Count);
        Assert.Equal("3d20kl1", FlailRolls.Save.Dice(10, 9).Text);
        Assert.Equal("3d20kh1", FlailRolls.Save.Dice(10, -9).Text);
    }

    [Fact]
    public void AParameterOutsideTheBoundsIsDraggedBackIn()
    {
        Assert.Equal(12, FlailRolls.Hit.Settle(9999));
        Assert.Equal(1, FlailRolls.Hit.Settle(-4));
        Assert.Equal(4, FlailRolls.Hit.Settle(null));
        Assert.Equal(20, FlailRolls.Save.Settle(40));
        Assert.Equal(10, FlailRolls.Save.Settle(null));
    }

    [Fact]
    public void APresetRollsTheSameDiceForTheSameSeed()
    {
        foreach (RollPreset preset in FlailRolls.All)
        {
            Assert.Equal(preset.Roll(3, 0, 99).Faces, preset.Roll(3, 0, 99).Faces);
        }
    }

    /// <summary>
    /// Poker results are reported on To Hit, and on nothing else.
    /// </summary>
    /// <remarks>
    /// The save deliberately has no notes. One d20 cannot pair with anything, and a tool that
    /// offered a poker reading of it would be inventing a rule.
    /// </remarks>
    [Theory]
    [InlineData(new[] { 3, 3, 5, 6 }, "poker/pair")]
    [InlineData(new[] { 3, 3, 5, 5 }, "poker/pairs")]
    [InlineData(new[] { 2, 2, 2, 6 }, "poker/triplet")]
    [InlineData(new[] { 2, 2, 2, 2 }, "poker/poker")]
    [InlineData(new[] { 2, 2, 2, 5, 5 }, "poker/house")]
    [InlineData(new[] { 1, 2, 3, 6 }, "poker/run3")]
    public void ToHitReportsItsPokerResults(int[] faces, string expectedKey)
    {
        Assert.Contains(FlailRolls.Hit.Note(faces, 4), note => note.Key == expectedKey);
        Assert.Empty(FlailRolls.Save.Note(faces, 10));
    }

    /// <summary>
    /// A sequence is named by its length, because that is how the rules refer to it.
    /// </summary>
    /// <remarks>
    /// A talent that fires on a four-number sequence should not have to be told that the chip
    /// reading "sequence" happened to be four long.
    /// </remarks>
    [Theory]
    [InlineData(new[] { 1, 2, 3, 6 }, "poker/run3")]
    [InlineData(new[] { 1, 2, 3, 4 }, "poker/run4")]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, "poker/run5")]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6 }, "poker/run6")]
    public void ASequenceIsNamedByItsLength(int[] faces, string expectedKey)
    {
        IReadOnlyList<RollReading> notes = FlailRolls.Hit.Note(faces, faces.Length);

        Assert.Contains(notes, note => note.Key == expectedKey);
        Assert.All(notes, note => Assert.Equal(1, note.Value));
    }

    /// <summary>
    /// Two pairs is announced as two pairs, not as a pair twice over.
    /// </summary>
    /// <remarks>
    /// This is the reading a Cutthroat's Opportunistic Strike keys off, so it is worth being exact
    /// about: the roll shows two pairs and says so once.
    /// </remarks>
    [Fact]
    public void TwoPairsIsSaidOnce()
    {
        IReadOnlyList<RollReading> notes = FlailRolls.Hit.Note([4, 4, 6, 6], 4);

        Assert.Equal("poker/pairs", Assert.Single(notes).Key);
        Assert.Equal(2, notes[0].Value);
    }

    /// <summary>A pool large enough to show three pairs says three, because talents ask for that.</summary>
    [Fact]
    public void ThreePairsCountsToThree()
    {
        IReadOnlyList<RollReading> notes = FlailRolls.Hit.Note([2, 2, 4, 4, 6, 6], 6);

        Assert.Equal(3, Assert.Single(notes, note => note.Key == "poker/pairs").Value);
    }

    [Fact]
    public void TwoTripletsCountToTwo()
    {
        IReadOnlyList<RollReading> notes = FlailRolls.Hit.Note([2, 2, 2, 5, 5, 5], 6);

        Assert.Equal(2, Assert.Single(notes, note => note.Key == "poker/triplet").Value);
        Assert.DoesNotContain(notes, note => note.Key == "poker/house");
    }

    [Fact]
    public void NothingMatchingSaysNothing()
    {
        Assert.Empty(FlailRolls.Hit.Note([1, 3, 5], 3));
    }

    /// <summary>Every poker result the code can produce has a name in the list tests check.</summary>
    [Fact]
    public void EveryPokerResultIsListed()
    {
        int[][] hands =
        [
            [3, 3, 5, 6],
            [3, 3, 5, 5],
            [2, 2, 2, 6],
            [2, 2, 2, 2],
            [2, 2, 2, 5, 5],
            [1, 2, 3, 6]
        ];

        foreach (int[] hand in hands)
        {
            Assert.All(
                FlailRolls.Hit.Note(hand, hand.Length),
                note => Assert.Contains(note.Key, FlailRolls.PokerKeys));
        }
    }

    /// <summary>An outcome with the faces stated, so a reading can be asserted without hunting seeds.</summary>
    private static RollOutcome Fake(string text, int[] faces, bool[]? kept = null)
    {
        Assert.True(DiceNotation.TryParse(text, out DiceNotation notation));

        return new RollOutcome
        {
            Notation = notation,
            Dice = [.. faces.Select((face, index) => new RolledDie(face, kept?[index] ?? true))],
            Total = faces.Where((_, index) => kept?[index] ?? true).Sum(),
            Seed = 0
        };
    }
}
