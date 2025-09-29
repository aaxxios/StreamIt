using System.Buffers;
using System.Net.WebSockets;
using Microsoft.Extensions.Options;

namespace StreamIt;

public sealed class StreamItRequestHandler
{
    private readonly StreamItConnectionContext ConnectionContext;

    private readonly IOptions<StreamItOptions> options;
    private readonly IStreamItEventHandler eventHandler;

    internal StreamItRequestHandler(StreamItConnectionContext context, IOptions<StreamItOptions> options,
        IStreamItEventHandler eventHandler)
    {
        ConnectionContext = context;
        this.options = options;
        this.eventHandler = eventHandler;
    }

    internal async Task HandleConnection(CancellationToken cancellationToken)
    {
        await eventHandler.OnConnected(ConnectionContext).ConfigureAwait(false);
        if (ConnectionContext.Aborted) return;
        ConnectionContext.FinalizeConnection();
        await KeepAlive(cancellationToken).ConfigureAwait(false);
    }

    internal async Task KeepAlive(CancellationToken cancellationToken)
    {
        await Task.Yield();
        while (!cancellationToken.IsCancellationRequested && !ConnectionContext.Aborted)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(options.Value.MaxMessageSize);
            try
            {
                var result = await ConnectionContext.ReceiveMessageWithResult(buffer).ConfigureAwait(false);
                if (result.Result.MessageType == WebSocketMessageType.Close)
                {
                    await eventHandler.OnDisconnected(ConnectionContext);
                    ConnectionContext.Abort();
                    break;
                }

                await eventHandler.OnMessage(ConnectionContext, buffer.AsSpan(0, result.Read));
                if (ConnectionContext.Aborted)
                {
                    break;
                }
            }
            catch (WebSocketException)
            {
                await eventHandler.OnDisconnected(ConnectionContext);
                throw;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }

            await Task.Yield();
            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        }
    }
}