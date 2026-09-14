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
}
