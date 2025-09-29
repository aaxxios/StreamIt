using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;

namespace StreamIt;

public sealed class StreamItConnectionContext(Guid clientId, WebSocket socket, StreamItOptions options) : IDisposable
{
    private Guid _clientId { get; set; } = clientId;
    public Guid ClientId => _clientId;

    public WebSocketState State => socket.State;
    private bool Finalized { get; set; }

    public Dictionary<string, object> Properties { get; } = [];


    /// <summary>
    /// gives client the opportunity to set client id before the connection is finalized
    /// </summary>
    /// <param name="guid"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SetClient(Guid guid)
    {
        if (Finalized)
            throw new InvalidOperationException("Connection is finalized");
        _clientId = guid;
    }

    public readonly HashSet<string> Groups = [];
    public bool Aborted { get; private set; }

    private readonly SemaphoreSlim writeLock = new(1);

    private readonly SemaphoreSlim readLock = new(1);

    internal readonly SemaphoreSlim GroupLock = new(1);


    public async Task SendAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        if (Aborted)
            return;
        await writeLock.WaitAsync(CancellationToken.None);
        await socket.SendAsync(message, WebSocketMessageType.Binary, true, cancellationToken);
        writeLock.Release();
    }

    /// <summary>
    /// abort the context
    /// </summary>
    public void Abort()
    {
        Aborted = true;
    }

    internal void FinalizeConnection()
    {
        Finalized = true;
    }

    /// <summary>
    /// reads raw bytes from connection
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="SocketCloseException"></exception>
    /// <exception cref="MessageTooLargeException"></exception>
    public async Task<int> ReadRawBytesAsync(byte[] buffer, CancellationToken cancellationToken = default)
    {
        if (Aborted)
            throw new SocketCloseException();
        await readLock.WaitAsync(CancellationToken.None);
        WebSocketReceiveResult reply;
        var read = 0;
        var remaining = options.MaxMessageSize;
        do
        {
            if (Aborted)
                throw new SocketCloseException();
            reply = socket.ReceiveAsync(new ArraySegment<byte>(buffer, read, remaining), cancellationToken)
                .GetAwaiter()
                .GetResult();
            read += reply.Count;
            remaining -= reply.Count;
        } while (!reply.EndOfMessage && read ! > options.MaxMessageSize);

        readLock.Release();
        if (read == options.MaxMessageSize && !reply.EndOfMessage)
            throw new MessageTooLargeException(options.MaxMessageSize, read);
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
    public async Task<T> ReceiveMessageAsync<T>(JsonSerializerOptions? serializerOptions,
        CancellationToken cancellationToken = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(options.MaxMessageSize);
        try
        {
            var read = await ReadRawBytesAsync(buffer, cancellationToken);
            return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, read), options: serializerOptions)!;
        }
        finally
        {
            readLock.Release();
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    public Task<int> ReceiveMessageAsync(byte[] buffer, CancellationToken cancellationToken = default)
    {
        return ReadRawBytesAsync(buffer, cancellationToken);
    }

    internal async Task<StreamItReceivedMessage> ReceiveMessageWithResult(byte[] buffer,
        CancellationToken cancellationToken = default)
    {
        if (Aborted)
            throw new SocketCloseException();
        await readLock.WaitAsync(CancellationToken.None);
        WebSocketReceiveResult reply;
        var read = 0;
        var remaining = options.MaxMessageSize;
        do
        {
            if (Aborted)
                throw new SocketCloseException();
            reply = socket.ReceiveAsync(new ArraySegment<byte>(buffer, read, remaining), cancellationToken)
                .GetAwaiter()
                .GetResult();
            read += reply.Count;
            remaining -= reply.Count;
        } while (!reply.EndOfMessage && read ! > options.MaxMessageSize && !Aborted);

        readLock.Release();
        if (read == options.MaxMessageSize && !reply.EndOfMessage)
            throw new MessageTooLargeException(options.MaxMessageSize, read);
        if (reply.MessageType == WebSocketMessageType.Close)
            Aborted = true;

        return new StreamItReceivedMessage(reply, read);
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
            socket.Dispose();
            readLock.Dispose();
            writeLock.Dispose();
        }

        disposed = true;
    }
}

public class StreamItReceivedMessage(WebSocketReceiveResult result, int read)
{
    internal readonly WebSocketReceiveResult Result = result;
    internal readonly int Read = read;
}