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


    public IEnumerable<StreamItConnectionContext> Connections => connections.Connections;

    public ValueTask AddConnection(StreamItConnectionContext context)
    {
        connections.Add(context);
        return ValueTask.CompletedTask;
    }

    public async Task RemoveConnection(StreamItConnectionContext context)
    {
        if (!connections.TryRemove(context, out _))
            return;
        if (context.Groups.Count == 0)
            return;
        await RemoveFromGroupsNoCheck(context.Groups, context);
    }

    public async Task AddToGroup(string groupName, StreamItConnectionContext context)
    {
        if (!connections.TryGetValue(context, out _))
        {
            return;
        }

        await context.GroupLock.WaitAsync();
        _groups.Add(context, groupName);
        context.Groups.Add(groupName);
        context.GroupLock.Release();
    }

    public async Task AddToGroups(IEnumerable<string> groups, StreamItConnectionContext connectionContext)
    {
        if (!connections.TryGetValue(connectionContext, out _))
            return;
        await connectionContext.GroupLock.WaitAsync();
        foreach (var groupName in groups)
        {
            _groups.Add(connectionContext, groupName);
        }

        connectionContext.GroupLock.Release();
    }


    public async Task RemoveFromGroup(string groupName, StreamItConnectionContext context)
    {
        if (!connections.TryGetValue(context, out var connectionContext))
            return;
        await connectionContext!.GroupLock.WaitAsync();
        _groups.Remove(connectionContext, groupName);
        context.GroupLock.Release();
    }

    public async Task RemoveFromGroups(IEnumerable<string> groups, StreamItConnectionContext context)
    {
        await RemoveFromGroups(groups.ToList(), context);
    }

    public async Task RemoveFromGroups(List<string> groups, StreamItConnectionContext context)
    {
        if (!connections.TryRemove(context, out _))
            return;
        if (groups.Count == 0)
            return;
        await RemoveFromGroupsNoCheck(groups, context);
    }

    
    /// <summary>
    /// remove from groups without checking if the connection exists
    /// </summary>
    /// <param name="groups"></param>
    /// <param name="context"></param>
    private async Task RemoveFromGroupsNoCheck(IEnumerable<string> groups, StreamItConnectionContext context)
    {
        await context.GroupLock.WaitAsync();
        foreach (var groupName in groups)
        {
            _groups.Remove(context, groupName);
        }

        context.GroupLock.Release();
    }
}


