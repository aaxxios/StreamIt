using System.Net.WebSockets;
using System.Text;

namespace Sample;

public class BackgroundClient : BackgroundService
{
    private readonly ILogger<BackgroundClient> _logger;

    public BackgroundClient(ILogger<BackgroundClient> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        _logger.LogInformation("{S} is running...", nameof(BackgroundClient));
        using var client = new ClientWebSocket();
        var connected = false;
        await Task.Delay(1000, stoppingToken);
        do
        {
            try
            {
                await client.ConnectAsync(new Uri("ws://localhost:5000/streamit"), stoppingToken);
                connected = true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "error connecting client. retrying.. ensure app is running on localhost:5000");
                await Task.Delay(1000, stoppingToken);
            }
        } while (!connected && !stoppingToken.IsCancellationRequested);

        while (!stoppingToken.IsCancellationRequested)
        {
            await client.SendAsync("hello world"u8.ToArray(), WebSocketMessageType.Text, true, stoppingToken)
                .ConfigureAwait(false);
            var buffer = new byte[1024];
            var response = await client.ReceiveAsync(buffer, stoppingToken).ConfigureAwait(false);
            _logger.LogInformation("message from server: {M}", Encoding.UTF8.GetString(buffer, 0, response.Count));
            await Task.Delay(1000, stoppingToken);
        }
    }
}