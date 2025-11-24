using System.Buffers;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StreamIt;

public abstract class StreamItStream : IDisposable
{
    private IOptions<StreamItOptions>? _options { get; set; }
    private ILogger<StreamItStream>? _logger { get; set; }

    private StreamItStorage? _storage { get; set; }


    private StreamItConnectionContext? _context { get; set; }

    protected StreamItConnectionContext Context =>
        _context ?? throw new InvalidOperationException("invalid stream state");

    protected StreamItGroupList Groups =>
        _storage == null ? throw new InvalidOperationException("invalid stream state") : _storage.Groups;

    protected StreamItStorage Storage => _storage ?? throw new InvalidOperationException("invalid stream state");

    internal Task HandleConnection(HttpContext context, CancellationToken cancellationToken = default)
    {
        if (context.WebSockets.IsWebSocketRequest) return HandleConnectionInner(context, cancellationToken);
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return Task.CompletedTask;
    }

    private async Task HandleConnectionInner(HttpContext context,
        CancellationToken cancellationToken = default)
    {
        using var websocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        _options = context.RequestServices.GetRequiredService<IOptions<StreamItOptions>>();
        _context = new StreamItConnectionContext(Guid.NewGuid(), websocket, _options);
        _storage = context.RequestServices.GetRequiredService<StreamItStorage>();
        _logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger<StreamItStream>();
        await _storage.AddConnection(_context);
        await OnConnected(cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested) return;
        if (_context.Aborted)
        {
            await _storage.RemoveConnection(_context);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("connection aborted: {C}", _context.ClientId);
            }

            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("finalising connection {C} and keeping alive", _context.ClientId);
        }

        try
        {
            await KeepAlive(context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await _storage.RemoveConnection(_context);
            }

            if (!context.RequestAborted.IsCancellationRequested)
            {
                await Task.WhenAll(CloseConnection(websocket, context.RequestAborted),
                    OnDisconnected(context.RequestAborted));
            }
        }
    }

    private static async Task CloseConnection(WebSocket webSocket, CancellationToken cancellationToken)
    {
        try
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken);
        }
        catch (WebSocketException e) when (e.WebSocketErrorCode is WebSocketError.InvalidState
                                               or WebSocketError.ConnectionClosedPrematurely)
        {
            // ignore if connection is in invalid state
        }
    }

    private async Task KeepAlive(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(_options!.Value.MaxMessageSize);
            try
            {
                var read = await _context!.ReceiveMessageAsync(buffer, cancellationToken).ConfigureAwait(false);
                await OnMessage(new ArraySegment<byte>(buffer, 0, read), cancellationToken).ConfigureAwait(false);
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
    /// <param name="raw"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual Task OnMessage(ArraySegment<byte> raw, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    protected bool disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            _context?.Dispose();
        }

        disposed = true;
    }
}