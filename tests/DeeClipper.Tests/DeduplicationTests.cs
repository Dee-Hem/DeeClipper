using DeeClipper.Core.Common;
using DeeClipper.Core.Models;
using DeeClipper.Core.Storage;
using Xunit;

namespace DeeClipper.Tests;

public class DeduplicationTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly DatabaseService _db;

    public DeduplicationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeeClipper_Tests_" + Guid.NewGuid().ToString("N"));
        var paths = new DatabasePaths(_tempDir);
        _db = new DatabaseService(paths);
    }

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task AddItem_SameContentTwice_UpdatesLastUsedAtAndIncrementsUseCount()
    {
        // Act 1: First add
        var item1 = await _db.AddOrUpdateTextItemAsync("Unique Content for Deduplication Test");
        Assert.Equal(1, item1.UseCount);
        var originalId = item1.Id;
        var firstLastUsed = item1.LastUsedAt;

        await Task.Delay(50);

        // Act 2: Add identical content again
        var item2 = await _db.AddOrUpdateTextItemAsync("Unique Content for Deduplication Test");

        // Assert: Same ID, never create second row, use count is 2, last_used_at is newer
        Assert.Equal(originalId, item2.Id);
        Assert.Equal(2, item2.UseCount);
        Assert.True(item2.LastUsedAt >= firstLastUsed);

        var totalCount = await _db.GetCountAsync();
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task AddItem_DuplicateOfPinnedItem_NeverChangesPinnedState()
    {
        // Arrange: Add item and pin it
        var item = await _db.AddOrUpdateTextItemAsync("Pinned Item Deduplication");
        await _db.TogglePinAsync(item.Id);

        var pinnedBefore = await _db.GetItemByIdAsync(item.Id);
        Assert.NotNull(pinnedBefore);
        Assert.True(pinnedBefore.IsPinned);

        await Task.Delay(50);

        // Act: Add same content again
        var itemAfter = await _db.AddOrUpdateTextItemAsync("Pinned Item Deduplication");

        // Assert: Pinned state MUST still be true
        Assert.Equal(item.Id, itemAfter.Id);
        Assert.True(itemAfter.IsPinned);
        Assert.Equal(2, itemAfter.UseCount);

        var retrieved = await _db.GetItemByIdAsync(item.Id);
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsPinned);
        Assert.Equal(2, retrieved.UseCount);
    }

    [Fact]
    public async Task AddItem_DuplicateOfUnpinnedItem_RemainsUnpinned()
    {
        // Arrange: Add unpinned item
        var item = await _db.AddOrUpdateTextItemAsync("Unpinned Item Deduplication");
        Assert.False(item.IsPinned);

        // Act: Add same content again
        var itemAfter = await _db.AddOrUpdateTextItemAsync("Unpinned Item Deduplication");

        // Assert: Still unpinned
        Assert.Equal(item.Id, itemAfter.Id);
        Assert.False(itemAfter.IsPinned);
        Assert.Equal(2, itemAfter.UseCount);
    }

    [Fact]
    public async Task AddItem_DuplicateItem_MovesToTopOfRecencyOrder()
    {
        // Arrange: Add item A, then item B
        var itemA = await _db.AddOrUpdateTextItemAsync("Item Alpha");
        await Task.Delay(50);
        var itemB = await _db.AddOrUpdateTextItemAsync("Item Beta");

        // Verify initial order: B is on top
        var itemsBefore = await _db.GetAllItemsAsync();
        Assert.Equal(itemB.Id, itemsBefore[0].Id);
        Assert.Equal(itemA.Id, itemsBefore[1].Id);

        await Task.Delay(50);

        // Act: Add item A again
        var itemAUpdated = await _db.AddOrUpdateTextItemAsync("Item Alpha");

        // Assert: Item A is now on top
        var itemsAfter = await _db.GetAllItemsAsync();
        Assert.Equal(itemAUpdated.Id, itemsAfter[0].Id);
        Assert.Equal(itemB.Id, itemsAfter[1].Id);
        Assert.Equal(2, itemsAfter.Count);
    }
}
