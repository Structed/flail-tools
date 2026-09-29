using FlailTools.Core.Connection;
using FlailTools.Core.Data;

namespace FlailTools.Core.Tests.Connection;

/// <summary>
/// The connection check has to reach the right conclusion, and have something to say when it does.
/// </summary>
/// <remarks>
/// <para>
/// The sockets belong to the browser and are not tested here. What is tested is the part that
/// decides what they meant, because that is the part a player acts on: one verdict sends them to
/// reload the page and another sends them to a different network, and swapping the two wastes an
/// evening of a game that was about to start.
/// </para>
/// <para>
/// The wording is checked too. <c>UiWordingTests</c> only sees keys written literally in a page,
/// and every key this produces is worked out at runtime, so a verdict with nothing behind it would
/// render as <c>diceCheckGaveUp</c> in front of the one player already having trouble.
/// </para>
/// </remarks>
public sealed class RelayCheckTests
{
    private static Dictionary<string, string> Relays(params string[] states)
    {
        Dictionary<string, string> relays = new(StringComparer.Ordinal);

        for (int index = 0; index < states.Length; index++)
        {
            relays["wss://relay" + index + ".example"] = states[index];
        }

        return relays;
    }

    /// <summary>
    /// One relay carrying the table is enough, however badly the others did.
    /// </summary>
    /// <remarks>
    /// Signalling is not a quorum. A player found through one relay is found, and telling them to
    /// go and fix their network because four of five are blocked would send them after the wrong
    /// problem entirely — at that point whatever is wrong is the direct leg between two browsers.
    /// </remarks>
    [Fact]
    public void OneOpenRelayIsEnoughToBeFindable()
    {
        Assert.Equal(
            "diceCheckOpen",
            RelayCheck.Verdict(Relays(RelayCheck.Blocked, RelayCheck.Blocked, RelayCheck.Open)));
    }

    /// <summary>
    /// Relays that answer while the page is not on them means the page gave up, not the network.
    /// </summary>
    /// <remarks>
    /// This is the failure that looks least like one. The transport abandons a relay for good once
    /// its backoff passes a minute and remembers that against the relay's address rather than the
    /// room, so the player who leaves the table and rejoins — the obvious thing to try — picks the
    /// same dead connection straight back up and concludes the tool is broken.
    /// </remarks>
    [Fact]
    public void RelaysThatAnswerWhileNothingIsOpenMeanTheFixIsAReload()
    {
        Assert.Equal(
            "diceCheckGaveUp",
            RelayCheck.Verdict(Relays(RelayCheck.Blocked, RelayCheck.Reachable)));
    }

    [Fact]
    public void NothingAnsweringAtAllIsTheNetworkOrABlocker()
    {
        Assert.Equal(
            "diceCheckBlocked",
            RelayCheck.Verdict(Relays(RelayCheck.Blocked, RelayCheck.Blocked, RelayCheck.Blocked)));
    }

    /// <summary>A check with nothing to test admits it rather than blaming the network.</summary>
    [Fact]
    public void NoRelaysAtAllIsNotAVerdictAboutAnything()
    {
        Assert.Equal("diceCheckUnknown", RelayCheck.Verdict(null));
        Assert.Equal("diceCheckUnknown", RelayCheck.Verdict(Relays()));
    }

    /// <summary>
    /// A state this build has never heard of is not quietly read as a working relay.
    /// </summary>
    /// <remarks>
    /// The states cross from JavaScript as bare strings. If the transport is ever swapped for one
    /// that words them differently, the honest answer is that the check no longer understands its
    /// own results — not a confident verdict drawn from strings nobody recognises.
    /// </remarks>
    [Fact]
    public void AStateFromSomewhereElseIsNotGuessedAt()
    {
        Assert.Equal(RelayCheck.Unknown, RelayCheck.Settle("connecting"));
        Assert.Equal(RelayCheck.Unknown, RelayCheck.Settle(null));
        Assert.Equal("diceRelayUnknown", RelayCheck.Label("connecting"));
        Assert.Equal("diceCheckUnknown", RelayCheck.Verdict(Relays("connecting")));
    }

    [Theory]
    [InlineData("wss://relay.mostr.pub", "relay.mostr.pub")]
    [InlineData("wss://yabu.me/v2", "yabu.me/v2")]
    [InlineData("relay.mostr.pub", "relay.mostr.pub")]
    public void TheSchemeIsNotWorthFiveLinesOfLeftMargin(string url, string expected)
    {
        Assert.Equal(expected, RelayCheck.Host(url));
    }

    /// <summary>Every verdict and every state label has a line waiting for it in the wording.</summary>
    [Fact]
    public async Task EverythingTheCheckCanSayIsWrittenDown()
    {
        UiText ui = (await TestData.LoadAsync()).Ui;

        foreach (string key in RelayCheck.Verdicts)
        {
            Assert.True(ui.Messages.ContainsKey(key), $"'{key}' is missing from ui.json.");
            Assert.NotEqual("", ui.Message(key));
        }

        foreach (string state in RelayCheck.States)
        {
            foreach (string key in (string[])[RelayCheck.Label(state), RelayCheck.Note(state)])
            {
                Assert.True(ui.Messages.ContainsKey(key), $"'{key}' is missing from ui.json.");
                Assert.NotEqual("", ui.Message(key));
            }
        }
    }

    /// <summary>
    /// The key explains the states that turned up, and only those.
    /// </summary>
    /// <remarks>
    /// Explaining all four every time would bury the row the player is actually looking at under
    /// three that did not happen, and one of those would be an apology for a state the transport
    /// never reported.
    /// </remarks>
    [Fact]
    public void TheKeyExplainsWhatTurnedUpAndNothingElse()
    {
        Assert.Equal(
            [RelayCheck.Open, RelayCheck.Blocked],
            RelayCheck.Present(Relays(RelayCheck.Blocked, RelayCheck.Open, RelayCheck.Blocked)));

        Assert.Empty(RelayCheck.Present(null));
        Assert.Empty(RelayCheck.Present(Relays()));
    }

    /// <summary>
    /// The key reads in the same order however the relays happened to answer.
    /// </summary>
    /// <remarks>
    /// Ordering it by arrival would reshuffle the explanations between two runs a few seconds
    /// apart, which reads as the page changing its mind rather than the relays changing theirs.
    /// </remarks>
    [Fact]
    public void TheKeyIsInTheSameOrderEveryTime()
    {
        Assert.Equal(
            RelayCheck.Present(Relays(RelayCheck.Blocked, RelayCheck.Reachable, RelayCheck.Open)),
            RelayCheck.Present(Relays(RelayCheck.Open, RelayCheck.Blocked, RelayCheck.Reachable)));

        Assert.Equal(
            [.. RelayCheck.States],
            RelayCheck.Present(
                Relays(RelayCheck.Unknown, RelayCheck.Blocked, RelayCheck.Reachable, RelayCheck.Open)));
    }

    /// <summary>
    /// Every verdict is reachable from some arrangement of relays.
    /// </summary>
    /// <remarks>
    /// The list above is written by hand, so a verdict could be named there, given wording, checked
    /// by the test above and never once produced. Asserted from the other end so the coverage claim
    /// is honest.
    /// </remarks>
    [Fact]
    public void EveryVerdictIsOneTheCheckCanActuallyReach()
    {
        HashSet<string> reached =
        [
            RelayCheck.Verdict(Relays(RelayCheck.Open)),
            RelayCheck.Verdict(Relays(RelayCheck.Reachable)),
            RelayCheck.Verdict(Relays(RelayCheck.Blocked)),
            RelayCheck.Verdict(null)
        ];

        Assert.Equal([.. RelayCheck.Verdicts.Order()], [.. reached.Order()]);
    }
}
