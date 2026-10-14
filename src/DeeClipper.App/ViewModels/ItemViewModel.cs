using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using DeeClipper.Core.Models;

namespace DeeClipper.App.ViewModels;

public class ItemViewModel : INotifyPropertyChanged
{
    private readonly ClipboardItem _item;
    private bool _isMasked;
    private BitmapImage? _thumbImage;
    private bool _thumbLoaded;

    public ClipboardItem Model => _item;

    public long Id => _item.Id;
    public ClipboardItemType Type => _item.Type;
    public string? ContentText => _item.ContentText;
    public string? ImagePath => _item.ImagePath;
    public string? ThumbPath => _item.ThumbPath;
    public string? FilesJson => _item.FilesJson;
    public long ByteSize => _item.ByteSize;
    public string? SourceApp => string.IsNullOrWhiteSpace(_item.SourceApp) ? "Unknown" : _item.SourceApp;
    public DateTimeOffset LastUsedAt => _item.LastUsedAt;
    public int UseCount => _item.UseCount;

    public bool IsPinned
    {
        get => _item.IsPinned;
        set
        {
            if (_item.IsPinned != value)
            {
                _item.IsPinned = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PinButtonLabel));
            }
        }
    }

    public string PinButtonLabel => IsPinned ? "📌" : "📍";

    public bool IsSensitive
    {
        get => _item.IsSensitive;
        set
        {
            if (_item.IsSensitive != value)
            {
                _item.IsSensitive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayPreview));
                OnPropertyChanged(nameof(DisplayFullText));
            }
        }
    }

    public bool IsMasked
    {
        get => _isMasked;
        set
        {
            if (_isMasked != value)
            {
                _isMasked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayPreview));
                OnPropertyChanged(nameof(DisplayFullText));
                OnPropertyChanged(nameof(EyeIcon));
            }
        }
    }

    public string EyeIcon => _isMasked ? "👁️" : "🙈";

    public string? Label
    {
        get => _item.Label;
        set
        {
            if (_item.Label != value)
            {
                _item.Label = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayPreview));
            }
        }
    }

    public long? GroupId
    {
        get => _item.GroupId;
        set
        {
            if (_item.GroupId != value)
            {
                _item.GroupId = value;
                OnPropertyChanged();
            }
        }
    }

    public int PinOrder
    {
        get => _item.PinOrder;
        set
        {
            if (_item.PinOrder != value)
            {
                _item.PinOrder = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SlotBadge));
                OnPropertyChanged(nameof(HasSlotBadge));
            }
        }
    }

    public string SlotBadge => (IsPinned && PinOrder is >= 1 and <= 9) ? $"Slot {PinOrder}" : string.Empty;
    public bool HasSlotBadge => IsPinned && PinOrder is >= 1 and <= 9;

    public string DisplayPreview
    {
        get
        {
            if (IsSensitive && IsMasked)
                return "•••••••••••••••• (Sensitive)";
            if (!string.IsNullOrWhiteSpace(_item.Label))
                return _item.Label;
            return string.IsNullOrEmpty(_item.Preview) ? "(Empty)" : _item.Preview;
        }
    }

    public string DisplayFullText
    {
        get
        {
            if (IsSensitive && IsMasked)
                return "••••••••••••••••\n[Sensitive content masked. Click eye to reveal]";
            return _item.ContentText ?? _item.Preview;
        }
    }

    public string TypeIcon => Type switch
    {
        ClipboardItemType.Link => "🔗",
        ClipboardItemType.Image => "🖼️",
        ClipboardItemType.Files => "📁",
        _ => "📄"
    };

    public string PrimaryTag => _item.Tags.Count > 0 ? _item.Tags[0] : string.Empty;

    public string RelativeTime => FormatRelativeTime(LastUsedAt);

    public BitmapImage? Thumbnail
    {
        get
        {
            if (!_thumbLoaded)
            {
                _thumbLoaded = true;
                _thumbImage = LoadThumbnail();
            }
            return _thumbImage;
        }
    }

    public ItemViewModel(ClipboardItem item)
    {
        _item = item;
        _isMasked = item.IsSensitive;
    }

    private BitmapImage? LoadThumbnail()
    {
        var path = !string.IsNullOrEmpty(ThumbPath) && File.Exists(ThumbPath)
            ? ThumbPath
            : (!string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath) ? ImagePath : null);

        if (path == null) return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.GetFullPath(path));
            bitmap.DecodePixelWidth = 256;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public static string FormatRelativeTime(DateTimeOffset time)
    {
        var span = DateTimeOffset.UtcNow - time;
        if (span.TotalSeconds < 60) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return time.ToString("MMM d");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
