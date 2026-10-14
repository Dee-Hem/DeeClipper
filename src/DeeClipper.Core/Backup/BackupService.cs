using System.IO.Compression;
using System.Text.Json;
using DeeClipper.Core.Common;
using DeeClipper.Core.Models;
using DeeClipper.Core.Storage;

namespace DeeClipper.Core.Backup;

public class BackupData
{
    public int Version { get; set; } = 1;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<BackupGroup> Groups { get; set; } = new();
    public List<BackupItem> Items { get; set; } = new();
}

public class BackupGroup
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class BackupItem
{
    public ClipboardItemType Type { get; set; }
    public string? ContentText { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string? ImageFileName { get; set; }
    public string? FilesJson { get; set; }
    public string Preview { get; set; } = string.Empty;
    public long ByteSize { get; set; }
    public string? SourceApp { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public int UseCount { get; set; }
    public bool IsPinned { get; set; }
    public int PinOrder { get; set; }
    public string? Label { get; set; }
    public string? GroupName { get; set; }
    public bool IsSensitive { get; set; }
}

public class BackupSummary
{
    public int AddedCount { get; set; }
    public int SkippedCount { get; set; }
    public int TotalCount => AddedCount + SkippedCount;
}

public class BackupService
{
    public const int CurrentFormatVersion = 1;

    private readonly DatabaseService _db;
    private readonly DatabasePaths _paths;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public BackupService(DatabaseService db, DatabasePaths paths)
    {
        _db = db;
        _paths = paths;
    }

    /// <summary>
    /// Exports all database items, groups, labels, pin ordering, and associated images to a ZIP archive.
    /// </summary>
    public async Task ExportAsync(string destinationZipPath)
    {
        var items = await _db.GetAllItemsAsync();
        var groups = await _db.GetGroupsAsync();

        var groupMap = groups.ToDictionary(g => g.Id, g => g.Name);

        var backupData = new BackupData
        {
            Version = CurrentFormatVersion,
            ExportedAt = DateTimeOffset.UtcNow,
            Groups = groups.Select(g => new BackupGroup
            {
                Id = g.Id,
                Name = g.Name,
                Color = g.Color,
                SortOrder = g.SortOrder
            }).ToList(),
            Items = new List<BackupItem>()
        };

        var imagesToInclude = new Dictionary<string, string>(); // entryName -> fullSourcePath

        foreach (var item in items)
        {
            string? imageFileName = null;
            if (item.Type == ClipboardItemType.Image && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
            {
                var fileName = Path.GetFileName(item.ImagePath);
                imageFileName = fileName;
                if (!imagesToInclude.ContainsKey(fileName))
                {
                    imagesToInclude[fileName] = item.ImagePath;
                }
            }

            string? groupName = null;
            if (item.GroupId.HasValue && groupMap.TryGetValue(item.GroupId.Value, out var gName))
            {
                groupName = gName;
            }

            backupData.Items.Add(new BackupItem
            {
                Type = item.Type,
                ContentText = item.ContentText,
                ContentHash = item.ContentHash,
                ImageFileName = imageFileName,
                FilesJson = item.FilesJson,
                Preview = item.Preview,
                ByteSize = item.ByteSize,
                SourceApp = item.SourceApp,
                CreatedAt = item.CreatedAt,
                LastUsedAt = item.LastUsedAt,
                UseCount = item.UseCount,
                IsPinned = item.IsPinned,
                PinOrder = item.PinOrder,
                Label = item.Label,
                GroupName = groupName,
                IsSensitive = item.IsSensitive
            });
        }

        // Delete destination if already exists
        if (File.Exists(destinationZipPath))
        {
            File.Delete(destinationZipPath);
        }

        using var zipStream = new FileStream(destinationZipPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false);

        // 1. Add items.json
        var jsonEntry = archive.CreateEntry("items.json", CompressionLevel.Optimal);
        using (var entryStream = jsonEntry.Open())
        {
            await JsonSerializer.SerializeAsync(entryStream, backupData, JsonOptions);
        }

        // 2. Add images
        foreach (var (fileName, fullPath) in imagesToInclude)
        {
            if (File.Exists(fullPath))
            {
                archive.CreateEntryFromFile(fullPath, $"images/{fileName}", CompressionLevel.Optimal);
            }
        }
    }

    /// <summary>
    /// Imports a ZIP archive, merges by content hash, validates file format and versions, and returns a summary.
    /// </summary>
    public async Task<BackupSummary> ImportAsync(string sourceZipPath)
    {
        if (!File.Exists(sourceZipPath))
        {
            throw new FileNotFoundException("Backup archive file not found.", sourceZipPath);
        }

        BackupData? backupData;
        using (var zipStream = new FileStream(sourceZipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            ZipArchive archive;
            try
            {
                archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Invalid backup file: the archive is corrupted or not a valid ZIP format.", ex);
            }

            using (archive)
            {
                var jsonEntry = archive.GetEntry("items.json");
                if (jsonEntry == null)
                {
                    throw new InvalidDataException("Invalid backup file: 'items.json' was not found inside the archive.");
                }

                try
                {
                    using var entryStream = jsonEntry.Open();
                    backupData = await JsonSerializer.DeserializeAsync<BackupData>(entryStream, JsonOptions);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException("Invalid backup file: failed to parse 'items.json'.", ex);
                }

                if (backupData == null || backupData.Items == null)
                {
                    throw new InvalidDataException("Invalid backup file: backup contains no items data.");
                }

                if (backupData.Version > CurrentFormatVersion)
                {
                    throw new InvalidOperationException(
                        $"Unsupported backup format: version {backupData.Version} is newer than supported format version {CurrentFormatVersion}. Please update ClipKeep."
                    );
                }

                var summary = new BackupSummary();

                // 1. Merge groups
                var existingGroups = await _db.GetGroupsAsync();
                var groupNameToId = existingGroups.ToDictionary(g => g.Name, g => g.Id, StringComparer.OrdinalIgnoreCase);

                foreach (var bg in backupData.Groups)
                {
                    if (!groupNameToId.ContainsKey(bg.Name))
                    {
                        var createdGroup = await _db.CreateGroupAsync(bg.Name, bg.Color);
                        groupNameToId[bg.Name] = createdGroup.Id;
                    }
                }

                // 2. Extract images directory if present
                _paths.EnsureDirectoriesExist();

                // 3. Merge items by content hash
                foreach (var bi in backupData.Items)
                {
                    if (string.IsNullOrEmpty(bi.ContentHash))
                    {
                        continue;
                    }

                    var existing = await _db.GetItemByHashAsync(bi.ContentHash);
                    if (existing != null)
                    {
                        summary.SkippedCount++;
                        continue;
                    }

                    // Extract image if applicable
                    string? imagePath = null;
                    string? thumbPath = null;
                    if (bi.Type == ClipboardItemType.Image && !string.IsNullOrEmpty(bi.ImageFileName))
                    {
                        var imgEntry = archive.GetEntry($"images/{bi.ImageFileName}");
                        if (imgEntry != null)
                        {
                            var targetImagePath = Path.Combine(_paths.ImagesDirectory, bi.ImageFileName);
                            if (!File.Exists(targetImagePath))
                            {
                                imgEntry.ExtractToFile(targetImagePath, overwrite: true);
                            }
                            imagePath = targetImagePath;
                            thumbPath = targetImagePath; // thumb fallback
                        }
                    }

                    long? targetGroupId = null;
                    if (!string.IsNullOrEmpty(bi.GroupName) && groupNameToId.TryGetValue(bi.GroupName, out var gid))
                    {
                        targetGroupId = gid;
                    }

                    var newItem = new ClipboardItem
                    {
                        Type = bi.Type,
                        ContentText = bi.ContentText,
                        ContentHash = bi.ContentHash,
                        ImagePath = imagePath,
                        ThumbPath = thumbPath,
                        FilesJson = bi.FilesJson,
                        Preview = bi.Preview,
                        ByteSize = bi.ByteSize,
                        SourceApp = bi.SourceApp,
                        CreatedAt = bi.CreatedAt == default ? DateTimeOffset.UtcNow : bi.CreatedAt,
                        LastUsedAt = bi.LastUsedAt == default ? DateTimeOffset.UtcNow : bi.LastUsedAt,
                        UseCount = bi.UseCount,
                        IsPinned = bi.IsPinned,
                        PinOrder = bi.PinOrder,
                        Label = bi.Label,
                        GroupId = targetGroupId,
                        IsSensitive = bi.IsSensitive
                    };

                    await _db.AddOrUpdateItemAsync(newItem);
                    summary.AddedCount++;
                }

                return summary;
            }
        }
    }
}
