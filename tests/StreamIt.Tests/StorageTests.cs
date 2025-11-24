using Microsoft.Extensions.Options;

[assembly: CaptureConsole]

namespace StreamIt.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly StreamItStorage storage;

    private readonly StreamItConnectionContext context;

    public StorageTests()
    {
        storage = new StreamItStorage();
        context = new StreamItConnectionContext(Guid.NewGuid(), new DummyWebSocket(),
            Options.Create(new StreamItOptions()));
    }

    [Fact]
    public void EmptyStorageCountIsZero()
    {
        Assert.Equal(0, storage.Groups.Count);
        Assert.Equal(0, storage.ConnectionCount);
    }

    [Fact]
    public async Task TestConnectionAdded()
    {
        await storage.AddConnection(context);
        Assert.Equal(1, storage.ConnectionCount);
    }

    [Fact]
    public async Task AddingIdenticalConnectionTwiceDoesNotAddItTwice()
    {
        await storage.AddConnection(context);
        await storage.AddConnection(context);
        Assert.Equal(1, storage.ConnectionCount);
    }

    [Fact]
    public async Task TestConnectionRemoved()
    {
        await storage.AddConnection(context);
        await storage.RemoveConnection(context);
        Assert.Equal(0, storage.ConnectionCount);
    }

    [Fact]
    public async Task RemovingConnectionFromGroupDeletesGroupIfEmpty()
    {
        await storage.AddConnection(context);
        await storage.AddToGroup("test", context);
        Assert.Equal(1, storage.Groups.Count);
        await storage.RemoveFromGroup("test", context);
        Assert.Equal(0, storage.Groups.Count);
        Assert.Null(storage.Groups["test"]);
        Assert.Empty(context.Groups);
    }

    [Fact]
    public async Task RemovingConnectionRemovesConnectionFromAllGroupsAndDeleteEmptyGroups()
    {
        await storage.AddConnection(context);
        await storage.AddToGroup("test", context);
        Assert.NotNull(storage.Groups["test"]);
        await storage.AddToGroup("test2", context);
        Assert.NotNull(storage.Groups["test2"]);
        await storage.RemoveConnection(context);
        Assert.Equal(0, storage.ConnectionCount);
        Assert.Equal(0, storage.Groups.Count);
        Assert.Null(storage.Groups["test"]);
        Assert.Null(storage.Groups["test2"]);
        Assert.Empty(context.Groups);
    }

    [Fact]
    public async Task GroupNotAddedForUnknownConnection()
    {
        await storage.AddToGroup("test", context);
        Assert.Equal(0, storage.Groups.Count);
        Assert.Empty(context.Groups);
    }

    [Fact]
    public async Task UpdateClientIdThrowsForUnknownConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(UpdateClientId);
    }

    private async Task UpdateClientId()
    {
        await storage.UpdateClientId(context, Guid.NewGuid());
    }

    [Fact]
    public async Task UpdatingClientIdSucceedsForExistingConnection()
    {
        await storage.AddConnection(context);
        await storage.UpdateClientId(context, Guid.NewGuid());
    }

    [Fact]
    public async Task UpdatingClientIdRemovesConnectionFromSubscribedGroups()
    {
        await storage.AddConnection(context);
        await storage.AddToGroup("test", context);
        await storage.AddToGroup("test2", context);
        Assert.Equal(2, storage.Groups.Count);
        Assert.NotNull(storage.Groups["test"]);
        Assert.NotNull(storage.Groups["test2"]);
        await storage.UpdateClientId(context, Guid.NewGuid());
        Assert.Null(storage.Groups["test"]);
        Assert.Null(storage.Groups["test"]);
    }

    [Fact]
    public async Task IsMemberReturnsTrueIfConnectionIsSubscribed()
    {
        await storage.AddConnection(context);
        await storage.AddToGroup("test", context);
        var group = storage.Groups.Group("test");
        Assert.NotNull(group);
        Assert.True(group.IsMember(context));
    }

    public void Dispose()
    {
        context.Dispose();
    }
}