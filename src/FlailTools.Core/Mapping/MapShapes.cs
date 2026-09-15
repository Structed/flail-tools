namespace FlailTools.Core.Mapping;

/// <summary>
/// The silhouettes the engine knows how to draw.
/// </summary>
/// <remarks>
/// These are the engine's abstract archetypes, not FLAIL!'s vocabulary. Tower floor plans use a
/// square footprint for <see cref="Boxy"/> and a round one for <see cref="Vessel"/> instead of the
/// overview generator. Listed here so that a typo in a data file is caught when the files load
/// rather than quietly drawn as a <see cref="Hollow"/>, which is the overview engine's fallback.
/// </remarks>
public static class MapShapes
{
    /// <summary>The engine's fallback: an enclosed, roughly round space.</summary>
    public const string Hollow = "hollow";

    /// <summary>A long thin run.</summary>
    public const string Linear = "linear";

    /// <summary>Tall and contained.</summary>
    public const string Vessel = "vessel";

    /// <summary>Square-cornered and built.</summary>
    public const string Boxy = "boxy";

    /// <summary>Many small chambers knotted together.</summary>
    public const string Warren = "warren";

    /// <summary>Spread out and loosely connected.</summary>
    public const string Sprawl = "sprawl";

    public static IReadOnlyList<string> All { get; } = [Hollow, Linear, Vessel, Boxy, Warren, Sprawl];

    public static bool IsKnown(string? shape) =>
        shape is not null && All.Contains(shape, StringComparer.Ordinal);
}
