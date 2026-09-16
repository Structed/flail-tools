using System.Text;

namespace FlailTools.Core;

/// <summary>
/// The notices the Games Omnivorous Third-Party Licence requires, held as code rather than data.
/// </summary>
/// <remarks>
/// <para>
/// These two sentences are not ours to edit, shorten or reword, and the licence grants no right to
/// translate them. Keeping them in <c>ui.json</c> alone would make them look like ordinary interface
/// strings: a future locale file, or a well-meant tidy-up, could quietly replace them and the build
/// would stay green. So the canonical wording lives here, and a test asserts that whatever the
/// interface actually renders still reads exactly this once the markup is taken off.
/// </para>
/// <para>
/// <c>ui.json</c> still owns the rendered form, because the notice wants a link in it and the site
/// should not be hard-coding anchors. What it may not do is change the words.
/// </para>
/// </remarks>
public static class Attribution
{
    /// <summary>The name the licence notice attributes this tool to.</summary>
    public const string Attributor = "the flail-tools contributors";

    /// <summary>The publisher whose licence this tool is published under.</summary>
    public const string Publisher = "Games Omnivorous";

    /// <summary>Where the licence itself is published.</summary>
    public const string LicenceUrl = "https://gamesomnivorous.com/pages/flail-license";

    /// <summary>The independence notice, verbatim and mandatory.</summary>
    public const string IndependentNotice =
        $"FLAIL! Tools is an independent production by {Attributor} and is not affiliated with " +
        $"{Publisher}. It is published under the {Publisher} Third-Party Licence.";

    /// <summary>The copyright notice, verbatim and mandatory.</summary>
    public const string CopyrightNotice = $"FLAIL is copyright of {Publisher}.";

    /// <summary>Both notices, in the order they are displayed.</summary>
    public static IReadOnlyList<string> RequiredNotices { get; } = [IndependentNotice, CopyrightNotice];

    /// <summary>
    /// <c>true</c> when <paramref name="html"/> still says exactly <paramref name="notice"/>.
    /// </summary>
    /// <remarks>
    /// Markup and the whitespace that comes with pretty-printed HTML are ignored; every word is not.
    /// </remarks>
    public static bool Preserves(string? html, string notice) =>
        string.Equals(PlainText(html), Normalise(notice), StringComparison.Ordinal);

    /// <summary>The readable text of a fragment of HTML, with tags removed and whitespace collapsed.</summary>
    public static string PlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return "";
        }

        StringBuilder text = new(html.Length);
        bool inTag = false;

        foreach (char character in html)
        {
            if (character == '<')
            {
                inTag = true;
            }
            else if (character == '>')
            {
                inTag = false;
            }
            else if (!inTag)
            {
                text.Append(character);
            }
        }

        return Normalise(Decode(text.ToString()));
    }

    private static string Decode(string text) => text
        .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
        .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
        .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase)
        .Replace("&quot;", "\"", StringComparison.OrdinalIgnoreCase)
        .Replace("&#39;", "'", StringComparison.Ordinal)
        .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase);

    private static string Normalise(string text)
    {
        StringBuilder normalised = new(text.Length);
        bool pendingSpace = false;

        foreach (char character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = normalised.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                normalised.Append(' ');
                pendingSpace = false;
            }

            normalised.Append(character);
        }

        return normalised.ToString();
    }
}
