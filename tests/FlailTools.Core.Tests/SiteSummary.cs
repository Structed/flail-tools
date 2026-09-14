using System.Text;
using FlailTools.Core.Generation;
using FlailTools.Core.Model;

namespace FlailTools.Core.Tests;

/// <summary>
/// Writes a site out as text, so two of them can be compared for what they say.
/// </summary>
/// <remarks>
/// <see cref="AdventureSite"/> is a record, but records compare collection properties by reference,
/// so two identically generated sites are never equal. Comparing this instead says what the tests
/// actually mean: not "the same object", but "the same place, field for field". It doubles as the
/// golden manifest, which keeps the thing the baseline pins and the thing the tests compare from
/// drifting apart.
/// </remarks>
internal static class SiteSummary
{
    public static string Describe(AdventureSite site, SitePlan plan)
    {
        StringBuilder text = new();
        Append(text, site, plan);

        return text.ToString();
    }

    public static void Append(StringBuilder text, AdventureSite site, SitePlan plan)
    {
        text.Append("== ").Append(site.Kind).Append(' ').Append(plan.Seed).Append('\n');
        text.Append("name\t").Append(site.Name).Append('\n');
        text.Append("silhouette\t").Append(site.Silhouette).Append('\t').Append(site.Shape)
            .Append('\t').Append(site.HasWater ? "water" : "dry")
            .Append('\t').Append(site.Scale).Append('\n');

        foreach (SiteField field in site.Fields)
        {
            text.Append("field\t").Append(field.Path).Append('\t')
                .Append(site.PinValues.GetValueOrDefault(field.Path, "-")).Append('\t')
                .Append(field.Value).Append('\n');
        }

        foreach (SiteArea area in site.Areas)
        {
            text.Append("area\t").Append(area.Number).Append('\t')
                .Append(area.Role).Append('\t')
                .Append(area.Path).Append('\t')
                .Append(site.PinValues.GetValueOrDefault(area.Path, "-")).Append('\t')
                .Append(area.Value).Append('\n');
        }
    }
}
