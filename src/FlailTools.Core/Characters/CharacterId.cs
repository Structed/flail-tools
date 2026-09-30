using System.Security.Cryptography;

namespace FlailTools.Core.Characters;

/// <summary>
/// The name a character answers to for the rest of its life.
/// </summary>
/// <remarks>
/// <para>
/// This is the load-bearing value of the whole feature and it does not look like one. It is the key
/// a character is stored under, and it is what a re-shared link overwrites <em>on</em>: the second
/// link somebody sends replaces the first sheet rather than sitting beside it, and it only does
/// that because both carry the same id. Generate a fresh one on edit, or change what a valid id
/// looks like, and re-sharing quietly turns into duplicating — which nobody notices until a party
/// list has three copies of the same fighter, at three different revisions, and no way to tell
/// which is current.
/// </para>
/// <para>
/// So it is a short code rather than a GUID. It rides in a link people paste into chat windows
/// beside the sheet itself, and thirty-six characters of hyphenated hexadecimal would be most of a
/// line for something nobody reads.
/// </para>
/// <para>
/// The alphabet skips <c>i</c>, <c>l</c> and <c>o</c>, because these get read aloud and typed back
/// in by hand, and it is entirely lower case so that a code cannot be broken by somebody's
/// autocapitalising keyboard. Both the alphabet and the length are pinned by a test: widening
/// either is fine for new characters and orphans every one already saved.
/// </para>
/// </remarks>
public static class CharacterId
{
    /// <summary>
    /// The characters an id is built from: digits and letters that cannot be confused for each
    /// other when read aloud.
    /// </summary>
    public const string Alphabet = "23456789abcdefghjkmnpqrstuvwxyz";

    /// <summary>
    /// How long an id is.
    /// </summary>
    /// <remarks>
    /// Ten characters of a thirty-one letter alphabet is a little under fifty bits. There is no
    /// server to notice a collision, so the only defence is the space being far larger than any
    /// player's collection of characters could ever be.
    /// </remarks>
    public const int Length = 10;

    /// <summary>A fresh id, belonging to nothing yet.</summary>
    public static string Create()
    {
        Span<char> code = stackalloc char[Length];

        for (int index = 0; index < Length; index++)
        {
            code[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }

    /// <summary>
    /// <c>true</c> for something this tool could have issued.
    /// </summary>
    /// <remarks>
    /// Checked rather than trusted because an id arrives from a stranger's link and is then used as
    /// a storage key and as the thing a save overwrites on. A malformed one is replaced with a
    /// fresh id on the way in, so the sheet is kept as a new character rather than landing on top
    /// of somebody else's.
    /// </remarks>
    public static bool IsValid(string? id) =>
        id is { Length: Length } && id.All(letter => Alphabet.Contains(letter, StringComparison.Ordinal));
}
