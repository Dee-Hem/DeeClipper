using DeeClipper.Core.Models;

namespace DeeClipper.App.Services;

public class MemorySensitiveStore
{
    private readonly List<ClipboardItem> _memoryItems = new();
    private readonly object _lock = new();
    private long _nextTempId = -1;

    public event Action? ItemsChanged;

    public List<ClipboardItem> GetItems()
    {
        lock (_lock)
        {
            return _memoryItems.ToList();
        }
    }

    public ClipboardItem AddOrUpdate(ClipboardItem item)
    {
        lock (_lock)
        {
            var existing = _memoryItems.FirstOrDefault(x => x.ContentHash == item.ContentHash);
            if (existing != null)
            {
                existing.LastUsedAt = DateTimeOffset.UtcNow;
                existing.UseCount++;
                ItemsChanged?.Invoke();
                return existing;
            }

            item.Id = _nextTempId--;
            item.IsSensitive = true;
            item.IsPinned = false;
            _memoryItems.Insert(0, item);
            ItemsChanged?.Invoke();
            return item;
        }
    }

    public bool Remove(long id)
    {
        lock (_lock)
        {
            var count = _memoryItems.RemoveAll(x => x.Id == id);
            if (count > 0)
            {
                ItemsChanged?.Invoke();
                return true;
            }
            return false;
        }
    }

    public ClipboardItem? TakeForPinning(long id)
    {
        lock (_lock)
        {
            var item = _memoryItems.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                _memoryItems.Remove(item);
                item.Id = 0; // Reset ID for database generation
                item.IsPinned = true;
                ItemsChanged?.Invoke();
                return item;
            }
            return null;
        }
    }

    public ClipboardItem? TakeForNotSensitive(long id)
    {
        lock (_lock)
        {
            var item = _memoryItems.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                _memoryItems.Remove(item);
                item.Id = 0;
                item.IsSensitive = false;
                ItemsChanged?.Invoke();
                return item;
            }
            return null;
        }
    }

    public int WipeOlderThan(TimeSpan age)
    {
        lock (_lock)
        {
            var cutoff = DateTimeOffset.UtcNow.Subtract(age);
            var removed = _memoryItems.RemoveAll(x => !x.IsPinned && x.LastUsedAt < cutoff);
            if (removed > 0)
            {
                ItemsChanged?.Invoke();
            }
            return removed;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _memoryItems.Clear();
            ItemsChanged?.Invoke();
        }
    }
}
