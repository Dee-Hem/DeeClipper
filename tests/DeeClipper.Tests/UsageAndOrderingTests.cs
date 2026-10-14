using DeeClipper.Core.Common;
using DeeClipper.Core.Storage;
using Xunit;

namespace DeeClipper.Tests;

public class UsageAndOrderingTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly DatabaseService _db;

    public UsageAndOrderingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeeClipper_UsageTests_" + Guid.NewGuid().ToString("N"));
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
            // Ignore
        }
    }

    [Fact]
    public async Task TouchItemUsage_IncrementsUseCountAndMovesToTopOfRecency()
    {
        // Arrange: Add 3 items in sequence
        var item1 = await _db.AddOrUpdateTextItemAsync("Item 1");
        await Task.Delay(20);
        var item2 = await _db.AddOrUpdateTextItemAsync("Item 2");
        await Task.Delay(20);
        var item3 = await _db.AddOrUpdateTextItemAsync("Item 3");

        // Initial order: Item 3 is first, Item 1 is last
        var listBefore = await _db.GetAllItemsAsync();
        Assert.Equal(item3.Id, listBefore[0].Id);
        Assert.Equal(item1.Id, listBefore[2].Id);
        Assert.Equal(1, listBefore[2].UseCount);

        await Task.Delay(50);

        // Act: Touch usage on Item 1 (e.g. pasted or copied)
        await _db.TouchItemUsageAsync(item1.Id);

        // Assert: Item 1 now has use_count = 2 and is at the top of the list!
        var listAfter = await _db.GetAllItemsAsync();
        Assert.Equal(item1.Id, listAfter[0].Id);
        Assert.Equal(2, listAfter[0].UseCount);
        Assert.Equal(item3.Id, listAfter[1].Id);
        Assert.Equal(item2.Id, listAfter[2].Id);
    }

    [Fact]
    public async Task TouchItemUsage_PreservesPinnedState()
    {
        var pinnedItem = await _db.AddOrUpdateTextItemAsync("Pinned Important Snippet");
        await _db.TogglePinAsync(pinnedItem.Id);

        var unpinnedItem = await _db.AddOrUpdateTextItemAsync("Unpinned Random Text");

        // Touch pinned item usage
        await Task.Delay(20);
        await _db.TouchItemUsageAsync(pinnedItem.Id);

        var retrieved = await _db.GetItemByIdAsync(pinnedItem.Id);
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsPinned);
        Assert.Equal(2, retrieved.UseCount);

        // Touch unpinned item usage
        await Task.Delay(20);
        await _db.TouchItemUsageAsync(unpinnedItem.Id);

        var retrievedUnpinned = await _db.GetItemByIdAsync(unpinnedItem.Id);
        Assert.NotNull(retrievedUnpinned);
        Assert.False(retrievedUnpinned.IsPinned);
        Assert.Equal(2, retrievedUnpinned.UseCount);
    }
}
