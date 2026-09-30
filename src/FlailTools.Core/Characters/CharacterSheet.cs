using FlailTools.Core.Serialization;

namespace FlailTools.Core.Characters;

/// <summary>Which of the two entry lists on a sheet is being worked on.</summary>
/// <remarks>
/// Talents and powers are the same shape — a kind, a name, a note — and are kept as two lists
/// rather than one so a sheet can show combat talents beside their levels and everything else
/// grouped by what it is.
/// </remarks>
public static class CharacterLists
{
    public const string Talents = "talents";

    public const string Powers = "powers";

    public static IReadOnlyList<string> All { get; } = [Talents, Powers];
}

/// <summary>
/// One sheet being filled in: the character, and every way somebody changes it.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart of <see cref="Generation.SiteWorkspace"/>, and here for the same reason: this is
/// the whole of the interface's behaviour, and it lives in Core so it can be tested without a
/// browser. A page holds one of these and draws it; every control is one call.
/// </para>
/// <para>
/// Not a record and not immutable, because it models a thing being filled in. The
/// <see cref="Character"/> inside it is immutable, which is what matters: every change replaces it
/// whole, so what is on screen is always exactly what a link or a save would write down.
/// </para>
/// </remarks>
public sealed class CharacterSheet
{
    public CharacterSheet(Character? character = null) =>
        Character = (character ?? Character.Create()).Clamped();

    public Character Character { get; private set; }

    /// <summary>The sheet as it would be written down: trimmed, and blank rows dropped.</summary>
    /// <remarks>
    /// Clamping again here is safe and deliberate. Everything that reaches
    /// <see cref="Character"/> has already been clamped once, so the id is known good and the one
    /// non-deterministic thing <see cref="Characters.Character.Clamped"/> does — minting a fresh id
    /// for an unreadable one — cannot fire. What is left is the trimming, which is wanted on the
    /// way out and unwanted mid-edit.
    /// </remarks>
    public Character Tidied => Character.Clamped().Tidied();

    /// <summary>Replaces the whole sheet, as when a link or a file is opened.</summary>
    public void Apply(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        Character = character.Clamped();
    }

    public void Rename(string? name) => Character = Character with { Name = Field(name) };

    public void SetClass(string? id) => Character = Character with { Class = Field(id) };

    public void SetLevel(int level) =>
        Character = Character with { Level = Math.Clamp(level, 0, CharacterLimits.MostLevel) };

    public void SetBackground(string? background) =>
        Character = Character with { Background = Field(background) };

    public void SetBackgroundPerk(string? perk) =>
        Character = Character with { BackgroundPerk = Prose(perk) };

    public void SetScore(string attribute, int score) => Character = Character.WithScore(attribute, score);

    public void SetHitPoints(int hitPoints) =>
        Character = Character with { HitPoints = Math.Clamp(hitPoints, 0, CharacterLimits.MostHitPoints) };

    public void SetMaxHitPoints(int hitPoints) =>
        Character = Character with { MaxHitPoints = Math.Clamp(hitPoints, 0, CharacterLimits.MostHitPoints) };

    public void SetDefence(int defence) =>
        Character = Character with { Defence = Math.Clamp(defence, 0, CharacterLimits.MostDefence) };

    public void SetCoins(int coins) =>
        Character = Character with { Coins = Math.Clamp(coins, 0, CharacterLimits.MostCoins) };

    public void SetNotes(string? notes) => Character = Character with { Notes = Prose(notes) };

    /// <summary>The items in one zone of the inventory.</summary>
    public IReadOnlyList<ItemEntry> Items(string zone) => Character.Zone(zone);

    /// <summary>Adds an empty row to a zone, for somebody to type into.</summary>
    public void AddItem(string zone)
    {
        IReadOnlyList<ItemEntry> items = Character.Zone(zone);

        if (items.Count >= CharacterLimits.Entries)
        {
            return;
        }

        Character = Character.WithZone(zone, [.. items, new ItemEntry()]);
    }

    public void RemoveItem(string zone, int index)
    {
        IReadOnlyList<ItemEntry> items = Character.Zone(zone);

        if (index < 0 || index >= items.Count)
        {
            return;
        }

        Character = Character.WithZone(zone, [.. items.Where((_, at) => at != index)]);
    }

    /// <summary>Replaces one row of a zone with an edited copy of it.</summary>
    public void SetItem(string zone, int index, ItemEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);

        IReadOnlyList<ItemEntry> items = Character.Zone(zone);

        if (index < 0 || index >= items.Count)
        {
            return;
        }

        Character = Character.WithZone(
            zone,
            [.. items.Select((held, at) => at == index ? item.Clamped() : held)]);
    }

    /// <summary>The entries in one of <see cref="CharacterLists"/>.</summary>
    public IReadOnlyList<PowerEntry> Entries(string list) => list switch
    {
        CharacterLists.Talents => Character.Talents,
        CharacterLists.Powers => Character.Powers,
        _ => []
    };

    /// <summary>Adds an empty entry of a given kind.</summary>
    public void AddEntry(string list, string kind = "")
    {
        IReadOnlyList<PowerEntry> entries = Entries(list);

        if (entries.Count >= CharacterLimits.Entries)
        {
            return;
        }

        Replace(list, [.. entries, new PowerEntry { Kind = kind }]);
    }

    public void RemoveEntry(string list, int index)
    {
        IReadOnlyList<PowerEntry> entries = Entries(list);

        if (index < 0 || index >= entries.Count)
        {
            return;
        }

        Replace(list, [.. entries.Where((_, at) => at != index)]);
    }

    public void SetEntry(string list, int index, PowerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        IReadOnlyList<PowerEntry> entries = Entries(list);

        if (index < 0 || index >= entries.Count)
        {
            return;
        }

        Replace(list, [.. entries.Select((held, at) => at == index ? entry.Clamped() : held)]);
    }

    public void AddCondition()
    {
        if (Character.Conditions.Count >= CharacterLimits.Entries)
        {
            return;
        }

        Character = Character with { Conditions = [.. Character.Conditions, ""] };
    }

    public void RemoveCondition(int index)
    {
        if (index < 0 || index >= Character.Conditions.Count)
        {
            return;
        }

        Character = Character with
        {
            Conditions = [.. Character.Conditions.Where((_, at) => at != index)]
        };
    }

    public void SetCondition(int index, string? condition)
    {
        if (index < 0 || index >= Character.Conditions.Count)
        {
            return;
        }

        Character = Character with
        {
            Conditions =
            [
                .. Character.Conditions.Select((held, at) => at == index ? Field(condition) : held)
            ]
        };
    }

    /// <summary>The query string that carries this sheet to somebody else.</summary>
    public string Query => CharacterLink.ToQuery(Tidied);

    /// <summary>The short query naming this character in this browser's own storage.</summary>
    public string IdQuery => CharacterLink.ToIdQuery(Character.Id);

    public string FileName => CharacterDocuments.FileName(Character);

    public string ToDocument() => CharacterDocuments.Write(Tidied);

    /// <summary><c>true</c> when this sheet says exactly what the stored copy says.</summary>
    public bool Matches(Character? stored) =>
        stored is not null && CharacterDocuments.SameContent(Tidied, stored);

    /// <summary>
    /// The sheet as it should be written down, given what is already stored under its id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The revision only moves when something has actually changed, and this is the one place that
    /// decides it. Bumping on every press would make a reader who opened a sheet and pressed Save
    /// out of habit look, to everybody they had sent it to, like somebody with newer work — and the
    /// stale warning that protects their party would start crying wolf.
    /// </para>
    /// <para>
    /// The new revision is one past the higher of the two, not one past this sheet's own. A sheet
    /// opened from an old link and then edited has to land <em>above</em> the copy already stored,
    /// or saving it would produce a character that claims to be older than the thing it just
    /// replaced.
    /// </para>
    /// </remarks>
    public Character ForSaving(Character? stored)
    {
        Character tidied = Tidied;

        if (stored is not null && CharacterDocuments.SameContent(tidied, stored))
        {
            return stored;
        }

        return tidied with
        {
            Revision = Math.Max(tidied.Revision, stored?.Revision ?? 0) + 1
        };
    }

    private void Replace(string list, IReadOnlyList<PowerEntry> entries) => Character = list switch
    {
        CharacterLists.Talents => Character with { Talents = entries },
        CharacterLists.Powers => Character with { Powers = entries },
        _ => Character
    };

    /// <summary>
    /// A typed field, cut to length but not yet trimmed of its spaces.
    /// </summary>
    /// <remarks>
    /// Trimming as somebody types would delete the space they just pressed in the middle of
    /// "Bramble of the". The trimming happens on the way into storage and into a link, where
    /// nobody's cursor is in the field.
    /// </remarks>
    private static string Field(string? value) => Cut(value, CharacterLimits.NameLength);

    private static string Prose(string? value) => Cut(value, CharacterLimits.ProseLength);

    private static string Cut(string? value, int longest)
    {
        string held = value ?? "";

        return held.Length <= longest ? held : held[..longest];
    }
}
