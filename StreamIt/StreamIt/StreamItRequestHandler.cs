using System.Buffers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable PossiblyMistakenUseOfCancellationToken

namespace StreamIt;

public abstract class StreamItRequestHandler
{
#pragma warning disable CS8618
    private IOptions<StreamItOptions> _options { get; set; }
    private ILogger<StreamItRequestHandler>? _logger { get; set; }
    private StreamItStorage _storage { get; set; }

    private StreamItConnectionContext _context { get; set; }

#pragma warning restore CS8618

    protected StreamItConnectionContext Context => _context;

    protected StreamItGroupList Groups => _storage.Groups;


    internal async Task HandleConnection(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var websocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        _context = new StreamItConnectionContext(Guid.NewGuid(), websocket, context.RequestServices);
        _storage = context.RequestServices.GetRequiredService<StreamItStorage>();
        _options = context.RequestServices.GetRequiredService<IOptions<StreamItOptions>>();
        _logger = context.RequestServices.GetService<ILogger<StreamItRequestHandler>>();

        await _storage.AddConnection(_context);
        await OnConnected(cancellationToken).ConfigureAwait(false);
        if (_context.Aborted)
        {
            await _context.CloseAsync(cancellationToken).ConfigureAwait(false);
            await _storage.RemoveConnection(_context);
            _logger?.LogDebug("connection aborted: {C}", _context.ClientId);
        }

        _logger?.LogDebug("finalising connection {C} and keeping alive", _context.ClientId);
        _context.FinalizeConnection();
        try
        {
            await KeepAlive(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await _storage.RemoveConnection(_context);
                await OnDisconnected(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task KeepAlive(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(_options.Value.MaxMessageSize);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.Value.ReadMessageTimeout);
            try
            {
                var read = await _context.ReceiveMessageAsync(buffer, cts.Token).ConfigureAwait(false);
                await OnMessage(buffer.AsSpan(0, read), cancellationToken)
                    .ConfigureAwait(false);
                if (_context.Aborted)
                {
                    break;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            await Task.Delay(_options.Value.KeepAliveInterval, cancellationToken).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// called when a client connects. 
    /// </summary>
    /// <returns></returns>
    protected virtual Task OnConnected(CancellationToken _ = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// called when a client disconnects
    /// </summary>
    /// <returns></returns>
    protected virtual Task OnDisconnected(CancellationToken _ = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// called when a message is received from a client
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual Task OnMessage(ReadOnlySpan<byte> message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}