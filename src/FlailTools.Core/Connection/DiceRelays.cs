namespace FlailTools.Core.Connection;

/// <summary>
/// The relays the dice table is signalled through, named here rather than left to chance.
/// </summary>
/// <remarks>
/// <para>
/// Left to itself the transport takes five relays from a list compiled into its bundle, shuffled by
/// the app id alone. That draw is fixed for the lifetime of the app: the same five every table,
/// every session, for everybody. One of the five it dealt this app — <c>chorus.pjv.me</c> — no
/// longer has a DNS record at all. Not down; deleted. The zone answers, the host does not exist,
/// and an invented subdomain of the same zone returns a byte-identical response.
/// </para>
/// <para>
/// So every table ran on four relays while believing it had five, and nothing a player could do
/// changed that — not a new room, not a reconnect, not a reload, because the draw never depended on
/// any of them. The upstream project has since dropped the dead host, but that work is unreleased
/// and the version we ship is the newest there is. Naming the relays is the only lever there is.
/// </para>
/// <para>
/// These were not chosen by taste. Every relay in the bundled list was opened, sent a real
/// subscription and held until it answered; twenty-one of the twenty-eight did, and each one below
/// was checked again for whether it demands authentication or payment to accept what the table
/// writes. What is deliberately absent is as considered as what is here: no relay whose own name
/// calls it staging or a test, and none run off a single named machine, because a dice table should
/// not depend on somebody's spare hardware staying plugged in.
/// </para>
/// <para>
/// Keep the list short. Naming relays replaces the draw outright rather than filtering it, and the
/// transport applies no cap of its own, so every entry here is a socket every player opens and
/// holds for as long as they are at the table. Seven is redundancy; thirty would be a burden passed
/// on to the relay operators, who are running these for nothing.
/// </para>
/// </remarks>
public static class DiceRelays
{
    /// <summary>
    /// The relays from the draw this app used before the list was chosen by hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kept for one reason, and it is not sentiment: players only meet if they share a relay. A tab
    /// that was opened before this change is still running the old build, still using the old draw,
    /// and there is no service worker or any other mechanism by which it would find out otherwise.
    /// If the new list overlapped the old one nowhere, that player and a player who reloaded this
    /// morning would sit in the same table code, each alone, each seeing a perfectly healthy
    /// connection. Nothing anywhere would report a fault.
    /// </para>
    /// <para>
    /// <c>chorus.pjv.me</c> is absent because it is the corpse this whole exercise is about; the
    /// other four are the draw's healthy remainder, and keeping them costs nothing. They are the
    /// bridge, and <see cref="All"/> may be pruned and rewritten freely so long as one of them
    /// survives in it.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> Inherited { get; } =
    [
        "wss://relay.mostr.pub",
        "wss://nostr.sathoarder.com",
        "wss://strfry.shock.network",
        "wss://schnorr.me"
    ];

    /// <summary>
    /// Every relay this app signals through, in the order they are dialled.
    /// </summary>
    /// <remarks>
    /// The four inherited relays first, so the overlap that keeps old tabs reachable is visible as
    /// the head of the list rather than scattered through it, then three long-running public relays
    /// under separate operators. Order carries no weight to the transport — all of them are opened
    /// at once — so it is arranged for the reader.
    /// </remarks>
    public static IReadOnlyList<string> All { get; } =
    [
        .. Inherited,
        "wss://nos.lol",
        "wss://purplerelay.com",
        "wss://nostr.data.haus"
    ];
}
