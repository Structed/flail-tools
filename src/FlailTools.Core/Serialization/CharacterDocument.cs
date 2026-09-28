using System.Text.Json;
using FlailTools.Core.Characters;
using Structed.Inkwell.Serialization;

namespace FlailTools.Core.Serialization;

/// <summary>
/// One character, written down.
/// </summary>
/// <remarks>
/// The opposite of <see cref="SiteDocument"/> in the one way that matters: a site is a seed and a
/// handful of locks because generation rebuilds it, and a character is the whole sheet because
/// nothing can rebuild a person's decisions. So there is no plan here, only the thing itself.
/// </remarks>
public sealed record CharacterDocument : IGeneratedDocument
{
    /// <summary>
    /// The format id, deliberately nullable.
    /// </summary>
    /// <remarks>
    /// A file that does not say what it is must be rejected rather than hopefully parsed, and a
    /// coerced default would take that ability away. It matters more here than for a site, because
    /// both kinds of file end in <c>.flail.json</c> and a reader may well drop the wrong one in.
    /// </remarks>
    public string? Format { get; init; }

    public int Version { get; init; }

    public Character Character { get => field ?? new Character(); init; } = new();
}

/// <summary>
/// Every character this browser is keeping.
/// </summary>
/// <remarks>
/// One document rather than a file per character, because it is held in a single storage key. A key
/// per character plus an index would be two things to keep in step, and a browser that cleared one
/// of them — a quota refusal part-way through a save — would leave an index pointing at characters
/// that are not there.
/// </remarks>
public sealed record PartyDocument : IGeneratedDocument
{
    public string? Format { get; init; }

    public int Version { get; init; }

    public IReadOnlyList<Character> Characters { get => field ?? []; init; } = [];
}

/// <summary>Reads and writes one character.</summary>
public static class CharacterDocuments
{
    public const string FormatId = "flail-tools/character";

    public const int CurrentVersion = 1;

    /// <summary>
    /// The same extension a saved site uses.
    /// </summary>
    /// <remarks>
    /// Shared on purpose: to a reader these are both "a FLAIL! Tools file", and inventing a second
    /// extension would ask them to remember which is which. Telling them apart is
    /// <see cref="Matches"/>'s job, which reads what the file says it is rather than what it is
    /// called.
    /// </remarks>
    public const string FileExtension = SiteDocuments.FileExtension;

    public static string Write(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        CharacterDocument document = new()
        {
            Format = FormatId,
            Version = CurrentVersion,
            Character = character
        };

        return JsonSerializer.Serialize(document, DocumentJsonContext.Default.CharacterDocument);
    }

    /// <summary>Reads a saved character back, bounded.</summary>
    /// <exception cref="DocumentFormatException">The file is not one of ours, or is too new.</exception>
    public static Character Read(string json) =>
        DocumentEnvelope.Read(
            json,
            FormatId,
            CurrentVersion,
            DocumentJsonContext.Default.CharacterDocument)
        .Character
        .Clamped();

    /// <summary><c>true</c> when a pasted or dropped file is a character rather than a site.</summary>
    public static bool Matches(string json) => DocumentEnvelope.Matches(json, FormatId);

    /// <summary>What to call the downloaded file.</summary>
    public static string FileName(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        return DocumentEnvelope.Slug(character.Name, "character") + FileExtension;
    }

    /// <summary>
    /// <c>true</c> when two sheets say the same thing, whatever their revisions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Compared as written down rather than field by field, and that is the point of it. A record's
    /// own equality is no use here — the lists inside a character compare by reference, so two
    /// identical sheets read out of two places are never equal — and a hand-written comparison
    /// would have to be remembered every time a property is added to the sheet, which is exactly
    /// the sort of thing that gets forgotten and then silently stops bumping the revision.
    /// </para>
    /// <para>
    /// This is what decides whether pressing Save counts as a change, so being wrong in that
    /// direction is not cosmetic: a sheet that stopped bumping its revision would be shared as
    /// though it were still the version the other player already has.
    /// </para>
    /// </remarks>
    public static bool SameContent(Character left, Character right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return string.Equals(
            Write(left with { Revision = 0 }),
            Write(right with { Revision = 0 }),
            StringComparison.Ordinal);
    }
}

/// <summary>Reads and writes the whole of what this browser is keeping.</summary>
public static class PartyDocuments
{
    public const string FormatId = "flail-tools/party";

    public const int CurrentVersion = 1;

    /// <summary>
    /// The storage key the roster lives under.
    /// </summary>
    /// <remarks>
    /// Named in the same shape as the dice table's <c>flail.dice.*</c> keys. Changing it does not
    /// lose anybody's characters so much as hide them: the old key stays in the browser, holding
    /// everything, with nothing left that reads it.
    /// </remarks>
    public const string StorageKey = "flail.party.characters";

    public static string Write(CharacterRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        PartyDocument document = new()
        {
            Format = FormatId,
            Version = CurrentVersion,
            Characters = roster.Characters
        };

        return JsonSerializer.Serialize(document, DocumentJsonContext.Default.PartyDocument);
    }

    /// <summary>
    /// Reads the roster back, taking whatever of it makes sense.
    /// </summary>
    /// <remarks>
    /// Nothing here throws, which is a deliberate difference from reading a file. A file is
    /// something a reader chose and can choose again; this is the contents of their browser, and
    /// the useful response to a key that has been truncated, hand-edited or written by a much older
    /// version is the characters that are still legible — not a tool that will not open.
    /// </remarks>
    public static CharacterRoster Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CharacterRoster.Empty;
        }

        try
        {
            PartyDocument document = DocumentEnvelope.Read(
                json,
                FormatId,
                CurrentVersion,
                DocumentJsonContext.Default.PartyDocument);

            return CharacterRoster.Of(document.Characters);
        }
        catch (DocumentFormatException)
        {
            return CharacterRoster.Empty;
        }
        catch (JsonException)
        {
            return CharacterRoster.Empty;
        }
    }
}
