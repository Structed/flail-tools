namespace FlailTools.Core.Characters;

/// <summary>What an arriving sheet is, compared with the copy this browser already holds.</summary>
public enum RosterMatch
{
    /// <summary>Nothing stored under this id. Saving adds a character.</summary>
    Unknown,

    /// <summary>The stored copy has been saved since this link was made. Saving loses that work.</summary>
    Older,

    /// <summary>The same revision as the stored copy. Saving changes nothing.</summary>
    Same,

    /// <summary>Newer than the stored copy. Saving brings this browser up to date.</summary>
    Newer
}

/// <summary>
/// The characters this browser is keeping.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, and every change hands back a new roster, so a page can hold one and the thing on
/// screen is always exactly what was last written to storage. A mutable collection would let the
/// list on screen quietly disagree with the key underneath it after a failed write.
/// </para>
/// <para>
/// Ordered by name rather than by when anything was last touched. A party list that rearranges
/// itself each time somebody edits a sheet is a list you have to re-read every time you open it,
/// and there is no clock here worth sorting by anyway.
/// </para>
/// </remarks>
public sealed class CharacterRoster
{
    private CharacterRoster(IReadOnlyList<Character> characters) => Characters = characters;

    /// <summary>A browser that is keeping nobody.</summary>
    public static CharacterRoster Empty { get; } = new([]);

    /// <summary>
    /// A roster of whatever these are, bounded, de-duplicated and in order.
    /// </summary>
    /// <remarks>
    /// Two characters sharing an id should be impossible — saving replaces by id — but this reads
    /// storage that anything may have written, and of the two the one saved more recently is the
    /// better guess at what its owner wanted.
    /// </remarks>
    public static CharacterRoster Of(IEnumerable<Character> characters)
    {
        ArgumentNullException.ThrowIfNull(characters);

        return new CharacterRoster(
        [
            .. characters
                .Select(character => character.Clamped())
                .GroupBy(character => character.Id, StringComparer.Ordinal)
                .Select(sharing => sharing.MaxBy(character => character.Revision)!)
                .OrderBy(character => character.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(character => character.Id, StringComparer.Ordinal)
        ]);
    }

    public IReadOnlyList<Character> Characters { get; }

    public int Count => Characters.Count;

    public Character? Find(string? id) => id is null
        ? null
        : Characters.FirstOrDefault(character => string.Equals(character.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// The roster with this character in it, replacing whatever shared its id.
    /// </summary>
    /// <remarks>
    /// Stores exactly what it is handed, revision included. Bumping here would be the obvious place
    /// for it and the wrong one: saving a received sheet unaltered would make this browser's copy
    /// look newer than the one its author is still editing, and sending it back would then offer to
    /// overwrite their work with their own sheet.
    /// </remarks>
    public CharacterRoster Save(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        Character held = character.Clamped();

        return Of([held, .. Characters.Where(other =>
            !string.Equals(other.Id, held.Id, StringComparison.Ordinal))]);
    }

    public CharacterRoster Remove(string? id) => id is null
        ? this
        : Of(Characters.Where(character => !string.Equals(character.Id, id, StringComparison.Ordinal)));

    /// <summary>How an arriving sheet stands against the copy already held.</summary>
    public RosterMatch Compare(Character incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        Character? stored = Find(incoming.Id);

        if (stored is null)
        {
            return RosterMatch.Unknown;
        }

        return incoming.Revision.CompareTo(stored.Revision) switch
        {
            > 0 => RosterMatch.Newer,
            < 0 => RosterMatch.Older,
            _ => RosterMatch.Same
        };
    }
}
