using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;

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

    public Task SendUserAsync<T>(Guid clientId, T message, JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return !_itConnectionContexts.TryGetValue(clientId, out var connection)
            ? Task.CompletedTask
            : connection.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, options: options), cancellationToken);
    }

    public Task SendUsersAsync<T>(Guid clientId1, Guid clientId2, T message, JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _itConnectionContexts.TryGetValue(clientId1, out var connection1);
        _itConnectionContexts.TryGetValue(clientId2, out var connection2);
        if (connection1 is null || connection2 is null)
            return Task.CompletedTask;
        var data = JsonSerializer.SerializeToUtf8Bytes(message, options: options);
        return Task.WhenAll(connection1.SendAsync(data, cancellationToken),
            connection2.SendAsync(data, cancellationToken));
    }
}