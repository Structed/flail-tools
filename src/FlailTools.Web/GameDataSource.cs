using FlailTools.Core.Data;
using Structed.Inkwell.Data;

namespace FlailTools.Web;

/// <summary>
/// Loads the data files once and hands the same copy to every page.
/// </summary>
/// <remarks>
/// The load is kept as a <see cref="Task"/> rather than awaited at start-up so the shell paints
/// while the files are in flight, and so a second page awaiting it gets the finished result rather
/// than a second download.
/// </remarks>
public sealed class GameDataSource(IDataFileReader reader)
{
    private Task<GameData>? _loading;

    public Task<GameData> GetAsync(CancellationToken cancellationToken = default) =>
        _loading ??= GameData.LoadAsync(reader, cancellationToken: cancellationToken);
}
