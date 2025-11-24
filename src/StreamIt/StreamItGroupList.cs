using System.Collections.Concurrent;
using System.Text.Json;

namespace StreamIt;

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

    /// <summary>
    /// gets the group from the list
    /// </summary>
    /// <param name="groupName">group name</param>
    /// <returns></returns>
    public StreamItGroup? Group(string groupName)
    {
        _groups.TryGetValue(groupName, out var group);
        return group;
    }
    
    /// <summary>
    /// send a message to all groups
    /// </summary>
    /// <param name="message">message to send</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(_groups.Values.Select(gr => gr.SendAllAsync(message, cancellationToken)));
    }

    /// <summary>
    /// send a message to all groups
    /// </summary>
    /// <param name="message">message to send</param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendAsync<T>(T message, JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(_groups.Values.Select(gr => gr.SendAllAsync(message, options, cancellationToken)));
    }

    /// <summary>
    /// add connection to the group, if the group does not exist, create it
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="groupName"></param>
    internal void Add(StreamItConnectionContext connection, string groupName)
    {
        CreateOrUpdateGroupWithConnection(groupName, connection);
    }

    internal void Remove(StreamItConnectionContext connection, string groupName)
    {
        if (!_groups.TryGetValue(groupName, out var group))
            return;
        if(!group.TryRemove(connection, out _))
            return;
        if (group.IsEmpty)
            _groups.TryRemove(groupName, out _);
    }

    /// <summary>
    /// get the number of groups in the list
    /// </summary>
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