using System.Data.Common;
using System.Text;
using DeeClipper.Core.Common;
using DeeClipper.Core.Detection;
using DeeClipper.Core.Models;
using DeeClipper.Core.Settings;
using Microsoft.Data.Sqlite;

namespace DeeClipper.Core.Storage;

public class DatabaseService : IAsyncDisposable
{
    private readonly DatabasePaths _paths;
    private readonly AppSettings _settings;
    private readonly string _connectionString;
    private readonly DatabaseQueue _writeQueue;
    private bool _isFtsAvailable = false;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public bool IsFtsAvailable => _isFtsAvailable;
    public DatabasePaths Paths => _paths;

    public DatabaseService(DatabasePaths? paths = null, AppSettings? settings = null)
    {
        _paths = paths ?? new DatabasePaths();
        _settings = settings ?? new AppSettings();
        _paths.EnsureDirectoriesExist();

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        _connectionString = builder.ToString();
        _writeQueue = new DatabaseQueue(_connectionString);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            await _writeQueue.EnqueueAsync(async conn =>
            {
                var userVersion = await GetUserVersionAsync(conn);
                if (userVersion < 1)
                {
                    await MigrateToV1Async(conn);
                    await SetUserVersionAsync(conn, 1);
                }
            });

            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<long> GetUserVersionAsync(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = await cmd.ExecuteScalarAsync();
        return result is long val ? val : Convert.ToInt64(result);
    }

    private async Task SetUserVersionAsync(SqliteConnection conn, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task MigrateToV1Async(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS groups (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                color TEXT NOT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                type TEXT NOT NULL,
                content_text TEXT NULL,
                content_hash TEXT NOT NULL UNIQUE,
                image_path TEXT NULL,
                thumb_path TEXT NULL,
                files_json TEXT NULL,
                preview TEXT NOT NULL,
                byte_size INTEGER NOT NULL,
                source_app TEXT NULL,
                created_at TEXT NOT NULL,
                last_used_at TEXT NOT NULL,
                use_count INTEGER NOT NULL DEFAULT 1,
                is_pinned INTEGER NOT NULL DEFAULT 0,
                pin_order INTEGER NOT NULL DEFAULT 0,
                label TEXT NULL,
                group_id INTEGER NULL REFERENCES groups(id) ON DELETE SET NULL,
                is_sensitive INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_items_content_hash ON items(content_hash);
            CREATE INDEX IF NOT EXISTS idx_items_last_used_at ON items(last_used_at DESC);
            CREATE INDEX IF NOT EXISTS idx_items_is_pinned ON items(is_pinned);
            CREATE INDEX IF NOT EXISTS idx_items_type ON items(type);
            CREATE INDEX IF NOT EXISTS idx_items_group_id ON items(group_id);
        ";
        await cmd.ExecuteNonQueryAsync();

        // Try setting up FTS5 virtual table and triggers
        try
        {
            using var ftsCmd = conn.CreateCommand();
            ftsCmd.CommandText = @"
                CREATE VIRTUAL TABLE IF NOT EXISTS items_fts USING fts5(
                    content_text,
                    label,
                    source_app,
                    content='items',
                    content_rowid='id'
                );

                CREATE TRIGGER IF NOT EXISTS items_ai AFTER INSERT ON items BEGIN
                    INSERT INTO items_fts(rowid, content_text, label, source_app)
                    VALUES (new.id, new.content_text, new.label, new.source_app);
                END;

                CREATE TRIGGER IF NOT EXISTS items_ad AFTER DELETE ON items BEGIN
                    INSERT INTO items_fts(items_fts, rowid, content_text, label, source_app)
                    VALUES ('delete', old.id, old.content_text, old.label, old.source_app);
                END;

                CREATE TRIGGER IF NOT EXISTS items_au AFTER UPDATE ON items BEGIN
                    INSERT INTO items_fts(items_fts, rowid, content_text, label, source_app)
                    VALUES ('delete', old.id, old.content_text, old.label, old.source_app);
                    INSERT INTO items_fts(rowid, content_text, label, source_app)
                    VALUES (new.id, new.content_text, new.label, new.source_app);
                END;
            ";
            await ftsCmd.ExecuteNonQueryAsync();
            _isFtsAvailable = true;
        }
        catch
        {
            // FTS5 not available; fallback to standard LIKE search
            _isFtsAvailable = false;
        }
    }

    /// <summary>
    /// Adds or updates text item with deduplication, automatic type, tag, and sensitive detection.
    /// </summary>
    public async Task<ClipboardItem> AddOrUpdateTextItemAsync(
        string text,
        string? sourceApp = null,
        string? label = null,
        long? groupId = null)
    {
        await InitializeAsync();

        var type = TypeDetector.DetectType(text);
        var isSensitive = _settings.SensitiveDetectionEnabled && SensitiveDetector.IsSensitive(text);
        var hash = HashHelper.ComputeSha256(text);
        var byteSize = Encoding.UTF8.GetByteCount(text);
        var preview = ClipboardItem.GeneratePreview(text);

        var item = new ClipboardItem
        {
            Type = type,
            ContentText = text,
            ContentHash = hash,
            Preview = preview,
            ByteSize = byteSize,
            SourceApp = sourceApp,
            Label = label,
            GroupId = groupId,
            IsSensitive = isSensitive,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow,
            UseCount = 1,
            IsPinned = false,
            Tags = TagDetector.DetectTags(text)
        };

        return await AddOrUpdateItemAsync(item);
    }

    /// <summary>
    /// Adds or updates any item (Text, Link, Image, Files).
    /// If content_hash already exists:
    ///   - Updates last_used_at to now
    ///   - Increments use_count
    ///   - NEVER changes is_pinned state
    ///   - Never creates a duplicate row
    /// </summary>
    public async Task<ClipboardItem> AddOrUpdateItemAsync(ClipboardItem item)
    {
        await InitializeAsync();

        if (string.IsNullOrWhiteSpace(item.ContentHash))
        {
            throw new ArgumentException("Item must have a valid content hash.", nameof(item));
        }

        return await _writeQueue.EnqueueAsync(async conn =>
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT * FROM items WHERE content_hash = @hash LIMIT 1;";
            checkCmd.Parameters.AddWithValue("@hash", item.ContentHash);

            ClipboardItem? existing = null;
            using (var reader = await checkCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    existing = MapClipboardItem(reader);
                }
            }

            var now = DateTimeOffset.UtcNow.ToString("o");

            if (existing != null)
            {
                // DEDUPLICATION: update last_used_at, increment use_count, move to top, KEEP PINNED STATE!
                using var updateCmd = conn.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE items SET
                        last_used_at = @now,
                        use_count = use_count + 1,
                        label = COALESCE(@label, label),
                        group_id = COALESCE(@groupId, group_id)
                    WHERE id = @id;
                ";
                updateCmd.Parameters.AddWithValue("@now", now);
                updateCmd.Parameters.AddWithValue("@label", (object?)item.Label ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@groupId", (object?)item.GroupId ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("@id", existing.Id);
                await updateCmd.ExecuteNonQueryAsync();

                existing.LastUsedAt = DateTimeOffset.Parse(now);
                existing.UseCount += 1;
                if (!string.IsNullOrEmpty(item.Label)) existing.Label = item.Label;
                if (item.GroupId.HasValue) existing.GroupId = item.GroupId;
                existing.Tags = TagDetector.DetectTags(existing.ContentText);
                return existing;
            }
            else
            {
                // INSERT NEW ITEM
                using var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO items (
                        type, content_text, content_hash, image_path, thumb_path,
                        files_json, preview, byte_size, source_app, created_at,
                        last_used_at, use_count, is_pinned, pin_order, label,
                        group_id, is_sensitive
                    ) VALUES (
                        @type, @content_text, @content_hash, @image_path, @thumb_path,
                        @files_json, @preview, @byte_size, @source_app, @created_at,
                        @last_used_at, @use_count, @is_pinned, @pin_order, @label,
                        @group_id, @is_sensitive
                    );
                    SELECT last_insert_rowid();
                ";

                insertCmd.Parameters.AddWithValue("@type", item.Type.ToString());
                insertCmd.Parameters.AddWithValue("@content_text", (object?)item.ContentText ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@content_hash", item.ContentHash);
                insertCmd.Parameters.AddWithValue("@image_path", (object?)item.ImagePath ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@thumb_path", (object?)item.ThumbPath ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@files_json", (object?)item.FilesJson ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@preview", item.Preview ?? string.Empty);
                insertCmd.Parameters.AddWithValue("@byte_size", item.ByteSize);
                insertCmd.Parameters.AddWithValue("@source_app", (object?)item.SourceApp ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@created_at", now);
                insertCmd.Parameters.AddWithValue("@last_used_at", now);
                insertCmd.Parameters.AddWithValue("@use_count", 1);
                insertCmd.Parameters.AddWithValue("@is_pinned", item.IsPinned ? 1 : 0);
                insertCmd.Parameters.AddWithValue("@pin_order", item.PinOrder);
                insertCmd.Parameters.AddWithValue("@label", (object?)item.Label ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@group_id", (object?)item.GroupId ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@is_sensitive", item.IsSensitive ? 1 : 0);

                var newId = (long)(await insertCmd.ExecuteScalarAsync())!;
                item.Id = newId;
                item.CreatedAt = DateTimeOffset.Parse(now);
                item.LastUsedAt = DateTimeOffset.Parse(now);
                item.Tags = TagDetector.DetectTags(item.ContentText);

                // Enforce retention for unpinned items
                await InternalEnforceRetentionAsync(conn, _settings.HistoryLimit, _settings.MaxAgeDays);

                return item;
            }
        });
    }

    /// <summary>
    /// Enforces retention limits on unpinned items.
    /// Pinned items are NEVER deleted by count limit or age limit.
    /// </summary>
    public async Task EnforceRetentionAsync(int? limit = null, int? maxAgeDays = null)
    {
        await InitializeAsync();
        var effLimit = limit ?? _settings.HistoryLimit;
        var effAge = maxAgeDays ?? _settings.MaxAgeDays;

        await _writeQueue.EnqueueAsync(async conn =>
        {
            await InternalEnforceRetentionAsync(conn, effLimit, effAge);
        });
    }

    private async Task InternalEnforceRetentionAsync(SqliteConnection conn, int historyLimit, int? maxAgeDays)
    {
        // 1. Retention by Count (latest N unpinned items kept)
        // If limit is -1 or <= 0, history is unlimited
        if (historyLimit > 0)
        {
            var deleteIds = new List<long>();
            var filePathsToDelete = new List<string>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, image_path, thumb_path FROM items
                    WHERE is_pinned = 0
                    ORDER BY last_used_at DESC
                    LIMIT -1 OFFSET @limit;
                ";
                cmd.Parameters.AddWithValue("@limit", historyLimit);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    deleteIds.Add(reader.GetInt64(0));
                    if (!reader.IsDBNull(1)) filePathsToDelete.Add(reader.GetString(1));
                    if (!reader.IsDBNull(2)) filePathsToDelete.Add(reader.GetString(2));
                }
            }

            if (deleteIds.Count > 0)
            {
                using var delCmd = conn.CreateCommand();
                delCmd.CommandText = $"DELETE FROM items WHERE id IN ({string.Join(",", deleteIds)}) AND is_pinned = 0;";
                await delCmd.ExecuteNonQueryAsync();

                CleanupFiles(filePathsToDelete);
            }
        }

        // 2. Retention by Max Age in Days
        if (maxAgeDays.HasValue && maxAgeDays.Value > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-maxAgeDays.Value).ToString("o");
            var ageIds = new List<long>();
            var ageFiles = new List<string>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT id, image_path, thumb_path FROM items
                    WHERE is_pinned = 0 AND last_used_at < @cutoff;
                ";
                cmd.Parameters.AddWithValue("@cutoff", cutoff);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    ageIds.Add(reader.GetInt64(0));
                    if (!reader.IsDBNull(1)) ageFiles.Add(reader.GetString(1));
                    if (!reader.IsDBNull(2)) ageFiles.Add(reader.GetString(2));
                }
            }

            if (ageIds.Count > 0)
            {
                using var delCmd = conn.CreateCommand();
                delCmd.CommandText = $"DELETE FROM items WHERE id IN ({string.Join(",", ageIds)}) AND is_pinned = 0;";
                await delCmd.ExecuteNonQueryAsync();

                CleanupFiles(ageFiles);
            }
        }
    }

    /// <summary>
    /// Clears unpinned history. Pinned items are NEVER removed.
    /// </summary>
    public async Task<int> ClearHistoryAsync()
    {
        await InitializeAsync();

        return await _writeQueue.EnqueueAsync(async conn =>
        {
            var filesToDelete = new List<string>();
            using (var selectCmd = conn.CreateCommand())
            {
                selectCmd.CommandText = "SELECT image_path, thumb_path FROM items WHERE is_pinned = 0;";
                using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0)) filesToDelete.Add(reader.GetString(0));
                    if (!reader.IsDBNull(1)) filesToDelete.Add(reader.GetString(1));
                }
            }

            using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM items WHERE is_pinned = 0;";
            var affected = await delCmd.ExecuteNonQueryAsync();

            CleanupFiles(filesToDelete);
            return affected;
        });
    }

    /// <summary>
    /// Wipes unpinned sensitive items older than specified duration.
    /// Pinned items (even if sensitive) are NEVER removed.
    /// </summary>
    public async Task<int> WipeSensitiveAsync(TimeSpan olderThan)
    {
        await InitializeAsync();

        var cutoff = DateTimeOffset.UtcNow.Subtract(olderThan).ToString("o");

        return await _writeQueue.EnqueueAsync(async conn =>
        {
            var filesToDelete = new List<string>();
            using (var selectCmd = conn.CreateCommand())
            {
                selectCmd.CommandText = @"
                    SELECT image_path, thumb_path FROM items
                    WHERE is_pinned = 0 AND is_sensitive = 1 AND last_used_at < @cutoff;
                ";
                selectCmd.Parameters.AddWithValue("@cutoff", cutoff);
                using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0)) filesToDelete.Add(reader.GetString(0));
                    if (!reader.IsDBNull(1)) filesToDelete.Add(reader.GetString(1));
                }
            }

            using var delCmd = conn.CreateCommand();
            delCmd.CommandText = @"
                DELETE FROM items
                WHERE is_pinned = 0 AND is_sensitive = 1 AND last_used_at < @cutoff;
            ";
            delCmd.Parameters.AddWithValue("@cutoff", cutoff);
            var affected = await delCmd.ExecuteNonQueryAsync();

            CleanupFiles(filesToDelete);
            return affected;
        });
    }

    /// <summary>
    /// Explicitly deletes everything including pinned items.
    /// Requires typing 'DELETE' as confirmation argument.
    /// </summary>
    public async Task<int> DeleteEverythingAsync(string confirmation)
    {
        if (confirmation != "DELETE")
        {
            throw new ArgumentException("To delete everything including pinned items, confirmation must be exactly 'DELETE'.", nameof(confirmation));
        }

        await InitializeAsync();

        return await _writeQueue.EnqueueAsync(async conn =>
        {
            var filesToDelete = new List<string>();
            using (var selectCmd = conn.CreateCommand())
            {
                selectCmd.CommandText = "SELECT image_path, thumb_path FROM items;";
                using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0)) filesToDelete.Add(reader.GetString(0));
                    if (!reader.IsDBNull(1)) filesToDelete.Add(reader.GetString(1));
                }
            }

            using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM items;";
            var affected = await delCmd.ExecuteNonQueryAsync();

            CleanupFiles(filesToDelete);
            return affected;
        });
    }

    /// <summary>
    /// Explicitly deletes an item by ID.
    /// </summary>
    public async Task<bool> DeleteItemAsync(long id)
    {
        await InitializeAsync();

        return await _writeQueue.EnqueueAsync(async conn =>
        {
            string? img = null;
            string? thumb = null;

            using (var sel = conn.CreateCommand())
            {
                sel.CommandText = "SELECT image_path, thumb_path FROM items WHERE id = @id LIMIT 1;";
                sel.Parameters.AddWithValue("@id", id);
                using var r = await sel.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    if (!r.IsDBNull(0)) img = r.GetString(0);
                    if (!r.IsDBNull(1)) thumb = r.GetString(1);
                }
                else
                {
                    return false;
                }
            }

            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM items WHERE id = @id;";
            del.Parameters.AddWithValue("@id", id);
            var count = await del.ExecuteNonQueryAsync();

            if (img != null) CleanupFiles(new[] { img });
            if (thumb != null) CleanupFiles(new[] { thumb });

            return count > 0;
        });
    }

    /// <summary>
    /// Toggles pinned state for an item.
    /// </summary>
    public async Task<bool> TogglePinAsync(long id)
    {
        await InitializeAsync();

        return await _writeQueue.EnqueueAsync(async conn =>
        {
            var isCurrentlyPinned = false;
            using (var sel = conn.CreateCommand())
            {
                sel.CommandText = "SELECT is_pinned FROM items WHERE id = @id LIMIT 1;";
                sel.Parameters.AddWithValue("@id", id);
                var res = await sel.ExecuteScalarAsync();
                if (res == null) return false;
                isCurrentlyPinned = Convert.ToInt64(res) != 0;
            }

            var nextState = !isCurrentlyPinned;
            var nextOrder = 0;
            if (nextState)
            {
                using var orderCmd = conn.CreateCommand();
                orderCmd.CommandText = "SELECT COALESCE(MAX(pin_order), 0) + 1 FROM items WHERE is_pinned = 1;";
                nextOrder = Convert.ToInt32(await orderCmd.ExecuteScalarAsync());
            }

            using (var upd = conn.CreateCommand())
            {
                upd.CommandText = "UPDATE items SET is_pinned = @pinned, pin_order = @order WHERE id = @id;";
                upd.Parameters.AddWithValue("@pinned", nextState ? 1 : 0);
                upd.Parameters.AddWithValue("@order", nextOrder);
                upd.Parameters.AddWithValue("@id", id);
                await upd.ExecuteNonQueryAsync();
            }

            return nextState;
        });
    }

    /// <summary>
    /// Sets label on an item.
    /// </summary>
    public async Task SetLabelAsync(long id, string? label)
    {
        await InitializeAsync();

        await _writeQueue.EnqueueAsync(async conn =>
        {
            using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE items SET label = @label WHERE id = @id;";
            upd.Parameters.AddWithValue("@label", (object?)label ?? DBNull.Value);
            upd.Parameters.AddWithValue("@id", id);
            await upd.ExecuteNonQueryAsync();
        });
    }

    /// <summary>
    /// Sets group ID on an item.
    /// </summary>
    public async Task SetGroupAsync(long id, long? groupId)
    {
        await InitializeAsync();

        await _writeQueue.EnqueueAsync(async conn =>
        {
            using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE items SET group_id = @groupId WHERE id = @id;";
            upd.Parameters.AddWithValue("@groupId", (object?)groupId ?? DBNull.Value);
            upd.Parameters.AddWithValue("@id", id);
            await upd.ExecuteNonQueryAsync();
        });
    }

    /// <summary>
    /// Searches items with ranked results (exact match -> prefix match -> substring/FTS match),
    /// then by pinned and recency. Allows filtering by type, pinned, and group.
    /// </summary>
    public async Task<List<ClipboardItem>> SearchAsync(
        string? query,
        ClipboardItemType? type = null,
        bool? isPinned = null,
        long? groupId = null,
        int limit = 100)
    {
        await InitializeAsync();

        var trimmedQuery = query?.Trim() ?? string.Empty;

        // Perform read on separate connection
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var sql = new StringBuilder();

        if (string.IsNullOrEmpty(trimmedQuery))
        {
            // No search query: order by is_pinned DESC, pin_order ASC, last_used_at DESC
            sql.Append(@"
                SELECT *, 0 as rank_score
                FROM items
                WHERE 1=1
            ");

            AppendFilters(sql, cmd, type, isPinned, groupId);
            sql.Append(" ORDER BY is_pinned DESC, pin_order ASC, last_used_at DESC LIMIT @limit;");
            cmd.Parameters.AddWithValue("@limit", limit);
        }
        else
        {
            // Ranked search:
            // Rank 1: Exact match on content_text or label
            // Rank 2: Prefix match on content_text or label
            // Rank 3: Substring / FTS match
            sql.Append(@"
                SELECT *,
                    CASE
                        WHEN LOWER(content_text) = LOWER(@q) OR LOWER(COALESCE(label, '')) = LOWER(@q) THEN 1
                        WHEN LOWER(content_text) LIKE LOWER(@prefix) OR LOWER(COALESCE(label, '')) LIKE LOWER(@prefix) THEN 2
                        ELSE 3
                    END as rank_score
                FROM items
                WHERE (
                    content_text LIKE @contains OR
                    COALESCE(label, '') LIKE @contains OR
                    COALESCE(source_app, '') LIKE @contains
                )
            ");

            cmd.Parameters.AddWithValue("@q", trimmedQuery);
            cmd.Parameters.AddWithValue("@prefix", trimmedQuery + "%");
            cmd.Parameters.AddWithValue("@contains", "%" + trimmedQuery + "%");

            AppendFilters(sql, cmd, type, isPinned, groupId);
            sql.Append(" ORDER BY rank_score ASC, is_pinned DESC, last_used_at DESC LIMIT @limit;");
            cmd.Parameters.AddWithValue("@limit", limit);
        }

        cmd.CommandText = sql.ToString();

        var results = new List<ClipboardItem>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(MapClipboardItem(reader));
        }

        return results;
    }

    private static void AppendFilters(
        StringBuilder sql,
        SqliteCommand cmd,
        ClipboardItemType? type,
        bool? isPinned,
        long? groupId)
    {
        if (type.HasValue)
        {
            sql.Append(" AND type = @type");
            cmd.Parameters.AddWithValue("@type", type.Value.ToString());
        }

        if (isPinned.HasValue)
        {
            sql.Append(" AND is_pinned = @isPinned");
            cmd.Parameters.AddWithValue("@isPinned", isPinned.Value ? 1 : 0);
        }

        if (groupId.HasValue)
        {
            sql.Append(" AND group_id = @groupId");
            cmd.Parameters.AddWithValue("@groupId", groupId.Value);
        }
    }

    public async Task<List<ClipboardItem>> GetAllItemsAsync(int limit = 100, int offset = 0)
    {
        await InitializeAsync();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM items
            ORDER BY is_pinned DESC, pin_order ASC, last_used_at DESC
            LIMIT @limit OFFSET @offset;
        ";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        var list = new List<ClipboardItem>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapClipboardItem(reader));
        }
        return list;
    }

    public async Task<ClipboardItem?> GetItemByIdAsync(long id)
    {
        await InitializeAsync();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapClipboardItem(reader);
        }
        return null;
    }

    public async Task<ClipboardItem?> GetItemByHashAsync(string hash)
    {
        await InitializeAsync();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE content_hash = @hash LIMIT 1;";
        cmd.Parameters.AddWithValue("@hash", hash);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapClipboardItem(reader);
        }
        return null;
    }

    public async Task<int> GetCountAsync(bool? pinnedOnly = null)
    {
        await InitializeAsync();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        if (pinnedOnly == null)
        {
            cmd.CommandText = "SELECT COUNT(*) FROM items;";
        }
        else
        {
            cmd.CommandText = "SELECT COUNT(*) FROM items WHERE is_pinned = @pinned;";
            cmd.Parameters.AddWithValue("@pinned", pinnedOnly.Value ? 1 : 0);
        }

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    // --- Groups CRUD ---

    public async Task<ClipboardGroup> CreateGroupAsync(string name, string color = "#0D9488", int sortOrder = 0)
    {
        await InitializeAsync();

        return await _writeQueue.EnqueueAsync(async conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO groups (name, color, sort_order)
                VALUES (@name, @color, @sort_order);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@color", color);
            cmd.Parameters.AddWithValue("@sort_order", sortOrder);

            var id = (long)(await cmd.ExecuteScalarAsync())!;
            return new ClipboardGroup
            {
                Id = id,
                Name = name,
                Color = color,
                SortOrder = sortOrder
            };
        });
    }

    public async Task<List<ClipboardGroup>> GetGroupsAsync()
    {
        await InitializeAsync();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, color, sort_order FROM groups ORDER BY sort_order ASC, name ASC;";

        var list = new List<ClipboardGroup>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ClipboardGroup
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Color = reader.GetString(2),
                SortOrder = reader.GetInt32(3)
            });
        }
        return list;
    }

    public async Task<bool> DeleteGroupAsync(long id)
    {
        await InitializeAsync();

        return await _writeQueue.EnqueueAsync(async conn =>
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM groups WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            var affected = await cmd.ExecuteNonQueryAsync();
            return affected > 0;
        });
    }

    private static ClipboardItem MapClipboardItem(DbDataReader reader)
    {
        var typeStr = reader.GetString(reader.GetOrdinal("type"));
        Enum.TryParse<ClipboardItemType>(typeStr, true, out var type);

        var item = new ClipboardItem
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            Type = type,
            ContentText = reader.IsDBNull(reader.GetOrdinal("content_text")) ? null : reader.GetString(reader.GetOrdinal("content_text")),
            ContentHash = reader.GetString(reader.GetOrdinal("content_hash")),
            ImagePath = reader.IsDBNull(reader.GetOrdinal("image_path")) ? null : reader.GetString(reader.GetOrdinal("image_path")),
            ThumbPath = reader.IsDBNull(reader.GetOrdinal("thumb_path")) ? null : reader.GetString(reader.GetOrdinal("thumb_path")),
            FilesJson = reader.IsDBNull(reader.GetOrdinal("files_json")) ? null : reader.GetString(reader.GetOrdinal("files_json")),
            Preview = reader.GetString(reader.GetOrdinal("preview")),
            ByteSize = reader.GetInt64(reader.GetOrdinal("byte_size")),
            SourceApp = reader.IsDBNull(reader.GetOrdinal("source_app")) ? null : reader.GetString(reader.GetOrdinal("source_app")),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            LastUsedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("last_used_at"))),
            UseCount = reader.GetInt32(reader.GetOrdinal("use_count")),
            IsPinned = reader.GetInt32(reader.GetOrdinal("is_pinned")) != 0,
            PinOrder = reader.GetInt32(reader.GetOrdinal("pin_order")),
            Label = reader.IsDBNull(reader.GetOrdinal("label")) ? null : reader.GetString(reader.GetOrdinal("label")),
            GroupId = reader.IsDBNull(reader.GetOrdinal("group_id")) ? null : reader.GetInt64(reader.GetOrdinal("group_id")),
            IsSensitive = reader.GetInt32(reader.GetOrdinal("is_sensitive")) != 0
        };

        if (!string.IsNullOrEmpty(item.ContentText))
        {
            item.Tags = TagDetector.DetectTags(item.ContentText);
        }

        return item;
    }

    private static void CleanupFiles(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // File deletion failure is swallowed
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _writeQueue.DisposeAsync();
        _initLock.Dispose();
    }
}
