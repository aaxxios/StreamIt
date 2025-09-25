using Microsoft.Extensions.DependencyInjection;

namespace StreamIt;

public class StreamItRequestHandler(IServiceProvider serviceProvider) : StreamItMarker
{
    private StreamItConnectionContext? _connectionContext;
    private StreamItStorage? _streamItStorage;
    internal async Task HandleConnection(StreamItConnectionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _connectionContext = context;
        _streamItStorage = serviceProvider.GetRequiredService<StreamItStorage>();
        await _streamItStorage.AddConnection(context);
        if (!await OnConnected())
        {
            return;
        }
        await KeepAlive(cancellationToken).ConfigureAwait(false);
    }

    internal async Task KeepAlive(CancellationToken cancellationToken)
    {
        await Task.Yield();
        while (!cancellationToken.IsCancellationRequested)
        {
        }
    }
    
    protected Task AddToGroup(string groupName)
    {
        ArgumentNullException.ThrowIfNull(_connectionContext);
        ArgumentNullException.ThrowIfNull(_streamItStorage);
        _streamItStorage.AddToGroup(groupName, _connectionContext);
        return Task.CompletedTask;
    }

    protected Task RemoveFromGroup(string groupName)
    {
        ArgumentNullException.ThrowIfNull(_connectionContext);
        ArgumentNullException.ThrowIfNull(_streamItStorage);
        _streamItStorage.RemoveFromGroup(groupName, _connectionContext);
        return Task.CompletedTask;
    }

    protected virtual Task<bool> OnConnected()
    {
        return Task.FromResult(true);
    }

    protected virtual Task OnDisconnected()
    {
        ArgumentNullException.ThrowIfNull(_connectionContext);
        ArgumentNullException.ThrowIfNull(_streamItStorage);
        _streamItStorage.RemoveConnection(_connectionContext);
        lock (_connectionContext.Groups)
        {
            foreach (var group in _connectionContext.Groups)
            { 
                _streamItStorage.RemoveFromGroup(group, _connectionContext);
            }
        }

        return Task.CompletedTask;
    }
    
    protected virtual Task OnMessage(byte[] message)
    {
        return Task.CompletedTask;
    }
}
