using DeeClipper.Core.Common;
using DeeClipper.Core.Models;
using DeeClipper.Core.Storage;
using Xunit;

namespace DeeClipper.Tests;

public class RetentionTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly DatabasePaths _paths;
    private readonly DatabaseService _db;

    public RetentionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeeClipper_RetTests_" + Guid.NewGuid().ToString("N"));
        _paths = new DatabasePaths(_tempDir);
        _db = new DatabaseService(_paths);
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
    public async Task EnforceRetention_CountLimit_PinnedItemsSurviveAndUnpinnedAreTrimmed()
    {
        // Arrange: Create 2 pinned items and 5 unpinned items
        var pinned1 = await _db.AddOrUpdateTextItemAsync("Pinned Item 1");
        await _db.TogglePinAsync(pinned1.Id);

        var pinned2 = await _db.AddOrUpdateTextItemAsync("Pinned Item 2");
        await _db.TogglePinAsync(pinned2.Id);

        var unpinnedItems = new List<ClipboardItem>();
        for (var i = 1; i <= 5; i++)
        {
            await Task.Delay(20);
            var item = await _db.AddOrUpdateTextItemAsync($"Unpinned Item {i}");
            unpinnedItems.Add(item);
        }

        Assert.Equal(7, await _db.GetCountAsync());
        Assert.Equal(2, await _db.GetCountAsync(pinnedOnly: true));

        // Act: Enforce count limit of 2 unpinned items
        await _db.EnforceRetentionAsync(limit: 2);

        // Assert:
        // Pinned items MUST SURVIVE
        var p1Check = await _db.GetItemByIdAsync(pinned1.Id);
        var p2Check = await _db.GetItemByIdAsync(pinned2.Id);
        Assert.NotNull(p1Check);
        Assert.True(p1Check.IsPinned);
        Assert.NotNull(p2Check);
        Assert.True(p2Check.IsPinned);

        // Unpinned count must be exactly 2 (the latest 2 added: items 4 and 5)
        var unpinnedRemaining = await _db.SearchAsync(null, isPinned: false);
        Assert.Equal(2, unpinnedRemaining.Count);
        Assert.Contains(unpinnedRemaining, x => x.ContentText == "Unpinned Item 5");
        Assert.Contains(unpinnedRemaining, x => x.ContentText == "Unpinned Item 4");

        // Total count = 2 pinned + 2 unpinned = 4
        Assert.Equal(4, await _db.GetCountAsync());
    }

    [Fact]
    public async Task EnforceRetention_AgeLimit_PinnedItemsSurviveAndOldUnpinnedAreDeleted()
    {
        // Arrange: 1 pinned item, 1 old unpinned item, 1 recent unpinned item
        var pinnedOld = await _db.AddOrUpdateTextItemAsync("Old Pinned Note");
        await _db.TogglePinAsync(pinnedOld.Id);

        var unpinnedOld = await _db.AddOrUpdateTextItemAsync("Old Unpinned Note");
        var unpinnedRecent = await _db.AddOrUpdateTextItemAsync("Recent Unpinned Note");

        // Backdate pinnedOld and unpinnedOld by 30 days
        var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30).ToString("o");
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString()))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE items SET last_used_at = @d WHERE id IN (@pId, @uId);";
            cmd.Parameters.AddWithValue("@d", thirtyDaysAgo);
            cmd.Parameters.AddWithValue("@pId", pinnedOld.Id);
            cmd.Parameters.AddWithValue("@uId", unpinnedOld.Id);
            await cmd.ExecuteNonQueryAsync();
        }

        // Act: Enforce age limit of 7 days
        await _db.EnforceRetentionAsync(limit: 500, maxAgeDays: 7);

        // Assert:
        // Pinned old item MUST survive!
        var pinnedCheck = await _db.GetItemByIdAsync(pinnedOld.Id);
        Assert.NotNull(pinnedCheck);
        Assert.True(pinnedCheck.IsPinned);

        // Old unpinned item must be deleted
        var oldUnpinnedCheck = await _db.GetItemByIdAsync(unpinnedOld.Id);
        Assert.Null(oldUnpinnedCheck);

        // Recent unpinned item must survive
        var recentUnpinnedCheck = await _db.GetItemByIdAsync(unpinnedRecent.Id);
        Assert.NotNull(recentUnpinnedCheck);
    }

    [Fact]
    public async Task ClearHistory_PinnedItemsSurviveAllClearOperations()
    {
        // Arrange: Create 3 pinned items and 5 unpinned items
        for (var i = 1; i <= 3; i++)
        {
            var p = await _db.AddOrUpdateTextItemAsync($"Keeper Pinned {i}");
            await _db.TogglePinAsync(p.Id);
        }

        for (var i = 1; i <= 5; i++)
        {
            await _db.AddOrUpdateTextItemAsync($"Disposable Unpinned {i}");
        }

        Assert.Equal(8, await _db.GetCountAsync());
        Assert.Equal(3, await _db.GetCountAsync(pinnedOnly: true));

        // Act: Clear history
        var deletedCount = await _db.ClearHistoryAsync();
        Assert.Equal(5, deletedCount);

        // Assert: ALL 3 pinned items still exist!
        var remaining = await _db.GetAllItemsAsync();
        Assert.Equal(3, remaining.Count);
        Assert.All(remaining, item => Assert.True(item.IsPinned));
        Assert.Equal(0, await _db.GetCountAsync(pinnedOnly: false));
    }

    [Fact]
    public async Task WipeSensitive_PinnedSensitiveItemsSurviveAndUnpinnedAreWiped()
    {
        // Arrange: 1 pinned sensitive item, 1 unpinned sensitive item
        var sensitiveToken = "ghp_SECRETTOKEN0123456789abcdef012345";
        var pinnedSensitive = await _db.AddOrUpdateTextItemAsync(sensitiveToken + "_PINNED");
        await _db.TogglePinAsync(pinnedSensitive.Id);

        var unpinnedSensitive = await _db.AddOrUpdateTextItemAsync(sensitiveToken + "_UNPINNED");

        // Backdate both by 10 minutes
        var tenMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("o");
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath }.ToString()))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE items SET last_used_at = @d WHERE id IN (@pId, @uId);";
            cmd.Parameters.AddWithValue("@d", tenMinutesAgo);
            cmd.Parameters.AddWithValue("@pId", pinnedSensitive.Id);
            cmd.Parameters.AddWithValue("@uId", unpinnedSensitive.Id);
            await cmd.ExecuteNonQueryAsync();
        }

        // Act: Wipe sensitive older than 5 minutes
        var wiped = await _db.WipeSensitiveAsync(TimeSpan.FromMinutes(5));
        Assert.Equal(1, wiped);

        // Assert: Pinned sensitive item MUST survive!
        var pinnedCheck = await _db.GetItemByIdAsync(pinnedSensitive.Id);
        Assert.NotNull(pinnedCheck);
        Assert.True(pinnedCheck.IsPinned);
        Assert.True(pinnedCheck.IsSensitive);

        // Unpinned sensitive item was deleted!
        var unpinnedCheck = await _db.GetItemByIdAsync(unpinnedSensitive.Id);
        Assert.Null(unpinnedCheck);
    }

    [Fact]
    public async Task DeleteEverything_RequiresTypingDELETE_AndRemovesAllItems()
    {
        // Arrange: Create pinned and unpinned items
        var pinned = await _db.AddOrUpdateTextItemAsync("Pinned Note");
        await _db.TogglePinAsync(pinned.Id);
        await _db.AddOrUpdateTextItemAsync("Regular Note");

        Assert.Equal(2, await _db.GetCountAsync());

        // Act 1: Call without typing exact DELETE confirmation
        await Assert.ThrowsAsync<ArgumentException>(() => _db.DeleteEverythingAsync("delete"));
        await Assert.ThrowsAsync<ArgumentException>(() => _db.DeleteEverythingAsync("YES"));

        // Items must still be intact
        Assert.Equal(2, await _db.GetCountAsync());

        // Act 2: Call with exact confirmation "DELETE"
        var deletedCount = await _db.DeleteEverythingAsync("DELETE");
        Assert.Equal(2, deletedCount);

        // Assert: Completely empty now
        Assert.Equal(0, await _db.GetCountAsync());
    }

    [Fact]
    public async Task DeleteItem_ExplicitDeletion_RemovesSpecificItem()
    {
        var item1 = await _db.AddOrUpdateTextItemAsync("Item 1");
        var item2 = await _db.AddOrUpdateTextItemAsync("Item 2");

        var deleted = await _db.DeleteItemAsync(item1.Id);
        Assert.True(deleted);

        Assert.Null(await _db.GetItemByIdAsync(item1.Id));
        Assert.NotNull(await _db.GetItemByIdAsync(item2.Id));
    }
}
