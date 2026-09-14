using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Model;
using Structed.Inkwell.Mapping;
using Structed.Inkwell.Rendering;

namespace FlailTools.Core.Mapping;

/// <summary>
/// Turns a generated site into something the map engine can draw.
/// </summary>
/// <remarks>
/// The engine draws abstract silhouettes and has never heard of a dungeon. Everything that knows a
/// wizard's tower should be tall and contained is either in <c>silhouettes.json</c> or here, and
/// nothing about it is in the engine — which is what lets the engine be shared with generators for
/// other games.
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
            string role = ui.RoleName(area.Role);
            string name = role.Length > 0 ? role : ui.AreaName(site.Kind);

            keys.Add(new MapKeySubject(name, area.Value));
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

    /// <summary>Lays out the map for a site.</summary>
    public static PlaceMap Draw(AdventureSite site, SitePlan plan, UiText ui) =>
        MapGenerator.Generate(BriefFor(site, ui), MapGenerator.SeedFor(plan));

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
        PlaceMap map = MapGenerator.Generate(BriefFor(site, ui), seed);

        return SvgMapRenderer.Render(map, seed, new MapRenderOptions
        {
            AriaLabel = ui.Message("mapAlt"),
            CssClass = "site-map",
            IntrinsicSize = true
        });
    }
}
