using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using DeeClipper.App.Services;
using DeeClipper.Core.Models;
using DeeClipper.Core.Settings;
using DeeClipper.Core.Storage;
using DeeClipper.Core.Text;
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
    private readonly AppSettings? _settings;
    private readonly PasteEngine? _pasteEngine;

    private string _searchText = string.Empty;
    private ItemFilter _selectedFilter = ItemFilter.All;
    private long? _selectedGroupId;
    private ItemViewModel? _selectedItem;
    private string _statusText = "Ready";

    public ObservableCollection<ItemViewModel> PinnedItems { get; } = new();
    public ObservableCollection<ItemViewModel> UnpinnedItems { get; } = new();
    public ObservableCollection<ClipboardGroup> Groups { get; } = new();

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

    public long? SelectedGroupId
    {
        get => _selectedGroupId;
        set
        {
            if (_selectedGroupId != value)
            {
                _selectedGroupId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGroupFilterActive));
                _ = RefreshAsync();
            }
        }
    }

    public bool IsGroupFilterActive => _selectedGroupId.HasValue;
    public bool QuickPasteSlotsEnabled => _settings?.QuickPasteSlots ?? false;

    public bool IsFilterAll => SelectedFilter == ItemFilter.All && !SelectedGroupId.HasValue;
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

    public MainViewModel(
        DatabaseService db,
        MemorySensitiveStore memoryStore,
        ClipboardEngine engine,
        AppSettings? settings = null,
        PasteEngine? pasteEngine = null)
    {
        _db = db;
        _memoryStore = memoryStore;
        _engine = engine;
        _settings = settings;
        _pasteEngine = pasteEngine;

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
            // 1. Fetch groups
            var groups = await _db.GetGroupsAsync();
            Groups.Clear();
            foreach (var g in groups) Groups.Add(g);

            // 2. Fetch database items
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
                groupId: SelectedGroupId,
                limit: 500
            );

            // 3. Fetch memory sensitive items (unpinned)
            var memItems = _memoryStore.GetItems();
            if (typeFilter.HasValue)
            {
                memItems = memItems.Where(x => x.Type == typeFilter.Value).ToList();
            }
            if (pinFilter == true || SelectedGroupId.HasValue)
            {
                memItems.Clear(); // memory store items are unpinned and not grouped
            }
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var q = SearchText.Trim();
                memItems = memItems.Where(x =>
                    (x.ContentText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (x.SourceApp?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (x.Label?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }

            // Combine items
            var allPinned = dbItems
                .Where(x => x.IsPinned)
                .OrderBy(x => x.PinOrder)
                .Select(x => new ItemViewModel(x))
                .ToList();

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

    public async Task SetItemLabelAsync(ItemViewModel vm, string? label)
    {
        if (vm.Id > 0)
        {
            await _db.SetLabelAsync(vm.Id, string.IsNullOrWhiteSpace(label) ? null : label);
        }
        vm.Label = string.IsNullOrWhiteSpace(label) ? null : label;
        await RefreshAsync();
    }

    public async Task SetItemGroupAsync(ItemViewModel vm, long? groupId)
    {
        if (vm.Id > 0)
        {
            await _db.SetGroupAsync(vm.Id, groupId);
        }
        vm.GroupId = groupId;
        await RefreshAsync();
    }

    public async Task BulkSetPinAsync(IEnumerable<ItemViewModel> items, bool isPinned)
    {
        var dbIds = items.Where(x => x.Id > 0).Select(x => x.Id).ToList();
        if (dbIds.Count > 0)
        {
            await _db.BulkSetPinAsync(dbIds, isPinned);
        }
        await RefreshAsync();
    }

    public async Task BulkSetGroupAsync(IEnumerable<ItemViewModel> items, long? groupId)
    {
        var dbIds = items.Where(x => x.Id > 0).Select(x => x.Id).ToList();
        if (dbIds.Count > 0)
        {
            await _db.BulkSetGroupAsync(dbIds, groupId);
        }
        await RefreshAsync();
    }

    public async Task BulkDeleteAsync(IEnumerable<ItemViewModel> items)
    {
        var list = items.ToList();
        foreach (var item in list)
        {
            if (item.Id < 0)
            {
                _memoryStore.Remove(item.Id);
            }
        }
        var dbIds = list.Where(x => x.Id > 0).Select(x => x.Id).ToList();
        if (dbIds.Count > 0)
        {
            await _db.BulkDeleteAsync(dbIds);
        }
        await RefreshAsync();
    }

    public async Task ReorderPinsAsync(IReadOnlyList<long> orderedPinnedIds)
    {
        await _db.UpdatePinOrderAsync(orderedPinnedIds);
        await RefreshAsync();
    }

    public async Task CreateGroupAsync(string name, string color)
    {
        await _db.CreateGroupAsync(name, color);
        await RefreshAsync();
    }

    public async Task UpdateGroupAsync(long id, string name, string color)
    {
        await _db.UpdateGroupAsync(id, name, color);
        await RefreshAsync();
    }

    public async Task DeleteGroupAsync(long id)
    {
        if (SelectedGroupId == id)
        {
            SelectedGroupId = null;
        }
        await _db.DeleteGroupAsync(id);
        await RefreshAsync();
    }

    public async Task EditTextItemAsync(ItemViewModel vm, string newText, bool saveAsNew)
    {
        if (string.IsNullOrWhiteSpace(newText)) return;

        if (saveAsNew)
        {
            await _db.AddOrUpdateTextItemAsync(newText, sourceApp: vm.SourceApp, groupId: vm.GroupId);
        }
        else
        {
            if (vm.Id > 0)
            {
                await _db.UpdateTextContentAsync(vm.Id, newText);
            }
        }
        await RefreshAsync();
    }

    public async Task MergeSelectedAsync(IReadOnlyList<ItemViewModel> selectedItems, string separator)
    {
        var textItems = selectedItems.Where(x => !string.IsNullOrEmpty(x.ContentText)).ToList();
        if (textItems.Count == 0) return;

        var merged = DatabaseService.MergeTexts(textItems.Select(x => x.ContentText!), separator);
        if (!string.IsNullOrWhiteSpace(merged))
        {
            await _db.AddOrUpdateTextItemAsync(merged, sourceApp: "DeeClipper");
            await RefreshAsync();
        }
    }

    public async Task PasteAsAsync(ItemViewModel vm, PasteAsTransform transform)
    {
        if (_pasteEngine != null)
        {
            await _pasteEngine.ExecutePasteAsAsync(vm.Model, transform);
        }
        else if (!string.IsNullOrEmpty(vm.ContentText))
        {
            var transformed = transform switch
            {
                PasteAsTransform.PlainText => TextTransforms.ToPlainText(vm.ContentText),
                PasteAsTransform.UpperCase => TextTransforms.ToUpperCase(vm.ContentText),
                PasteAsTransform.LowerCase => TextTransforms.ToLowerCase(vm.ContentText),
                PasteAsTransform.TitleCase => TextTransforms.ToTitleCase(vm.ContentText),
                PasteAsTransform.SingleLine => TextTransforms.ToSingleLine(vm.ContentText),
                PasteAsTransform.Trimmed => TextTransforms.Trim(vm.ContentText),
                PasteAsTransform.CleanLink => TextTransforms.StripTrackingParameters(vm.ContentText),
                _ => vm.ContentText
            };
            var transient = new ClipboardItem
            {
                Type = ClipboardItemType.Text,
                ContentText = transformed,
                Preview = ClipboardItem.GeneratePreview(transformed)
            };
            _engine.CopyToClipboard(transient);
        }
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
