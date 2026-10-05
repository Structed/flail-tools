using System.Globalization;

namespace FlailTools.Core.Connection;

/// <summary>
/// Whether this device's clock is far enough wrong to have hidden it from the table, read back in
/// words a player can act on.
/// </summary>
/// <remarks>
/// <para>
/// The relays that carry the signalling date every message by the sender's clock, and judge it by
/// their own. Probed directly, the four strfry relays then in the pinned list refused an
/// announcement stamped more than a minute in the past (<c>invalid: ephemeral event expired</c>) or
/// a quarter of an hour in the future (<c>invalid: created_at too late</c>). The fifth accepted it
/// and then left it out of every other player's subscription, because each of those asked only for
/// messages from the moment they arrived onwards. Either way the player behind the wrong clock is
/// never seen, and sees nobody.
/// </para>
/// <para>
/// It was found as a player who was alone at every table, every evening, while everybody else
/// could see each other. The connection check said the signalling worked, because one relay was
/// still open — it was, and it was carrying messages nobody would ever be shown. The transport
/// at the time also took each refusal as a reason to abandon the relay for the rest of the page's
/// life, so the check read four perfectly healthy relays as ones the page had given up on, and sent
/// the player off to reload a page that would only give up on them again.
/// </para>
/// <para>
/// The channel now measures the clock against the site's own server before it joins, and allows
/// for the difference in every time the transport reads. This says so, because a clock that is
/// several minutes out is wrong for every other site the player uses too, and they may as well
/// hear it from somewhere. The measuring is done in the browser; what the measurement means is
/// decided here, where it can be tested without one.
/// </para>
/// </remarks>
public static class ClockCheck
{
    /// <summary>The clock was slow, and the table has allowed for it.</summary>
    public const string Behind = "diceClockBehind";

    /// <summary>The clock was fast, and the table has allowed for it.</summary>
    public const string Ahead = "diceClockAhead";

    /// <summary>The clock is wrong, and nothing has allowed for it.</summary>
    public const string Wrong = "diceClockWrong";

    /// <summary>The label read beside how far out the clock is.</summary>
    public const string DriftLabel = "diceClockDrift";

    /// <summary>The smallest difference worth mentioning, in milliseconds.</summary>
    /// <remarks>
    /// The same threshold the channel corrects at. A difference smaller than this is a long way
    /// inside what any relay tolerates, and measuring it against a <c>Date</c> header that only
    /// counts whole seconds would turn ordinary drift into a warning nobody needs.
    /// </remarks>
    public const double NoticeMilliseconds = 10_000;

    /// <summary>
    /// Every line the check can produce, named for the wording file.
    /// </summary>
    /// <remarks>
    /// Listed rather than discovered, for the same reason <see cref="RelayCheck.Verdicts"/> is: a
    /// line with no wording behind it renders as its own key, in front of the one player who most
    /// needs a sentence.
    /// </remarks>
    public static IReadOnlyList<string> Lines { get; } = [Behind, Ahead, Wrong];

    /// <summary>
    /// Reads a measured clock as one thing to say, or nothing when there is nothing worth saying.
    /// </summary>
    /// <param name="offset">
    /// How far the server's clock is ahead of this device's, so positive when this one is slow.
    /// Null when it could not be measured.
    /// </param>
    /// <param name="corrected">Whether the channel is allowing for the difference.</param>
    /// <remarks>
    /// <para>
    /// An unmeasured clock says nothing. Guessing would mean warning everybody whose network
    /// happened to drop one request, and the channel carries on uncorrected in that case anyway —
    /// which is exactly how it behaved before any of this existed.
    /// </para>
    /// <para>
    /// A large difference the channel is <em>not</em> allowing for gets its own line, because the
    /// cure is no longer optional: until the clock is fixed, nobody can see this browser.
    /// </para>
    /// </remarks>
    public static string? Line(TimeSpan? offset, bool corrected)
    {
        if (offset is not { } drift || !Notable(drift))
        {
            return null;
        }

        if (!corrected)
        {
            return Wrong;
        }

        return drift > TimeSpan.Zero ? Behind : Ahead;
    }

    /// <summary>
    /// How far out the clock is, in digits and colons, so it needs no wording of its own.
    /// </summary>
    /// <remarks>
    /// Minutes and seconds under an hour, hours, minutes and seconds beyond that. Hours are never
    /// rolled up into days: a clock set to the wrong date reads as a large number of hours, which is
    /// unmistakable, and a days field would need a unit word that belongs in the wording file.
    /// </remarks>
    public static string Drift(TimeSpan offset)
    {
        // Not Duration(), which throws on TimeSpan.MinValue rather than reading it.
        long seconds = (long)Math.Round(Math.Abs(offset.TotalSeconds), MidpointRounding.AwayFromZero);
        long hours = seconds / 3600;
        long minutes = seconds % 3600 / 60;
        long rest = seconds % 60;

        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{rest:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}:{rest:00}");
    }

    private static bool Notable(TimeSpan drift) => Math.Abs(drift.TotalMilliseconds) >= NoticeMilliseconds;
}
