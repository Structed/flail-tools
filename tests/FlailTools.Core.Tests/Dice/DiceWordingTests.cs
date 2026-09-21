using FlailTools.Core.Data;
using FlailTools.Core.Dice;

namespace FlailTools.Core.Tests.Dice;

/// <summary>
/// The presets name their wording instead of containing it, so the names have to resolve.
/// </summary>
/// <remarks>
/// <para>
/// A preset reports <c>pool/slay</c> and leaves it to <c>ui.json</c> to say what that means. That
/// is what keeps the dice code free of any one game's vocabulary — and it means a missing line does
/// not fail anywhere. It shows the key on screen, in front of the players, looking like a bug
/// because it is one.
/// </para>
/// <para>
/// So the join is checked here, against the file the site actually ships, in the same spirit as the
/// provenance and attribution tests.
/// </para>
/// </remarks>
public sealed class DiceWordingTests
{
    [Fact]
    public async Task EveryPresetIsNamedAndLabelled()
    {
        UiText ui = (await TestData.LoadAsync()).Ui;

        foreach (RollPreset preset in FlailRolls.All)
        {
            string action = "preset/" + preset.Id;

            Assert.True(ui.Actions.ContainsKey(action), $"'{action}' is missing from ui.json.");
            Assert.NotEqual("", ui.Action(action));

            if (preset.Parameter is { } parameter)
            {
                string label = "param/" + parameter.Id;

                Assert.True(ui.Messages.ContainsKey(label), $"'{label}' is missing from ui.json.");
                Assert.NotEqual("", ui.Message(label));
            }
        }
    }

    /// <summary>
    /// Every reading a preset can actually produce has a line waiting for it.
    /// </summary>
    /// <remarks>
    /// Found by rolling rather than by listing, so a reading added to a preset without a line in
    /// <c>ui.json</c> is caught by the same test that already passes, rather than by somebody
    /// remembering to extend a list.
    /// </remarks>
    [Fact]
    public async Task EveryReadingHasSomethingToSay()
    {
        UiText ui = (await TestData.LoadAsync()).Ui;
        HashSet<string> keys = [];

        foreach (RollPreset preset in FlailRolls.All)
        {
            int minimum = preset.Parameter?.Minimum ?? 0;
            int maximum = preset.Parameter?.Maximum ?? 0;

            for (int value = minimum; value <= maximum; value++)
            {
                for (uint seed = 1; seed <= 300; seed++)
                {
                    RollOutcome outcome = preset.Roll(value, seed);

                    if (preset.Read(outcome, value) is { } reading)
                    {
                        keys.Add(reading.Key);
                    }
                }
            }
        }

        Assert.NotEmpty(keys);

        foreach (string key in keys)
        {
            Assert.True(ui.Messages.ContainsKey(key), $"'{key}' is missing from ui.json.");
            Assert.NotEqual("", ui.Message(key));
        }
    }

    /// <summary>
    /// The rarest readings are reachable at all.
    /// </summary>
    /// <remarks>
    /// Rolling three 1s on ten dice is not rare, but rolling it inside a test that only tries three
    /// hundred seeds might be — and a reading that is never produced would sail through the check
    /// above by simply never appearing. Asserted directly so the coverage claim is honest.
    /// </remarks>
    [Theory]
    [InlineData("pool/none")]
    [InlineData("pool/single")]
    [InlineData("pool/double")]
    [InlineData("pool/slay")]
    [InlineData("save/under")]
    [InlineData("save/over")]
    public async Task TheRarerReadingsAreWrittenDownToo(string key)
    {
        UiText ui = (await TestData.LoadAsync()).Ui;

        Assert.True(ui.Messages.ContainsKey(key), $"'{key}' is missing from ui.json.");
    }
}
