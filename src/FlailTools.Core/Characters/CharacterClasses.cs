namespace FlailTools.Core.Characters;

/// <summary>
/// The eight classes a FLAIL! character can be, and the kinds of thing their sheets track.
/// </summary>
/// <remarks>
/// <para>
/// Names only. The Games Omnivorous Third-Party Licence permits reusing FLAIL!'s terminology, and a
/// class name is terminology; what each class can actually <em>do</em> is the book's content and is
/// deliberately not here. This tool gives a player somewhere to write their own picks down and
/// leaves the picking to somebody holding the rules.
/// </para>
/// <para>
/// The ids are permanent for the same reason the site kinds are: one is stored inside every saved
/// character and travels inside every shared link, so renaming one silently empties the class off
/// every sheet already written.
/// </para>
/// </remarks>
public static class CharacterClasses
{
    public const string Bard = "bard";

    public const string BoneWhisperer = "bone-whisperer";

    public const string Cleric = "cleric";

    public const string Cutthroat = "cutthroat";

    public const string Druid = "druid";

    public const string Tinkerer = "tinkerer";

    public const string Warrior = "warrior";

    public const string Wizard = "wizard";

    /// <summary>Every class, in the order the picker offers them.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Bard, BoneWhisperer, Cleric, Cutthroat, Druid, Tinkerer, Warrior, Wizard];

    /// <summary><c>true</c> for a class this tool knows the name of.</summary>
    /// <remarks>
    /// An unknown class is not an error anywhere: a sheet from a future version, or one a table has
    /// house-ruled, keeps whatever it says. This only decides whether the picker can show it.
    /// </remarks>
    public static bool IsKnown(string? id) => id is not null && All.Contains(id, StringComparer.Ordinal);
}

/// <summary>
/// The three attributes FLAIL! rolls a d20 at or under.
/// </summary>
/// <remarks>
/// Held as ids rather than as three named properties so the sheet can draw them in a loop and label
/// them from <c>ui.json</c>, the way every other field in this tool is labelled.
/// </remarks>
public static class CharacterAttributes
{
    public const string Strength = "str";

    public const string Dexterity = "dex";

    public const string Luck = "luck";

    public static IReadOnlyList<string> All { get; } = [Strength, Dexterity, Luck];

    public static bool IsKnown(string? id) => id is not null && All.Contains(id, StringComparer.Ordinal);
}

/// <summary>
/// Where a carried thing sits: in hand, worn, hung about the neck, or in the bag.
/// </summary>
/// <remarks>
/// FLAIL!'s inventory is positional rather than a weight total, so a sheet keeping one undivided
/// list of possessions would lose the part of it that matters at the table — which of them you are
/// actually holding when something goes wrong.
/// </remarks>
public static class CharacterZones
{
    public const string Hands = "hands";

    public const string Body = "body";

    public const string Adornments = "adornments";

    public const string Satchel = "satchel";

    public static IReadOnlyList<string> All { get; } = [Hands, Body, Adornments, Satchel];

    public static bool IsKnown(string? id) => id is not null && All.Contains(id, StringComparer.Ordinal);
}

/// <summary>
/// What sort of class ability an entry on the sheet is.
/// </summary>
/// <remarks>
/// <para>
/// One list of entries with a kind beside each, rather than a spellbook, a prayer list, a gadget
/// belt and an instrument rack as separate fields. Eight classes each with their own compartment
/// would be eight sections on the sheet, seven of them permanently empty, and a ninth class would
/// mean a ninth field, a ninth label and a new document version.
/// </para>
/// <para>
/// <see cref="Other"/> is the escape hatch, and it is here on purpose: a table that has house-ruled
/// something, or a class this list has not caught up with, still has somewhere to write it down.
/// </para>
/// </remarks>
public static class PowerKinds
{
    public const string Spell = "spell";

    public const string Prayer = "prayer";

    public const string Gift = "gift";

    public const string Gadget = "gadget";

    public const string Thieving = "thieving";

    public const string Instrument = "instrument";

    public const string Other = "other";

    public static IReadOnlyList<string> All { get; } =
        [Spell, Prayer, Gift, Gadget, Thieving, Instrument, Other];

    public static bool IsKnown(string? id) => id is not null && All.Contains(id, StringComparer.Ordinal);
}
