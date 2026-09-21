namespace FlailTools.Core.Dice;

/// <summary>
/// The single number a preset asks for before it can build its dice.
/// </summary>
/// <param name="Id">Names the number for the wording file; never shown as-is.</param>
/// <param name="Minimum">The smallest sensible answer.</param>
/// <param name="Maximum">The largest.</param>
/// <param name="Default">What the box starts at.</param>
/// <remarks>
/// One number, not a form. A preset that needed three inputs would be a rules engine, and the
/// notation box is already there for anyone who wants to spell out something stranger.
/// </remarks>
public sealed record RollParameter(string Id, int Minimum, int Maximum, int Default)
{
    /// <summary>Drags <paramref name="value"/> back inside the bounds.</summary>
    /// <remarks>
    /// Clamped rather than rejected because this arrives from a number box, where browsers will
    /// happily hand over an empty string, a pasted <c>999</c>, or whatever a spin button did on the
    /// way past.
    /// </remarks>
    public int Clamp(int value) => Math.Clamp(value, Minimum, Maximum);
}

/// <summary>
/// What a preset makes of the dice once they have landed.
/// </summary>
/// <param name="Key">
/// Names a line in the wording file. No sentence is built here, so a preset can be read in any
/// language the site later grows without any of this having to change.
/// </param>
/// <param name="Value">The number that line talks about — a count of successes, a target, a margin.</param>
public sealed record RollReading(string Key, int Value);

/// <summary>
/// A named roll a player can make without spelling out the notation.
/// </summary>
/// <remarks>
/// <para>
/// Presets exist because the dice are rarely the interesting part. "Attack with three dice" and
/// "save against Strength 12" are what actually gets said at the table; <c>3d6</c> and <c>1d20</c>
/// are the arithmetic underneath, and having to translate one into the other every time is exactly
/// the friction a dice tool ought to remove.
/// </para>
/// <para>
/// Nothing in this type knows any game. It holds an id, a number to ask for, a way to turn that
/// number into dice, and a way to read the dice back as a key and a value. The game lives entirely
/// in the functions handed to it, which is what keeps this file portable and
/// <see cref="FlailRolls"/> the only one that is not.
/// </para>
/// </remarks>
public sealed record RollPreset
{
    /// <summary>Names the preset for the wording file, and identifies it in the markup.</summary>
    public required string Id { get; init; }

    /// <summary>The number to ask for, or <c>null</c> if the preset needs nothing.</summary>
    public RollParameter? Parameter { get; init; }

    /// <summary>Builds the dice from the parameter, which is <see cref="RollParameter.Default"/> when there is none.</summary>
    public required Func<int, DiceNotation> Notation { get; init; }

    /// <summary>Reads the landed dice, or <c>null</c> to leave the total to speak for itself.</summary>
    public Func<RollOutcome, int, RollReading?>? Reading { get; init; }

    /// <summary>The parameter as it will actually be used, clamped and defaulted.</summary>
    public int Settle(int? value) =>
        Parameter is null ? 0 : Parameter.Clamp(value ?? Parameter.Default);

    /// <summary>Rolls the preset, with a fresh seed.</summary>
    public RollOutcome Roll(int? value) => Roll(value, DiceRolls.CreateSeed());

    /// <summary>Rolls the preset as <paramref name="seed"/> dictates.</summary>
    public RollOutcome Roll(int? value, uint seed) => DiceRolls.Roll(Notation(Settle(value)), seed);

    /// <summary>Reads an outcome this preset produced.</summary>
    public RollReading? Read(RollOutcome outcome, int? value)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return Reading?.Invoke(outcome, Settle(value));
    }
}
