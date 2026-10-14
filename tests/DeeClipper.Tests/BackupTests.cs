using System.IO.Compression;
using System.Text.Json;
using DeeClipper.Core.Backup;
using DeeClipper.Core.Common;
using DeeClipper.Core.Models;
using DeeClipper.Core.Storage;
using Xunit;

namespace DeeClipper.Tests;

public class BackupTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly DatabasePaths _paths;
    private readonly DatabaseService _db;
    private readonly BackupService _backupService;

    public BackupTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeeClipper_BackupTests_" + Guid.NewGuid().ToString("N"));
        _paths = new DatabasePaths(_tempDir);
        _paths.EnsureDirectoriesExist();
        _db = new DatabaseService(_paths);
        _backupService = new BackupService(_db, _paths);
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
        catch { }
    }

    [Fact]
    public async Task ExportAndImport_RoundTrip_RestoresItemsGroupsPinsAndLabels()
    {
        // 1. Setup Source Database with Group, Pinned Item, Label, Pin Order
        var workGroup = await _db.CreateGroupAsync("Work Snippets", "#3B82F6");
        var item1 = await _db.AddOrUpdateTextItemAsync("export-test-1", sourceApp: "VSCode", groupId: workGroup.Id);
        await _db.TogglePinAsync(item1.Id);
        await _db.SetLabelAsync(item1.Id, "Production API Key Template");

        var item2 = await _db.AddOrUpdateTextItemAsync("export-test-2", sourceApp: "Slack");

        // 2. Export to ZIP
        var zipPath = Path.Combine(_tempDir, "backup_test.zip");
        await _backupService.ExportAsync(zipPath);

        Assert.True(File.Exists(zipPath));

        // Verify ZIP contents
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            Assert.NotNull(archive.GetEntry("items.json"));
        }

        // 3. Setup Target Clean Database
        var targetTempDir = Path.Combine(Path.GetTempPath(), "DeeClipper_Target_" + Guid.NewGuid().ToString("N"));
        var targetPaths = new DatabasePaths(targetTempDir);
        targetPaths.EnsureDirectoriesExist();
        var targetDb = new DatabaseService(targetPaths);
        await targetDb.InitializeAsync();

        var targetBackupService = new BackupService(targetDb, targetPaths);

        try
        {
            // 4. Import into clean database
            var summary = await targetBackupService.ImportAsync(zipPath);

            Assert.Equal(2, summary.AddedCount);
            Assert.Equal(0, summary.SkippedCount);

            var importedItems = await targetDb.GetAllItemsAsync();
            Assert.Equal(2, importedItems.Count);

            var importedPinned = importedItems.FirstOrDefault(x => x.ContentText == "export-test-1");
            Assert.NotNull(importedPinned);
            Assert.True(importedPinned.IsPinned);
            Assert.Equal("Production API Key Template", importedPinned.Label);
            Assert.NotNull(importedPinned.GroupId);

            var targetGroups = await targetDb.GetGroupsAsync();
            Assert.Single(targetGroups);
            Assert.Equal("Work Snippets", targetGroups[0].Name);
            Assert.Equal(targetGroups[0].Id, importedPinned.GroupId);

            // 5. Re-importing into the same database merges by content hash (all skipped!)
            var secondSummary = await targetBackupService.ImportAsync(zipPath);
            Assert.Equal(0, secondSummary.AddedCount);
            Assert.Equal(2, secondSummary.SkippedCount);
        }
        finally
        {
            await targetDb.DisposeAsync();
            if (Directory.Exists(targetTempDir))
            {
                try { Directory.Delete(targetTempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Import_NewerVersion_ThrowsClearError()
    {
        var zipPath = Path.Combine(_tempDir, "future_version.zip");
        using (var fs = new FileStream(zipPath, FileMode.Create))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("items.json");
            using var es = entry.Open();
            var futureBackup = new BackupData { Version = 99 };
            await JsonSerializer.SerializeAsync(es, futureBackup);
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _backupService.ImportAsync(zipPath));
        Assert.Contains("newer than supported format version", ex.Message);
    }

    [Fact]
    public async Task Import_MissingItemsJson_ThrowsInvalidDataException()
    {
        var zipPath = Path.Combine(_tempDir, "corrupt.zip");
        using (var fs = new FileStream(zipPath, FileMode.Create))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("other_file.txt");
            using var es = entry.Open();
            using var writer = new StreamWriter(es);
            writer.Write("hello");
        }

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => _backupService.ImportAsync(zipPath));
        Assert.Contains("items.json", ex.Message);
    }
}
