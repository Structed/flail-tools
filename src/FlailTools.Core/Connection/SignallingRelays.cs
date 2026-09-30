namespace FlailTools.Core.Connection;

/// <summary>
/// The Nostr relays the dice table is signalled through.
/// </summary>
/// <remarks>
/// <para>
/// Left to itself, Trystero takes five relays from a list compiled into its bundle, shuffled by the
/// app id alone. The draw is therefore the same for every table this app ever opens, and a relay
/// that dies stays in it however often anybody rejoins or reloads. Ours came out as
/// <c>chorus.pjv.me</c>, whose DNS record no longer exists, and <c>relay.mostr.pub</c>, which
/// players' browsers could not reach — so every table ran on three relays while the connection check
/// listed five, two of them permanently answering nothing. Naming the relays here replaces that draw
/// outright: every relay listed is used, and nothing else is.
/// </para>
/// <para>
/// Load-bearing, in the same way as the app id. Relays are how two browsers find each other, so two
/// players only meet if they share at least one. The player who has not reloaded and the player on
/// yesterday's deployment are both still signalling through the previous list, and a change that
/// leaves nothing in common with it splits every table across the deployment with no error at either
/// end — the far side simply never arrives. That is why the three relays from Trystero's draw that
/// still answer are kept at the head of this list, and why any future change must keep at least one
/// relay from the list it replaces.
/// </para>
/// <para>
/// Every entry comes from Trystero's own bundled list, which is curated for relays that accept the
/// ephemeral events its signalling is carried in; a relay chosen from anywhere else might answer the
/// socket and quietly refuse to carry the handshake.
/// </para>
/// <para>
/// Kept here rather than beside the app id in <c>FlailRolls.cs</c>: nothing about a relay says which
/// game this is, and that file is the one with a budget.
/// </para>
/// </remarks>
public static class SignallingRelays
{
    /// <summary>Every relay the table signals through, in the order they are handed to the transport.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        "wss://nostr.sathoarder.com",
        "wss://strfry.shock.network",
        "wss://schnorr.me",
        "wss://nos.lol",
        "wss://bucket.coracle.social"
    ];
}
