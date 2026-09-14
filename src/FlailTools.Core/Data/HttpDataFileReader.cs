using Structed.Inkwell.Data;

namespace FlailTools.Core.Data;

/// <summary>
/// Fetches data files over HTTP, which is how they arrive in the browser.
/// </summary>
/// <remarks>
/// The counterpart to the engine's <see cref="FileSystemDataFileReader"/>, which the tests use to
/// read the very same files off disk. Both sit behind <see cref="IDataFileReader"/> so the loader,
/// the validator and every generator are unaware of the difference.
/// </remarks>
public sealed class HttpDataFileReader(HttpClient client, string dataRoot = "data") : IDataFileReader
{
    private readonly HttpClient _client = client ?? throw new ArgumentNullException(nameof(client));

    private readonly string _dataRoot = dataRoot?.TrimEnd('/') ??
        throw new ArgumentNullException(nameof(dataRoot));

    public async Task<Stream> OpenAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string url = $"{_dataRoot}/{relativePath.TrimStart('/')}";

        HttpResponseMessage response = await _client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new GameDataException(
                $"The data file '{url}' could not be fetched: {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }
}
