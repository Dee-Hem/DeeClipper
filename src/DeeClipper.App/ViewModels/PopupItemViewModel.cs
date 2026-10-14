using System.ComponentModel;
using System.Runtime.CompilerServices;
using DeeClipper.Core.Models;

namespace DeeClipper.App.ViewModels;

public class PopupItemViewModel : INotifyPropertyChanged
{
    private readonly ItemViewModel _inner;
    private int _displayIndex;

    public ItemViewModel Inner => _inner;
    public ClipboardItem Model => _inner.Model;

    public long Id => _inner.Id;
    public ClipboardItemType Type => _inner.Type;
    public string? ContentText => _inner.ContentText;
    public string? ImagePath => _inner.ImagePath;
    public string? ThumbPath => _inner.ThumbPath;
    public string? FilesJson => _inner.FilesJson;
    public long ByteSize => _inner.ByteSize;
    public string? SourceApp => _inner.SourceApp;
    public DateTimeOffset LastUsedAt => _inner.LastUsedAt;
    public int UseCount => _inner.UseCount;
    public bool IsPinned => _inner.IsPinned;
    public bool IsSensitive => _inner.IsSensitive;
    public bool IsMasked => _inner.IsMasked;
    public string DisplayPreview => _inner.DisplayPreview;
    public string DisplayFullText => _inner.DisplayFullText;
    public string TypeIcon => _inner.TypeIcon;
    public string PrimaryTag => _inner.PrimaryTag;
    public string RelativeTime => _inner.RelativeTime;
    public string PinButtonLabel => _inner.PinButtonLabel;
    public string? Label
    {
        get => _inner.Label;
        set => _inner.Label = value;
    }
    public long? GroupId
    {
        get => _inner.GroupId;
        set => _inner.GroupId = value;
    }
    public int PinOrder => _inner.PinOrder;
    public string SlotBadge => _inner.SlotBadge;
    public bool HasSlotBadge => _inner.HasSlotBadge;

    public int DisplayIndex
    {
        get => _displayIndex;
        set
        {
            if (_displayIndex != value)
            {
                _displayIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NumberBadge));
                OnPropertyChanged(nameof(HasNumberBadge));
            }
        }
    }

    public string NumberBadge => _displayIndex is >= 1 and <= 9 ? _displayIndex.ToString() : string.Empty;
    public bool HasNumberBadge => _displayIndex is >= 1 and <= 9;

    public PopupItemViewModel(ItemViewModel inner, int displayIndex)
    {
        _inner = inner;
        _displayIndex = displayIndex;
        _inner.PropertyChanged += (_, e) =>
        {
            OnPropertyChanged(e.PropertyName);
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
