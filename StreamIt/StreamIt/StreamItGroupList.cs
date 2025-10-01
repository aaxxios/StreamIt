using System.Collections.Concurrent;

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

    public IEnumerable<StreamItGroup> Groups => _groups.Values;


    /// <summary>
    /// send a message to all users in all groups
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendUserAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(_groups.Values.Select(gr => gr.SendMessage(message, cancellationToken)));
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
        group.TryRemove(connection, out _);
        if (group.Count == 0)
            _groups.TryRemove(groupName, out _);
    }

    public int GroupCount => _groups.Count;


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