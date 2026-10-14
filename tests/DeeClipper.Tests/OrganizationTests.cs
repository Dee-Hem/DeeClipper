using DeeClipper.Core.Common;
using DeeClipper.Core.Models;
using DeeClipper.Core.Storage;
using DeeClipper.Core.Text;
using Xunit;

namespace DeeClipper.Tests;

public class OrganizationTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly DatabaseService _db;

    public OrganizationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeeClipper_OrgTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task DeleteGroup_NeverDeletesItems_UngroupsThem()
    {
        // 1. Create a group
        var group = await _db.CreateGroupAsync("Work", "#3B82F6");
        Assert.NotNull(group);
        Assert.True(group.Id > 0);

        // 2. Add multiple items assigned to this group (both pinned and unpinned)
        var item1 = await _db.AddOrUpdateTextItemAsync("Work task 1", groupId: group.Id);
        var item2 = await _db.AddOrUpdateTextItemAsync("Work snippet 2", groupId: group.Id);
        var item3 = await _db.AddOrUpdateTextItemAsync("Work notes 3", groupId: group.Id);

        // Pin item2
        await _db.TogglePinAsync(item2.Id);

        // Verify items belong to group
        var initialList = await _db.SearchAsync(groupId: group.Id);
        Assert.Equal(3, initialList.Count);

        // 3. Delete the group
        var deleted = await _db.DeleteGroupAsync(group.Id);
        Assert.True(deleted);

        // Group is gone
        var groups = await _db.GetGroupsAsync();
        Assert.DoesNotContain(groups, g => g.Id == group.Id);

        // 4. CRITICAL: All 3 items MUST still exist in the database!
        var afterItem1 = await _db.GetItemByIdAsync(item1.Id);
        var afterItem2 = await _db.GetItemByIdAsync(item2.Id);
        var afterItem3 = await _db.GetItemByIdAsync(item3.Id);

        Assert.NotNull(afterItem1);
        Assert.NotNull(afterItem2);
        Assert.NotNull(afterItem3);

        // 5. CRITICAL: Their group_id must now be NULL (ungrouped)
        Assert.Null(afterItem1.GroupId);
        Assert.Null(afterItem2.GroupId);
        Assert.Null(afterItem3.GroupId);

        // Pinned state preserved
        Assert.True(afterItem2.IsPinned);
        Assert.False(afterItem1.IsPinned);
    }

    [Fact]
    public async Task PinOrdering_ReordersPinnedItemsAndRespectsPinOrder()
    {
        // Create 3 items
        var itemA = await _db.AddOrUpdateTextItemAsync("Alpha");
        await Task.Delay(15);
        var itemB = await _db.AddOrUpdateTextItemAsync("Beta");
        await Task.Delay(15);
        var itemC = await _db.AddOrUpdateTextItemAsync("Gamma");

        // Pin all 3 items
        await _db.TogglePinAsync(itemA.Id);
        await _db.TogglePinAsync(itemB.Id);
        await _db.TogglePinAsync(itemC.Id);

        // Reorder pins to: C (order 1), A (order 2), B (order 3)
        await _db.UpdatePinOrderAsync(new[] { itemC.Id, itemA.Id, itemB.Id });

        // Retrieve items
        var items = await _db.GetAllItemsAsync();
        var pinnedOnly = items.Where(x => x.IsPinned).ToList();

        Assert.Equal(3, pinnedOnly.Count);
        Assert.Equal(itemC.Id, pinnedOnly[0].Id);
        Assert.Equal(1, pinnedOnly[0].PinOrder);

        Assert.Equal(itemA.Id, pinnedOnly[1].Id);
        Assert.Equal(2, pinnedOnly[1].PinOrder);

        Assert.Equal(itemB.Id, pinnedOnly[2].Id);
        Assert.Equal(3, pinnedOnly[2].PinOrder);

        // Test slot retrieval (1..9)
        var slot1 = await _db.GetPinnedItemBySlotAsync(1);
        Assert.NotNull(slot1);
        Assert.Equal(itemC.Id, slot1.Id);

        var slot2 = await _db.GetPinnedItemBySlotAsync(2);
        Assert.NotNull(slot2);
        Assert.Equal(itemA.Id, slot2.Id);

        var slot3 = await _db.GetPinnedItemBySlotAsync(3);
        Assert.NotNull(slot3);
        Assert.Equal(itemB.Id, slot3.Id);
    }

    [Theory]
    [InlineData("\n", "Line1\nLine2\nLine3")]
    [InlineData(" ", "Line1 Line2 Line3")]
    [InlineData(", ", "Line1, Line2, Line3")]
    [InlineData(" --- ", "Line1 --- Line2 --- Line3")]
    public void MergeTexts_JoinsSelectedItemsInOrderWithSeparator(string separator, string expected)
    {
        var texts = new[] { "Line1", "Line2", "Line3" };
        var merged = DatabaseService.MergeTexts(texts, separator);
        Assert.Equal(expected, merged);
    }

    [Fact]
    public async Task SetLabel_RenamesPinnedItemAndAllowsSearchByLabel()
    {
        var item = await _db.AddOrUpdateTextItemAsync("https://example.com/some/long/unfriendly/url/endpoint");
        await _db.TogglePinAsync(item.Id);

        // Set friendly label
        await _db.SetLabelAsync(item.Id, "Production API Docs");

        var retrieved = await _db.GetItemByIdAsync(item.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Production API Docs", retrieved.Label);

        // Search by label prefix/substring
        var searchResults = await _db.SearchAsync("Production");
        Assert.Contains(searchResults, x => x.Id == item.Id);
    }

    [Fact]
    public async Task BulkOperations_PinUnpinGroupAndDelete()
    {
        var i1 = await _db.AddOrUpdateTextItemAsync("Bulk 1");
        var i2 = await _db.AddOrUpdateTextItemAsync("Bulk 2");
        var i3 = await _db.AddOrUpdateTextItemAsync("Bulk 3");

        var ids = new[] { i1.Id, i2.Id, i3.Id };

        // Bulk Pin
        await _db.BulkSetPinAsync(ids, true);
        var pinned = await _db.GetPinnedItemsAsync();
        Assert.Equal(3, pinned.Count);

        // Bulk Group
        var group = await _db.CreateGroupAsync("BulkGroup", "#10B981");
        await _db.BulkSetGroupAsync(ids, group.Id);

        var groupedItems = await _db.SearchAsync(groupId: group.Id);
        Assert.Equal(3, groupedItems.Count);

        // Bulk Unpin
        await _db.BulkSetPinAsync(new[] { i1.Id }, false);
        var item1After = await _db.GetItemByIdAsync(i1.Id);
        Assert.False(item1After!.IsPinned);

        // Bulk Delete
        await _db.BulkDeleteAsync(new[] { i2.Id, i3.Id });
        var afterDelete = await _db.GetAllItemsAsync();
        Assert.DoesNotContain(afterDelete, x => x.Id == i2.Id || x.Id == i3.Id);
        Assert.Contains(afterDelete, x => x.Id == i1.Id);
    }
}
