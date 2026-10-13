using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using DeeClipper.App.Services;
using DeeClipper.Core.Models;
using DeeClipper.Core.Storage;
using WpfApplication = System.Windows.Application;

namespace DeeClipper.App.ViewModels;

public enum ItemFilter
{
    All,
    Pinned,
    Text,
    Links,
    Images,
    Files
}

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _db;
    private readonly MemorySensitiveStore _memoryStore;
    private readonly ClipboardEngine _engine;

    private string _searchText = string.Empty;
    private ItemFilter _selectedFilter = ItemFilter.All;
    private ItemViewModel? _selectedItem;
    private string _statusText = "Ready";

    public ObservableCollection<ItemViewModel> PinnedItems { get; } = new();
    public ObservableCollection<ItemViewModel> UnpinnedItems { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                _ = RefreshAsync();
            }
        }
    }

    public ItemFilter SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (_selectedFilter != value)
            {
                _selectedFilter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFilterAll));
                OnPropertyChanged(nameof(IsFilterPinned));
                OnPropertyChanged(nameof(IsFilterText));
                OnPropertyChanged(nameof(IsFilterLinks));
                OnPropertyChanged(nameof(IsFilterImages));
                OnPropertyChanged(nameof(IsFilterFiles));
                _ = RefreshAsync();
            }
        }
    }

    public bool IsFilterAll => SelectedFilter == ItemFilter.All;
    public bool IsFilterPinned => SelectedFilter == ItemFilter.Pinned;
    public bool IsFilterText => SelectedFilter == ItemFilter.Text;
    public bool IsFilterLinks => SelectedFilter == ItemFilter.Links;
    public bool IsFilterImages => SelectedFilter == ItemFilter.Images;
    public bool IsFilterFiles => SelectedFilter == ItemFilter.Files;

    public ItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem != value)
            {
                _selectedItem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedItem));
            }
        }
    }

    public bool HasSelectedItem => SelectedItem != null;

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public MainViewModel(DatabaseService db, MemorySensitiveStore memoryStore, ClipboardEngine engine)
    {
        _db = db;
        _memoryStore = memoryStore;
        _engine = engine;

        _engine.ItemsUpdated += () =>
        {
            WpfApplication.Current?.Dispatcher?.InvokeAsync(async () => await RefreshAsync());
        };

        _memoryStore.ItemsChanged += () =>
        {
            WpfApplication.Current?.Dispatcher?.InvokeAsync(async () => await RefreshAsync());
        };

        _engine.PauseStateChanged += isPaused =>
        {
            UpdateStatus(isPaused);
        };
    }

    public async Task RefreshAsync()
    {
        try
        {
            // 1. Fetch database items
            ClipboardItemType? typeFilter = SelectedFilter switch
            {
                ItemFilter.Text => ClipboardItemType.Text,
                ItemFilter.Links => ClipboardItemType.Link,
                ItemFilter.Images => ClipboardItemType.Image,
                ItemFilter.Files => ClipboardItemType.Files,
                _ => null
            };

            bool? pinFilter = SelectedFilter == ItemFilter.Pinned ? true : null;

            var dbItems = await _db.SearchAsync(
                query: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                type: typeFilter,
                isPinned: pinFilter,
                limit: 500
            );

            // 2. Fetch memory sensitive items (unpinned)
            var memItems = _memoryStore.GetItems();
            if (typeFilter.HasValue)
            {
                memItems = memItems.Where(x => x.Type == typeFilter.Value).ToList();
            }
            if (pinFilter == true)
            {
                memItems.Clear(); // memory store only contains unpinned items
            }
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var q = SearchText.Trim();
                memItems = memItems.Where(x =>
                    (x.ContentText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (x.SourceApp?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }

            // Combine items
            var allPinned = dbItems.Where(x => x.IsPinned).Select(x => new ItemViewModel(x)).ToList();

            var allUnpinned = memItems.Select(x => new ItemViewModel(x))
                .Concat(dbItems.Where(x => !x.IsPinned).Select(x => new ItemViewModel(x)))
                .OrderByDescending(x => x.LastUsedAt)
                .ToList();

            PinnedItems.Clear();
            foreach (var p in allPinned) PinnedItems.Add(p);

            UnpinnedItems.Clear();
            foreach (var u in allUnpinned) UnpinnedItems.Add(u);

            if (SelectedItem == null || (!PinnedItems.Contains(SelectedItem) && !UnpinnedItems.Contains(SelectedItem)))
            {
                SelectedItem = PinnedItems.FirstOrDefault() ?? UnpinnedItems.FirstOrDefault();
            }

            UpdateStatus(_engine.IsMonitoringPaused);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error refreshing MainViewModel", ex);
        }
    }

    public async Task TogglePinAsync(ItemViewModel vm)
    {
        if (vm.Id < 0)
        {
            // Memory item: pinning writes it to the database!
            var memItem = _memoryStore.TakeForPinning(vm.Id);
            if (memItem != null)
            {
                await _db.AddOrUpdateItemAsync(memItem);
            }
        }
        else
        {
            await _db.TogglePinAsync(vm.Id);
        }
        await RefreshAsync();
    }

    public async Task DeleteItemAsync(ItemViewModel vm)
    {
        if (vm.Id < 0)
        {
            _memoryStore.Remove(vm.Id);
        }
        else
        {
            await _db.DeleteItemAsync(vm.Id);
        }
        await RefreshAsync();
    }

    public async Task MarkNotSensitiveAsync(ItemViewModel vm)
    {
        if (vm.Id < 0)
        {
            var item = _memoryStore.TakeForNotSensitive(vm.Id);
            if (item != null)
            {
                await _db.AddOrUpdateItemAsync(item);
            }
        }
        else
        {
            vm.IsSensitive = false;
            vm.IsMasked = false;
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = _db.Paths.DatabasePath }.ToString());
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE items SET is_sensitive = 0 WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", vm.Id);
            await cmd.ExecuteNonQueryAsync();
        }
        await RefreshAsync();
    }

    public async Task ClearHistoryAsync()
    {
        _memoryStore.Clear();
        await _db.ClearHistoryAsync();
        await RefreshAsync();
    }

    public void CopyItem(ItemViewModel vm)
    {
        _engine.CopyToClipboard(vm.Model);
    }

    private void UpdateStatus(bool isPaused)
    {
        var count = PinnedItems.Count + UnpinnedItems.Count;
        var modeStr = isPaused ? "Monitoring Paused" : "Monitoring Active";
        StatusText = $"{count} items ({PinnedItems.Count} pinned) · {modeStr}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
