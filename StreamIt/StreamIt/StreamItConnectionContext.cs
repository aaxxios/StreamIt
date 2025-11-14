using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace StreamIt;

public sealed class StreamItConnectionContext : IDisposable
{
    private Guid _clientId { get; set; }
    private readonly WebSocket connection;
    private readonly IOptions<StreamItOptions> options;

    /// <summary>
    /// groups which this connection belongs to
    /// </summary>
    public readonly ContextGroup<string> Groups;

    public bool Aborted { get; private set; }

    private readonly SemaphoreSlim writeLock;

    private readonly SemaphoreSlim readLock;

    internal readonly SemaphoreSlim GroupLock;

    public StreamItConnectionContext(Guid clientId, WebSocket webSocket, IServiceProvider serviceProvider)
    {
        _clientId = clientId;
        connection = webSocket;
        options = serviceProvider.GetRequiredService<IOptions<StreamItOptions>>();
        writeLock = new SemaphoreSlim(1, maxCount: 1);
        readLock = new SemaphoreSlim(1, 1);
        GroupLock = new SemaphoreSlim(1, 1);
        Groups = new ContextGroup<string>();
    }

    public Guid ClientId => _clientId;

    private bool Finalized { get; set; }

    /// <summary>
    /// stores context aware data
    /// </summary>
    public Dictionary<string, object> Data { get; } = new();


    /// <summary>
    /// call once to reset the client id before the connection is finalized
    /// </summary>
    /// <param name="guid"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SetClient(Guid guid)
    {
        if (Finalized)
            throw new InvalidOperationException("connection is finalized");
        _clientId = guid;
    }


    /// <summary>
    /// send a message to this connection
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    public async Task SendAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        if (Aborted)
            return;
        await writeLock.WaitAsync(CancellationToken.None);
        await connection.SendAsync(message, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
        writeLock.Release();
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Aborted)
            throw new InvalidOperationException("connection is already aborted");
        Aborted = true;
        return connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", cancellationToken);
    }

    /// <summary>
    /// abort the context 
    /// </summary>
    public void Abort()
    {
        if (Aborted)
            throw new InvalidOperationException("connection is already aborted");
        Aborted = true;
    }

    public void FinalizeConnection()
    {
        if (Finalized)
            throw new InvalidOperationException("connection is already finalized");
        Finalized = true;
    }

    /// <summary>
    /// reads raw bytes from connection
    /// </summary>
    /// <param name="buffer">buffer to read into</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="SocketCloseException"></exception>
    /// <exception cref="MessageTooLargeException"></exception>
    private async Task<int> ReadRawBytesAsync(byte[] buffer, CancellationToken cancellationToken = default)
    {
        await readLock.WaitAsync(CancellationToken.None);
        WebSocketReceiveResult reply;
        var read = 0;
        var remaining = options.Value.MaxMessageSize;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(options.Value.ReadMessageTimeout);
        do
        {
            if (Aborted)
                throw new SocketCloseException();
            reply = await connection.ReceiveAsync(new ArraySegment<byte>(buffer, read, remaining), cts.Token)
                .ConfigureAwait(false);
            if (cts.IsCancellationRequested)
            {
                readLock.Release();
                throw new OperationCanceledException();
            }
            read += reply.Count;
            remaining -= reply.Count;
        } while (!reply.EndOfMessage && remaining > 0);

        readLock.Release();
        if (read == options.Value.MaxMessageSize && !reply.EndOfMessage)
            throw new MessageTooLargeException(options.Value.MaxMessageSize, read);
        if (reply.MessageType != WebSocketMessageType.Close)
            return read;
        Aborted = true;
        throw new SocketCloseException();
    }

    /// <summary>
    /// reads message from connection
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task<T> ReceiveMessageAsync<T>(CancellationToken cancellationToken = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(options.Value.MaxMessageSize);
        try
        {
            var read = await ReadRawBytesAsync(buffer, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, read), options: options.Value.SerializerOptions)!;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>
    /// reads message from connection 
    /// </summary>
    /// <param name="buffer">destination to read message into</param>
    /// <param name="cancellationToken"></param>
    /// <returns>number of bytes read</returns>
    public Task<int> ReceiveMessageAsync(byte[] buffer, CancellationToken cancellationToken = default)
    {
        return ReadRawBytesAsync(buffer, cancellationToken);
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
            readLock.Dispose();
            writeLock.Dispose();
            GroupLock.Dispose();
        }
        
        disposed = true;
    }
}