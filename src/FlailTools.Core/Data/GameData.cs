using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FlailTools.Core.Mapping;
using Structed.Inkwell.Data;

namespace FlailTools.Core.Data;

/// <summary>Where one data file came from, for the about page to show.</summary>
public sealed record DataFileProvenance(string Path, DataProvenance Source);

/// <summary>
/// Every data file, loaded and checked once.
/// </summary>
/// <remarks>
/// Loaded through the engine's <see cref="LocalisingDataFileReader"/> even though only English
/// ships. Wiring it later would mean revisiting every call site at the moment there is also a
/// translation to get right; wiring it now costs a constructor argument and the decorator is a
/// pass-through for the canonical locale.
/// </remarks>
public sealed class GameData
{
    private GameData(
        Locale locale,
        UiText ui,
        SiteFile site,
        SilhouettesFile silhouettes,
        DungeonFile dungeon,
        CaveFile cave,
        TowerFile tower,
        HexLocationFile location,
        HexLandmarkFile landmark)
    {
        Locale = locale;
        Ui = ui;
        Site = site;
        Silhouettes = silhouettes;
        Dungeon = dungeon;
        Cave = cave;
        Tower = tower;
        Location = location;
        Landmark = landmark;

        Provenance =
        [
            new DataFileProvenance(DataPaths.Site, site.Source),
            new DataFileProvenance(DataPaths.Silhouettes, silhouettes.Source),
            new DataFileProvenance(DataPaths.Dungeon, dungeon.Source),
            new DataFileProvenance(DataPaths.Cave, cave.Source),
            new DataFileProvenance(DataPaths.Tower, tower.Source),
            new DataFileProvenance(DataPaths.Location, location.Source),
            new DataFileProvenance(DataPaths.Landmark, landmark.Source)
        ];

        List<string> notices = [.. Attribution.RequiredNotices];

        foreach (DataFileProvenance file in Provenance)
        {
            foreach (string notice in file.Source.Notices)
            {
                if (!string.IsNullOrWhiteSpace(notice) &&
                    !notices.Contains(notice, StringComparer.Ordinal))
                {
                    notices.Add(notice);
                }
            }
        }

        Notices = notices;
    }

    public Locale Locale { get; }

    public UiText Ui { get; }

    public SiteFile Site { get; }

    public SilhouettesFile Silhouettes { get; }

    public DungeonFile Dungeon { get; }

    public CaveFile Cave { get; }

    public TowerFile Tower { get; }

    /// <summary>The hexcrawl Locations tables.</summary>
    public HexLocationFile Location { get; }

    /// <summary>The hexcrawl Landmarks tables.</summary>
    public HexLandmarkFile Landmark { get; }

    /// <summary>Where every table file came from, in the order the about page lists them.</summary>
    public IReadOnlyList<DataFileProvenance> Provenance { get; }

    /// <summary>
    /// Every notice that must be shown: the licence's two, then anything a data file adds.
    /// </summary>
    /// <remarks>
    /// The licence's notices lead and are always present, whatever the files say, so that a data
    /// file cannot become the reason the tool stops complying.
    /// </remarks>
    public IReadOnlyList<string> Notices { get; }

    /// <summary>
    /// <c>true</c> once every generator has a table to roll on.
    /// </summary>
    /// <remarks>
    /// Deliberately an <c>and</c>, not an <c>or</c>: the interface uses this to decide whether to
    /// explain itself instead of showing blank fields, and a half-loaded set of files is exactly
    /// the case worth explaining. Any one table arriving is not enough, because the generator a
    /// reader happens to pick may be one of the empty ones.
    /// </remarks>
    public bool HasContent =>
        Site.NameStems.Count > 0 &&
        Dungeon.Flavours.Count > 0 &&
        Cave.Flavours.Count > 0 &&
        Tower.Shapes.Count > 0 &&
        Location.Locations.Count > 0 &&
        Landmark.Landmarks.Count > 0;

    public static async Task<GameData> LoadAsync(
        IDataFileReader reader,
        Locale? locale = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        Locale resolved = locale ?? Locale.English;
        LocalisingDataFileReader localising = new(reader, resolved);

        GameData data = new(
            resolved,
            await ReadAsync(localising, DataPaths.Ui, GameDataJsonContext.Default.UiText, cancellationToken),
            await ReadAsync(localising, DataPaths.Site, GameDataJsonContext.Default.SiteFile, cancellationToken),
            await ReadAsync(localising, DataPaths.Silhouettes, GameDataJsonContext.Default.SilhouettesFile, cancellationToken),
            await ReadAsync(localising, DataPaths.Dungeon, GameDataJsonContext.Default.DungeonFile, cancellationToken),
            await ReadAsync(localising, DataPaths.Cave, GameDataJsonContext.Default.CaveFile, cancellationToken),
            await ReadAsync(localising, DataPaths.Tower, GameDataJsonContext.Default.TowerFile, cancellationToken),
            await ReadAsync(localising, DataPaths.Location, GameDataJsonContext.Default.HexLocationFile, cancellationToken),
            await ReadAsync(localising, DataPaths.Landmark, GameDataJsonContext.Default.HexLandmarkFile, cancellationToken));

        data.Validate();

        return data;
    }

    /// <summary>
    /// Checks that the files hold together, and says which file is wrong when they do not.
    /// </summary>
    /// <remarks>
    /// Structure is required; entries are not. A file may legitimately have an empty table — that is
    /// the state this project ships in first — but it may not name a kind that does not exist, leave
    /// a kind undrawable, or give a die roll anything other than one outcome per face.
    /// </remarks>
    public void Validate()
    {
        HashSet<string> kindIds = new(StringComparer.Ordinal);

        foreach (KindRow kind in Site.Kinds)
        {
            if (!SiteKinds.IsKnown(kind.Id))
            {
                throw new GameDataException(
                    $"'{DataPaths.Site}' offers the kind '{kind.Id}', which this tool does not generate.");
            }

            if (!kindIds.Add(kind.Id))
            {
                throw new GameDataException($"'{DataPaths.Site}' lists the kind '{kind.Id}' more than once.");
            }
        }

        foreach (string kind in SiteKinds.All)
        {
            if (!kindIds.Contains(kind))
            {
                throw new GameDataException($"'{DataPaths.Site}' is missing the kind '{kind}'.");
            }
        }

        HashSet<string> silhouetteIds = new(StringComparer.Ordinal);

        foreach (SilhouetteRow silhouette in Silhouettes.Silhouettes)
        {
            if (silhouette.Id.Length == 0)
            {
                throw new GameDataException($"'{DataPaths.Silhouettes}' has a silhouette with no id.");
            }

            if (!silhouetteIds.Add(silhouette.Id))
            {
                throw new GameDataException(
                    $"'{DataPaths.Silhouettes}' lists the silhouette '{silhouette.Id}' more than once.");
            }

            if (!MapShapes.IsKnown(silhouette.Shape))
            {
                throw new GameDataException(
                    $"Silhouette '{silhouette.Id}' in '{DataPaths.Silhouettes}' asks to be drawn as " +
                    $"'{silhouette.Shape}', which is not a shape the map engine knows.");
            }

            foreach (string kind in silhouette.Kinds)
            {
                if (!SiteKinds.IsKnown(kind))
                {
                    throw new GameDataException(
                        $"Silhouette '{silhouette.Id}' in '{DataPaths.Silhouettes}' admits the kind " +
                        $"'{kind}', which this tool does not generate.");
                }
            }
        }

        foreach (string kind in SiteKinds.All)
        {
            if (!Silhouettes.Silhouettes.Any(row => row.Allows(kind)))
            {
                throw new GameDataException(
                    $"No silhouette in '{DataPaths.Silhouettes}' admits the kind '{kind}', so it could " +
                    "never be drawn.");
            }
        }

        RequireFaces(DataPaths.Cave, "chambers", Cave.Chambers, 6);
        RequireFaces(DataPaths.Tower, "floorTypes", Tower.FloorTypes, 6);
        RequireFaces(DataPaths.Tower, "topFloorTypes", Tower.TopFloorTypes, 4);

        // A chamber face either carries the book's d4 or carries nothing, so an empty row here means
        // "this face has no second roll" rather than "not written yet". Both read as zero, which is
        // why the outer count is checked: four rows would silently unpair the two that do have one.
        if (Cave.ChamberDetails.Count is not 0)
        {
            RequireFaces(DataPaths.Cave, "chamberDetails", Cave.ChamberDetails, 6);

            for (int index = 0; index < Cave.ChamberDetails.Count; index++)
            {
                RequireFaces(DataPaths.Cave, $"chamberDetails[{index}]", Cave.ChamberDetails[index], 4);
            }
        }

        if (Tower.FloorDetails.Count is not 0)
        {
            RequireFaces(DataPaths.Tower, "floorDetails", Tower.FloorDetails, 6);

            for (int index = 0; index < Tower.FloorDetails.Count; index++)
            {
                RequireFaces(DataPaths.Tower, $"floorDetails[{index}]", Tower.FloorDetails[index], 4);
            }
        }

        // The ten hexcrawl axes are read by RollContext.Text, which picks uniformly over whatever
        // length it is handed, so — unlike the cave and tower tables above — a short one would not
        // fail, roll blank, or look wrong. It would quietly reweight the generator away from the
        // book, and the Biome columns make that unreadable by eye: they repeat entries on purpose,
        // spanning each biome across two or four faces, so a dropped row looks like the spans.
        RequireFaces(DataPaths.Location, "locations", Location.Locations, 20);
        RequireFaces(DataPaths.Location, "biomes", Location.Biomes, 20);
        RequireFaces(DataPaths.Location, "conditions", Location.Conditions, 20);
        RequireFaces(DataPaths.Location, "keyFeatures", Location.KeyFeatures, 20);
        RequireFaces(DataPaths.Location, "occupants", Location.Occupants, 20);

        RequireFaces(DataPaths.Landmark, "landmarks", Landmark.Landmarks, 20);
        RequireFaces(DataPaths.Landmark, "biomes", Landmark.Biomes, 20);
        RequireFaces(DataPaths.Landmark, "conditions", Landmark.Conditions, 20);
        RequireFaces(DataPaths.Landmark, "keyFeatures", Landmark.KeyFeatures, 20);
        RequireFaces(DataPaths.Landmark, "occupants", Landmark.Occupants, 20);
    }

    /// <summary>A table read by a die must have exactly one outcome per face, or none at all yet.</summary>
    private static void RequireFaces<T>(string file, string table, IReadOnlyList<T> rows, int faces)
    {
        if (rows.Count is not 0 && rows.Count != faces)
        {
            throw new GameDataException(
                $"'{file}' gives '{table}' {rows.Count} entries, but it is read by a d{faces} and so " +
                $"needs exactly {faces}.");
        }
    }

    private static async Task<T> ReadAsync<T>(
        IDataFileReader reader,
        string path,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        await using Stream stream = await reader.OpenAsync(path, cancellationToken);

        try
        {
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken)
                ?? throw new GameDataException($"The data file '{path}' is empty.");
        }
        catch (JsonException error)
        {
            throw new GameDataException($"The data file '{path}' could not be read: {error.Message}", error);
        }
    }
}
