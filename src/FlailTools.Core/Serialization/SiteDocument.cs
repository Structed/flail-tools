using System.Text.Json;
using System.Text.Json.Serialization;
using FlailTools.Core.Generation;
using FlailTools.Core.Model;
using Structed.Inkwell.Serialization;

namespace FlailTools.Core.Serialization;

/// <summary>
/// A saved site: the seed and the locks, not the words.
/// </summary>
/// <remarks>
/// <para>
/// What is written down is the plan, because the plan is what a site actually is — generation is a
/// pure function of it. Storing the rolled text instead would double the file and create a second
/// copy that could disagree with the first.
/// </para>
/// <para>
/// <see cref="Name"/> is the one exception, and it is here only so a downloaded file can be called
/// after the place rather than after its seed. Nothing reads it back.
/// </para>
/// </remarks>
public sealed record SiteDocument : IGeneratedDocument
{
    /// <summary>
    /// The format id, deliberately nullable.
    /// </summary>
    /// <remarks>
    /// A file that does not say what it is must be rejected rather than hopefully parsed, and a
    /// coerced default would take that ability away.
    /// </remarks>
    public string? Format { get; init; }

    public int Version { get; init; }

    public string Name { get => field ?? ""; init; } = "";

    public uint Seed { get; init; }

    /// <summary>The kind that was asked for, or <c>null</c> if it was rolled.</summary>
    public string? Kind { get; init; }

    public IReadOnlyDictionary<string, string> Pins
    {
        get => field ?? new Dictionary<string, string>(StringComparer.Ordinal);
        init;
    } = new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Rerolls
    {
        get => field ?? new Dictionary<string, int>(StringComparer.Ordinal);
        init;
    } = new Dictionary<string, int>(StringComparer.Ordinal);
}

/// <summary>Reads and writes <see cref="SiteDocument"/> files.</summary>
public static class SiteDocuments
{
    /// <summary>
    /// What this tool's files say they are.
    /// </summary>
    /// <remarks>
    /// Includes the umbrella noun rather than any one kind, because a dungeon and a landmark are the
    /// same document with different pins in it.
    /// </remarks>
    public const string FormatId = "flail-tools/site";

    public const int CurrentVersion = 1;

    public const string FileExtension = ".flail.json";

    public static string Write(AdventureSite site, SitePlan plan)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plan);

        SiteDocument document = new()
        {
            Format = FormatId,
            Version = CurrentVersion,
            Name = site.Name,
            Seed = plan.Seed,
            Kind = plan.Kind,
            Pins = plan.Pins,
            Rerolls = plan.Rerolls
        };

        return JsonSerializer.Serialize(document, DocumentJsonContext.Default.SiteDocument);
    }

    /// <summary>Reads a saved file back into the plan that produced it.</summary>
    /// <exception cref="DocumentFormatException">The file is not one of ours, or is too new.</exception>
    public static SitePlan Read(string json)
    {
        SiteDocument document = DocumentEnvelope.Read(
            json,
            FormatId,
            CurrentVersion,
            DocumentJsonContext.Default.SiteDocument);

        return new SitePlan
        {
            Seed = document.Seed,
            Kind = document.Kind,
            Pins = document.Pins,
            Rerolls = document.Rerolls
        };
    }

    /// <summary><c>true</c> when a pasted or dropped file looks like one of ours.</summary>
    public static bool Matches(string json) => DocumentEnvelope.Matches(json, FormatId);

    /// <summary>What to call the downloaded file.</summary>
    public static string FileName(AdventureSite site)
    {
        ArgumentNullException.ThrowIfNull(site);

        return DocumentEnvelope.Slug(site.Name, "site") + FileExtension;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true)]
[JsonSerializable(typeof(SiteDocument))]
public sealed partial class DocumentJsonContext : JsonSerializerContext;
