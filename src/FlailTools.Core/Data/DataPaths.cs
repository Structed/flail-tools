namespace FlailTools.Core.Data;

/// <summary>
/// Where each data file lives, relative to the data root.
/// </summary>
/// <remarks>
/// There is no <c>srd/</c> directory here, and its absence is the design. The Games Omnivorous
/// Third-Party Licence permits reusing random tables and their entries, so a directory of
/// book-derived tables would now be allowed; this tool still does not have one. Every table it rolls
/// on is original and all of them live under <c>house/</c>. A data file appearing anywhere else
/// would be a visible, reviewable event rather than a judgement call buried in a diff.
/// </remarks>
public static class DataPaths
{
    public const string HouseRoot = "house";

    public const string Ui = "ui.json";

    public const string Site = $"{HouseRoot}/site.json";

    public const string Silhouettes = $"{HouseRoot}/silhouettes.json";

    public const string Dungeon = $"{HouseRoot}/dungeon.json";

    public const string Cave = $"{HouseRoot}/cave.json";

    public const string Tower = $"{HouseRoot}/tower.json";

    public const string Location = $"{HouseRoot}/location.json";

    public const string Landmark = $"{HouseRoot}/landmark.json";

    /// <summary>Every table file, in the order the about page lists them.</summary>
    public static IReadOnlyList<string> TableFiles { get; } =
    [
        Site,
        Silhouettes,
        Dungeon,
        Cave,
        Tower,
        Location,
        Landmark
    ];

    /// <summary>Every file the app loads, including the interface strings.</summary>
    public static IReadOnlyList<string> AllFiles { get; } = [Ui, .. TableFiles];
}
