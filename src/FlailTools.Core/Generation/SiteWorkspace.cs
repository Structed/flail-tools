using FlailTools.Core.Data;
using FlailTools.Core.Mapping;
using FlailTools.Core.Model;
using FlailTools.Core.Serialization;
using Structed.Inkwell.Randomness;

namespace FlailTools.Core.Generation;

/// <summary>
/// One site being worked on: the plan, what it generated, and the ways a reader changes it.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of the interface's behaviour, and it is here rather than in a component so it
/// can be tested without a browser. A page that uses it holds one of these and draws it; every
/// button is one call.
/// </para>
/// <para>
/// Not a record and not immutable, because it models a thing being fiddled with. The
/// <see cref="Plan"/> inside it is immutable, which is what matters: every change replaces the plan
/// and regenerates, so the site on screen is always exactly what the current link would rebuild.
/// </para>
/// </remarks>
public sealed class SiteWorkspace
{
    private readonly GameData _data;
    private string? _mapSvg;

    public SiteWorkspace(GameData data, SitePlan? plan = null)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));

        Plan = plan ?? new SitePlan { Seed = SeedCodec.CreateRandom() };
        Site = SiteGenerator.Generate(_data, Plan);
    }

    public SitePlan Plan { get; private set; }

    public AdventureSite Site { get; private set; }

    public UiText Ui => _data.Ui;

    /// <summary><c>true</c> while the tables have no entries, so the interface can say so.</summary>
    public bool IsAwaitingContent => !_data.HasContent;

    /// <summary>The short seed as it appears in a link.</summary>
    public string SeedCode => SeedCodec.Encode(Plan.Seed);

    /// <summary>The query string that rebuilds exactly this.</summary>
    public string Query => SiteUrl.ToQuery(Plan);

    /// <summary>
    /// The inked map, drawn once per change rather than once per render.
    /// </summary>
    /// <remarks>
    /// Blazor re-renders far more often than the site changes, and laying out and inking a map is
    /// not free. Cached here rather than in the component so that every consumer gets it.
    /// </remarks>
    public string MapSvg => _mapSvg ??= SiteMapper.RenderSvg(Site, Plan, _data.Ui);

    public string FileName => SiteDocuments.FileName(Site);

    public string ToDocument() => SiteDocuments.Write(Site, Plan);

    /// <summary>
    /// Rolls a fresh seed, keeping the chosen kind and every lock.
    /// </summary>
    /// <remarks>
    /// Locks survive this on purpose: holding a field and then pressing the big button is exactly
    /// how somebody keeps the one detail they liked and asks for a new place around it. A re-roll
    /// that discarded locks would make them pointless.
    /// </remarks>
    public void RollAgain() =>
        Apply(new SitePlan
        {
            Seed = SeedCodec.CreateRandom(),
            Kind = Plan.Kind,
            Pins = Plan.Pins
        });

    public void SetKind(string? kind) => Apply(Plan.WithKind(SiteKinds.IsKnown(kind) ? kind : null));

    public void RerollKind() => Apply(Plan.WithKindRerolled());

    /// <summary>Re-rolls one field, releasing its lock so the new roll is visible.</summary>
    public void Reroll(string path) =>
        Apply(IsNameField(path) ? Plan.WithNameRerolled() : Plan.WithReroll(path));

    public bool IsPinned(string path) =>
        IsNameField(path) ? Plan.IsNamePinned : Plan.IsPinned(path);

    /// <summary>Locks a field to what it currently shows, or releases it.</summary>
    public void TogglePin(string path)
    {
        if (IsPinned(path))
        {
            Apply(IsNameField(path) ? Plan.WithNameUnpinned() : Plan.WithoutPin(path));
            return;
        }

        if (IsNameField(path))
        {
            Apply(Plan.WithNamePinned(Site.PinValues));
            return;
        }

        Apply(Site.PinValues.TryGetValue(path, out string? pinValue)
            ? Plan.WithPin(path, pinValue)
            : Plan);
    }

    /// <summary>Replaces a field with words somebody typed, and locks it there.</summary>
    /// <remarks>
    /// Stored as the text itself rather than as a row position, which is exactly what
    /// <c>PinReference</c> falls back to when what it holds is not a <c>#N</c>. So a typed field
    /// survives a re-roll of everything around it, and survives the table being rewritten later.
    /// </remarks>
    public void SetText(string path, string text) => Apply(Plan.WithPin(path, text));

    public void Apply(SitePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        Plan = plan;
        Site = SiteGenerator.Generate(_data, plan);
        _mapSvg = null;
    }

    /// <summary>The label to show beside a field.</summary>
    public string LabelFor(string path) => _data.Ui.LabelFor(path);

    /// <summary>What to call one numbered part of this site: a room, a chamber, a floor.</summary>
    public string AreaName(SiteArea area)
    {
        ArgumentNullException.ThrowIfNull(area);

        string role = _data.Ui.RoleName(area.Role);

        return role.Length > 0 ? role : _data.Ui.AreaName(Site.Kind);
    }

    /// <summary>The name is two rolls but one field, so both halves move together.</summary>
    private static bool IsNameField(string path) =>
        FieldPaths.NameParts.Contains(path, StringComparer.Ordinal);
}
