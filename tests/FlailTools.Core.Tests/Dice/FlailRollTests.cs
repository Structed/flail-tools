using FlailTools.Core.Dice;

namespace FlailTools.Core.Tests.Dice;

/// <summary>
/// The two rolls FLAIL! actually asks for, read the way the rules read them.
/// </summary>
public sealed class FlailRollTests
{
    [Fact]
    public void ThePoolIsThatManySixSidedDice()
    {
        for (int dice = 1; dice <= 10; dice++)
        {
            RollOutcome outcome = FlailRolls.Pool.Roll(dice, 1);

            Assert.Equal(dice, outcome.Dice.Count);
            Assert.Equal(6, outcome.Notation.Sides);
            Assert.All(outcome.Dice, die => Assert.True(die.IsKept));
        }
    }

    /// <summary>
    /// The pool is read by counting 1s, which is nothing at all like summing it.
    /// </summary>
    /// <remarks>
    /// Written as a table of faces rather than seeds so the rule being asserted is legible without
    /// running anything. Finding a seed that rolls three 1s is not the point of this test.
    /// </remarks>
    [Theory]
    [InlineData(new[] { 2, 3 }, "pool/none", 0)]
    [InlineData(new[] { 1, 4 }, "pool/single", 1)]
    [InlineData(new[] { 1, 1 }, "pool/double", 2)]
    [InlineData(new[] { 1, 1, 1 }, "pool/slay", 3)]
    [InlineData(new[] { 1, 1, 1, 1, 6 }, "pool/slay", 4)]
    public void ThePoolIsReadByCountingOnes(int[] faces, string expectedKey, int expectedCount)
    {
        RollOutcome outcome = Fake("2d6", faces);
        RollReading? reading = FlailRolls.Pool.Read(outcome, faces.Length);

        Assert.NotNull(reading);
        Assert.Equal(expectedKey, reading.Key);
        Assert.Equal(expectedCount, reading.Value);
    }

    [Fact]
    public void TheSaveIsOneTwentySidedDie()
    {
        RollOutcome outcome = FlailRolls.Save.Roll(12, 9);

        Assert.Single(outcome.Dice);
        Assert.Equal(20, outcome.Notation.Sides);
        Assert.Equal("1d20", outcome.Notation.Text);
    }

    /// <summary>Rolling your score exactly fails, which is the part that is easy to get wrong.</summary>
    [Theory]
    [InlineData(9, 10, "save/under")]
    [InlineData(10, 10, "save/over")]
    [InlineData(11, 10, "save/over")]
    [InlineData(1, 1, "save/over")]
    [InlineData(1, 20, "save/under")]
    public void TheSaveIsRolledStrictlyUnderTheScore(int face, int score, string expectedKey)
    {
        RollReading? reading = FlailRolls.Save.Read(Fake("1d20", [face]), score);

        Assert.NotNull(reading);
        Assert.Equal(expectedKey, reading.Key);
        Assert.Equal(score, reading.Value);
    }

    [Fact]
    public void AParameterOutsideTheBoundsIsDraggedBackIn()
    {
        Assert.Equal(10, FlailRolls.Pool.Settle(9999));
        Assert.Equal(1, FlailRolls.Pool.Settle(-4));
        Assert.Equal(2, FlailRolls.Pool.Settle(null));
        Assert.Equal(20, FlailRolls.Save.Settle(40));
        Assert.Equal(10, FlailRolls.Save.Settle(null));
    }

    [Fact]
    public void APresetRollsTheSameDiceForTheSameSeed()
    {
        foreach (RollPreset preset in FlailRolls.All)
        {
            Assert.Equal(preset.Roll(3, 99).Faces, preset.Roll(3, 99).Faces);
        }
    }

    /// <summary>An outcome with the faces stated, so a reading can be asserted without hunting seeds.</summary>
    private static RollOutcome Fake(string text, int[] faces)
    {
        Assert.True(DiceNotation.TryParse(text, out DiceNotation notation));

        return new RollOutcome
        {
            Notation = notation,
            Dice = [.. faces.Select(face => new RolledDie(face, true))],
            Total = faces.Sum(),
            Seed = 0
        };
    }
}
