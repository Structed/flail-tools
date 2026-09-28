using FlailTools.Core.Characters;
using FlailTools.Core.Serialization;
using Microsoft.JSInterop;

namespace FlailTools.Web;

/// <summary>
/// The characters this browser is keeping, and the only thing that touches storage to get them.
/// </summary>
/// <remarks>
/// <para>
/// There is no server, so this is the whole of "saved". A browser's storage is also the one thing
/// in this tool that can refuse: private windows, a full quota and a browser set to deny it all
/// fail at the same call. So <see cref="SaveAsync"/> reports whether the write actually happened,
/// because a party list that silently forgot the character somebody just pressed Save on is worse
/// than one that says it could not.
/// </para>
/// <para>
/// A service rather than a helper on each page, so that the party list and a single sheet cannot
/// drift into reading the key two different ways.
/// </para>
/// </remarks>
public sealed class LocalStore(IJSRuntime js)
{
    /// <summary>Everything stored, or nobody if storage is unreadable or holds nonsense.</summary>
    public async Task<CharacterRoster> LoadAsync() => PartyDocuments.Read(await ReadAsync());

    /// <summary><c>true</c> when the roster actually reached storage.</summary>
    public async Task<bool> SaveAsync(CharacterRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        try
        {
            return await js.InvokeAsync<bool>(
                "flailTools.write",
                PartyDocuments.StorageKey,
                PartyDocuments.Write(roster));
        }
        catch (JSException)
        {
            return false;
        }
    }

    private async Task<string?> ReadAsync()
    {
        try
        {
            return await js.InvokeAsync<string?>("flailTools.read", PartyDocuments.StorageKey);
        }
        catch (JSException)
        {
            return null;
        }
    }
}
