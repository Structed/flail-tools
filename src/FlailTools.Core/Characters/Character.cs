namespace FlailTools.Core.Characters;

/// <summary>
/// How much of anything a sheet will hold.
/// </summary>
/// <remarks>
/// <para>
/// Not tidiness. A character arrives from a link somebody else wrote, so every one of these is a
/// bound on untrusted input: without them a link could carry a megabyte of notes and forty thousand
/// satchel entries, and the page would dutifully try to draw all of it.
/// </para>
/// <para>
/// They are also what keeps a link paste-able. The whole sheet travels inside the address, so the
/// ceiling on what a sheet can hold is the ceiling on how long that address can get.
/// </para>
/// </remarks>
public static class CharacterLimits
{
    /// <summary>The longest a character's name, class, background or an entry's name may be.</summary>
    public const int NameLength = 64;

    /// <summary>The longest a note beside an entry may be.</summary>
    public const int NoteLength = 240;

    /// <summary>The longest the free-text areas — the background's perk, the notes — may be.</summary>
    public const int ProseLength = 2000;

    /// <summary>How many entries one list on the sheet may hold.</summary>
    public const int Entries = 40;

    /// <summary>The highest level, attribute score, and slot count a field will take.</summary>
    public const int MostLevel = 20;

    public const int MostScore = 20;

    public const int MostSlots = 20;

    /// <summary>Hit points, defence and coins, which are bounded only to keep a sheet finite.</summary>
    public const int MostHitPoints = 999;

    public const int MostDefence = 99;

    public const int MostCoins = 999999;
}

/// <summary>One thing a character is carrying.</summary>
/// <remarks>
/// <see cref="Slots"/> is the character's own bookkeeping rather than a rule this tool applies. It
/// does not stop a satchel being overfilled, because how many slots a satchel has is a question the
/// rulebook answers and this is a sheet, not a referee.
/// </remarks>
public sealed record ItemEntry
{
    public string Name { get => field ?? ""; init; } = "";

    public int Slots { get; init; }

    public string Note { get => field ?? ""; init; } = "";

    /// <summary>Whether the thing is magical, which several class abilities care about.</summary>
    public bool IsMagical { get; init; }

    /// <summary><c>true</c> when nothing has been written in it yet.</summary>
    /// <remarks>
    /// Whitespace counts as nothing, and deliberately does not wait for the trimming in
    /// <see cref="Character.Clamped"/> to make it so. A row somebody pressed the space bar in and
    /// then abandoned is still an abandoned row, and this is asked on the way into a link.
    /// </remarks>
    public bool IsBlank => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Note);

    internal ItemEntry Clamped() => this with
    {
        Name = Text.Trim(Name, CharacterLimits.NameLength),
        Note = Text.Trim(Note, CharacterLimits.NoteLength),
        Slots = Math.Clamp(Slots, 0, CharacterLimits.MostSlots)
    };
}

/// <summary>One thing a character can do: a spell, a prayer, a gadget, a talent.</summary>
public sealed record PowerEntry
{
    /// <summary>One of <see cref="PowerKinds"/>, or anything at all from a sheet this tool did not write.</summary>
    public string Kind { get => field ?? ""; init; } = "";

    public string Name { get => field ?? ""; init; } = "";

    public string Note { get => field ?? ""; init; } = "";

    public bool IsBlank => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Note);

    internal PowerEntry Clamped() => this with
    {
        Kind = Text.Trim(Kind, CharacterLimits.NameLength),
        Name = Text.Trim(Name, CharacterLimits.NameLength),
        Note = Text.Trim(Note, CharacterLimits.NoteLength)
    };
}

/// <summary>
/// A player character: everything on the sheet, and nothing about how it got there.
/// </summary>
/// <remarks>
/// <para>
/// Unlike a generated site, a character is not a seed and a handful of locks — there is no function
/// that regenerates it, because a person wrote it. So the whole thing is stored, and the whole
/// thing travels in the link.
/// </para>
/// <para>
/// Every non-nullable property coerces in its getter. This model is deserialised straight out of a
/// stranger's link by the <c>System.Text.Json</c> source generator, which discards property
/// initialisers, so an absent field arrives as <c>null</c> however it was declared.
/// </para>
/// </remarks>
public sealed record Character
{
    /// <summary>What this character will be called for the rest of its life. See <see cref="CharacterId"/>.</summary>
    public string Id { get => field ?? ""; init; } = "";

    /// <summary>
    /// How many times this sheet has been saved with something changed in it.
    /// </summary>
    /// <remarks>
    /// The whole of the answer to "a two-week-old link just ate my character". There is no server
    /// and no clock anybody trusts, so a counter that only ever goes up is what lets an arriving
    /// sheet be compared against the copy already stored and reported as older, newer or the same.
    /// </remarks>
    public int Revision { get; init; }

    public string Name { get => field ?? ""; init; } = "";

    /// <summary>One of <see cref="CharacterClasses"/>, or empty, or something this tool has never heard of.</summary>
    public string Class { get => field ?? ""; init; } = "";

    public int Level { get; init; }

    public string Background { get => field ?? ""; init; } = "";

    /// <summary>What the background lets this character do, in the player's own words.</summary>
    public string BackgroundPerk { get => field ?? ""; init; } = "";

    public int Strength { get; init; }

    public int Dexterity { get; init; }

    public int Luck { get; init; }

    public int HitPoints { get; init; }

    public int MaxHitPoints { get; init; }

    public int Defence { get; init; }

    public int Coins { get; init; }

    public IReadOnlyList<ItemEntry> Hands { get => field ?? []; init; } = [];

    public IReadOnlyList<ItemEntry> Body { get => field ?? []; init; } = [];

    public IReadOnlyList<ItemEntry> Adornments { get => field ?? []; init; } = [];

    public IReadOnlyList<ItemEntry> Satchel { get => field ?? []; init; } = [];

    /// <summary>The combat talents taken, one per level.</summary>
    public IReadOnlyList<PowerEntry> Talents { get => field ?? []; init; } = [];

    /// <summary>Spells, prayers, gifts, gadgets and the rest, each saying which it is.</summary>
    public IReadOnlyList<PowerEntry> Powers { get => field ?? []; init; } = [];

    /// <summary>Injuries, curses and anything else currently true of this character.</summary>
    public IReadOnlyList<string> Conditions { get => field ?? []; init; } = [];

    public string Notes { get => field ?? ""; init; } = "";

    /// <summary>A blank sheet with an id of its own.</summary>
    /// <remarks>
    /// The attributes start at 10 rather than at zero. A zero is not a neutral placeholder in a game
    /// that rolls at or under an attribute — it is a character who fails everything — and a sheet
    /// that opens in that state reads as broken rather than as empty.
    /// </remarks>
    public static Character Create() => new()
    {
        Id = CharacterId.Create(),
        Level = 1,
        Strength = 10,
        Dexterity = 10,
        Luck = 10
    };

    /// <summary>The same character with every field inside the bounds a sheet will hold.</summary>
    /// <remarks>
    /// Applied to anything arriving from outside this browser — a link, a dropped file, a storage
    /// key somebody has edited by hand — rather than trusted to have come from this tool.
    /// </remarks>
    public Character Clamped() => this with
    {
        Id = CharacterId.IsValid(Id) ? Id : CharacterId.Create(),
        Revision = Math.Max(Revision, 0),
        Name = Text.Trim(Name, CharacterLimits.NameLength),
        Class = Text.Trim(Class, CharacterLimits.NameLength),
        Level = Math.Clamp(Level, 0, CharacterLimits.MostLevel),
        Background = Text.Trim(Background, CharacterLimits.NameLength),
        BackgroundPerk = Text.Trim(BackgroundPerk, CharacterLimits.ProseLength),
        Strength = Math.Clamp(Strength, 0, CharacterLimits.MostScore),
        Dexterity = Math.Clamp(Dexterity, 0, CharacterLimits.MostScore),
        Luck = Math.Clamp(Luck, 0, CharacterLimits.MostScore),
        HitPoints = Math.Clamp(HitPoints, 0, CharacterLimits.MostHitPoints),
        MaxHitPoints = Math.Clamp(MaxHitPoints, 0, CharacterLimits.MostHitPoints),
        Defence = Math.Clamp(Defence, 0, CharacterLimits.MostDefence),
        Coins = Math.Clamp(Coins, 0, CharacterLimits.MostCoins),
        Hands = Clamp(Hands),
        Body = Clamp(Body),
        Adornments = Clamp(Adornments),
        Satchel = Clamp(Satchel),
        Talents = Clamp(Talents),
        Powers = Clamp(Powers),
        Conditions = [.. Conditions.Take(CharacterLimits.Entries)
            .Select(condition => Text.Trim(condition, CharacterLimits.NameLength))],
        Notes = Text.Trim(Notes, CharacterLimits.ProseLength)
    };

    /// <summary>
    /// The same character with the rows nobody wrote anything in taken out.
    /// </summary>
    /// <remarks>
    /// Applied on the way into storage rather than as you type, so that an empty row added to be
    /// typed into stays put while the sheet is open. Without it, a sheet somebody clicked Add on
    /// twice and thought better of carries two empty rows into every link it is ever shared with,
    /// and they come back as two empty rows on the recipient's screen.
    /// </remarks>
    public Character Tidied() => this with
    {
        Hands = Tidy(Hands),
        Body = Tidy(Body),
        Adornments = Tidy(Adornments),
        Satchel = Tidy(Satchel),
        Talents = Tidy(Talents),
        Powers = Tidy(Powers),
        Conditions = [.. Conditions.Where(condition => !string.IsNullOrWhiteSpace(condition))]
    };

    /// <summary>The items in one of <see cref="CharacterZones"/>, or nothing for an unknown zone.</summary>
    public IReadOnlyList<ItemEntry> Zone(string zone) => zone switch
    {
        CharacterZones.Hands => Hands,
        CharacterZones.Body => Body,
        CharacterZones.Adornments => Adornments,
        CharacterZones.Satchel => Satchel,
        _ => []
    };

    /// <summary>The same character with one zone replaced.</summary>
    public Character WithZone(string zone, IReadOnlyList<ItemEntry> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return zone switch
        {
            CharacterZones.Hands => this with { Hands = items },
            CharacterZones.Body => this with { Body = items },
            CharacterZones.Adornments => this with { Adornments = items },
            CharacterZones.Satchel => this with { Satchel = items },
            _ => this
        };
    }

    /// <summary>One of the three attribute scores, by id.</summary>
    public int Score(string attribute) => attribute switch
    {
        CharacterAttributes.Strength => Strength,
        CharacterAttributes.Dexterity => Dexterity,
        CharacterAttributes.Luck => Luck,
        _ => 0
    };

    /// <summary>The same character with one attribute score replaced.</summary>
    public Character WithScore(string attribute, int score)
    {
        int held = Math.Clamp(score, 0, CharacterLimits.MostScore);

        return attribute switch
        {
            CharacterAttributes.Strength => this with { Strength = held },
            CharacterAttributes.Dexterity => this with { Dexterity = held },
            CharacterAttributes.Luck => this with { Luck = held },
            _ => this
        };
    }

    private static IReadOnlyList<ItemEntry> Clamp(IReadOnlyList<ItemEntry> items) =>
        [.. items.Take(CharacterLimits.Entries).Select(item => item.Clamped())];

    private static IReadOnlyList<PowerEntry> Clamp(IReadOnlyList<PowerEntry> powers) =>
        [.. powers.Take(CharacterLimits.Entries).Select(power => power.Clamped())];

    private static IReadOnlyList<ItemEntry> Tidy(IReadOnlyList<ItemEntry> items) =>
        [.. items.Where(item => !item.IsBlank)];

    private static IReadOnlyList<PowerEntry> Tidy(IReadOnlyList<PowerEntry> powers) =>
        [.. powers.Where(power => !power.IsBlank)];
}

/// <summary>Trimming, in the one place both the entries and the sheet can reach it.</summary>
internal static class Text
{
    /// <summary>
    /// A field cut to length, with its surrounding whitespace gone.
    /// </summary>
    /// <remarks>
    /// Trimmed before cutting rather than after, so a field that is nothing but spaces comes back
    /// empty instead of coming back as a shorter run of spaces.
    /// </remarks>
    internal static string Trim(string? value, int longest)
    {
        string trimmed = (value ?? "").Trim();

        return trimmed.Length <= longest ? trimmed : trimmed[..longest];
    }
}
