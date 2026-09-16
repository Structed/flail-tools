namespace FlailTools.Core.Model;

/// <summary>
/// One rolled axis: the path it was rolled under and the words that came out.
/// </summary>
/// <remarks>
/// <para>
/// A flat, ordered list of these is the whole of a site's rolled detail, rather than a differently
/// shaped record per kind. The five generators differ only in which paths they roll, so giving each
/// its own record would buy nothing but five near-identical branches in every consumer — and the
/// interface would need a case for each in order to draw a row with a label, a lock and a re-roll
/// button.
/// </para>
/// <para>
/// The properties coerce null in their getters like the data models do, because a site travels
/// inside an exported document and comes back through the same source-generated deserialiser.
/// </para>
/// </remarks>
public sealed record SiteField
{
    public string Path { get => field ?? ""; init; } = "";

    public string Value { get => field ?? ""; init; } = "";
}

/// <summary>
/// One numbered part of a site: a dungeon room, a cave chamber or a tower floor.
/// </summary>
/// <remarks>
/// <see cref="Number"/> is one-based because it is the number printed on the map and read aloud at
/// the table. The zero-based index in the field path is a separate thing and deliberately so: the
/// path is machinery, the number is user-facing, and conflating them would mean a change to either
/// silently moved the other.
/// </remarks>
public sealed record SiteArea
{
    /// <summary>The number shown on the map and in the legend, counting from one.</summary>
    public int Number { get; init; }

    /// <summary>The field path this area's contents were rolled under.</summary>
    public string Path { get => field ?? ""; init; } = "";

    /// <summary>What this area is structurally: see <see cref="AreaRoles"/>.</summary>
    public string Role { get => field ?? ""; init; } = "";

    /// <summary>What is in it.</summary>
    public string Value { get => field ?? ""; init; } = "";
}

/// <summary>
/// The structural parts a site's areas can play.
/// </summary>
/// <remarks>
/// These are ids, not words. What each one is called in the interface lives in <c>ui.json</c>, so
/// the labels can be rewritten without touching anything a seed depends on.
/// </remarks>
public static class AreaRoles
{
    public const string Plain = "plain";

    /// <summary>A dungeon's area 1.</summary>
    public const string Entrance = "entrance";

    /// <summary>A dungeon's area 10, where it is meant to end.</summary>
    public const string Finale = "finale";

    /// <summary>The cave die that landed nearest the edge of the page.</summary>
    public const string Entry = "entry";

    /// <summary>The cave die that landed nearest the centre.</summary>
    public const string Core = "core";

    /// <summary>A cave die that landed off the paper.</summary>
    public const string Hidden = "hidden";

    /// <summary>A cave die sharing its number with an adjacent one.</summary>
    public const string Cluster = "cluster";

    /// <summary>The d4 on top of the stack.</summary>
    public const string Top = "top";
}

/// <summary>
/// A generated adventure site.
/// </summary>
/// <remarks>
/// "Adventure Site" is FLAIL!'s umbrella noun for the five things this generates. Its structure and
/// its procedures are the game's, which the Third-Party Licence permits us to implement; every word
/// that fills them is ours.
/// </remarks>
public sealed record AdventureSite
{
    public string Name { get => field ?? ""; init; } = "";

    /// <summary>One of <see cref="Data.SiteKinds"/>.</summary>
    public string Kind { get => field ?? ""; init; } = "";

    /// <summary>The id of the silhouette row that was chosen.</summary>
    public string Silhouette { get => field ?? ""; init; } = "";

    /// <summary>The engine archetype the map is laid out inside.</summary>
    public string Shape { get => field ?? ""; init; } = "";

    public bool HasWater { get; init; }

    /// <summary>
    /// How big the map is drawn, 1 to 6.
    /// </summary>
    /// <remarks>
    /// Derived from the kind's own procedure — a dungeon's room count, a tower's floors — rather
    /// than rolled on an axis of its own. FLAIL! has no size axis, and inventing one would put a
    /// field in the interface that does not answer to anything in the book.
    /// </remarks>
    public int Scale { get => field is >= 1 and <= 6 ? field : 3; init; } = 3;

    /// <summary>The rolled axes, in the order they are shown.</summary>
    public IReadOnlyList<SiteField> Fields { get => field ?? []; init; } = [];

    /// <summary>The numbered parts, empty for the two hexcrawl kinds.</summary>
    public IReadOnlyList<SiteArea> Areas { get => field ?? []; init; } = [];

    /// <summary>
    /// What each field would be locked to, keyed by path.
    /// </summary>
    /// <remarks>
    /// Recorded as the roll happened, because only the generator knows which row of which table a
    /// value came from. Locking stores that position rather than the words.
    /// </remarks>
    public IReadOnlyDictionary<string, string> PinValues
    {
        get => field ?? new Dictionary<string, string>(StringComparer.Ordinal);
        init;
    } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The value rolled for a path, or an empty string if that path was not rolled.</summary>
    public string ValueOf(string path) =>
        Fields.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.Ordinal))?.Value ?? "";

    /// <summary><c>true</c> when nothing rolled produced any words, because the tables are still empty.</summary>
    public bool IsBlank =>
        string.IsNullOrWhiteSpace(Name) &&
        Fields.All(f => string.IsNullOrWhiteSpace(f.Value));
}
