using System.Text.Json.Serialization;
using Structed.Inkwell.Data;

namespace FlailTools.Core.Data;

/// <summary>
/// The provenance header every data file carries.
/// </summary>
/// <remarks>
/// <para>
/// A base record rather than a repeated property because these files are only ever read. The
/// engine's <see cref="Structed.Inkwell.Serialization.IGeneratedDocument"/> is an interface
/// precisely to stop a base record reordering serialised output, but nothing here is written back
/// out, so that hazard does not apply.
/// </para>
/// <para>
/// Every non-nullable property in this namespace coerces its backing field in the getter. The
/// System.Text.Json source generator discards property initialisers, so a property absent from a
/// JSON file arrives as null — or, for a number, as zero — however it was declared here.
/// </para>
/// </remarks>
public abstract record DataFile
{
    [JsonPropertyName("_source")]
    public DataProvenance Source { get => field ?? new DataProvenance(); init; } = new();
}

/// <summary>One kind of adventure site, and how often it comes up.</summary>
public sealed record KindRow
{
    public string Id { get => field ?? ""; init; } = "";

    /// <summary>Relative likelihood. Coerced to 1 so an unweighted row is merely ordinary, not impossible.</summary>
    public int Weight { get => field > 0 ? field : 1; init; } = 1;
}

/// <summary>
/// One silhouette a site can be drawn inside, and which kinds may be drawn as it.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of the mapping from a kind of place onto one of the engine's abstract
/// archetypes. It lives in data rather than in a switch inside the generator, so adding a sort of
/// place is a data edit and the engine goes on knowing nothing about what a <c>vessel</c> is.
/// </para>
/// <para>
/// A row carries <strong>no prose whatsoever</strong> — no name, no description, only layout
/// instruction. That is deliberate: this file can therefore ship complete and correct without
/// holding a single word that could be anybody's but ours.
/// </para>
/// </remarks>
public sealed record SilhouetteRow
{
    public string Id { get => field ?? ""; init; } = "";

    /// <summary>
    /// The engine archetype: <c>hollow</c>, <c>linear</c>, <c>vessel</c>, <c>boxy</c>,
    /// <c>warren</c> or <c>sprawl</c>. Anything else the engine draws as a hollow.
    /// </summary>
    public string Shape { get => field ?? ""; init; } = "";

    /// <summary>Which site kinds this silhouette is available to.</summary>
    public IReadOnlyList<string> Kinds { get => field ?? []; init; } = [];

    public bool HasWater { get; init; }

    public int Weight { get => field > 0 ? field : 1; init; } = 1;

    public bool Allows(string kind) => Kinds.Contains(kind, StringComparer.Ordinal);
}
