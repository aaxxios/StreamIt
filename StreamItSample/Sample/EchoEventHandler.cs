using System.Text;
using StreamIt;

namespace Sample;

public class EchoEventHandler : StreamItRequestHandler
{
    protected override Task OnConnected(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"client connected: {Context.ClientId}");
        return Task.CompletedTask;
    }

    protected override Task OnDisconnected(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"client disconnected: {Context.ClientId}");
        return Task.CompletedTask;
    }

    protected override Task OnMessage(ReadOnlySpan<byte> message, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("message received: {0}", Encoding.UTF8.GetString(message));
        return Context.SendAsync(message.ToArray(), cancellationToken: cancellationToken);
    }
}