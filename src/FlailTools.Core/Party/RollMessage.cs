using System.Text.Json;
using System.Text.Json.Serialization;
using FlailTools.Core.Dice;

namespace FlailTools.Core.Party;

/// <summary>
/// One roll, as it travels between players.
/// </summary>
/// <remarks>
/// <para>
/// This is the entire protocol. There is no chat, no presence beyond a name, no way to push a page
/// or a site or a file at anybody: one type, one direction, and a strict reader. A channel that can
/// only carry dice cannot be talked into carrying anything else, and since these messages arrive
/// from other people's browsers over a link this code did not set up, that narrowness is the
/// security model rather than a simplification of it.
/// </para>
/// <para>
/// A <see cref="Secret"/> message is the ghost of a roll: it says that somebody rolled and says
/// nothing else. The dice are not sent encrypted or hidden for later — they are never sent. A secret
/// the wire never carried cannot be uncovered by anyone patient.
/// </para>
/// </remarks>
public sealed record RollMessage
{
    /// <summary>The only version this code speaks.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The longest JSON this reader will consider.</summary>
    /// <remarks>
    /// A hundred dice, a name and a notation come to a few hundred bytes. Four kilobytes is room to
    /// be wrong in, and a firm answer to anybody who would rather send a novel.
    /// </remarks>
    public const int MaximumBytes = 4096;

    /// <summary>The longest name that will be shown.</summary>
    public const int MaximumNameLength = 24;

    /// <summary>The most dice one message may describe.</summary>
    public const int MaximumDice = 100;

    /// <summary>The longest notation, reading key or roll id that will be accepted.</summary>
    public const int MaximumTextLength = 64;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>Identifies the roll, so a replayed history does not show it twice.</summary>
    public string Id { get => field ?? ""; init; } = "";

    /// <summary>Who rolled, as they chose to be called.</summary>
    public string Player { get => field ?? ""; init; } = "";

    /// <summary><c>true</c> for a roll the party is told about but not shown.</summary>
    public bool Secret { get; init; }

    /// <summary>The canonical notation, or empty for a secret roll.</summary>
    public string Notation { get => field ?? ""; init; } = "";

    public IReadOnlyList<int> Faces { get => field ?? []; init; } = [];

    /// <summary>Which faces counted, matched to <see cref="Faces"/> by position.</summary>
    public IReadOnlyList<bool> Kept { get => field ?? []; init; } = [];

    public int Total { get; init; }

    public uint Seed { get; init; }

    /// <summary>Names the line of wording that reads this roll, if a preset made it.</summary>
    public string ReadingKey { get => field ?? ""; init; } = "";

    public int ReadingValue { get; init; }

    /// <summary>When it was rolled, by the roller's clock.</summary>
    /// <remarks>
    /// The sender's clock, and therefore not to be trusted for ordering — arrival order decides
    /// that. It is here so a history replayed into a session that started an hour late can still say
    /// roughly when things happened.
    /// </remarks>
    public long At { get; init; }

    /// <summary>Announces a roll that has just been made here.</summary>
    /// <remarks>
    /// Takes the ghosting decision at the moment of creation rather than at the moment of sending,
    /// so there is never a window in which a full private roll exists in a variable that something
    /// else might pick up and transmit.
    /// </remarks>
    public static RollMessage From(
        string id,
        string player,
        RollOutcome outcome,
        RollReading? reading,
        bool secret,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return new RollMessage
        {
            Id = id,
            Player = player,
            Secret = secret,
            Notation = outcome.Notation.Text,
            Faces = [.. outcome.Dice.Select(die => die.Face)],
            Kept = [.. outcome.Dice.Select(die => die.IsKept)],
            Total = outcome.Total,
            Seed = outcome.Seed,
            ReadingKey = reading?.Key ?? "",
            ReadingValue = reading?.Value ?? 0,
            At = at.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>The same announcement with every detail of the roll removed.</summary>
    /// <remarks>
    /// Applied before a private roll is sent, and again before any stored history is replayed to a
    /// newcomer. Being able to run twice without changing anything the second time is the point: the
    /// log keeps the roller's own copy intact so they can see their own dice, and this makes sure
    /// that copy can never be the one that leaves the machine.
    /// </remarks>
    public RollMessage Ghost() => new()
    {
        Version = Version,
        Id = Id,
        Player = Player,
        Secret = true,
        At = At
    };

    /// <summary>Writes the message for the wire.</summary>
    public string Write() => JsonSerializer.Serialize(this, PartyJsonContext.Default.RollMessage);

    /// <summary>
    /// Reads a message from another player, refusing anything that is not exactly what it claims.
    /// </summary>
    /// <remarks>
    /// Every field is checked and every string is trimmed, shortened and stripped of control
    /// characters before it goes anywhere near the page. This input is hostile by default: it comes
    /// from whoever has the table code, which on a public relay is whoever has the table code.
    /// </remarks>
    public static bool TryRead(string? json, out RollMessage message)
    {
        message = null!;

        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumBytes)
        {
            return false;
        }

        RollMessage? read;

        try
        {
            read = JsonSerializer.Deserialize(json, PartyJsonContext.Default.RollMessage);
        }
        catch (JsonException)
        {
            return false;
        }

        if (read is null || read.Version != CurrentVersion)
        {
            return false;
        }

        string id = Clean(read.Id, MaximumTextLength);
        string player = Clean(read.Player, MaximumNameLength);

        if (id.Length == 0 || player.Length == 0)
        {
            return false;
        }

        if (read.Secret)
        {
            message = new RollMessage
            {
                Id = id,
                Player = player,
                Secret = true,
                At = read.At
            };

            return true;
        }

        string notation = Clean(read.Notation, MaximumTextLength);

        if (notation.Length == 0 || read.Faces.Count is 0 or > MaximumDice)
        {
            return false;
        }

        // Missing keep flags mean every die counted; a mismatched count means the two lists disagree
        // about how many dice there were, which is not something to guess at.
        bool[] kept;

        if (read.Kept.Count == 0)
        {
            kept = new bool[read.Faces.Count];
            Array.Fill(kept, true);
        }
        else if (read.Kept.Count == read.Faces.Count)
        {
            kept = [.. read.Kept];
        }
        else
        {
            return false;
        }

        message = new RollMessage
        {
            Id = id,
            Player = player,
            Notation = notation,
            Faces = [.. read.Faces],
            Kept = kept,
            Total = read.Total,
            Seed = read.Seed,
            ReadingKey = Clean(read.ReadingKey, MaximumTextLength),
            ReadingValue = read.ReadingValue,
            At = read.At
        };

        return true;
    }

    /// <summary>Trims, shortens, and drops anything that would not print.</summary>
    private static string Clean(string value, int maximum)
    {
        string trimmed = string.Concat(value.Where(character => !char.IsControl(character))).Trim();

        return trimmed.Length <= maximum ? trimmed : trimmed[..maximum].Trim();
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(RollMessage))]
public sealed partial class PartyJsonContext : JsonSerializerContext;
