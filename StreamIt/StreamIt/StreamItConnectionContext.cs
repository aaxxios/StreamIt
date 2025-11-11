using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace StreamIt;

public sealed class StreamItConnectionContext : IDisposable
{
    private Guid _clientId { get; set; }
    private readonly WebSocket socket;
    private readonly IOptions<StreamItOptions> options;

    public StreamItConnectionContext(Guid clientId, WebSocket socket, IOptions<StreamItOptions> options)
    {
        _clientId = clientId;
        this.socket = socket;
        this.options = options;
    }

    public Guid ClientId => _clientId;

    public WebSocketState State => socket.State;

    public WebSocketCloseStatus? CloseStatus => socket.CloseStatus;
    private bool Finalized { get; set; }

    /// <summary>
    /// where data can be stored on the context
    /// </summary>
    public Dictionary<string, object> Properties { get; } = new();


    /// <summary>
    /// call once to reset the client id before the connection is finalized
    /// </summary>
    /// <param name="guid"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SetClient(Guid guid)
    {
        if (Finalized)
            throw new InvalidOperationException("Connection is finalized");
        _clientId = guid;
    }

    public readonly HashSet<string> Groups = new();
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

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        return socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", cancellationToken);
    }

    /// <summary>
    /// abort the context
    /// </summary>
    public void Abort()
    {
        Aborted = true;
    }

    public void FinalizeConnection()
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
        var remaining = options.Value.MaxMessageSize;
        do
        {
            if (Aborted)
                throw new SocketCloseException();
            reply = socket.ReceiveAsync(new ArraySegment<byte>(buffer, read, remaining), cancellationToken)
                .GetAwaiter()
                .GetResult();
            read += reply.Count;
            remaining -= reply.Count;
        } while (!reply.EndOfMessage && read ! > options.Value.MaxMessageSize);

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
            var read = await ReadRawBytesAsync(buffer, cancellationToken);
            return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, read), options: options.Value.SerializerOptions)!;
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
        var remaining = options.Value.MaxMessageSize;
        do
        {
            if (Aborted)
                throw new SocketCloseException();
            reply = socket.ReceiveAsync(new ArraySegment<byte>(buffer, read, remaining), cancellationToken)
                .GetAwaiter()
                .GetResult();
            read += reply.Count;
            remaining -= reply.Count;
        } while (!reply.EndOfMessage && read ! > options.Value.MaxMessageSize && !Aborted);

        readLock.Release();
        if (read == options.Value.MaxMessageSize && !reply.EndOfMessage)
            throw new MessageTooLargeException(options.Value.MaxMessageSize, read);
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

public class StreamItReceivedMessage
{
    public StreamItReceivedMessage(WebSocketReceiveResult result, int read)
    {
        Result = result;
        Read = read;
    }
    internal readonly WebSocketReceiveResult Result;
    internal readonly int Read;
}