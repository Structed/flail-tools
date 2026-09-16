namespace FlailTools.Core.Data;

/// <summary>The kinds of adventure site this tool generates.</summary>
/// <remarks>
/// <para>
/// "Adventure Site" is FLAIL!'s own umbrella noun and is used here deliberately. The Games
/// Omnivorous Third-Party Licence permits reusing its terminology and its random tables alike, and
/// the tables filling these axes are the book's own.
/// </para>
/// <para>
/// The five ids are permanent. Each one is a field-path segment, so renaming one would change what
/// every existing seed rolls and orphan every lock saved against it.
/// </para>
/// </remarks>
public static class SiteKinds
{
    public const string Dungeon = "dungeon";

    public const string Cave = "cave";

    public const string Tower = "tower";

    /// <summary>The hexcrawl Locations generator. Named for its chapter, not for the umbrella.</summary>
    public const string Location = "location";

    /// <summary>The hexcrawl Landmarks generator.</summary>
    public const string Landmark = "landmark";

    public static IReadOnlyList<string> All { get; } = [Dungeon, Cave, Tower, Location, Landmark];

    public static bool IsKnown(string? kind) => kind is not null && All.Contains(kind, StringComparer.Ordinal);
}

/// <summary>The site kinds and the two halves a site name is built from.</summary>
public sealed record SiteFile : DataFile
{
    public IReadOnlyList<KindRow> Kinds { get => field ?? []; init; } = [];

    /// <summary>
    /// Name stems and tails, kept as two tables rather than one of whole names so each half pins by
    /// position. FLAIL! supplies no name tables; these are entirely ours.
    /// </summary>
    public IReadOnlyList<string> NameStems { get => field ?? []; init; } = [];

    public IReadOnlyList<string> NameTails { get => field ?? []; init; } = [];
}

/// <summary>Which engine archetype each kind of site is drawn inside.</summary>
public sealed record SilhouettesFile : DataFile
{
    public IReadOnlyList<SilhouetteRow> Silhouettes { get => field ?? []; init; } = [];
}

/// <summary>
/// The Dungeons generator: five d6 axes, plus the room-stocking procedure.
/// </summary>
/// <remarks>
/// The axes and their rows are FLAIL!'s, which the licence permits reusing. The stocking checklist
/// the book uses is credited in-book to the Goblin Punch blog, so that credit is passed on here
/// rather than stopping at Games Omnivorous.
/// </remarks>
public sealed record DungeonFile : DataFile
{
    public IReadOnlyList<string> Flavours { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Types { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Locations { get => field ?? []; init; } = [];

    public IReadOnlyList<string> KeyFeatures { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Creatures { get => field ?? []; init; } = [];

    /// <summary>
    /// The keying concepts an area is stocked with.
    /// </summary>
    /// <remarks>
    /// Not a die table. FLAIL! lists nine concepts and says to stock each area with one of them,
    /// "repeating the one that feels more appropriate", so this is picked across rather than rolled
    /// on — a d6 over nine rows would make the last three unreachable.
    /// </remarks>
    public IReadOnlyList<string> Stocking { get => field ?? []; init; } = [];
}

/// <summary>
/// The Caves generator: the same five axes as dungeons, over different tables, plus the dice drop.
/// </summary>
/// <remarks>
/// Deliberately <em>not</em> sharing paths with <see cref="DungeonFile"/> even though the axis
/// names match. A pin stores a table position, so sharing a path between two different tables would
/// let a lock taken on a dungeon resolve silently against a cave's rows.
/// </remarks>
public sealed record CaveFile : DataFile
{
    public IReadOnlyList<string> Flavours { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Types { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Locations { get => field ?? []; init; } = [];

    public IReadOnlyList<string> KeyFeatures { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Creatures { get => field ?? []; init; } = [];

    /// <summary>The six d6 chamber outcomes, in face order.</summary>
    public IReadOnlyList<string> Chambers { get => field ?? []; init; } = [];
}

/// <summary>The Wizard Towers generator: four d10 axes, plus the stacked-dice floor procedure.</summary>
public sealed record TowerFile : DataFile
{
    public IReadOnlyList<string> Shapes { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Occupants { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Reactions { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Goals { get => field ?? []; init; } = [];

    /// <summary>The six d6 floor outcomes, in face order. One die per floor below the top.</summary>
    public IReadOnlyList<string> FloorTypes { get => field ?? []; init; } = [];

    /// <summary>
    /// The four d4 outcomes for each floor kind, in the same order as <see cref="FloorTypes"/>.
    /// </summary>
    /// <remarks>
    /// A floor is two rolls in FLAIL!, not one: the d6 says what kind of floor it is and a d4 says
    /// which one. Held as six rows of four so the pairing cannot drift — row <c>n</c> here belongs
    /// to face <c>n</c> there.
    /// </remarks>
    public IReadOnlyList<IReadOnlyList<string>> FloorDetails { get => field ?? []; init; } = [];

    /// <summary>The four d4 outcomes for the top floor, in face order.</summary>
    public IReadOnlyList<string> TopFloorTypes { get => field ?? []; init; } = [];
}

/// <summary>The hexcrawl Locations generator: five d20 axes.</summary>
public sealed record HexLocationFile : DataFile
{
    /// <summary>The generator's first axis, which the book also calls "Location".</summary>
    public IReadOnlyList<string> Locations { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Biomes { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Conditions { get => field ?? []; init; } = [];

    public IReadOnlyList<string> KeyFeatures { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Occupants { get => field ?? []; init; } = [];
}

/// <summary>
/// The hexcrawl Landmarks generator: five d20 axes.
/// </summary>
/// <remarks>
/// Its Biome, Condition, Key Feature and Occupant axes are kept on their own paths and their own
/// tables rather than shared with <see cref="HexLocationFile"/>. Sharing would be convenient and is
/// irreversible: once a path is published, discovering that the two generators want different rows
/// cannot be fixed without orphaning every lock already saved.
/// </remarks>
public sealed record HexLandmarkFile : DataFile
{
    /// <summary>The generator's first axis, which the book also calls "Landmark".</summary>
    public IReadOnlyList<string> Landmarks { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Biomes { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Conditions { get => field ?? []; init; } = [];

    public IReadOnlyList<string> KeyFeatures { get => field ?? []; init; } = [];

    public IReadOnlyList<string> Occupants { get => field ?? []; init; } = [];
}
