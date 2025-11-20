using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace StreamIt;

/// <summary>
/// a unique group of connections
/// </summary>
public sealed class StreamItGroup
{
    /// <summary>
    /// name of the group
    /// </summary>
    public string Name { get; }

    public StreamItGroup(string name)
    {
        Name = name;
    }

    private readonly StreamItConnectionList _connectionList = new();


    /// <summary>
    /// number of connections in the group
    /// </summary>
    public int Count => _connectionList.Count;
    
    public StreamItConnectionContext? this[Guid clientId]
    {
        get
        {
            _connectionList.TryGetValue(clientId, out var value);
            return value;
        }
    }

    public bool IsMember(StreamItConnectionContext context)
    {
        return _connectionList.Contains(context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryRemove(StreamItConnectionContext context, [NotNullWhen(true)] out StreamItConnectionContext? removed)
    {
        return _connectionList.TryRemove(context, out removed);
    }


    /// <summary>
    /// add a connection to the group
    /// </summary>
    /// <param name="connection"></param>
    /// <returns></returns>
    internal StreamItGroup Add(StreamItConnectionContext connection)
    {
        _connectionList.Add(connection);
        return this;
    }

    /// <summary>
    /// send a message to  all users in the group
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendAllAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        return _connectionList.SendMessageAsync(message, cancellationToken);
    }

    /// <summary>
    /// send a message to all connections in the group
    /// </summary>
    /// <param name="message"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public Task SendAllAsync<T>(T message, JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _connectionList.SendMessageAsync(message, options, cancellationToken);
    }

    /// <summary>
    /// send a message to a connection in the group
    /// </summary>
    /// <param name="clientId">id identifying the connection</param>
    /// <param name="message">message to send</param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendUserAsync<T>(Guid clientId, T message, JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _connectionList.SendUserAsync(clientId, message, options, cancellationToken);
    }

    /// <summary>
    /// send a message to specified connections in the group
    /// </summary>
    /// <param name="clientId1"></param>
    /// <param name="clientId2"></param>
    /// <param name="message"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendUsersAsync<T>(Guid clientId1, Guid clientId2, T message,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _connectionList.SendUsersAsync(clientId1, clientId2, message, options, cancellationToken);
    }
}