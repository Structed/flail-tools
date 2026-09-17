using FlailTools.Core.Model;
using Structed.Inkwell.Mapping;

namespace FlailTools.Core.Mapping;

public abstract record SiteMap;

public sealed record OverviewSiteMap(PlaceMap Layout) : SiteMap;

public sealed record TowerSiteMap(string Subject, IReadOnlyList<TowerFloorPlan> Floors) : SiteMap;

/// <summary>
/// One floor, drawn as a plan.
/// </summary>
/// <param name="Footprint">
/// The name of the outline the floor is drawn inside, which comes from the tower's rolled Shape.
/// </param>
/// <param name="Furnishing">
/// The die face the floor's kind was read from, or <c>null</c> when it was pinned to words of
/// somebody's own. It is the face rather than the words so that a translated table still furnishes
/// a library with shelves, and so rewording an entry cannot silently change what is drawn.
/// </param>
public sealed record TowerFloorPlan(
    SiteArea Area,
    string Footprint,
    MapPolygon Boundary,
    MapPoint? StairsUp,
    MapPoint? StairsDown,
    bool HasMoat,
    int? Furnishing);
