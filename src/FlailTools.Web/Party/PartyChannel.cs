using FlailTools.Core.Dice;
using FlailTools.Core.Party;
using Microsoft.JSInterop;

namespace FlailTools.Web.Party;

/// <summary>How the connection to the table is going.</summary>
public enum PartyStatus
{
    /// <summary>Not at a table.</summary>
    Away,

    /// <summary>Opening the room.</summary>
    Joining,

    /// <summary>At a table, but no relay is answering, so nobody new can find us.</summary>
    Searching,

    /// <summary>At a table and findable.</summary>
    Open,

    /// <summary>The browser would not do it at all.</summary>
    Failed
}

/// <summary>
/// The page's end of the dice channel.
/// </summary>
/// <remarks>
/// <para>
/// Owns the JavaScript module, the log, and the rule that decides what leaves the machine. Rolls
/// arrive here as strings from other people's browsers and are read by
/// <see cref="RollMessage.TryRead"/> before anything is done with them; nothing on the JavaScript
/// side is trusted to have checked anything, because nothing on the JavaScript side knows what a
/// roll is.
/// </para>
/// <para>
/// Private rolls never reach the channel at all. <see cref="RollAsync"/> ghosts them before sending
/// and keeps the full version only in the local log, and <see cref="Replay"/> ghosts again on the
/// way out. Two independent chances to get it right, because getting it wrong is the one failure
/// here that cannot be undone by refreshing.
/// </para>
/// </remarks>
public sealed class PartyChannel(IJSRuntime js) : IAsyncDisposable
{
    private const string ModulePath = "./js/party/party.js";

    private readonly IJSRuntime js = js;
    private IJSObjectReference? module;
    private DotNetObjectReference<PartyChannel>? self;

    /// <summary>What the table has rolled.</summary>
    public RollLog Log { get; } = new();

    public PartyStatus Status { get; private set; } = PartyStatus.Away;

    /// <summary>The table code, or empty when away.</summary>
    public string Code { get; private set; } = "";

    /// <summary>How many other people are connected.</summary>
    public int Peers { get; private set; }

    /// <summary>What this player is called, as it will appear to everybody else.</summary>
    public string Player { get; set; } = "";

    /// <summary>Raised whenever anything on screen would need to change.</summary>
    public event Action? Changed;

    public bool IsJoined => Status is PartyStatus.Joining or PartyStatus.Searching or PartyStatus.Open;

    /// <summary>Joins a table, leaving any current one first.</summary>
    public async Task JoinAsync(string code)
    {
        if (!TableCode.TryParse(code, out string parsed))
        {
            return;
        }

        await LeaveAsync();

        Code = parsed;
        Status = PartyStatus.Joining;
        Changed?.Invoke();

        try
        {
            module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            self ??= DotNetObjectReference.Create(this);

            await module.InvokeAsync<string>("join", parsed, self);
        }
        catch (Exception error) when (error is JSException or InvalidOperationException)
        {
            // A browser without WebRTC, a blocked relay, an extension that ate the module: all of
            // them arrive here, and all of them mean the same thing to a player.
            Status = PartyStatus.Failed;
            Changed?.Invoke();
        }
    }

    /// <summary>Leaves the table and forgets what was rolled at it.</summary>
    public async Task LeaveAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("leave");
            }
            catch (Exception error) when (error is JSException or JSDisconnectedException)
            {
                // Leaving a room that has already gone is not a failure worth reporting.
            }
        }

        Code = "";
        Peers = 0;
        Status = PartyStatus.Away;
        Log.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// Announces a roll: to the table, or only to this screen.
    /// </summary>
    /// <returns>The entry as it was added locally, so the roller sees their own dice either way.</returns>
    public async Task<RollMessage> RollAsync(
        RollOutcome outcome,
        RollReading? reading,
        bool secret,
        string preset = "")
    {
        RollMessage message = RollMessage.From(
            Guid.NewGuid().ToString("n"),
            Player,
            outcome,
            reading,
            secret,
            DateTimeOffset.UtcNow,
            preset);

        Log.Add(message, isMine: true);

        if (IsJoined && module is not null)
        {
            try
            {
                await module.InvokeAsync<bool>("send", secret ? message.Ghost().Write() : message.Write());
            }
            catch (Exception error) when (error is JSException or JSDisconnectedException)
            {
                // The roll is on this screen regardless. Losing it in transit is the transport's
                // business, and the status line is already saying how the transport is doing.
            }
        }

        Changed?.Invoke();
        return message;
    }

    /// <summary>Takes one roll from another player.</summary>
    [JSInvokable]
    public void ReceiveRoll(string json)
    {
        if (RollMessage.TryRead(json, out RollMessage message) && Log.Add(message))
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Takes a history from another player, merging it with what is already here.</summary>
    [JSInvokable]
    public void ReceiveHistory(string json)
    {
        if (Log.Merge(RollHistory.Read(json)) > 0)
        {
            Changed?.Invoke();
        }
    }

    [JSInvokable]
    public void ReceivePeers(int count)
    {
        if (Peers == count)
        {
            return;
        }

        Peers = Math.Max(count, 0);
        Changed?.Invoke();
    }

    [JSInvokable]
    public void ReceiveStatus(string status)
    {
        PartyStatus read = status switch
        {
            "joining" => PartyStatus.Joining,
            "searching" => PartyStatus.Searching,
            "open" => PartyStatus.Open,
            _ => PartyStatus.Failed
        };

        if (Status == read || Status is PartyStatus.Away)
        {
            return;
        }

        Status = read;
        Changed?.Invoke();
    }

    /// <summary>Answers a newcomer asking what they missed.</summary>
    /// <remarks>
    /// Called from JavaScript, which does not decide what goes in it. The log ghosts private rolls
    /// on the way out, so this is safe to answer without checking whose rolls are in it.
    /// </remarks>
    [JSInvokable]
    public string Replay() => RollHistory.Write(Log.Replay());

    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("leave");
                await module.DisposeAsync();
            }
            catch (Exception error) when (error is JSException or JSDisconnectedException)
            {
                // The page is going away, which is the only reason this is being called.
            }
        }

        module = null;
        self?.Dispose();
        self = null;
    }
}
