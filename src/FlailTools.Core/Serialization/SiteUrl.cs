using System.Text;
using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using Structed.Inkwell.Randomness;

namespace FlailTools.Core.Serialization;

/// <summary>
/// Puts a plan in a query string and gets it back out.
/// </summary>
/// <remarks>
/// <para>
/// The link <em>is</em> the save file for most people, so everything the generator reads has to be
/// in it: the seed, the kind if one was chosen, the locks, and the re-roll counters. A link that
/// carried only the seed would quietly drop every lock the sender had set, and the receiver would
/// see a different place with no indication anything had been lost.
/// </para>
/// <para>
/// The keys are single letters because the whole thing gets pasted into chat windows, and they are
/// as load-bearing as the field paths: changing one orphans every link already shared.
/// </para>
/// </remarks>
public static class SiteUrl
{
    public const string SeedKey = "s";
    public const string KindKey = "k";
    public const string PinsKey = "p";
    public const string RerollsKey = "r";

    /// <summary>The query string for a plan, including the leading <c>?</c>, or empty for a bare default.</summary>
    public static string ToQuery(SitePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        StringBuilder query = new();

        Append(query, SeedKey, SeedCodec.Encode(plan.Seed));

        if (SiteKinds.IsKnown(plan.Kind))
        {
            Append(query, KindKey, plan.Kind!);
        }

        if (plan.Pins.Count > 0)
        {
            AppendPacked(query, PinsKey, Pack(plan.Pins.OrderBy(pin => pin.Key, StringComparer.Ordinal)
                .Select(pin => (pin.Key, pin.Value))));
        }

        if (plan.Rerolls.Count > 0)
        {
            AppendPacked(query, RerollsKey, Pack(plan.Rerolls.OrderBy(reroll => reroll.Key, StringComparer.Ordinal)
                .Select(reroll => (reroll.Key, reroll.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
        }

        return query.ToString();
    }

    /// <summary>
    /// Reads a plan out of a query string, taking whatever of it makes sense.
    /// </summary>
    /// <remarks>
    /// Nothing here throws. A link arrives having been through a chat client, an email wrapper and
    /// somebody's clipboard, and the useful response to a mangled one is the site it mostly
    /// describes — not an error page.
    /// </remarks>
    public static SitePlan FromQuery(string? query)
    {
        Dictionary<string, string> parameters = Split(query);

        uint seed = parameters.TryGetValue(SeedKey, out string? encoded)
            ? SeedCodec.DecodeOrRandom(Uri.UnescapeDataString(encoded))
            : SeedCodec.CreateRandom();

        string? kind = parameters.TryGetValue(KindKey, out string? requested)
            && SiteKinds.IsKnown(Uri.UnescapeDataString(requested))
                ? Uri.UnescapeDataString(requested)
                : null;

        Dictionary<string, string> pins = new(StringComparer.Ordinal);

        if (parameters.TryGetValue(PinsKey, out string? packedPins))
        {
            foreach ((string path, string value) in Unpack(packedPins))
            {
                pins[path] = value;
            }
        }

        Dictionary<string, int> rerolls = new(StringComparer.Ordinal);

        if (parameters.TryGetValue(RerollsKey, out string? packedRerolls))
        {
            foreach ((string path, string value) in Unpack(packedRerolls))
            {
                if (int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out int count) && count > 0)
                {
                    rerolls[path] = count;
                }
            }
        }

        return new SitePlan { Seed = seed, Kind = kind, Pins = pins, Rerolls = rerolls };
    }

    private static void Append(StringBuilder query, string key, string value)
    {
        query.Append(query.Length == 0 ? '?' : '&')
            .Append(key)
            .Append('=')
            .Append(Uri.EscapeDataString(value));
    }

    /// <summary>
    /// Appends a value that has already been escaped pair by pair.
    /// </summary>
    /// <remarks>
    /// Escaping it again would turn every <c>%2F</c> into <c>%252F</c> — harmless to read back, but
    /// it triples the length of a list of paths in something people paste into chat windows.
    /// </remarks>
    private static void AppendPacked(StringBuilder query, string key, string packed)
    {
        query.Append(query.Length == 0 ? '?' : '&')
            .Append(key)
            .Append('=')
            .Append(packed);
    }

    private static string Pack(IEnumerable<(string Key, string Value)> pairs) =>
        string.Join('~', pairs.Select(pair => $"{Escape(pair.Key)}*{Escape(pair.Value)}"));

    /// <summary>
    /// Escapes one half of a pair so that neither separator can appear inside it.
    /// </summary>
    /// <remarks>
    /// Two deliberate departures from <see cref="Uri.EscapeDataString"/>. A slash is left alone
    /// because it is legal in a query and every field path is full of them, so escaping it would
    /// bury the readable part of the link. A tilde is escaped by hand because it is <em>unreserved</em>
    /// and so survives <c>EscapeDataString</c> untouched — and it separates one pair from the next,
    /// which means a typed value containing one would otherwise split into two nonsense pins.
    /// </remarks>
    private static string Escape(string value) =>
        Uri.EscapeDataString(value)
            .Replace("%2F", "/", StringComparison.Ordinal)
            .Replace("~", "%7E", StringComparison.Ordinal);

    private static IEnumerable<(string Path, string Value)> Unpack(string packed)
    {
        foreach (string entry in packed.Split('~', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = entry.IndexOf('*', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            yield return (
                Uri.UnescapeDataString(entry[..separator]),
                Uri.UnescapeDataString(entry[(separator + 1)..]));
        }
    }

    /// <summary>
    /// Splits a query into its parameters, leaving every value exactly as it arrived.
    /// </summary>
    /// <remarks>
    /// Unescaping here would be a bug rather than a convenience: a packed list has to be split on
    /// its separators before its halves are unescaped, or an escaped separator inside somebody's
    /// typed words would reappear and tear the list apart at the wrong place.
    /// </remarks>
    private static Dictionary<string, string> Split(string? query)
    {
        Dictionary<string, string> parameters = new(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(query))
        {
            return parameters;
        }

        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = pair.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            parameters[pair[..separator]] = pair[(separator + 1)..];
        }

        return parameters;
    }
}
