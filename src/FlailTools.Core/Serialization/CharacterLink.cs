using System.Buffers.Text;
using System.IO.Compression;
using System.Text;
using FlailTools.Core.Characters;

namespace FlailTools.Core.Serialization;

/// <summary>
/// A whole character in a link.
/// </summary>
/// <remarks>
/// <para>
/// A site's link carries a seed and a few locks because the generator rebuilds the rest. Nothing
/// rebuilds a character, so this carries the sheet itself — and a full sheet is around a kilobyte
/// of JSON, which is far too much to hang off an address in the readable, packed form the site
/// links use. It is deflated and written in base64url instead.
/// </para>
/// <para>
/// The first character of the payload is the format, and it is the reason a second format is
/// possible later without orphaning every link already sent: a reader that meets a
/// <see cref="DeflatedVersion"/> it does not know says so, rather than confidently decoding
/// nonsense. It is deliberately one character and not a query parameter of its own, because it must
/// be impossible to paste half of.
/// </para>
/// <para>
/// Nothing here throws. A link arrives having been through a chat client, an email wrapper and
/// somebody's clipboard; the useful response to a mangled one is a notice saying so, not an error
/// page in the middle of somebody's game.
/// </para>
/// </remarks>
public static class CharacterLink
{
    /// <summary>The parameter carrying a whole shared sheet.</summary>
    public const string SheetKey = "c";

    /// <summary>The parameter naming a character this browser already holds.</summary>
    /// <remarks>
    /// Kept apart from <see cref="SheetKey"/> so the address bar of a character being edited stays
    /// short. There is no point restating a kilobyte of sheet in the address every keystroke when
    /// the sheet is sitting in this browser's own storage; the payload is built when somebody asks
    /// for a link to send.
    /// </remarks>
    public const string IdKey = "id";

    /// <summary>The only payload format there is so far: deflated JSON in base64url.</summary>
    public const char DeflatedVersion = '1';

    /// <summary>
    /// How much a payload is allowed to become once unpacked.
    /// </summary>
    /// <remarks>
    /// A few hundred bytes of deflate can name many megabytes of output, and this runs in the
    /// reader's own browser with no server between them and whoever sent the link. The cap is far
    /// above any real sheet — <see cref="CharacterLimits"/> keeps one to a few kilobytes — and far
    /// below anything that would trouble a tab.
    /// </remarks>
    private const int MostDecodedBytes = 256 * 1024;

    /// <summary>A link longer than this is not a sheet, and is not worth trying to unpack.</summary>
    private const int MostPayloadLength = 64 * 1024;

    /// <summary>The query string that carries this character to somebody else.</summary>
    public static string ToQuery(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        return $"?{SheetKey}={Encode(character)}";
    }

    /// <summary>The query string for a character this browser is already keeping.</summary>
    public static string ToIdQuery(string? id) =>
        CharacterId.IsValid(id) ? $"?{IdKey}={id}" : "";

    /// <summary>The character a link carries, or <c>null</c> if it carries none this can read.</summary>
    public static Character? FromQuery(string? query)
    {
        Dictionary<string, string> parameters = QueryParts.Split(query);

        return parameters.TryGetValue(SheetKey, out string? payload)
            ? Decode(Uri.UnescapeDataString(payload))
            : null;
    }

    /// <summary>The stored character a link names, or <c>null</c> if it names none.</summary>
    public static string? IdFromQuery(string? query)
    {
        Dictionary<string, string> parameters = QueryParts.Split(query);

        if (!parameters.TryGetValue(IdKey, out string? id))
        {
            return null;
        }

        string unescaped = Uri.UnescapeDataString(id);

        return CharacterId.IsValid(unescaped) ? unescaped : null;
    }

    /// <summary>The payload alone, version and all.</summary>
    public static string Encode(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        byte[] json = Encoding.UTF8.GetBytes(CharacterDocuments.Write(character));

        using MemoryStream packed = new();

        using (DeflateStream deflate = new(packed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(json, 0, json.Length);
        }

        return DeflatedVersion + Base64Url.EncodeToString(packed.ToArray());
    }

    /// <summary>
    /// The character a payload holds, or <c>null</c> for anything this cannot make sense of.
    /// </summary>
    /// <remarks>
    /// Every failure is the same answer on purpose. A reader cannot act on the difference between
    /// a truncated paste, a payload from a version that does not exist yet and a link that was
    /// never one — all three mean "that link did not bring a character", and saying which would
    /// only invite trying to repair it.
    /// </remarks>
    public static Character? Decode(string? payload)
    {
        if (payload is not { Length: > 1 } || payload.Length > MostPayloadLength)
        {
            return null;
        }

        if (payload[0] != DeflatedVersion)
        {
            return null;
        }

        byte[] packed = new byte[Base64Url.GetMaxDecodedLength(payload.Length - 1)];

        if (!TryUnpick(payload, packed, out int written))
        {
            return null;
        }

        string? json = Unpack(packed.AsSpan(0, written));

        if (json is null)
        {
            return null;
        }

        try
        {
            return CharacterDocuments.Read(json);
        }
        catch (Exception error) when (error is Structed.Inkwell.Serialization.DocumentFormatException
            or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Base64url decoding that answers rather than throws.
    /// </summary>
    /// <remarks>
    /// <see cref="Base64Url.TryDecodeFromChars"/> returns <c>false</c> for a buffer that is too
    /// small and <em>throws</em> for a character that is not base64url — which is exactly what a
    /// link mangled by a chat client's line wrapping looks like. Catching it here is the difference
    /// between a notice saying the link did not bring a character and an error page in the middle
    /// of somebody's game.
    /// </remarks>
    private static bool TryUnpick(string payload, byte[] packed, out int written)
    {
        try
        {
            return Base64Url.TryDecodeFromChars(payload.AsSpan(1), packed, out written);
        }
        catch (FormatException)
        {
            written = 0;

            return false;
        }
    }

    /// <summary>Inflates a payload, giving up rather than growing past <see cref="MostDecodedBytes"/>.</summary>
    private static string? Unpack(ReadOnlySpan<byte> packed)
    {
        try
        {
            using MemoryStream input = new(packed.ToArray(), writable: false);
            using DeflateStream inflate = new(input, CompressionMode.Decompress);
            using MemoryStream output = new();

            byte[] buffer = new byte[8192];
            int read;

            while ((read = inflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MostDecodedBytes)
                {
                    return null;
                }

                output.Write(buffer, 0, read);
            }

            return Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
