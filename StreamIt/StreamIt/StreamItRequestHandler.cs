using System.Buffers;
using System.Net.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable PossiblyMistakenUseOfCancellationToken

namespace StreamIt;

public sealed class StreamItRequestHandler : IDisposable
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
        await eventHandler.OnConnected(ConnectionContext, cancellationToken).ConfigureAwait(false);
        if (ConnectionContext.Aborted)
        {
            await ConnectionContext.CloseAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning("connection aborted: {C}", ConnectionContext.ClientId);
        }
       
        logger.LogInformation("Finalising connection {C} and keeping alive", ConnectionContext.ClientId);
        ConnectionContext.FinalizeConnection();
        await KeepAlive(cancellationToken).ConfigureAwait(false);
    }

    private async Task KeepAlive(CancellationToken cancellationToken)
    {
        await Task.Yield();
        while (ConnectionContext.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested &&
               !ConnectionContext.Aborted)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(options.Value.MaxMessageSize);
            try
            {
                using var recTokenSource = new CancellationTokenSource(options.Value.ReadMessageTimeout);
                var result = await ConnectionContext.ReceiveMessageWithResult(buffer, recTokenSource.Token)
                    .ConfigureAwait(false);
                logger.LogInformation("receive message from client: {C}", result);
                if (result.Result.MessageType == WebSocketMessageType.Close)
                {
                    logger.LogInformation("connection closed or timed out: {C}", ConnectionContext.ClientId);
                    await eventHandler.OnDisconnected(ConnectionContext, cancellationToken).ConfigureAwait(false);
                    await ConnectionContext.CloseAsync(cancellationToken).ConfigureAwait(false);
                    ConnectionContext.Abort();
                    break;
                }

                try
                {
                    await eventHandler.OnMessage(ConnectionContext, buffer.AsSpan(0, result.Read), cancellationToken);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "event handler error handling message from client: {C}",
                        ConnectionContext.ClientId);
                    await ConnectionContext.CloseAsync(cancellationToken).ConfigureAwait(false);
                    ConnectionContext.Abort();
                    break;
                }

                if (ConnectionContext.Aborted)
                {
                    logger.LogInformation("connection aborted: {C}", ConnectionContext.ClientId);
                    break;
                }
            }
            catch (WebSocketException)
            {
                await eventHandler.OnDisconnected(ConnectionContext, cancellationToken);
                await ConnectionContext.CloseAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
            catch (SocketCloseException)
            {
                await eventHandler.OnDisconnected(ConnectionContext, cancellationToken);
                await ConnectionContext.CloseAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
            catch (TaskCanceledException)
            {
                await eventHandler.OnDisconnected(ConnectionContext, cancellationToken);
                await ConnectionContext.CloseAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
            catch (Exception)
            {
                await eventHandler.OnDisconnected(ConnectionContext, cancellationToken);
                await ConnectionContext.CloseAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }

            await Task.Yield();
            await Task.Delay(options.Value.KeepAliveInterval, cancellationToken).ConfigureAwait(false);
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