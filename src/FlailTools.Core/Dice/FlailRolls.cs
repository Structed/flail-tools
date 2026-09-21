namespace FlailTools.Core.Dice;

/// <summary>
/// The two rolls FLAIL! actually asks for.
/// </summary>
/// <remarks>
/// <para>
/// Everything else under <c>Dice</c> is arithmetic and could belong to any game. This file is the
/// one that knows which game it is playing, and it is deliberately the smallest file here: two
/// presets, four lines of rules, no wording. If the rest of this ever moves to the shared engine,
/// this is the file that stays behind.
/// </para>
/// <para>
/// FLAIL! counts 1s on a pool of d6 rather than summing it — one 1 is a hit, two is double damage,
/// three or more slays outright — and settles everything else by rolling a d20 under an ability
/// score. Both are read here as a key and a number; what those keys say is
/// <c>ui.json</c>'s business.
/// </para>
/// </remarks>
public static class FlailRolls
{
    /// <summary>Identifies the attack pool preset.</summary>
    public const string PoolId = "pool";

    /// <summary>Identifies the roll-under save preset.</summary>
    public const string SaveId = "save";

    /// <summary>A pool of d6, read by how many of them came up 1.</summary>
    public static RollPreset Pool { get; } = new()
    {
        Id = PoolId,
        Parameter = new RollParameter("dice", 1, 10, 2),
        Notation = dice => DiceNotation.Pool(dice, 6)!,
        Reading = (outcome, _) =>
        {
            int ones = outcome.CountOf(1);

            return new RollReading(
                ones switch
                {
                    0 => "pool/none",
                    1 => "pool/single",
                    2 => "pool/double",
                    _ => "pool/slay"
                },
                ones);
        }
    };

    /// <summary>A d20 rolled under an ability score.</summary>
    public static RollPreset Save { get; } = new()
    {
        Id = SaveId,
        Parameter = new RollParameter("score", 1, 20, 10),
        Notation = _ => DiceNotation.Pool(1, 20)!,

        // Under, not under-or-equal: rolling your score exactly is a failure in FLAIL!, which is the
        // sort of detail that is worth writing down next to the code that depends on it.
        Reading = (outcome, score) =>
            new RollReading(outcome.Total < score ? "save/under" : "save/over", score)
    };

    /// <summary>Both presets, in the order they are offered.</summary>
    public static IReadOnlyList<RollPreset> All { get; } = [Pool, Save];
}
