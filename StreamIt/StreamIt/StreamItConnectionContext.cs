using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;

namespace StreamIt;

public sealed class StreamItConnectionContext(Guid clientId, ClientWebSocket socket, StreamItOptions options)
{
    private Guid _clientId { get; set; } = clientId;
    public Guid ClientId => _clientId;

    private bool Finalized { get; set; }


    /// <summary>
    /// gives client opportunity to set client id before the connection is finalized
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
        await writeLock.WaitAsync(cancellationToken);
        await socket.SendAsync(message, WebSocketMessageType.Binary, true, CancellationToken.None);
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
    /// <returns></returns>
    /// <exception cref="SocketCloseException"></exception>
    /// <exception cref="MessageTooLargeException"></exception>
    public async Task<int> ReadRawBytes(byte[] buffer)
    {
        if (Aborted)
            throw new SocketCloseException();
        using var cts = new CancellationTokenSource(options.ReadMessageTimeout);
        await readLock.WaitAsync(cts.Token);
        WebSocketReceiveResult reply;
        var read = 0;
        var remaining = options.MaxMessageSize;
        do
        {
            reply = socket.ReceiveAsync(new ArraySegment<byte>(buffer, read, remaining), cts.Token)
                .GetAwaiter()
                .GetResult();
            read += reply.Count;
            remaining -= reply.Count;
        } while (!reply.EndOfMessage && read ! > options.MaxMessageSize);

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
    public async Task<T> ReceiveMessage<T>()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(options.MaxMessageSize);
        try
        {
            var read = await ReadRawBytes(buffer);
            return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, read))!;
        }
        finally
        {
            readLock.Release();
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    public Task<int> ReceiveMessage(byte[] buffer)
    {
        return ReadRawBytes(buffer);
    }

    internal async Task<StreamItReceivedMessage> ReceiveMessageWithResult(byte[] buffer)
    {
        if (Aborted)
            throw new SocketCloseException();
        using var cts = new CancellationTokenSource(options.ReadMessageTimeout);
        await readLock.WaitAsync(cts.Token);
        WebSocketReceiveResult reply;
        var read = 0;
        var remaining = options.MaxMessageSize;
        do
        {
            reply = socket.ReceiveAsync(new ArraySegment<byte>(buffer, read, remaining), cts.Token)
                .GetAwaiter()
                .GetResult();
            read += reply.Count;
            remaining -= reply.Count;
        } while (!reply.EndOfMessage && read ! > options.MaxMessageSize);

        if (read == options.MaxMessageSize && !reply.EndOfMessage)
            throw new MessageTooLargeException(options.MaxMessageSize, read);
        if (reply.MessageType == WebSocketMessageType.Close)
            Aborted = true;
        return new StreamItReceivedMessage(reply, read);
    }
}

public class StreamItReceivedMessage(WebSocketReceiveResult result, int read)
{
    internal readonly WebSocketReceiveResult Result = result;
    internal readonly int Read = read;
}