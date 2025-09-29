using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

// ReSharper disable UnusedMethodReturnValue.Global

namespace StreamIt;

/// <summary>
/// global storage of all connections and groups
/// </summary>
public sealed class StreamItStorage
{
    private readonly StreamItConnectionList connections = new();

    private StreamItGroupList _groups { get; } = new();

    public IEnumerable<StreamItGroup> Groups => _groups.Groups;


    public StreamItGroup? Group(string groupName)
    {
        return _groups[groupName];
    }


    public ValueTask AddConnection(StreamItConnectionContext context)
    {
        connections.Add(context);
        return ValueTask.CompletedTask;
    }

    public async Task RemoveConnection(StreamItConnectionContext connectionContext)
    {
        await connectionContext.GroupLock.WaitAsync();
        if (!connections.TryRemove(connectionContext, out _))
            return;
        foreach (var groupName in connectionContext.Groups)
        {
            if (_groups[groupName] is { } group)
            {
                group.TryRemove(connectionContext, out _);
            }
        }
    }

    public async Task AddToGroup(string groupName, StreamItConnectionContext context)
    {
        if (!connections.TryGetValue(context, out var connectionContext))
            return;
        if (_groups[groupName] is { } group)
        {
            await connectionContext!.GroupLock.WaitAsync();
            group.Add(context);
            connectionContext.GroupLock.Release();
        }
    }

    public async Task AddToGroups(IEnumerable<string> groups, StreamItConnectionContext connectionContext)
    {
        if (!connections.TryGetValue(connectionContext, out var context))
            return;
        await connectionContext.GroupLock.WaitAsync();
        foreach (var groupName in groups)
        {
            if (_groups[groupName] is not { } group) continue;
            group.Add(context!);
            connectionContext.Groups.Add(groupName);
        }

        connectionContext.GroupLock.Release();
    }


    public async Task RemoveFromGroup(string groupName, StreamItConnectionContext context)
    {
        if (!connections.TryGetValue(context, out var connectionContext))
            return;
        await connectionContext!.GroupLock.WaitAsync();
        if (_groups[groupName] is { } group)
        {
            if (!group.TryRemove(connectionContext, out _))
                return;
            connectionContext.Groups.Remove(groupName);
        }

        context.GroupLock.Release();
    }

    public async Task RemoveFromGroups(IEnumerable<string> groups, StreamItConnectionContext context)
    {
        await context.GroupLock.WaitAsync();
        if (connections.TryRemove(context, out var connectionContext))
            foreach (var groupName in groups)
            {
                if (!connectionContext!.Groups.Contains(groupName))
                    continue;
                if (_groups[groupName] is not { } group) continue;
                context.Groups.Remove(groupName);
                group.TryRemove(context, out _);
            }
        context.GroupLock.Release();
    }
}

/// <summary>
/// manage group list
/// </summary>
public sealed class StreamItGroupList
{
    private readonly ConcurrentDictionary<string, StreamItGroup> _groups = new(StringComparer.Ordinal);

    public StreamItGroup? this[string groupName]
    {
        get
        {
            _groups.TryGetValue(groupName, out var group);
            return group;
        }
    }

    public IEnumerable<StreamItGroup> Groups => _groups.Values;


    /// <summary>
    /// send message to all users all groups
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendUserAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(_groups.Values.Select(gr => gr.SendMessage(message, cancellationToken)));
    }

    /// <summary>
    /// add connection to group
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="groupName"></param>
    internal void Add(StreamItConnectionContext connection, string groupName)
    {
        CreateOrUpdateGroupWithConnection(groupName, connection);
    }

    public int Count => _groups.Count;


    private void CreateOrUpdateGroupWithConnection(string groupName, StreamItConnectionContext connection)
    {
        _groups.AddOrUpdate(groupName, _ => AddConnectionToGroup(connection, new StreamItGroup(groupName)),
            (_, oldCollection) =>
            {
                AddConnectionToGroup(connection, oldCollection);
                return oldCollection;
            });
    }

    private static StreamItGroup AddConnectionToGroup(
        StreamItConnectionContext connection, StreamItGroup streamIt)
    {
        streamIt.Add(connection);
        return streamIt;
    }
}

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
}

public sealed class StreamItConnectionList
{
    private readonly ConcurrentDictionary<Guid, StreamItConnectionContext> _itConnectionContexts = new();

    public IEnumerable<StreamItConnectionContext> Connections => _itConnectionContexts.Values;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(StreamItConnectionContext context, out StreamItConnectionContext? value)
    {
        return _itConnectionContexts.TryGetValue(context.ClientId, out value);
    }

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
            : connection!.SendAsync(message, cancellationToken);
    }
}