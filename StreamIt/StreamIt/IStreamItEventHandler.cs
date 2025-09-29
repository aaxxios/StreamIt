namespace StreamIt;

public interface IStreamItEventHandler
{
    Task OnConnected(StreamItConnectionContext context);
    Task OnDisconnected(StreamItConnectionContext context);
    Task OnMessage(StreamItConnectionContext context, ReadOnlySpan<byte> message);
}
