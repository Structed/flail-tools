using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlailTools.Core.Data;

/// <summary>
/// The source-generated deserialiser for every file the app loads.
/// </summary>
/// <remarks>
/// <para>
/// Source-generated because the app is trimmed and ahead-of-time compiled for the browser, where
/// reflection-based serialisation is neither available nor small.
/// </para>
/// <para>
/// The leniency below is copied deliberately from
/// <see cref="Structed.Inkwell.Data.LocalisingDataFileReader.DocumentOptions"/> and must stay equal
/// to it. That reader parses a file into a node tree to merge an overlay over it; this context then
/// parses the result into objects. If one accepts a trailing comma or a comment and the other does
/// not, a data file loads in English and fails in another language, which is a miserable thing to
/// diagnose. A test asserts the two agree.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(UiText))]
[JsonSerializable(typeof(SiteFile))]
[JsonSerializable(typeof(SilhouettesFile))]
[JsonSerializable(typeof(DungeonFile))]
[JsonSerializable(typeof(CaveFile))]
[JsonSerializable(typeof(TowerFile))]
[JsonSerializable(typeof(HexLocationFile))]
[JsonSerializable(typeof(HexLandmarkFile))]
public sealed partial class GameDataJsonContext : JsonSerializerContext;
