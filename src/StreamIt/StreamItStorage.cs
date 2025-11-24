namespace StreamIt;

public sealed class StreamItStorage
{
    private readonly StreamItConnectionList connections = new();

    private readonly StreamItGroupList _groups = new();

    public StreamItGroupList Groups => _groups;

    public StreamItGroup? Group(string groupName)
    {
        return _groups[groupName];
    }

    public int ConnectionCount => connections.Count;

    internal ValueTask AddConnection(StreamItConnectionContext context)
    {
        connections.Add(context);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// should be called when the context id changes. This will remove context from all existing groups 
    /// </summary>
    public async ValueTask UpdateClientId(StreamItConnectionContext context, Guid newClientId)
    {
        if (!connections.TryRemove(context, out var ctx))
            throw new InvalidOperationException("unknown connection");
        await RemoveFromGroupsNoCheck(ctx.Groups, ctx);
        context.SetClient(newClientId);
        connections.Add(context);
    }

    internal Task RemoveConnection(StreamItConnectionContext context)
    {
        if (!connections.TryRemove(context, out _) || context.Groups.Count == 0)
            return Task.CompletedTask;
        return RemoveFromGroupsNoCheck(context.Groups, context);
    }


    /// <summary>
    /// add the connection to the group
    /// </summary>
    /// <param name="groupName"></param>
    /// <param name="context"></param>
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

    /// <summary>
    /// add the connection to multiple groups
    /// </summary>
    /// <param name="groups"></param>
    /// <param name="connectionContext"></param>
    public Task AddToGroups(IEnumerable<string> groups, StreamItConnectionContext connectionContext)
    {
        return AddToGroups(groups.ToList(), connectionContext);
    }

    public async Task AddToGroups(List<string> groups, StreamItConnectionContext connectionContext)
    {
        if (!connections.TryGetValue(connectionContext, out _))
            return;
        await connectionContext.GroupLock.WaitAsync();
        foreach (var groupName in groups)
        {
            _groups.Add(connectionContext, groupName);
            connectionContext.Groups.Add(groupName);
        }

        connectionContext.GroupLock.Release();
    }


    public async Task RemoveFromGroup(string groupName, StreamItConnectionContext context)
    {
        if (!connections.TryGetValue(context, out var connectionContext))
            return;
        await connectionContext!.GroupLock.WaitAsync();
        _groups.Remove(connectionContext, groupName);
        context.Groups.Remove(groupName);
        context.GroupLock.Release();
    }

    public Task RemoveFromGroups(IEnumerable<string> groups, StreamItConnectionContext context)
    {
        return RemoveFromGroups(groups.ToList(), context);
    }

    public Task RemoveFromGroups(List<string> groups, StreamItConnectionContext context)
    {
        if (groups.Count == 0 || !connections.TryGetValue(context, out _))
            return Task.CompletedTask;
        return RemoveFromGroupsNoCheck(groups, context);
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
            context.Groups.Remove(groupName);
        }

        context.GroupLock.Release();
    }
}