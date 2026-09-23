namespace FlailTools.Core.Tests;

/// <summary>
/// The dice, the table and the channel have moved out. This is the test that stops them moving back.
/// </summary>
/// <remarks>
/// <para>
/// They were built here, proved at a real table, and then lifted into <c>Structed.Inkwell</c> so the
/// Mausritter tool could have the same table without a second implementation of it. The lift was
/// cheap because the code had been kept free of this repository's furniture from the start; what
/// makes it expensive to keep is the opposite pressure. A roll needs one more field, the package is
/// not cut yet, and the quickest way forward is a small local copy "just for now".
/// </para>
/// <para>
/// A local copy does not announce itself. Both versions compile, both look right, and the two
/// implementations drift until one browser is saying something the other cannot read — which the
/// people at the table experience as the dice table silently not working with a friend. So the rule
/// is written down here, where it fails loudly: the engine's, or nobody's.
/// </para>
/// <para>
/// Reading the file system rather than the compiled types is deliberate. A file that merely
/// <em>exists</em> is the whole problem, whether or not anything references it yet.
/// </para>
/// </remarks>
public sealed class PortabilityTests
{
    /// <summary>
    /// The one file that is allowed to know which game it is playing.
    /// </summary>
    /// <remarks>
    /// Named rather than pattern-matched, so that adding a second game-specific file is a decision
    /// somebody has to make in this list rather than a side effect of naming a file well.
    /// </remarks>
    private const string StaysBehind = "FlailRolls.cs";

    /// <summary>
    /// Nothing but the FLAIL!-specific presets may live under <c>Dice</c> any more.
    /// </summary>
    /// <remarks>
    /// The rest of what used to be here is <c>Structed.Inkwell.Dice</c>. A new file appearing beside
    /// this one is either game-specific — in which case it belongs to the constant above, and to a
    /// conversation about whether two files still counts as "the file that stays behind" — or it is
    /// engine work that has been started in the wrong repository.
    /// </remarks>
    [Fact]
    public void OnlyTheGameSpecificFileIsLeftUnderDice()
    {
        string root = Path.Combine(TestData.RepositoryRoot, "src", "FlailTools.Core", "Dice");

        string[] strays = [.. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetFileName(path))
            .Where(name => !string.Equals(name, StaysBehind, StringComparison.Ordinal))];

        Assert.True(
            strays.Length == 0,
            $"The dice live in Structed.Inkwell.Dice now. Found here as well: {string.Join(", ", strays)}.");
    }

    /// <summary>
    /// The party code is gone from both projects, and so is the transport it talked through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three folders, because the table came apart into three pieces: the messages and the roster in
    /// <c>Core/Party</c>, the JS interop wrapper in <c>Web/Party</c>, and the Trystero bundle in
    /// <c>wwwroot/js/party</c>. All three ship in <c>Structed.Inkwell.Party.Blazor</c> now, the last
    /// of them served out of <c>_content/</c>.
    /// </para>
    /// <para>
    /// A stale copy of the bundle under <c>wwwroot</c> is the nastiest of the three: it would still
    /// be published, still be fetchable, and be shadowed by the packaged one at a path nobody is
    /// looking at — a dead file that looks maintained.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("src/FlailTools.Core/Party")]
    [InlineData("src/FlailTools.Web/Party")]
    [InlineData("src/FlailTools.Web/wwwroot/js/party")]
    public void ThePartyCodeIsNotHereAnyMore(string folder)
    {
        string path = Path.Combine(TestData.RepositoryRoot, folder.Replace('/', Path.DirectorySeparatorChar));

        Assert.False(
            Directory.Exists(path),
            $"'{folder}' has grown back. The dice table ships in Structed.Inkwell.Party.Blazor.");
    }

    /// <summary>
    /// The app id has to be exactly the string the table codes were handed out under.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It namespaces the signalling, so two browsers only find each other if theirs match. It used
    /// to be a constant buried in this repository's JavaScript; it is now an argument handed to a
    /// channel that knows nothing about this game, which makes it look far more like configuration
    /// than it is.
    /// </para>
    /// <para>
    /// Written out as a literal here rather than referenced, so the test compares the value against
    /// the intent rather than against itself. Tidying it — renaming the app, dropping the vendor
    /// prefix — would quietly invalidate every table code already written on a character sheet, and
    /// the failure looks like the other player simply never joining.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheTableIsStillCalledWhatItWasCalled()
    {
        Assert.Equal("structed-flail-tools-dice", Core.Dice.FlailRolls.PartyAppId);
    }

    /// <summary>
    /// The game-specific file has to stay small enough to be worth having left behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a style rule. If the FLAIL! presets grow into a rules engine, the split stops making
    /// sense and the shape of it needs revisiting — this is where that conversation gets scheduled.
    /// </para>
    /// <para>
    /// Counted in code rather than in lines, because this repository explains itself at length and a
    /// raw line count would have the rule firing at good documentation while a dense hundred lines
    /// of rules arithmetic slipped past it. What is being watched for is logic.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheGameSpecificFileIsStillTheSmallOne()
    {
        string path = Path.Combine(TestData.RepositoryRoot, "src", "FlailTools.Core", "Dice", StaysBehind);

        Assert.True(File.Exists(path), $"'{StaysBehind}' has moved; the extraction rule needs updating.");

        int code = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Count(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal));

        Assert.True(code < 130, $"'{StaysBehind}' is growing into a rules engine.");
    }
}
