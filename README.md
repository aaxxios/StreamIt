# StreamIt

A lightweight .NET WebSocket library for building real-time 
streaming applications with ASP.NET Core.

## Quick Start

### 1. Create a Stream

Inherit from `StreamItStream` and override the lifecycle methods:

```csharp
using StreamIt;
using System.Text;

public class EchoStream : StreamItStream
{
    protected override async Task OnConnected(CancellationToken cancellationToken = default)
    {
        // Called when client connects
        await Context.SendAsync("Welcome!"u8.ToArray(), cancellationToken);
    }

    protected override async Task OnMessage(ArraySegment<byte> raw, CancellationToken cancellationToken = default)
    {
        // Echo back to sender
        await Context.SendAsync(raw, cancellationToken);
        
    }

    protected override Task OnDisconnected(CancellationToken cancellationToken = default)
    {
        // Called when client disconnects
        return Task.CompletedTask;
    }
}
```

### 2. Configure Your Application

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register StreamIt services
builder.Services.AddStreamIt(options =>
{
    options.MaxMessageSize = 4096;
    options.ReadMessageTimeout = TimeSpan.FromSeconds(30); // disconnect if message not received from client in 30 seconds
    options.EnableStatistics = true;  // enable statistics collection
});

var app = builder.Build();

// Enable WebSocket middleware
app.UseStreamIt();

// Map your stream to an endpoint, allowing clients to connect via websocket
app.MapStream<EchoStream>("/echo");

app.Run();
```

## Groups

Organize connections into groups for broadcast messaging:

```csharp
// define a message type for susbcriptions
public class SubscriptionMessage
{
    public List<string> Groups { get; set; }
}

/// example stream allowing clients to subscribe to groups
public class SubscriptionStream : StreamItStream
{
    protected override async Task OnConnected(CancellationToken cancellationToken = default)
    {
        // receive subscription message
        var subscription = await Context.ReceiveMessageAsync<SubscriptionMessage>(cancellationToken);
        
        // subscribe the contect to the groups specified in the message
        await Storage.AddToGroups(subscription.Groups, Context);
    }
}
```

Use a background service to stream updates to clients. All writes/reads are thread-safe.

```csharp
// inject StreamItStorage into your service, get access to groups/connections
public class NotificationService(StreamItStorage streamItStorage) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            // emulate getting the latest updates for notification
            var updates = new List<string>()
            {
                "update 1",
                "update 2", "update n"
            };
            //
            if (streamItStorage.Groups.Group("mygroup") is { } group)
            {
                // send the latest updates efficiently to all group subscribers
                await group.SendAllAsync(updates, cancellationToken: stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
```

## Error Handling

```csharp
// Handle connection errors with endpoint filter
app.MapStream<SubscriptionStream>("/subscribe")
   .AddEndpointFilter<ConnectionErrorFilter>();

public class ConnectionErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (WebSocketException ex)
        {
            // Log and handle WebSocket errors
            return null;
        }
        catch (ContextAbortedException)
        {
            // Connection was aborted
            return null;
        }
        catch (MessageTooLargeException ex)
        {
            // Message exceeded MaxMessageSize
            return null;
        }
    }
}
```