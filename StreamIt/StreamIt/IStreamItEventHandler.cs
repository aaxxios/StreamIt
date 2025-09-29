namespace StreamIt;

public interface IStreamItEventHandler
{
    Task OnConnected(StreamItConnectionContext context, CancellationToken cancellationToken = default);
    Task OnDisconnected(StreamItConnectionContext context, CancellationToken cancellationToken = default);
    Task OnMessage(StreamItConnectionContext context, ReadOnlySpan<byte> message, CancellationToken cancellationToken = default);
}


public class OnConnectedResponse
{
    public TimeSpan? CancelAfter { get; set; }
}