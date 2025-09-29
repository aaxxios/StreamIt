using System.Buffers;
using System.Net.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StreamIt;

public sealed class StreamItRequestHandler: IDisposable
{
    private readonly StreamItConnectionContext ConnectionContext;

    private readonly IOptions<StreamItOptions> options;
    private readonly IStreamItEventHandler eventHandler;
    private readonly ILogger<StreamItRequestHandler> logger;

    internal StreamItRequestHandler(StreamItConnectionContext context, IOptions<StreamItOptions> options,
        IStreamItEventHandler eventHandler, IServiceProvider serviceProvider)
    {
        ConnectionContext = context;
        this.options = options;
        this.eventHandler = eventHandler;
        logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<StreamItRequestHandler>();
    }

    internal async Task HandleConnection(CancellationToken cancellationToken = default)
    {
        await eventHandler.OnConnected(ConnectionContext).ConfigureAwait(false);
        if (ConnectionContext.Aborted) 
             return;
        logger.LogInformation("Finalising connection and keeping alive");
        ConnectionContext.FinalizeConnection();
        await KeepAlive(cancellationToken).ConfigureAwait(false);
    }

    internal async Task KeepAlive(CancellationToken cancellationToken)
    {
        await Task.Yield();
        while (!cancellationToken.IsCancellationRequested && !ConnectionContext.Aborted)
        {
            logger.LogInformation("checking connection: {C}", ConnectionContext.ClientId);
            var buffer = ArrayPool<byte>.Shared.Rent(options.Value.MaxMessageSize);
            try
            {
                using var recTokenSource = new CancellationTokenSource(options.Value.ReadMessageTimeout);
                var result = await ConnectionContext.ReceiveMessageWithResult(buffer, recTokenSource.Token)
                    .ConfigureAwait(false);
                logger.LogInformation("receiving message: {C}", result);
                if (result.Result.MessageType == WebSocketMessageType.Close || recTokenSource.IsCancellationRequested)
                {
                    logger.LogInformation("connection closed or timed out");
                    await eventHandler.OnDisconnected(ConnectionContext);
                    ConnectionContext.Abort();
                    break;
                }
                await eventHandler.OnMessage(ConnectionContext, buffer.AsSpan(0, result.Read));
                if (ConnectionContext.Aborted)
                {
                    logger.LogInformation("connection aborted");
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

    private bool disposed;
    public void Dispose()
    {
        Dispose(true);
    }

    public void Dispose(bool disposing)
    {
        if (disposed)
            return;
        if (disposing)
        {
            ConnectionContext.Dispose();
        }
        disposed = true;
    }
}