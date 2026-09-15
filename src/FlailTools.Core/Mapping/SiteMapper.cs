using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Model;
using Structed.Inkwell.Mapping;
using Structed.Inkwell.Rendering;

namespace FlailTools.Core.Mapping;

/// <summary>
/// Turns a generated site into an overview or a stack of tower floor plans.
/// </summary>
/// <remarks>
/// Overview maps use the engine's abstract silhouettes. Towers need one plan per floor rather than
/// roads and buildings inside a silhouette; their layout lives here and reuses the engine's seeded
/// pen without teaching the engine anything about FLAIL!.
/// </remarks>
public static class SiteMapper
{
    public static MapBrief BriefFor(AdventureSite site, UiText ui)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(ui);

        List<MapKeySubject> keys = [];

        foreach (SiteArea area in site.Areas)
        {
            keys.Add(new MapKeySubject(ui.AreaName(site.Kind, area.Role), area.Value));
        }

        return new MapBrief
        {
            Shape = site.Shape,
            Scale = site.Scale,
            Subject = site.Name,
            HasWater = site.HasWater,
            Keys = keys
        };
    }

    /// <summary>Lays out an overview, or separate tower floors ordered from ground to top.</summary>
    public static SiteMap Draw(AdventureSite site, SitePlan plan, UiText ui)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(ui);

        return site.Kind == SiteKinds.Tower
            ? TowerFloorPlans.Draw(site)
            : new OverviewSiteMap(MapGenerator.Generate(BriefFor(site, ui), MapGenerator.SeedFor(plan)));
    }

    /// <summary>
    /// Draws the map and inks it as SVG.
    /// </summary>
    /// <remarks>
    /// <see cref="MapRenderOptions.IntrinsicSize"/> is on because the SVG is downloaded as well as
    /// shown. An SVG without a width and height renders at the 300×150 default object size the
    /// moment it leaves the page it was styled on.
    /// </remarks>
    public static string RenderSvg(AdventureSite site, SitePlan plan, UiText ui)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(ui);

        uint seed = MapGenerator.SeedFor(plan);
        return Draw(site, plan, ui) switch
        {
            TowerSiteMap tower => TowerFloorPlans.Render(tower, seed, ui),
            OverviewSiteMap overview => SvgMapRenderer.Render(overview.Layout, seed, new MapRenderOptions
            {
                AriaLabel = ui.Message("mapAlt"),
                CssClass = "site-map",
                IntrinsicSize = true
            }),
            _ => throw new InvalidOperationException("The site map has no supported rendering.")
        };
    }
}
