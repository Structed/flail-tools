using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FlailTools.Core.Data;
using FlailTools.Core.Serialization;
using Structed.Inkwell.Data;

namespace FlailTools.Core.Tests;

/// <summary>
/// Guards the trap that the System.Text.Json source generator sets.
/// </summary>
/// <remarks>
/// <para>
/// The generator discards property initialisers. A property absent from a data file therefore
/// arrives as <c>null</c> however it was declared — <c>= ""</c> and <c>= []</c> are not honoured —
/// and a number arrives as zero. That turns a missing key in a JSON file into a null reference
/// exception somewhere far away, or a weight of zero that makes a row unpickable.
/// </para>
/// <para>
/// The remedy is to coerce in the getter of every non-nullable property. The remedy is easy to
/// forget on the next property added, so it is checked here by reflection rather than by memory:
/// every root type is deserialised from an empty object and every property that claims it cannot be
/// null is required to prove it.
/// </para>
/// </remarks>
public sealed class JsonDefaultsTests
{
    public static TheoryData<Type> RootTypes()
    {
        TheoryData<Type> types = [];

        foreach (Type type in Registered)
        {
            types.Add(type);
        }

        return types;
    }

    public static TheoryData<Type> DocumentTypes()
    {
        TheoryData<Type> types = [];

        foreach (Type type in RegisteredOn(typeof(DocumentJsonContext)))
        {
            types.Add(type);
        }

        return types;
    }

    [Theory]
    [MemberData(nameof(RootTypes))]
    public void AnEmptyFileNeverProducesANull(Type type)
    {
        JsonTypeInfo? typeInfo = GameDataJsonContext.Default.GetTypeInfo(type);

        Assert.NotNull(typeInfo);

        object? instance = JsonSerializer.Deserialize("{}", typeInfo);

        Assert.NotNull(instance);
        AssertNothingNull(instance, type.Name, depth: 0);
    }

    /// <summary>
    /// The same trap, one step further from anybody who would notice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A data file with a key missing is at least ours, and fails the first time the app is run. A
    /// document is a link somebody was handed, or a key in a browser written by a version that no
    /// longer exists, and it arrives on a machine nobody is watching. Half of one is exactly what
    /// the source generator hands back when a key is absent, so the same coercion rule has to hold
    /// here, and for the same reason it has to be checked rather than remembered.
    /// </para>
    /// <para>
    /// Note this reaches only as far as <see cref="IsOurs"/> does: a list inside a document is
    /// generic, so its members are not walked. The document's own properties are the ones a reader
    /// touches first, and they are the ones pinned.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(DocumentTypes))]
    public void AnEmptyDocumentNeverProducesANull(Type type)
    {
        JsonTypeInfo? typeInfo = DocumentJsonContext.Default.GetTypeInfo(type);

        Assert.NotNull(typeInfo);

        object? instance = JsonSerializer.Deserialize("{}", typeInfo);

        Assert.NotNull(instance);
        AssertNothingNull(instance, type.Name, depth: 0);
    }

    /// <summary>Every type the app deserialises a whole file into.</summary>
    /// <remarks>
    /// Read from the attributes rather than from the context's properties, because the source
    /// generator also emits a property for every type it had to reach through on the way — strings,
    /// booleans, element lists — and none of those is a file.
    /// </remarks>
    private static IReadOnlyList<Type> Registered { get; } = RegisteredOn(typeof(GameDataJsonContext));

    private static IReadOnlyList<Type> RegisteredOn(Type context) =>
    [
        .. context
            .GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType == typeof(JsonSerializableAttribute))
            .Select(attribute => (Type)attribute.ConstructorArguments[0].Value!)
    ];

    [Fact]
    public void AnAbsentWeightIsOrdinaryRatherThanImpossible()
    {
        SiteFile site = JsonSerializer.Deserialize(
            """{ "kinds": [ { "id": "dungeon" } ] }""",
            GameDataJsonContext.Default.SiteFile)!;

        Assert.Equal(1, site.Kinds[0].Weight);
    }

    [Fact]
    public void AnAbsentSilhouetteWeightIsOrdinaryRatherThanImpossible()
    {
        SilhouettesFile silhouettes = JsonSerializer.Deserialize(
            """{ "silhouettes": [ { "id": "warren-dry", "shape": "warren" } ] }""",
            GameDataJsonContext.Default.SilhouettesFile)!;

        Assert.Equal(1, silhouettes.Silhouettes[0].Weight);
        Assert.Empty(silhouettes.Silhouettes[0].Kinds);
    }

    /// <summary>
    /// The two parsers that see every data file have to be equally lenient.
    /// </summary>
    /// <remarks>
    /// The localising reader parses a file into a node tree to merge an overlay over it; this
    /// context then parses the result into objects. If one accepts a comment or a trailing comma and
    /// the other does not, a file loads in English and fails in another language.
    /// </remarks>
    [Fact]
    public void TheLoaderAndTheDeserialiserAgreeOnLeniency()
    {
        JsonSerializerOptions options = GameDataJsonContext.Default.Options;

        Assert.Equal(LocalisingDataFileReader.DocumentOptions.AllowTrailingCommas, options.AllowTrailingCommas);
        Assert.Equal(LocalisingDataFileReader.DocumentOptions.CommentHandling, options.ReadCommentHandling);
    }

    [Fact]
    public void ADataFileMayCarryCommentsAndTrailingCommas()
    {
        SiteFile site = JsonSerializer.Deserialize(
            """
            {
              // Kinds this file offers.
              "kinds": [
                { "id": "cave", "weight": 2 },
              ],
            }
            """,
            GameDataJsonContext.Default.SiteFile)!;

        Assert.Equal("cave", site.Kinds[0].Id);
    }

    private static void AssertNothingNull(object instance, string path, int depth)
    {
        if (depth > 3)
        {
            return;
        }

        NullabilityInfoContext nullability = new();

        foreach (PropertyInfo property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? value = property.GetValue(instance);
            string here = $"{path}.{property.Name}";

            if (nullability.Create(property).ReadState == NullabilityState.NotNull)
            {
                Assert.True(
                    value is not null,
                    $"'{here}' is declared non-nullable but came back null from an empty JSON object. " +
                    "The source generator drops property initialisers, so it needs coercing in the " +
                    "getter: 'get => field ?? \"\";' or 'get => field ?? [];'.");
            }

            if (value is not null && IsOurs(property.PropertyType))
            {
                AssertNothingNull(value, here, depth + 1);
            }
        }
    }

    private static bool IsOurs(Type type) =>
        !type.IsPrimitive &&
        type != typeof(string) &&
        !type.IsGenericType &&
        (type.Assembly == typeof(GameData).Assembly || type.Assembly == typeof(DataProvenance).Assembly);
}
