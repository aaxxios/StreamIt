using System.Buffers;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;

namespace StreamIt;

public sealed class StreamItConnectionContext(Guid clientId, ClientWebSocket socket, StreamItOptions options)
{
    public Guid ClientId => clientId;

    public readonly List<string> Groups = [];
    
    internal bool Aborted { get; set; }
    
    public Guid? UserId { get; set; } 

    private readonly SemaphoreSlim writeLock = new(1);

    private readonly SemaphoreSlim readLock = new(1);

    public async Task SendAsync(byte[] message)
    {
        await writeLock.WaitAsync();
        await socket.SendAsync(message, WebSocketMessageType.Binary, true, CancellationToken.None);
        writeLock.Release();
    }

    public async Task<T> ReceiveMessage<T>()
    {
        try
        {
            var buffer = ArrayPool<byte>.Shared.Rent(options.MaxMessageSize);
            using var cts = new CancellationTokenSource(options.ReadMessageTimeout);
            WebSocketReceiveResult reply;
            var offset = 0;
            var remaining = options.MaxMessageSize;
            await readLock.WaitAsync(cts.Token);
            do
            {
                reply = socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, remaining), cts.Token)
                    .GetAwaiter()
                    .GetResult();
                offset += reply.Count;
                remaining -= reply.Count;
            } while (!reply.EndOfMessage && offset ! > options.MaxMessageSize);

            if (offset == options.MaxMessageSize && !reply.EndOfMessage)
                throw new Exception("Message too large");
            return reply.MessageType == WebSocketMessageType.Close
                ? throw new Exception("Connection closed")
                : JsonSerializer.Deserialize<T>(buffer.AsSpan(0, offset))!;
        }
        finally
        {
            readLock.Release();
        }
    }
    
}