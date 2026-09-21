using Structed.Inkwell.Generation;
using Structed.Inkwell.Randomness;

namespace FlailTools.Core.Generation;

/// <summary>
/// Rolls the engine's <see cref="RollContext"/> does not cover, kept to the same contract.
/// </summary>
/// <remarks>
/// <c>RollContext.Text</c> picks uniformly, which is right for a printed table where every row is
/// one result. A weighted pick is a different thing and the engine leaves it to the caller — but it
/// still has to honour a pin and still has to record what it drew, or a locked field would come
/// unstuck the moment it was weighted. That contract is easy to get subtly wrong twice, so it is
/// written once here.
/// </remarks>
internal static class Rolls
{
    /// <summary>Picks one row by weight, honouring a pin on the path and recording the position drawn.</summary>
    public static T Weighted<T>(
        RollContext roll,
        string path,
        IReadOnlyList<T> rows,
        Func<T, int> weight)
    {
        ArgumentNullException.ThrowIfNull(roll);
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            throw new ArgumentException($"Nothing to pick for '{path}'.", nameof(rows));
        }

        if (roll.TryGetPin(path, out string pinned) &&
            PinReference.TryGetIndex(pinned, out int pinnedIndex) &&
            pinnedIndex < rows.Count)
        {
            roll.Record(path, pinned);
            return rows[pinnedIndex];
        }

        int index = roll.Dice(path).PickWeightedIndex(rows, weight);
        roll.Record(path, PinReference.ForIndex(index));

        return rows[index];
    }

    /// <summary>
    /// Reads one face of a die and returns the row it lands on, honouring a pin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used where the book rolls a die whose face <em>is</em> the row — a dungeon's d6 stocking, a
    /// tower's floors. Kept apart from <c>Text</c> because the face matters in its own right: a
    /// table of six outcomes read by a d6 is not the same as a six-row table read by an index, once
    /// somebody adds a seventh row.
    /// </para>
    /// <para>
    /// The index comes back as well as the words because a cave needs to know which of its dice
    /// came up the same number. It is <c>-1</c> when a pin holds text somebody typed rather than a
    /// row of the table, which is not a face at all and so matches nothing.
    /// </para>
    /// </remarks>
    public static (int Index, string Value) Face(
        RollContext roll,
        string path,
        IReadOnlyList<string> rows,
        int sides)
    {
        ArgumentNullException.ThrowIfNull(roll);
        ArgumentNullException.ThrowIfNull(rows);

        if (roll.TryGetPin(path, out string pinned))
        {
            roll.Record(path, pinned);

            return PinReference.TryGetIndex(pinned, out int pinnedIndex)
                ? (pinnedIndex, Row(rows, pinnedIndex))
                : (-1, PinReference.Resolve(pinned, rows));
        }

        int index = roll.Dice(path).Roll(sides) - 1;
        roll.Record(path, PinReference.ForIndex(index));

        return (index, Row(rows, index));
    }

    private static string Row(IReadOnlyList<string> rows, int index) =>
        index >= 0 && index < rows.Count ? rows[index] : "";

    /// <summary>
    /// Rolls a whole number in an inclusive range, honouring a pin and recording what it drew.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FLAIL! bounds several things by a range rather than by a table: a tower wizard is set
    /// "between 6 and 10", with hit points of 10-20 and mana of 20-30, and a tower is built from
    /// "between four and six" dice. None of those is a printed table, so none of them can go
    /// through <c>Text</c> — but they still have to honour a lock and still have to be reproducible.
    /// </para>
    /// <para>
    /// What is recorded is the offset from <paramref name="min"/> rather than the number itself, so
    /// that a pin here is a position exactly as it is everywhere else. A pin holding something that
    /// is not an offset in range — typed text, or an offset left over from a narrower range — is
    /// discarded and re-rolled rather than clamped, because a silently clamped lock is a lock that
    /// shows a number nobody chose.
    /// </para>
    /// </remarks>
    public static int Number(RollContext roll, string path, int min, int max)
    {
        ArgumentNullException.ThrowIfNull(roll);

        if (max < min)
        {
            throw new ArgumentOutOfRangeException(
                nameof(max), max, $"The range for '{path}' ends before it starts.");
        }

        int span = max - min + 1;

        if (roll.TryGetPin(path, out string pinned) &&
            PinReference.TryGetIndex(pinned, out int pinnedOffset) &&
            pinnedOffset >= 0 && pinnedOffset < span)
        {
            roll.Record(path, pinned);
            return min + pinnedOffset;
        }

        int offset = roll.Dice(path).Roll(span) - 1;
        roll.Record(path, PinReference.ForIndex(offset));

        return min + offset;
    }

    /// <summary>A value in <c>[0, 1]</c> drawn from a stream, for the geometry a dice drop needs.</summary>
    /// <remarks>
    /// Drawn through <see cref="DiceRoller.NextIndex"/> rather than any floating-point source so it
    /// stays exactly as reproducible as every other roll: the same seed has to land the same dice on
    /// the same part of the page on every machine.
    /// </remarks>
    public static double Unit(DiceRoller dice) => dice.NextIndex(Resolution) / (double)(Resolution - 1);

    private const int Resolution = 1024;
}
