using FlailTools.Core.Connection;

namespace FlailTools.Core.Tests.Connection;

/// <summary>
/// The relay list is a compatibility surface wearing configuration's clothes.
/// </summary>
/// <remarks>
/// <para>
/// Relays are how two browsers find each other, so players only ever meet if they share one. That
/// makes this list exactly as load-bearing as the app id, and exactly as harmless-looking: it is a
/// handful of addresses in a file, and tidying it is the most natural thing in the world. The
/// difference is that a mistake here reports nothing. Both players see a table, a code and a
/// healthy connection, and simply never see each other.
/// </para>
/// <para>
/// These are rules about the shape of the list, not about whether any given relay is up this
/// afternoon. Public relays come and go, and a test that reached for the network would fail on a
/// train, in CI behind a proxy, and every time somebody else's server rebooted.
/// </para>
/// </remarks>
public sealed class DiceRelaysTests
{
    /// <summary>
    /// Every entry has to be a URL the channel will accept, or the dice page does not open at all.
    /// </summary>
    /// <remarks>
    /// The channel validates in its constructor and throws on anything that is not an absolute
    /// <c>ws://</c> or <c>wss://</c> URL with a host. That is the right place for it — a malformed
    /// relay fails as silently as a dead one — but it means a typo here is not a degraded table, it
    /// is an exception in the middle of building the page, for everybody, on first load.
    /// </remarks>
    [Fact]
    public void EveryRelayIsAnAddressTheChannelWillTake()
    {
        Assert.NotEmpty(DiceRelays.All);

        foreach (string relay in DiceRelays.All)
        {
            Assert.True(
                Uri.TryCreate(relay, UriKind.Absolute, out Uri? parsed)
                    && !string.IsNullOrEmpty(parsed.Host)
                    && (parsed.Scheme == Uri.UriSchemeWss || parsed.Scheme == Uri.UriSchemeWs),
                $"'{relay}' is not an absolute ws:// or wss:// address, and the channel will refuse it.");

            Assert.Equal(relay.Trim(), relay);
        }
    }

    /// <summary>
    /// The same relay twice is one relay and one wasted socket.
    /// </summary>
    /// <remarks>
    /// The transport caches its client per URL, so a duplicate buys no redundancy whatever while
    /// still reading like it does. Worth catching here because the list is assembled from two
    /// pieces, and the obvious way to add a relay is to paste one.
    /// </remarks>
    [Fact]
    public void NoRelayIsListedTwice()
    {
        Assert.Equal(
            DiceRelays.All.Count,
            DiceRelays.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// Something from the old draw has to survive, or tabs opened before the change are stranded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the rule that is easiest to break by accident, because breaking it looks like
    /// housekeeping. A tab opened before this list existed is still running the old build and still
    /// dialling the draw the transport made for it; nothing tells it otherwise, and nothing will
    /// until somebody reloads. Share no relay with that draw and the two builds cannot see each
    /// other at all.
    /// </para>
    /// <para>
    /// One is enough, and the rest of the list may be rewritten freely around it. When every plausible
    /// stale tab has been closed this stops mattering — but that is a judgement somebody should make
    /// deliberately, by deleting this test, rather than discover by stranding a table.
    /// </para>
    /// </remarks>
    [Fact]
    public void AtLeastOneRelayFromTheOldDrawIsStillDialled()
    {
        Assert.NotEmpty(DiceRelays.Inherited);

        Assert.True(
            DiceRelays.Inherited.Any(relay => DiceRelays.All.Contains(relay, StringComparer.Ordinal)),
            "Nothing from the previous relay draw is left. A player who has not reloaded since this "
                + "changed can no longer reach anybody, and neither end will report a fault.");
    }

    /// <summary>
    /// The relay that started all this does not come back.
    /// </summary>
    /// <remarks>
    /// <c>chorus.pjv.me</c> has no DNS record. It is in the transport's bundled list, it was in the
    /// draw this app was dealt, and it is the reason the list is chosen by hand at all. It would
    /// return to the list only by somebody copying the old draw back in wholesale, which is a
    /// plausible enough accident to be worth a line.
    /// </remarks>
    [Fact]
    public void TheDeadRelayIsNotAmongThem()
    {
        Assert.DoesNotContain(
            DiceRelays.All,
            relay => relay.Contains("chorus.pjv.me", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            DiceRelays.Inherited,
            relay => relay.Contains("chorus.pjv.me", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Enough relays to be resilient, few enough to be polite.
    /// </summary>
    /// <remarks>
    /// Naming relays replaces the transport's draw rather than filtering it, and no cap is applied
    /// on the other side, so every entry is a socket every player opens and holds for the whole
    /// session. These are volunteer-run servers. The lower bound matters too: a single relay is a
    /// single point of failure for the entire tool, and the public relay network is not reliable
    /// enough for that.
    /// </remarks>
    [Fact]
    public void TheListIsShortEnoughToBeFairToTheRelays()
    {
        Assert.InRange(DiceRelays.All.Count, 3, 10);
    }
}
