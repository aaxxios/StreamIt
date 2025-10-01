using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace StreamIt;

public sealed class StreamItConnectionList
{
    private readonly ConcurrentDictionary<Guid, StreamItConnectionContext> _itConnectionContexts = new();

    public IEnumerable<StreamItConnectionContext> Connections => _itConnectionContexts.Values;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(StreamItConnectionContext context, out StreamItConnectionContext? value)
    {
        return _itConnectionContexts.TryGetValue(context.ClientId, out value);
    }
    
    
    public int Count => _itConnectionContexts.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(Guid id, out StreamItConnectionContext? value)
    {
        return _itConnectionContexts.TryGetValue(id, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemove(StreamItConnectionContext context, out StreamItConnectionContext? removed)
    {
        return _itConnectionContexts.TryRemove(context.ClientId, out removed);
    }

    public void Add(StreamItConnectionContext connection)
    {
        _itConnectionContexts.AddOrUpdate(connection.ClientId, connection, (_, _) => connection);
    }

    public Task SendMessage(byte[] message, CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            _itConnectionContexts.Values.Select(context => context.SendAsync(message, cancellationToken)));
    }

    public Task SendUserAsync(Guid clientId, byte[] message, CancellationToken cancellationToken = default)
    {
        return !_itConnectionContexts.TryGetValue(clientId, out var connection)
            ? Task.CompletedTask
            : connection.SendAsync(message, cancellationToken);
    }

    public Task SendUsersAsync(Guid clientId1, Guid clientId2, byte[] message,
        CancellationToken cancellationToken = default)
    {
        List<Task>? tasks = null;
        if (_itConnectionContexts.TryGetValue(clientId1, out var connection1))
            (tasks = []).Add(connection1.SendAsync(message, cancellationToken));
        if (_itConnectionContexts.TryGetValue(clientId2, out var connection2))
            (tasks ??= []).Add(connection2.SendAsync(message, cancellationToken));
        return tasks is null ? Task.CompletedTask : Task.WhenAll(tasks);
    }
}