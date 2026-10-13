using DeeClipper.Core.Common;
using DeeClipper.Core.Models;
using DeeClipper.Core.Storage;
using Xunit;

namespace DeeClipper.Tests;

public class SearchTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly DatabaseService _db;

    public SearchTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeeClipper_SearchTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task Search_RankedResults_ExactAndPrefixMatchFirstThenRecency()
    {
        // Arrange items matching "apple":
        // 1. Substring match: "Buy some pineapple fruit"
        // 2. Prefix match: "apple pie recipe"
        // 3. Exact match: "apple"
        await _db.AddOrUpdateTextItemAsync("Buy some pineapple fruit");
        await Task.Delay(20);
        await _db.AddOrUpdateTextItemAsync("apple pie recipe");
        await Task.Delay(20);
        await _db.AddOrUpdateTextItemAsync("apple");

        // Act: Search for "apple"
        var results = await _db.SearchAsync("apple");

        // Assert:
        // Rank 1: exact match ("apple")
        // Rank 2: prefix match ("apple pie recipe")
        // Rank 3: substring match ("Buy some pineapple fruit")
        Assert.Equal(3, results.Count);
        Assert.Equal("apple", results[0].ContentText);
        Assert.Equal("apple pie recipe", results[1].ContentText);
        Assert.Equal("Buy some pineapple fruit", results[2].ContentText);
    }

    [Fact]
    public async Task Search_FilterByType_ReturnsOnlySpecifiedType()
    {
        await _db.AddOrUpdateTextItemAsync("https://deeclipper.example.com"); // Link
        await _db.AddOrUpdateTextItemAsync("Just some text note mentioning deeclipper"); // Text

        var linkResults = await _db.SearchAsync("deeclipper", type: ClipboardItemType.Link);
        Assert.Single(linkResults);
        Assert.Equal(ClipboardItemType.Link, linkResults[0].Type);

        var textResults = await _db.SearchAsync("deeclipper", type: ClipboardItemType.Text);
        Assert.Single(textResults);
        Assert.Equal(ClipboardItemType.Text, textResults[0].Type);
    }

    [Fact]
    public async Task Search_FilterByPinned_ReturnsOnlyPinnedOrUnpinned()
    {
        var item1 = await _db.AddOrUpdateTextItemAsync("Important Config Item");
        await _db.TogglePinAsync(item1.Id);

        var item2 = await _db.AddOrUpdateTextItemAsync("Disposable Config Item");

        var pinnedResults = await _db.SearchAsync("Config", isPinned: true);
        Assert.Single(pinnedResults);
        Assert.True(pinnedResults[0].IsPinned);
        Assert.Equal(item1.Id, pinnedResults[0].Id);

        var unpinnedResults = await _db.SearchAsync("Config", isPinned: false);
        Assert.Single(unpinnedResults);
        Assert.False(unpinnedResults[0].IsPinned);
        Assert.Equal(item2.Id, unpinnedResults[0].Id);
    }

    [Fact]
    public async Task Search_FilterByGroup_ReturnsOnlyGroupItems()
    {
        var groupWork = await _db.CreateGroupAsync("Work", "#0D9488");
        var groupPersonal = await _db.CreateGroupAsync("Personal", "#3B82F6");

        var itemWork = await _db.AddOrUpdateTextItemAsync("Sprint Planning Notes", groupId: groupWork.Id);
        var itemPersonal = await _db.AddOrUpdateTextItemAsync("Weekend Grocery List", groupId: groupPersonal.Id);

        var workResults = await _db.SearchAsync(null, groupId: groupWork.Id);
        Assert.Single(workResults);
        Assert.Equal("Sprint Planning Notes", workResults[0].ContentText);

        var personalResults = await _db.SearchAsync(null, groupId: groupPersonal.Id);
        Assert.Single(personalResults);
        Assert.Equal("Weekend Grocery List", personalResults[0].ContentText);
    }
}
