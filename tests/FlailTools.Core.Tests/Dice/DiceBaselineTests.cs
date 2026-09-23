using System.Globalization;
using System.Text;
using FlailTools.Core.Tests.Golden;
using Structed.Inkwell.Dice;

namespace FlailTools.Core.Tests.Dice;

/// <summary>
/// A seed has to mean the same dice on every machine that ever reads it, including machines running
/// a different version of the engine.
/// </summary>
/// <remarks>
/// <para>
/// The seed is the fallback when the peer-to-peer link will not form: it gets pasted into a chat
/// window and rebuilt by hand. That only works if it rebuilds the same roll, so this is checked the
/// same way the site baselines are — against a fixture, byte for byte.
/// </para>
/// <para>
/// The dice themselves now live in <c>Structed.Inkwell</c>, and everything else that used to be
/// tested here is tested there. This one stayed behind on purpose. It is the only check that spans
/// the two repositories: it fails here, in the app people actually use, if a future version of the
/// engine quietly changes what a seed produces. Deleting it as a duplicate would remove the single
/// thing standing between a package bump and every seed ever shared meaning something else.
/// </para>
/// </remarks>
public sealed class DiceBaselineTests
{
    private static DiceNotation Parse(string text)
    {
        Assert.True(DiceNotation.TryParse(text, out DiceNotation notation));
        return notation;
    }

    [Fact]
    public void TheDiceHaveNotMoved()
    {
        string[] notations = ["d20", "2d6", "3d6+2", "4d6kh3", "5d6kl2", "10d6", "1d100-5"];
        uint[] seeds = [1u, 7u, 42u, 1337u, 65535u];
        StringBuilder manifest = new();

        foreach (string text in notations)
        {
            foreach (uint seed in seeds)
            {
                RollOutcome outcome = DiceRolls.Roll(Parse(text), seed);

                manifest.Append(outcome.Notation.Text).Append('\t')
                    .Append(outcome.SeedCode).Append('\t')
                    .AppendJoin(
                        ',',
                        outcome.Dice.Select(die =>
                            die.IsKept
                                ? die.Face.ToString(CultureInfo.InvariantCulture)
                                : $"({die.Face.ToString(CultureInfo.InvariantCulture)})"))
                    .Append('\t')
                    .Append(outcome.Total)
                    .Append('\n');
            }
        }

        GoldenFile.Verify("dice-baseline.txt", manifest.ToString());
    }
}
