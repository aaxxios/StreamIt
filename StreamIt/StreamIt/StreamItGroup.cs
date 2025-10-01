using System.Runtime.CompilerServices;

namespace StreamIt;

public sealed class StreamItGroup
{
    public string Name { get; }

    public StreamItGroup(string name)
    {
        Name = name;
    }

    private readonly StreamItConnectionList _connectionList = new();

    public IEnumerable<StreamItConnectionContext> Connections => _connectionList.Connections;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(StreamItConnectionContext context, out StreamItConnectionContext? value)
    {
        return _connectionList.TryGetValue(context, out value);
    }
    
    public int Count => _connectionList.Count;

    /// <summary>
    /// get connection by client id
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(Guid clientId, out StreamItConnectionContext? value)
    {
        return _connectionList.TryGetValue(clientId, out value);
    }

    public StreamItConnectionContext? this[Guid clientId]
    {
        get
        {
            _connectionList.TryGetValue(clientId, out var value);
            return value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemove(StreamItConnectionContext context, out StreamItConnectionContext? removed)
    {
        return _connectionList.TryRemove(context, out removed);
    }


    /// <summary>
    /// add a connection to the group
    /// </summary>
    /// <param name="connection"></param>
    /// <returns></returns>
    public StreamItGroup Add(StreamItConnectionContext connection)
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
    public Task SendMessage(byte[] message, CancellationToken cancellationToken = default)
    {
        return _connectionList.SendMessage(message, cancellationToken);
    }


    /// <summary>
    /// send message to user in this group
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendUserAsync(Guid clientId, byte[] message, CancellationToken cancellationToken = default)
    {
        return _connectionList.SendUserAsync(clientId, message, cancellationToken);
    }

    /// <summary>
    /// send message to two users in this group
    /// </summary>
    /// <param name="clientId1"></param>
    /// <param name="clientId2"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendUsersAsync(Guid clientId1, Guid clientId2, byte[] message,
        CancellationToken cancellationToken = default)
    {
        return _connectionList.SendUsersAsync(clientId1, clientId2, message, cancellationToken);
    }
}