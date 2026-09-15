using System.Text.Json;
using System.Text.Json.Nodes;
using FlailTools.Core.Data;
using Structed.Inkwell.Data;

namespace FlailTools.Core.Tests;

/// <summary>
/// Reads the very files the site ships, off disk.
/// </summary>
/// <remarks>
/// Deliberately not a copy of them. A fixture that drifts from the deployed data is worse than no
/// fixture, because it goes on passing. The engine's <see cref="FileSystemDataFileReader"/> is the
/// same interface the browser's HTTP reader implements, so what these tests load is what the site
/// loads.
/// </remarks>
internal static class TestData
{
    /// <summary>The directory the site serves as <c>/data</c>.</summary>
    public static string DataRoot { get; } = Locate(Path.Combine("src", "FlailTools.Web", "wwwroot", "data"));

    /// <summary>The repository root, found by walking up to the solution file.</summary>
    public static string RepositoryRoot { get; } = Locate("");

    public static Task<GameData> LoadAsync() => Loaded.Value;

    public static Task<GameData> LoadEmptyAsync() =>
        GameData.LoadAsync(new EmptyTablesReader(new FileSystemDataFileReader(DataRoot)));

    private static readonly Lazy<Task<GameData>> Loaded =
        new(() => GameData.LoadAsync(new FileSystemDataFileReader(DataRoot)));

    private static string Locate(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FlailTools.slnx")))
            {
                return Path.Combine(directory.FullName, relativePath);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find 'FlailTools.slnx' above '{AppContext.BaseDirectory}'.");
    }

    private sealed class EmptyTablesReader(IDataFileReader inner) : IDataFileReader
    {
        public async Task<Stream> OpenAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            if (relativePath == DataPaths.Ui || relativePath == DataPaths.Silhouettes)
            {
                return await inner.OpenAsync(relativePath, cancellationToken);
            }

            await using Stream stream = await inner.OpenAsync(relativePath, cancellationToken);
            JsonObject file = Assert.IsType<JsonObject>(await JsonNode.ParseAsync(
                stream,
                documentOptions: LocalisingDataFileReader.DocumentOptions,
                cancellationToken: cancellationToken));

            foreach ((string key, JsonNode? value) in file.ToArray())
            {
                if (value is JsonArray && key != "kinds")
                {
                    file[key] = new JsonArray();
                }
            }

            return new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(file));
        }
    }
}
