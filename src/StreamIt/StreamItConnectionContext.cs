using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace StreamIt;

public sealed class StreamItConnectionContext : IDisposable
{
    private Guid _clientId { get; set; }
    private readonly WebSocket connection;
    private readonly IOptions<StreamItOptions> _options;

    /// <summary>
    /// groups which this connection belongs to
    /// </summary>
    public readonly ContextGroup<string> Groups;

    public bool Aborted { get; private set; }

    private readonly SemaphoreSlim writeLock;

    private readonly SemaphoreSlim readLock;

    internal readonly SemaphoreSlim GroupLock;

    private readonly bool collectStats;

    /// <summary>
    /// get the number of bytes read from this context
    /// </summary>
    public long BytesRead { get; private set; }

    /// <summary>
    /// get the number of bytes written to this context
    /// </summary>
    public long BytesWritten { get; private set; }

    public StreamItConnectionContext(Guid clientId, WebSocket webSocket, IOptions<StreamItOptions> options)
    {
        _clientId = clientId;
        connection = webSocket;
        _options = options;
        writeLock = new SemaphoreSlim(1, maxCount: 1);
        readLock = new SemaphoreSlim(1, 1);
        GroupLock = new SemaphoreSlim(1, 1);
        Groups = new ContextGroup<string>();
        collectStats = _options.Value.EnableStatistics;
    }

    public Guid ClientId => _clientId;

    /// <summary>
    /// call once to reset the client id before the connection is finalized
    /// </summary>
    /// <param name="guid"></param>
    /// <exception cref="InvalidOperationException"></exception>
    internal void SetClient(Guid guid)
    {
        _clientId = guid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateBytesRead(int read)
    {
        if (collectStats)
        {
            BytesRead += read;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateBytesWritten(int written)
    {
        if (collectStats)
        {
            BytesWritten += written;
        }
    }

    /// <summary>
    /// send a message to this connection
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    public async Task SendAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        ThrowIfAborted();
        await writeLock.WaitAsync(CancellationToken.None);
        await connection.SendAsync(message, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
        UpdateBytesWritten(message.Length);
        writeLock.Release();
    }

    /// <summary>
    /// send a message to this connection
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    public async Task SendAsync(ArraySegment<byte> message, CancellationToken cancellationToken = default)
    {
        ThrowIfAborted();
        await writeLock.WaitAsync(CancellationToken.None);
        await connection.SendAsync(message, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
        UpdateBytesWritten(message.Count);
        writeLock.Release();
    }

    public Task SendAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        ThrowIfAborted();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, options: _options.Value.SerializerOptions);
        return SendAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// abort the context 
    /// </summary>
    public void Abort()
    {
        ThrowIfAborted();
        Aborted = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfAborted()
    {
        if (Aborted)
            throw new ContextAbortedException();
    }


    /// <summary>
    /// reads raw bytes from connection
    /// </summary>
    /// <param name="buffer">buffer to read into</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ContextAbortedException"></exception>
    /// <exception cref="MessageTooLargeException"></exception>
    private async Task<ReceivedMessageInfo> ReadRawBytesAsync(byte[] buffer,
        CancellationToken cancellationToken = default)
    {
        await readLock.WaitAsync(CancellationToken.None);
        WebSocketReceiveResult reply;
        var read = 0;
        var remaining = _options.Value.MaxMessageSize;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.Value.ReadMessageTimeout);
        do
        {
            ThrowIfAborted();
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

        UpdateBytesRead(read);
        readLock.Release();
        if (read == _options.Value.MaxMessageSize && !reply.EndOfMessage)
            throw new MessageTooLargeException(_options.Value.MaxMessageSize, read);
        return new ReceivedMessageInfo()
        {
            Length = read, MessageType = reply.MessageType
        };
    }

    /// <summary>
    /// reads message from connection
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task<T?> ReceiveMessageAsync<T>(CancellationToken cancellationToken = default)
    {
        ThrowIfAborted();
        var buffer = ArrayPool<byte>.Shared.Rent(_options.Value.MaxMessageSize);
        try
        {
            var receivedMessageInfo = await ReadRawBytesAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (receivedMessageInfo.MessageType is WebSocketMessageType.Close)
                return default;
            return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, receivedMessageInfo.Length),
                options: _options.Value.SerializerOptions)!;
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
    public Task<ReceivedMessageInfo> ReceiveMessageAsync(byte[] buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfAborted();
        return ReadRawBytesAsync(buffer, cancellationToken);
    }

    private bool disposed;

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
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