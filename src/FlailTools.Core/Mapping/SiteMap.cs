using FlailTools.Core.Model;
using Structed.Inkwell.Mapping;

namespace FlailTools.Core.Mapping;

public abstract record SiteMap;

public sealed record OverviewSiteMap(PlaceMap Layout) : SiteMap;

public sealed record TowerSiteMap(string Subject, IReadOnlyList<TowerFloorPlan> Floors) : SiteMap;

public sealed record TowerFloorPlan(
    SiteArea Area,
    MapPolygon Boundary,
    MapPoint? StairsUp,
    MapPoint? StairsDown,
    bool HasMoat);
