namespace FlailTools.Core.Reporting;

/// <summary>
/// Turns "something went wrong" into a GitHub issue form with the tedious half already filled in.
/// </summary>
/// <remarks>
/// <para>
/// There is no server here to hold a token, so the tool cannot file an issue itself. What it can do
/// is open GitHub's new-issue page on a form that already knows which page the reporter was on and
/// what they were running, and leave them to say what went wrong. The reporter needs a GitHub
/// account; that is the price of not running anything.
/// </para>
/// <para>
/// The labels are not in the link. GitHub honours a <c>labels=</c> query parameter only from people
/// allowed to label issues, and silently drops it for everyone else — which is everyone reporting
/// from the tool. Labels named by the issue template are applied whoever submits it, so they live
/// there, and <see cref="Labels"/> is what a test holds the template to.
/// </para>
/// <para>
/// The issue is public, and this tool has one secret: a dice table code, which is the whole of a
/// table's key and sits in the dice page's address. So the page is reported against an allow-list
/// rather than a deny-list. A new route, or a new parameter on an old one, is reported without its
/// query until somebody decides it is safe to share — the failure mode is a report missing a
/// detail, never a report leaking one.
/// </para>
/// </remarks>
public static class ProblemReport
{
    /// <summary>Where issues are filed.</summary>
    public const string Repository = "https://github.com/Structed/flail-tools";

    /// <summary>The issue form under <c>.github/ISSUE_TEMPLATE/</c> the link opens.</summary>
    /// <remarks>
    /// GitHub resolves it against the default branch, so a template that exists only on a feature
    /// branch opens a blank issue instead — with no labels and no error.
    /// </remarks>
    public const string Template = "user-report.yml";

    /// <summary>The form field that receives the page the reporter was on.</summary>
    public const string PageField = "page";

    /// <summary>The form field that receives the build, the browser and the window size.</summary>
    public const string EnvironmentField = "environment";

    /// <summary>The labels the template must apply to every report.</summary>
    public static IReadOnlyList<string> Labels { get; } = ["user reported", "triage"];

    /// <summary>The screenshot is on the clipboard.</summary>
    public const string Copied = "copied";

    /// <summary>The clipboard refused the picture, so it was saved as a file instead.</summary>
    public const string Downloaded = "downloaded";

    /// <summary>The reporter declined to share the tab.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>The browser could not take the picture at all.</summary>
    public const string Failed = "failed";

    /// <summary>Every way taking a screenshot can end.</summary>
    /// <remarks>
    /// Listed so a test can assert each one has a line of wording waiting for it. The screenshot is
    /// taken in JavaScript and its outcome arrives as a bare string, which is looked up by key at
    /// runtime where the wording test's literal scan cannot see it.
    /// </remarks>
    public static IReadOnlyList<string> Outcomes { get; } = [Copied, Downloaded, Cancelled, Failed];

    /// <summary>The longest link this will hand to GitHub.</summary>
    /// <remarks>
    /// GitHub answers an over-long new-issue link with <c>414 URI Too Long</c> rather than a form,
    /// somewhere around eight thousand characters. A generator link carrying many locks is the only
    /// thing here that grows, so this leaves it room and then gives up on it before GitHub does.
    /// </remarks>
    internal const int MaximumLength = 6000;

    /// <summary>How much of the browser's user agent is worth keeping.</summary>
    internal const int MaximumUserAgentLength = 300;

    /// <summary>The routes whose query may be reported, because it is a seed rather than a key.</summary>
    /// <remarks>
    /// The generator's query is its seed, kind, locks and re-rolls — exactly what a maintainer needs
    /// to see the same site, and nothing anybody could misuse. Everything else is reported by path.
    /// </remarks>
    private static readonly string[] SharedQueries = ["site"];

    /// <summary>
    /// The page to report, with anything not known to be safe to publish taken off it.
    /// </summary>
    /// <param name="page">The address as the browser has it, which may be newer than the router's.</param>
    /// <param name="baseUri">The deployment's base address, ending in a slash.</param>
    /// <remarks>
    /// The browser's own address is asked for rather than the router's, because the pages rewrite
    /// it with <c>history.replaceState</c> as the reporter works and the router is never told. The
    /// fragment always goes. An address that is not under the base at all is reported as the base,
    /// rather than guessed at.
    /// </remarks>
    public static string Page(string? page, string baseUri)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        if (page is null || !page.StartsWith(baseUri, StringComparison.OrdinalIgnoreCase))
        {
            return baseUri;
        }

        string rest = page[baseUri.Length..];
        int hash = rest.IndexOf('#', StringComparison.Ordinal);

        if (hash >= 0)
        {
            rest = rest[..hash];
        }

        int mark = rest.IndexOf('?', StringComparison.Ordinal);
        string route = mark < 0 ? rest : rest[..mark];
        string query = mark < 0 ? "" : rest[mark..];

        bool shared = SharedQueries.Contains(route.Trim('/'), StringComparer.OrdinalIgnoreCase);

        return baseUri + route + (shared && query.Length > 1 ? query : "");
    }

    /// <summary>What the reporter was running, as a block a maintainer can read at a glance.</summary>
    /// <remarks>
    /// Keyed lines for a maintainer rather than wording for a player, so they are not in the wording
    /// file: they are read on GitHub, by whoever triages the issue, in whatever language the report
    /// itself was written in. Joined with a bare line feed so the same report reads the same from
    /// every platform the tests run on.
    /// </remarks>
    public static string Environment(string? build, string? userAgent, string? viewport)
    {
        string agent = (userAgent ?? "").Trim();

        if (agent.Length > MaximumUserAgentLength)
        {
            agent = agent[..MaximumUserAgentLength];
        }

        return string.Join(
            '\n',
            $"Build: {Or(build)}",
            $"Browser: {Or(agent)}",
            $"Window: {Or(viewport)}");

        static string Or(string? value) => value is { Length: > 0 } ? value : "unknown";
    }

    /// <summary>
    /// The link that opens the form, filled in as far as it safely can be.
    /// </summary>
    /// <remarks>
    /// Falls back in steps rather than failing. A generator link with more locks than GitHub will
    /// take is reported by its path; if even that will not fit, the form opens empty. A form with
    /// less filled in is still a report — a <c>414</c> is not.
    /// </remarks>
    public static string NewIssueUrl(string page, string environment)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(environment);

        string url = Compose(page, environment);

        if (url.Length > MaximumLength)
        {
            int mark = page.IndexOf('?', StringComparison.Ordinal);
            url = Compose(mark < 0 ? page : page[..mark], environment);
        }

        return url.Length > MaximumLength ? Compose("", "") : url;
    }

    /// <summary>
    /// The commit this build was made from, shortened the way people paste it.
    /// </summary>
    /// <remarks>
    /// The SDK appends the commit to the informational version as <c>+sha</c> when it builds from a
    /// git checkout, which the deployment always does. A build from anywhere else has no commit to
    /// report, and says so rather than making one up.
    /// </remarks>
    public static string? Build(string? informationalVersion)
    {
        if (informationalVersion is null)
        {
            return null;
        }

        int plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);

        if (plus < 0)
        {
            return null;
        }

        string commit = informationalVersion[(plus + 1)..];

        return commit.Length >= 7 && commit[..7].All(char.IsAsciiHexDigit) ? commit[..7] : null;
    }

    /// <summary>What to tell the reporter about the screenshot they tried to take.</summary>
    /// <remarks>
    /// The outcome arrives as a bare string from JavaScript. Anything unrecognised is read as a
    /// failure here, rather than reaching a wording lookup and rendering as its own key.
    /// </remarks>
    public static string Outcome(string? outcome) => outcome switch
    {
        Copied => "reportCopied",
        Downloaded => "reportDownloaded",
        Cancelled => "reportCancelled",
        _ => "reportCaptureFailed"
    };

    private static string Compose(string page, string environment)
    {
        string url = $"{Repository}/issues/new?template={Uri.EscapeDataString(Template)}";

        if (page.Length > 0)
        {
            url += $"&{PageField}={Uri.EscapeDataString(page)}";
        }

        if (environment.Length > 0)
        {
            url += $"&{EnvironmentField}={Uri.EscapeDataString(environment)}";
        }

        return url;
    }
}
