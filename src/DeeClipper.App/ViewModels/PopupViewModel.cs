using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using DeeClipper.App.Services;
using DeeClipper.Core.Models;
using DeeClipper.Core.Settings;
using DeeClipper.Core.Storage;
using WpfApplication = System.Windows.Application;

namespace DeeClipper.App.ViewModels;

public class PopupViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _db;
    private readonly MemorySensitiveStore _memoryStore;
    private readonly ClipboardEngine _engine;
    private readonly PasteEngine _pasteEngine;
    private readonly AppSettings _settings;

    private string _searchText = string.Empty;
    private ItemFilter _selectedFilter = ItemFilter.All;
    private long? _selectedGroupId;
    private PopupItemViewModel? _selectedItem;
    private bool _showPreviewPane = true;

    public ObservableCollection<PopupItemViewModel> FilteredItems { get; } = new();
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
                _ = RefreshItemsAsync();
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
                _ = RefreshItemsAsync();
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
                _ = RefreshItemsAsync();
            }
        }
    }

    public PopupItemViewModel? SelectedItem
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

    public bool ShowPreviewPane
    {
        get => _showPreviewPane;
        set
        {
            if (_showPreviewPane != value)
            {
                _showPreviewPane = value;
                _settings.ShowPreviewPane = value;
                OnPropertyChanged();
            }
        }
    }

    public PopupViewModel(
        DatabaseService db,
        MemorySensitiveStore memoryStore,
        ClipboardEngine engine,
        PasteEngine pasteEngine,
        AppSettings settings)
    {
        _db = db;
        _memoryStore = memoryStore;
        _engine = engine;
        _pasteEngine = pasteEngine;
        _settings = settings;
        _showPreviewPane = settings.ShowPreviewPane;

        _engine.ItemsUpdated += () =>
        {
            WpfApplication.Current?.Dispatcher?.InvokeAsync(async () => await RefreshItemsAsync());
        };

        _memoryStore.ItemsChanged += () =>
        {
            WpfApplication.Current?.Dispatcher?.InvokeAsync(async () => await RefreshItemsAsync());
        };
    }

    public async Task RefreshItemsAsync()
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
                limit: 100
            );

            var memItems = _memoryStore.GetItems();
            if (typeFilter.HasValue)
            {
                memItems = memItems.Where(x => x.Type == typeFilter.Value).ToList();
            }
            if (pinFilter == true || SelectedGroupId.HasValue)
            {
                memItems.Clear();
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

            // Pinned first, then newest unpinned
            var pinnedList = dbItems
                .Where(x => x.IsPinned)
                .OrderBy(x => x.PinOrder)
                .Select(x => new ItemViewModel(x));

            var unpinnedList = memItems.Select(x => new ItemViewModel(x))
                .Concat(dbItems.Where(x => !x.IsPinned).Select(x => new ItemViewModel(x)))
                .OrderByDescending(x => x.LastUsedAt);

            var combined = pinnedList.Concat(unpinnedList).ToList();

            FilteredItems.Clear();
            for (var i = 0; i < combined.Count; i++)
            {
                FilteredItems.Add(new PopupItemViewModel(combined[i], i + 1));
            }

            if (SelectedItem == null || !FilteredItems.Contains(SelectedItem))
            {
                SelectedItem = FilteredItems.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error in PopupViewModel.RefreshItemsAsync", ex);
        }
    }

    public void CycleFilterChip(bool forward)
    {
        var values = (ItemFilter[])Enum.GetValues(typeof(ItemFilter));
        var currentIndex = Array.IndexOf(values, SelectedFilter);
        var nextIndex = forward
            ? (currentIndex + 1) % values.Length
            : (currentIndex - 1 + values.Length) % values.Length;

        SelectedFilter = values[nextIndex];
    }

    public async Task PasteNthItemAsync(int index, PasteMode mode)
    {
        if (index >= 0 && index < FilteredItems.Count)
        {
            var item = FilteredItems[index];
            await _pasteEngine.ExecutePasteAsync(item.Model, mode);
        }
    }

    public async Task PasteSelectedItemAsync(PasteMode mode)
    {
        if (SelectedItem != null)
        {
            await _pasteEngine.ExecutePasteAsync(SelectedItem.Model, mode);
        }
    }

    public async Task PasteAsAsync(PopupItemViewModel item, PasteAsTransform transform)
    {
        await _pasteEngine.ExecutePasteAsAsync(item.Model, transform);
    }

    public async Task TogglePinAsync(PopupItemViewModel item)
    {
        if (item.Id < 0)
        {
            var memItem = _memoryStore.TakeForPinning(item.Id);
            if (memItem != null)
            {
                await _db.AddOrUpdateItemAsync(memItem);
            }
        }
        else
        {
            await _db.TogglePinAsync(item.Id);
        }
        await RefreshItemsAsync();
    }

    public async Task DeleteItemAsync(PopupItemViewModel item)
    {
        if (item.Id < 0)
        {
            _memoryStore.Remove(item.Id);
        }
        else
        {
            await _db.DeleteItemAsync(item.Id);
        }
        await RefreshItemsAsync();
    }

    public async Task SetItemLabelAsync(PopupItemViewModel item, string? label)
    {
        if (item.Id > 0)
        {
            await _db.SetLabelAsync(item.Id, string.IsNullOrWhiteSpace(label) ? null : label);
        }
        item.Label = string.IsNullOrWhiteSpace(label) ? null : label;
        await RefreshItemsAsync();
    }

    public async Task SetItemGroupAsync(PopupItemViewModel item, long? groupId)
    {
        if (item.Id > 0)
        {
            await _db.SetGroupAsync(item.Id, groupId);
        }
        item.GroupId = groupId;
        await RefreshItemsAsync();
    }

    public async Task EditTextItemAsync(PopupItemViewModel item, string newText, bool saveAsNew)
    {
        if (string.IsNullOrWhiteSpace(newText)) return;

        if (saveAsNew)
        {
            await _db.AddOrUpdateTextItemAsync(newText, sourceApp: item.SourceApp, groupId: item.GroupId);
        }
        else
        {
            if (item.Id > 0)
            {
                await _db.UpdateTextContentAsync(item.Id, newText);
            }
        }
        await RefreshItemsAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
