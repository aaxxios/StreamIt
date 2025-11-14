using System.Text.Json;
using StreamIt;

namespace Sample;

public class SampleStreamItStream : StreamItStream
{
    private readonly ILogger<SampleStreamItStream> _logger;

    private HashSet<string> ValidParams = ["foo", "bar", "baz"];

    public SampleStreamItStream(ILogger<SampleStreamItStream> logger)
    {
        _logger = logger;
    }

    private async Task HandleMessage(StreamMessage message, CancellationToken cancellationToken = default)
    {
        switch (message.Type)
        {
            case StreamMessageType.Ping:
                await Context.SendAsync("pong"u8.ToArray(), cancellationToken).ConfigureAwait(false);
                break;
            case StreamMessageType.Subscription when message.Params.Count == 0:
                await Context.SendAsync("must specify a channel to subscribe to"u8.ToArray(), cancellationToken)
                    .ConfigureAwait(false);
                return;
            case StreamMessageType.Subscription:
                if (message.Params.Any(p => !ValidParams.Contains(p)))
                {
                    await Context.SendAsync("invalid channel specified"u8.ToArray(), cancellationToken);
                    return;
                }

                await Storage.AddToGroups(message.Params, Context).ConfigureAwait(false);
                break;
            case StreamMessageType.UnSubscription when message.Params.Count == 0:
                Context.Abort(); // signal disconnect
                return;
            case StreamMessageType.UnSubscription:
                await Storage.RemoveFromGroups(message.Params, Context).ConfigureAwait(false);
                break;
            default:
                await Context.SendAsync("invalid message type"u8.ToArray(), cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    protected override async Task OnConnected(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("client connected: {Id}", Context.ClientId);
        var message = await Context.ReceiveMessageAsync<StreamMessage>(cancellationToken).ConfigureAwait(false);
        if (message.Type is not StreamMessageType.Subscription)
        {
            await Context.SendAsync("invalid initialization message"u8.ToArray(), cancellationToken)
                .ConfigureAwait(false);
            Context.Abort();
        }

        await HandleMessage(message, cancellationToken).ConfigureAwait(false);
    }

    protected override Task OnDisconnected(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("client disconnected: {Id}", Context.ClientId);
        return Task.CompletedTask;
    }

    protected override Task OnMessage(ReadOnlySpan<byte> raw, CancellationToken cancellationToken = default)
    {
        var message = JsonSerializer.Deserialize<StreamMessage>(raw);
        return HandleMessage(message!, cancellationToken);
    }
}