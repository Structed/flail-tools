using System.Globalization;
using Structed.Inkwell.Dice;

namespace FlailTools.Core.Dice;

/// <summary>
/// The two rolls FLAIL! actually asks for, what the dice mean when they land, and the name this
/// game answers to on the wire.
/// </summary>
/// <remarks>
/// <para>
/// The arithmetic that used to sit beside this file now lives in <c>Structed.Inkwell</c>, where it
/// is shared with other games. This is the file that stayed behind: the one that knows which game
/// it is playing. Nothing else in this repository is allowed to, and a test says so.
/// </para>
/// <para>
/// FLAIL! settles risky actions with a d20 rolled at or under an attribute, and fights with a pool
/// of d6 counting 1s: one is a hit, two doubles the damage, three kills outright. On top of that the
/// same pool is read as a poker hand — pairs, triplets, full houses, runs — and that reading is what
/// class talents, legendary weapons and half the bestiary hang their effects on.
/// </para>
/// <para>
/// Nothing here decides what any of those triggers <em>do</em>. A Cutthroat rolling two pairs may
/// strike again; a Cleric rolling a full house calls on their god. Encoding every talent would make
/// this a rules engine and a maintenance burden, and would still be wrong for the table that
/// house-ruled one of them. So the dice are read honestly and completely, and the player applies
/// their own sheet — which is the part they came to the table for.
/// </para>
/// </remarks>
public static class FlailRolls
{
    /// <summary>The name this app answers to when two browsers look for each other.</summary>
    /// <remarks>
    /// <para>
    /// Load-bearing, and it does not look it. The channel namespaces its signalling by this string,
    /// so two tables only meet if their app ids match exactly. Every table code anybody has written
    /// on a character sheet was handed out under this one; change it and those codes stop finding
    /// their tables, with no error at either end — the far side simply never arrives.
    /// </para>
    /// <para>
    /// It reads like configuration and belongs with the rest of the transport, which is exactly why
    /// it is kept here instead: this is the file that is allowed to know which game this is, and the
    /// engine that does the connecting deliberately does not.
    /// </para>
    /// </remarks>
    public const string PartyAppId = "structed-flail-tools-dice";

    /// <summary>Identifies the To Hit preset.</summary>
    public const string HitId = "hit";

    /// <summary>Identifies the save preset.</summary>
    public const string SaveId = "save";

    /// <summary>The fewest dice a weapon asks for, and the most before any edge is applied.</summary>
    private const int FewestHitDice = 1;

    private const int MostHitDice = 12;

    /// <summary>
    /// A pool of d6, counting 1s.
    /// </summary>
    /// <remarks>
    /// An advantage is one more die and a disadvantage one fewer, which is what FLAIL! means by
    /// +1 and -1 To Hit. The pool is never allowed below a single die: a character buried in
    /// penalties still rolls, and still has one chance in six of an answer.
    /// </remarks>
    public static RollPreset Hit { get; } = new()
    {
        Id = HitId,
        Parameter = new RollParameter("dice", FewestHitDice, MostHitDice, 4),
        HasEdge = true,
        Notation = (dice, edge) =>
            DiceNotation.Pool(Math.Clamp(dice + edge, FewestHitDice, MostHitDice + RollEdge.Most), 6)!,

        Reading = (outcome, _) =>
        {
            int ones = outcome.CountOf(1);

            // A fumble is two or more 6s with no 1s among them, so it can only be reached once a
            // miss is established. Checking it inside the miss is the rule, not a shortcut.
            if (ones == 0)
            {
                int sixes = outcome.CountOf(6);

                return sixes >= 2
                    ? new RollReading("hit/fumble", sixes)
                    : new RollReading("hit/miss", 0);
            }

            return new RollReading(
                ones switch
                {
                    1 => "hit/minor",
                    2 => "hit/major",
                    _ => "hit/death"
                },
                ones);
        },

        Notes = (faces, _) => ReadPoker(faces)
    };

    /// <summary>
    /// A d20 rolled at or under an attribute.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At or under, not strictly under: FLAIL! says a result equal to the attribute succeeds. The
    /// distinction is one number wide and decides a twentieth of every save in the game, so it is
    /// worth writing down beside the code that depends on it.
    /// </para>
    /// <para>
    /// Advantage here is not a bonus but a second die with the kinder of the two kept, and the
    /// stacking rule caps at three dice however many advantages pile up.
    /// </para>
    /// </remarks>
    public static RollPreset Save { get; } = new()
    {
        Id = SaveId,
        Parameter = new RollParameter("score", 1, 20, 10),
        HasEdge = true,

        Notation = (_, edge) => edge switch
        {
            0 => DiceNotation.Pool(1, 20)!,
            > 0 => Keep(edge + 1, KeepRule.Lowest),
            _ => Keep(1 - edge, KeepRule.Highest)
        },

        Reading = (outcome, score) =>
        {
            // Read from the die that counted rather than from the total, so a 1 kept out of two dice
            // is still a critical and a 1 that disadvantage threw away is not.
            int rolled = outcome.Dice.FirstOrDefault(die => die.IsKept).Face;

            return rolled switch
            {
                1 => new RollReading("save/critical", rolled),
                20 => new RollReading("save/fumble", rolled),
                _ => new RollReading(rolled <= score ? "save/pass" : "save/fail", score)
            };
        }
    };

    /// <summary>Both presets, in the order they are offered.</summary>
    public static IReadOnlyList<RollPreset> All { get; } = [Hit, Save];

    /// <summary>Every poker result a To Hit roll can show, named for the wording file.</summary>
    /// <remarks>
    /// <para>
    /// Listed rather than discovered, so a test can assert each one has a line waiting for it, and
    /// so the whole set a talent might key off is visible in one place.
    /// </para>
    /// <para>
    /// Sequences are named by length because that is how the book refers to them, and how the
    /// talents that trigger on them are written — a three-number sequence and a four-number sequence
    /// are different things to different classes. Six is as long as a run of d6 faces can get.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> PokerKeys { get; } =
    [
        "poker/pair",
        "poker/pairs",
        "poker/triplet",
        "poker/poker",
        "poker/house",
        "poker/run3",
        "poker/run4",
        "poker/run5",
        "poker/run6"
    ];

    /// <summary>The longest run the faces of a d6 can possibly make.</summary>
    private const int LongestNamedSequence = 6;

    /// <summary>
    /// Reads the pool as a poker hand, in the order a player would say it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two pairs replaces a pair rather than joining it, because "two pairs" is the trigger a
    /// Cutthroat is watching for and reporting both would read as three pairs to anyone skimming.
    /// Everything else stacks: a full house is announced alongside the triplet and the pair that
    /// make it up, because different talents key off each of them.
    /// </para>
    /// <para>
    /// The value on each reading counts how many times the thing happened, and is only worth showing
    /// when it is more than one. A pool of twelve dice can genuinely hold three pairs or two
    /// triplets, and there are abilities that ask for exactly that.
    /// </para>
    /// </remarks>
    private static List<RollReading> ReadPoker(IReadOnlyList<int> faces)
    {
        PokerResults poker = DicePoker.Read(faces);
        List<RollReading> notes = [];

        if (poker.Pairs >= 2)
        {
            notes.Add(new RollReading("poker/pairs", poker.Pairs));
        }
        else if (poker.Pairs == 1)
        {
            notes.Add(new RollReading("poker/pair", 1));
        }

        if (poker.Triplets > 0)
        {
            notes.Add(new RollReading("poker/triplet", poker.Triplets));
        }

        if (poker.Pokers > 0)
        {
            notes.Add(new RollReading("poker/poker", poker.Pokers));
        }

        if (poker.FullHouse)
        {
            notes.Add(new RollReading("poker/house", 1));
        }

        if (poker.Sequence >= DicePoker.ShortestSequence)
        {
            int length = Math.Min(poker.Sequence, LongestNamedSequence);

            notes.Add(new RollReading(
                string.Create(CultureInfo.InvariantCulture, $"poker/run{length}"),
                1));
        }

        return notes;
    }

    /// <summary>A handful of d20s of which exactly one counts.</summary>
    private static DiceNotation Keep(int count, KeepRule rule)
    {
        DiceNotation.TryParse(
            $"{count}d20k{(rule is KeepRule.Lowest ? "l" : "h")}1",
            out DiceNotation notation);

        return notation;
    }
}
