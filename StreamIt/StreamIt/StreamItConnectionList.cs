using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace StreamIt;

/// <summary>
/// list of connections
/// </summary>
public sealed class StreamItConnectionList
{
    private readonly ConcurrentDictionary<Guid, StreamItConnectionContext> _itConnectionContexts = new();

    public ICollection<StreamItConnectionContext> Connections => _itConnectionContexts.Values;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(StreamItConnectionContext context, out StreamItConnectionContext? value)
    {
        return _itConnectionContexts.TryGetValue(context.ClientId, out value);
    }


    /// <summary>
    /// number of connections in the list
    /// </summary>
    public int Count => _itConnectionContexts.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(Guid id, out StreamItConnectionContext? value)
    {
        return _itConnectionContexts.TryGetValue(id, out value);
    }

    /// <summary>
    /// try to remove a connection from the list
    /// </summary>
    /// <param name="context"></param>
    /// <param name="removed"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemove(StreamItConnectionContext context, out StreamItConnectionContext? removed)
    {
        return _itConnectionContexts.TryRemove(context.ClientId, out removed);
    }

    /// <summary>
    /// add a connection to the list
    /// </summary>
    /// <param name="connection"></param>
    public void Add(StreamItConnectionContext connection)
    {
        _itConnectionContexts.AddOrUpdate(connection.ClientId, connection, (_, _) => connection);
    }

    /// <summary>
    /// send a message to all connections in the list
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendMessageAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        if (_itConnectionContexts.IsEmpty)
            return Task.CompletedTask;
        return Task.WhenAll(
            _itConnectionContexts.Values.Select(context => context.SendAsync(message, cancellationToken)));
    }

    /// <summary>
    /// send message to all connections in the list
    /// </summary>
    /// <param name="message"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public Task SendMessageAsync<T>(T message, JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_itConnectionContexts.IsEmpty)
            return Task.CompletedTask;
        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(message, options: options);
        return Task.WhenAll(
            _itConnectionContexts.Values.Select(context => context.SendAsync(messageBytes, cancellationToken)));
    }

    /// <summary>
    /// send a message to a connection
    /// </summary>
    /// <param name="clientId">id of the connection</param>
    /// <param name="message">message to send</param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public Task SendUserAsync<T>(Guid clientId, T message, JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _itConnectionContexts.TryGetValue(clientId, out var connection)
            ? connection.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message, options: options), cancellationToken)
            : Task.CompletedTask;
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