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
/// subscription and held until it answered; twenty of the twenty-eight did, and each one below was
/// checked again for whether it demands authentication or payment to accept what the table writes.
/// A relay that only accepts writes from a list it keeps is no good to us either — it answers a
/// subscription perfectly and drops every roll — so the ones advertising restricted writes were put
/// aside too. What is deliberately absent is as considered as what is here: no relay whose own name
/// calls it staging or a test, and none run off a single named machine, because a dice table should
/// not depend on somebody's spare hardware staying plugged in.
/// </para>
/// <para>
/// Test them <em>from a browser</em>, and mistrust any other result. The first pass here was run
/// with <c>ClientWebSocket</c>, which passed <c>relay.mostr.pub</c> five times out of five while it
/// was failing for every actual player: the host answers the handshake with a permanent redirect to
/// <c>relay.ditto.pub</c>, .NET follows it without saying so, and a browser refuses — the WebSocket
/// handshake admits no reply but 101. A redirect, an authentication wall and a healthy relay are
/// indistinguishable from a command line, and only one of the three is any use here.
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
    /// Two of the five are absent. <c>chorus.pjv.me</c> is the corpse this whole exercise is about,
    /// and <c>relay.mostr.pub</c> turned out to be a second one wearing better clothes: it answers
    /// the handshake with a permanent redirect, which no browser will follow, so it never carried a
    /// roll for anyone. The old draw was three relays deep all along and said five. Those three are
    /// the bridge, and <see cref="All"/> may be pruned and rewritten freely so long as one of them
    /// survives in it.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> Inherited { get; } =
    [
        "wss://nostr.sathoarder.com",
        "wss://strfry.shock.network",
        "wss://schnorr.me"
    ];

    /// <summary>
    /// Every relay this app signals through, in the order they are dialled.
    /// </summary>
    /// <remarks>
    /// The three inherited relays first, so the overlap that keeps old tabs reachable is visible as
    /// the head of the list rather than scattered through it, then four long-running public relays
    /// under separate operators. Order carries no weight to the transport — all of them are opened
    /// at once — so it is arranged for the reader.
    /// </remarks>
    public static IReadOnlyList<string> All { get; } =
    [
        .. Inherited,
        "wss://nos.lol",
        "wss://purplerelay.com",
        "wss://nostr.data.haus",
        "wss://nostr-01.uid.ovh"
    ];
}
