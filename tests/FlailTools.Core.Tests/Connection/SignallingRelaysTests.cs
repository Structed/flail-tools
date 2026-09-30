using FlailTools.Core.Connection;

namespace FlailTools.Core.Tests.Connection;

/// <summary>
/// The relays the dice table signals through are a compatibility surface, not configuration.
/// </summary>
/// <remarks>
/// <para>
/// Two browsers only find each other through a relay they both use. Every browser still open on
/// yesterday's deployment is signalling through yesterday's list, so a list that moves too far in one
/// step splits every table in two across the deployment — and the split looks exactly like the other
/// player never turning up.
/// </para>
/// <para>
/// The channel validates each URL as well, but only when the page opens in a browser. These fail at
/// build time instead, where nobody is waiting for a game to start.
/// </para>
/// </remarks>
public sealed class SignallingRelaysTests
{
    /// <summary>
    /// What Trystero drew for <c>structed-flail-tools-dice</c> before the relays were named.
    /// </summary>
    /// <remarks>
    /// Its bundled list, shuffled by the app id alone, and the first five taken. Every build before
    /// Inkwell 0.3.0 signals through exactly these, and <c>chorus.pjv.me</c> and
    /// <c>relay.mostr.pub</c> are the two that stopped answering.
    /// </remarks>
    private static readonly string[] PreviousDraw =
    [
        "wss://chorus.pjv.me",
        "wss://relay.mostr.pub",
        "wss://nostr.sathoarder.com",
        "wss://strfry.shock.network",
        "wss://schnorr.me"
    ];

    /// <summary>
    /// The list is exactly the one the deployed build signals through.
    /// </summary>
    /// <remarks>
    /// Written out as a literal rather than referenced, so the test compares the value against the
    /// intent rather than against itself. Replacing a relay that has died is expected; replacing
    /// every relay at once is how a table splits.
    /// </remarks>
    [Fact]
    public void TheTableIsSignalledThroughExactlyTheseRelays()
    {
        Assert.True(
            SignallingRelays.All.SequenceEqual(
            [
                "wss://nostr.sathoarder.com",
                "wss://strfry.shock.network",
                "wss://schnorr.me",
                "wss://nos.lol",
                "wss://bucket.coracle.social"
            ],
            StringComparer.Ordinal),
            "The relay list has changed. Keep at least one relay from the list it replaces, or players " +
            "on the deployed build and on this one will never find each other; then update this literal.");
    }

    /// <summary>
    /// Every relay is a secure WebSocket address with a host, listed once.
    /// </summary>
    /// <remarks>
    /// A malformed relay fails as silently as a dead one, and the page is served over HTTPS, so a
    /// plain <c>ws://</c> relay would be refused by the browser as mixed content before it was ever
    /// asked anything.
    /// </remarks>
    [Fact]
    public void EveryRelayIsASecureWebSocketAddressListedOnce()
    {
        Assert.NotEmpty(SignallingRelays.All);

        foreach (string relay in SignallingRelays.All)
        {
            Assert.True(
                Uri.TryCreate(relay, UriKind.Absolute, out Uri? uri)
                    && string.Equals(uri.Scheme, "wss", StringComparison.Ordinal)
                    && uri.Host.Length > 0,
                $"'{relay}' is not an absolute wss:// URL with a host. The channel would refuse it at runtime.");
        }

        Assert.Equal(
            SignallingRelays.All.Count,
            SignallingRelays.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// A browser that has not reloaded since the relays were named can still find this one.
    /// </summary>
    [Fact]
    public void TheBuildBeforeTheRelaysWereNamedStillSharesOne()
    {
        Assert.True(
            SignallingRelays.All.Intersect(PreviousDraw, StringComparer.Ordinal).Any(),
            "No relay is left in common with the previous build's draw, so players on it and on this " +
            "build would never meet. Keep one of its live relays.");
    }
}
