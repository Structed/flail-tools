namespace FlailTools.Core.Connection;

/// <summary>
/// What the dice table's signalling is actually doing, read back in words a player can act on.
/// </summary>
/// <remarks>
/// <para>
/// A table that will not connect looks identical from the inside whatever is wrong with it. The
/// code is on screen, the player is in the roster, and the status line says it is looking for the
/// others — and goes on saying it. Being "at a table" is a purely local fact: it is true the moment
/// the channel opens, and says nothing whatever about whether anybody can be reached.
/// </para>
/// <para>
/// Two quite different failures hide behind that one sentence, and they have opposite cures. Either
/// the relays that carry the signalling cannot be reached from this machine at all — a content
/// blocker, a VPN, a filtered network — or they can be reached perfectly well and this page has
/// stopped talking to them. The transport retries a relay on a doubling backoff and abandons it for
/// good once that backoff passes a minute, and it remembers having done so against the relay's
/// address rather than against the room, so leaving the table and rejoining picks the same dead
/// connection straight back up. Only reloading the page clears it.
/// </para>
/// <para>
/// So the check asks each relay twice: once for what the transport currently holds, and once
/// directly, now. A relay that answers a fresh socket while the transport's is shut is the second
/// failure. A relay that answers neither is the first. Telling a player which of the two they have
/// is the entire reason this exists — the alternative is reading a browser console, and nobody
/// sitting down to play has one open.
/// </para>
/// <para>
/// The sockets are opened in JavaScript, because only the browser can open one. Everything that
/// decides what the answers <em>mean</em> is here, where it can be tested without one.
/// </para>
/// </remarks>
public static class RelayCheck
{
    /// <summary>The transport holds a live socket to this relay.</summary>
    public const string Open = "open";

    /// <summary>The relay answered a socket opened just now, but the transport is not on it.</summary>
    public const string Reachable = "reachable";

    /// <summary>Nothing reached the relay at all.</summary>
    public const string Blocked = "blocked";

    /// <summary>The relay was reported in terms this build does not recognise.</summary>
    public const string Unknown = "unknown";

    /// <summary>How long to wait for a relay to answer, in milliseconds.</summary>
    /// <remarks>
    /// Long enough that a slow connection is not written off as a blocked one, short enough that
    /// somebody who pressed the button does not decide it has hung. A relay that has not finished a
    /// WebSocket handshake in eight seconds was never going to carry a dice table.
    /// </remarks>
    public const int ProbeMilliseconds = 8000;

    /// <summary>Every state a relay can be reported in.</summary>
    public static IReadOnlyList<string> States { get; } = [Open, Reachable, Blocked, Unknown];

    /// <summary>
    /// Every verdict the check can reach, named for the wording file.
    /// </summary>
    /// <remarks>
    /// Listed rather than discovered, so a test can assert each one has a line waiting for it. A
    /// verdict with no wording behind it renders as its own key, in front of a player who is already
    /// having a bad time of it.
    /// </remarks>
    public static IReadOnlyList<string> Verdicts { get; } =
    [
        "diceCheckOpen",
        "diceCheckGaveUp",
        "diceCheckBlocked",
        "diceCheckUnknown"
    ];

    /// <summary>
    /// Takes a state as reported and returns one this build knows.
    /// </summary>
    /// <remarks>
    /// These arrive as bare strings from JavaScript, which is not a place to take promises from.
    /// Anything unrecognised is settled here rather than further on, where it would end up in a
    /// class name or a wording lookup.
    /// </remarks>
    public static string Settle(string? state) => state switch
    {
        Open => Open,
        Reachable => Reachable,
        Blocked => Blocked,
        _ => Unknown
    };

    /// <summary>What to call one relay's state on screen.</summary>
    public static string Label(string? state) => Settle(state) switch
    {
        Open => "diceRelayOpen",
        Reachable => "diceRelayReachable",
        Blocked => "diceRelayBlocked",
        _ => "diceRelayUnknown"
    };

    /// <summary>
    /// What one relay's state means, for a reader who has never seen this list before.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The states are three words each, because a list five rows long has to be scannable. Three
    /// words are not enough to act on, though, and the row that needs explaining most is the one
    /// that looks worst: a single relay reporting nothing is the ordinary condition of the public
    /// relay network and costs the table nothing, while every relay reporting nothing is the whole
    /// problem. Same two words, opposite meanings, and no way to tell from the row itself.
    /// </para>
    /// <para>
    /// Separate from <see cref="Verdict"/> on purpose. The verdict is the one thing to go and do;
    /// these explain the evidence it was drawn from, so a player who wants to check the reasoning
    /// can, and a player who does not can ignore them.
    /// </para>
    /// </remarks>
    public static string Note(string? state) => Settle(state) switch
    {
        Open => "diceRelayOpenNote",
        Reachable => "diceRelayReachableNote",
        Blocked => "diceRelayBlockedNote",
        _ => "diceRelayUnknownNote"
    };

    /// <summary>
    /// Which states actually turned up, so only those need explaining.
    /// </summary>
    /// <remarks>
    /// Ordered by <see cref="States"/> rather than by the order the relays came back in, so the key
    /// reads the same way every time however the relays happened to answer. Explaining all four
    /// regardless would bury the one the player is looking at.
    /// </remarks>
    public static IReadOnlyList<string> Present(IReadOnlyDictionary<string, string>? relays)
    {
        if (relays is null || relays.Count == 0)
        {
            return [];
        }

        HashSet<string> found = [.. relays.Values.Select(Settle)];

        return [.. States.Where(found.Contains)];
    }

    /// <summary>
    /// Reads the whole check as one thing to go and do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One open relay is enough to be found, so a single <see cref="Open"/> settles it however badly
    /// the rest did: whatever is wrong at that point is the direct connection between two browsers,
    /// which is a different problem with a different answer.
    /// </para>
    /// <para>
    /// Failing that, one relay that answers on its own account is the more useful finding, because
    /// it proves the network is fine and points at the page instead. It is checked before the
    /// blanket verdict for exactly that reason — a mix of blocked and reachable is still a page that
    /// wants reloading.
    /// </para>
    /// </remarks>
    public static string Verdict(IReadOnlyDictionary<string, string>? relays)
    {
        if (relays is null || relays.Count == 0)
        {
            return "diceCheckUnknown";
        }

        List<string> states = [.. relays.Values.Select(Settle)];

        if (states.Contains(Open))
        {
            return "diceCheckOpen";
        }

        if (states.Contains(Reachable))
        {
            return "diceCheckGaveUp";
        }

        return states.TrueForAll(state => state == Blocked) ? "diceCheckBlocked" : "diceCheckUnknown";
    }

    /// <summary>
    /// The relay's address as somebody would read it out.
    /// </summary>
    /// <remarks>
    /// Every one of them is <c>wss://</c> and none of them carries a port, so keeping the scheme
    /// would put five identical prefixes down the left margin of a list whose only job is to be
    /// scanned in a hurry.
    /// </remarks>
    public static string Host(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        int mark = url.IndexOf("://", StringComparison.Ordinal);

        return mark < 0 ? url : url[(mark + 3)..];
    }
}
