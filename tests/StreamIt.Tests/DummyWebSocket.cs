using System.Net.WebSockets;

namespace StreamIt.Tests;

public class DummyWebSocket: WebSocket
{
    public override void Abort()
    {
        throw new NotImplementedException();
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override WebSocketCloseStatus? CloseStatus { get; }
    public override string? CloseStatusDescription { get; }
    public override WebSocketState State { get; }
    public override string? SubProtocol { get; }
}