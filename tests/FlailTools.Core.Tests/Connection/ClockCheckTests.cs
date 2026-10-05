using FlailTools.Core.Connection;
using FlailTools.Core.Data;

namespace FlailTools.Core.Tests.Connection;

/// <summary>
/// The clock check has to speak up when a wrong clock has hidden somebody, and only then.
/// </summary>
/// <remarks>
/// The measuring belongs to the browser and is not tested here. What is tested is what a measured
/// difference means to the player, because a warning on every screen trains people to ignore the
/// one that matters, and silence on the screen that needed it costs somebody a whole evening alone
/// at a table everybody else can see.
/// </remarks>
public sealed class ClockCheckTests
{
    /// <summary>
    /// A positive offset means the server is ahead, which means this device is the slow one.
    /// </summary>
    /// <remarks>
    /// The sign is the thing most likely to be got backwards, and getting it backwards tells a
    /// player to look for the opposite fault on a clock they cannot see the server's side of.
    /// </remarks>
    [Fact]
    public void AServerAheadOfThisDeviceMeansThisClockIsSlow()
    {
        Assert.Equal(ClockCheck.Behind, ClockCheck.Line(TimeSpan.FromMinutes(4), corrected: true));
        Assert.Equal(ClockCheck.Ahead, ClockCheck.Line(TimeSpan.FromMinutes(-4), corrected: true));
    }

    /// <summary>
    /// Ordinary drift is nobody's business.
    /// </summary>
    /// <remarks>
    /// The relays tolerate a minute in the past and a quarter of an hour in the future, and the
    /// measurement itself is only good to about a second. A few seconds out is every clock in the
    /// world on a bad day.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(9_999)]
    [InlineData(-9_999)]
    public void AFewSecondsOutSaysNothing(double milliseconds)
    {
        Assert.Null(ClockCheck.Line(TimeSpan.FromMilliseconds(milliseconds), corrected: true));
        Assert.Null(ClockCheck.Line(TimeSpan.FromMilliseconds(milliseconds), corrected: false));
    }

    [Fact]
    public void TheThresholdItselfIsWorthMentioning()
    {
        Assert.Equal(
            ClockCheck.Behind,
            ClockCheck.Line(TimeSpan.FromMilliseconds(ClockCheck.NoticeMilliseconds), corrected: true));
    }

    /// <summary>A clock that could not be measured is not guessed at.</summary>
    [Fact]
    public void AnUnmeasuredClockSaysNothing()
    {
        Assert.Null(ClockCheck.Line(null, corrected: false));
        Assert.Null(ClockCheck.Line(null, corrected: true));
    }

    /// <summary>
    /// A wrong clock nothing is allowing for is the one line that is not advice.
    /// </summary>
    /// <remarks>
    /// Corrected, the table works and the player is being told about their clock as a courtesy.
    /// Uncorrected, nobody can see them, and the sentence has to say so rather than reassure them.
    /// </remarks>
    [Fact]
    public void AWrongClockNobodyCorrectedSaysTheTableCannotSeeYou()
    {
        Assert.Equal(ClockCheck.Wrong, ClockCheck.Line(TimeSpan.FromMinutes(5), corrected: false));
        Assert.Equal(ClockCheck.Wrong, ClockCheck.Line(TimeSpan.FromMinutes(-20), corrected: false));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(10_000, "0:10")]
    [InlineData(252_000, "4:12")]
    [InlineData(-252_000, "4:12")]
    [InlineData(3_599_499, "59:59")]
    [InlineData(3_600_000, "1:00:00")]
    [InlineData(90_061_000, "25:01:01")]
    public void TheDriftIsWrittenInDigitsAlone(double milliseconds, string expected)
    {
        Assert.Equal(expected, ClockCheck.Drift(TimeSpan.FromMilliseconds(milliseconds)));
    }

    /// <summary>The far ends of a TimeSpan are read rather than thrown at.</summary>
    [Fact]
    public void EvenAnAbsurdClockCanBeWrittenDown()
    {
        Assert.NotEmpty(ClockCheck.Drift(TimeSpan.MinValue));
        Assert.NotEmpty(ClockCheck.Drift(TimeSpan.MaxValue));
    }

    /// <summary>Every line, and the label beside the drift, has wording waiting for it.</summary>
    [Fact]
    public async Task EverythingTheCheckCanSayIsWrittenDown()
    {
        UiText ui = (await TestData.LoadAsync()).Ui;

        foreach (string key in (string[])[.. ClockCheck.Lines, ClockCheck.DriftLabel])
        {
            Assert.True(ui.Messages.ContainsKey(key), $"'{key}' is missing from ui.json.");
            Assert.NotEqual("", ui.Message(key));
        }
    }

    /// <summary>
    /// Every line is one the check can actually reach.
    /// </summary>
    /// <remarks>
    /// The list is written by hand, so a line could be named there, worded and checked above, and
    /// never once produced.
    /// </remarks>
    [Fact]
    public void EveryLineIsOneTheCheckCanActuallyReach()
    {
        // A null would be dropped here, and the comparison below would catch the gap it left.
        string?[] lines =
        [
            ClockCheck.Line(TimeSpan.FromMinutes(2), corrected: true),
            ClockCheck.Line(TimeSpan.FromMinutes(-2), corrected: true),
            ClockCheck.Line(TimeSpan.FromMinutes(2), corrected: false)
        ];

        HashSet<string> reached = [.. lines.OfType<string>()];

        Assert.Equal([.. ClockCheck.Lines.Order()], [.. reached.Order()]);
    }
}
