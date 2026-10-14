using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Interop;
using DeeClipper.Core.Common;
using DeeClipper.Core.Detection;
using DeeClipper.Core.Models;
using DeeClipper.Core.Settings;
using DeeClipper.Core.Storage;
using FormsClipboard = System.Windows.Forms.Clipboard;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataObject = System.Windows.IDataObject;
using WpfDataFormats = System.Windows.DataFormats;

namespace DeeClipper.App.Services;

public class ClipboardEngine : IDisposable
{
    private readonly DatabaseService _db;
    private readonly AppSettings _settings;
    private readonly MemorySensitiveStore _memoryStore;
    private readonly System.Threading.Timer _debounceTimer;
    private readonly System.Threading.Timer _sensitiveWipeTimer;

    private HwndSource? _hwndSource;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _isDisposed;
    private bool _suppressNext;
    private uint _lastSequenceNumber;

    public bool IsPaused { get; private set; }
    public DateTimeOffset? PrivateModeExpiry { get; private set; }

    public event Action? ItemsUpdated;
    public event Action<bool>? PauseStateChanged;

    public ClipboardEngine(
        DatabaseService db,
        AppSettings settings,
        MemorySensitiveStore memoryStore)
    {
        _db = db;
        _settings = settings;
        _memoryStore = memoryStore;

        _debounceTimer = new System.Threading.Timer(
            _ => QueueClipboardProcessing(),
            null,
            Timeout.Infinite,
            Timeout.Infinite
        );

        // Periodic sensitive auto-wipe timer running every 30 seconds
        _sensitiveWipeTimer = new System.Threading.Timer(
            _ => CheckSensitiveWipe(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30)
        );

        InitializeListener();
    }

    private void InitializeListener()
    {
        try
        {
            var parameters = new HwndSourceParameters("DeeClipperListener")
            {
                ParentWindow = NativeMethods.HWND_MESSAGE
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);
            _hwnd = _hwndSource.Handle;

            var success = NativeMethods.AddClipboardFormatListener(_hwnd);
            if (!success)
            {
                Logger.LogWarning("AddClipboardFormatListener returned false.");
            }
            else
            {
                Logger.LogInfo("Clipboard listener registered successfully.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to initialize clipboard listener window", ex);
        }
    }

    public void ReRegisterListener()
    {
        if (_hwnd != IntPtr.Zero)
        {
            try
            {
                NativeMethods.RemoveClipboardFormatListener(_hwnd);
                var success = NativeMethods.AddClipboardFormatListener(_hwnd);
                Logger.LogInfo($"Re-registered clipboard format listener: {success}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to re-register clipboard listener", ex);
            }
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            // Debounce bursts of clipboard update messages (150 ms)
            _debounceTimer.Change(150, Timeout.Infinite);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void QueueClipboardProcessing()
    {
        if (_hwndSource == null || _isDisposed)
            return;

        _ = _hwndSource.Dispatcher.InvokeAsync(ProcessClipboardChangeAsync);
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        if (!paused)
        {
            PrivateModeExpiry = null;
        }
        PauseStateChanged?.Invoke(IsMonitoringPaused);
    }

    public void SetPrivateMode(int minutes)
    {
        PrivateModeExpiry = DateTimeOffset.UtcNow.AddMinutes(minutes);
        PauseStateChanged?.Invoke(IsMonitoringPaused);
    }

    public bool IsMonitoringPaused
    {
        get
        {
            if (IsPaused) return true;
            if (PrivateModeExpiry.HasValue)
            {
                if (DateTimeOffset.UtcNow < PrivateModeExpiry.Value)
                    return true;
                PrivateModeExpiry = null;
            }
            return false;
        }
    }

    private async Task ProcessClipboardChangeAsync()
    {
        try
        {
            var currentSeq = NativeMethods.GetClipboardSequenceNumber();
            if (_suppressNext || (currentSeq != 0 && currentSeq == _lastSequenceNumber))
            {
                _suppressNext = false;
                _lastSequenceNumber = currentSeq;
                return;
            }

            if (IsMonitoringPaused)
            {
                return;
            }

            var sourceApp = SourceAppResolver.GetActiveSourceApp();

            // PRIVACY: Skip blocklisted applications
            if (_settings.IsAppBlocked(sourceApp))
            {
                Logger.LogInfo($"Ignored copy from blocklisted application: {sourceApp}");
                return;
            }

            // Retry reading clipboard up to 10 times with 50 ms delay
            WpfDataObject? data = null;
            for (var attempt = 1; attempt <= 10; attempt++)
            {
                try
                {
                    data = WpfClipboard.GetDataObject();
                    if (data != null) break;
                }
                catch
                {
                    if (attempt == 10)
                    {
                        Logger.LogWarning("Failed to access clipboard after 10 retries.");
                        return;
                    }
                    await Task.Delay(50);
                }
            }

            if (data == null)
                return;

            // PRIVACY: Check privacy flags set by password managers
            try
            {
                if (data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing"))
                {
                    Logger.LogInfo("Ignored clipboard event: ExcludeClipboardContentFromMonitorProcessing flag present.");
                    return;
                }

                if (data.GetDataPresent("CanIncludeInClipboardHistory"))
                {
                    var canInclude = data.GetData("CanIncludeInClipboardHistory");
                    if (canInclude is int intVal && intVal == 0) return;
                    if (canInclude is string strVal && (strVal == "0" || strVal.Equals("false", StringComparison.OrdinalIgnoreCase))) return;
                    if (canInclude is bool boolVal && !boolVal) return;
                }
            }
            catch
            {
                // Format check failed safely
            }

            // Capture 1: Files
            if (data.GetDataPresent(WpfDataFormats.FileDrop))
            {
                if (data.GetData(WpfDataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    await HandleFilesCaptureAsync(files, sourceApp);
                    ItemsUpdated?.Invoke();
                    return;
                }
            }

            // Capture 2: Image
            if (data.GetDataPresent(WpfDataFormats.Bitmap))
            {
                if (FormsClipboard.ContainsImage())
                {
                    using var img = FormsClipboard.GetImage();
                    if (img != null)
                    {
                        await HandleImageCaptureAsync(img, sourceApp);
                        ItemsUpdated?.Invoke();
                        return;
                    }
                }
            }

            // Capture 3: Plain text
            if (data.GetDataPresent(WpfDataFormats.UnicodeText) || data.GetDataPresent(WpfDataFormats.Text))
            {
                var text = (string?)data.GetData(WpfDataFormats.UnicodeText) ?? (string?)data.GetData(WpfDataFormats.Text);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    await HandleTextCaptureAsync(text, sourceApp);
                    ItemsUpdated?.Invoke();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error in ProcessClipboardChangeAsync", ex);
        }
    }

    private async Task HandleTextCaptureAsync(string text, string? sourceApp)
    {
        var byteSize = Encoding.UTF8.GetByteCount(text);
        if (byteSize > _settings.MaxTextSizeBytes)
        {
            Logger.LogWarning($"Text ignored: byte size {byteSize} exceeds max limit {_settings.MaxTextSizeBytes}");
            return;
        }

        var isSensitive = _settings.SensitiveDetectionEnabled && SensitiveDetector.IsSensitive(text);

        if (isSensitive)
        {
            // PRIVACY: Keep in MEMORY ONLY (never write to database unless user pins it)
            var memItem = new ClipboardItem
            {
                Type = TypeDetector.DetectType(text),
                ContentText = text,
                ContentHash = HashHelper.ComputeSha256(text),
                Preview = ClipboardItem.GeneratePreview(text),
                ByteSize = byteSize,
                SourceApp = sourceApp,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUsedAt = DateTimeOffset.UtcNow,
                UseCount = 1,
                IsPinned = false,
                IsSensitive = true,
                Tags = TagDetector.DetectTags(text)
            };

            _memoryStore.AddOrUpdate(memItem);
            Logger.LogInfo("Captured sensitive text in memory store (masked, not saved to disk).");
        }
        else
        {
            await _db.AddOrUpdateTextItemAsync(text, sourceApp);
        }
    }

    private async Task HandleImageCaptureAsync(System.Drawing.Image image, string? sourceApp)
    {
        var result = ImageStorageHelper.SaveClipboardImage(image, _db.Paths, _settings.MaxImageSizeBytes);
        if (result == null) return;

        var (imagePath, thumbPath, byteSize, hash) = result.Value;

        var item = new ClipboardItem
        {
            Type = ClipboardItemType.Image,
            ContentHash = hash,
            ImagePath = imagePath,
            ThumbPath = thumbPath,
            Preview = $"Image ({image.Width}x{image.Height})",
            ByteSize = byteSize,
            SourceApp = sourceApp,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow,
            UseCount = 1,
            IsPinned = false,
            IsSensitive = false
        };

        await _db.AddOrUpdateItemAsync(item);
    }

    private async Task HandleFilesCaptureAsync(string[] filePaths, string? sourceApp)
    {
        var filesJson = JsonSerializer.Serialize(filePaths);
        var hash = HashHelper.ComputeSha256(string.Join("|", filePaths));
        var preview = filePaths.Length == 1
            ? Path.GetFileName(filePaths[0])
            : $"{Path.GetFileName(filePaths[0])} and {filePaths.Length - 1} other files";

        var item = new ClipboardItem
        {
            Type = ClipboardItemType.Files,
            ContentText = string.Join(Environment.NewLine, filePaths),
            ContentHash = hash,
            FilesJson = filesJson,
            Preview = preview,
            ByteSize = filePaths.Sum(f => (long)f.Length),
            SourceApp = sourceApp,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow,
            UseCount = 1,
            IsPinned = false,
            IsSensitive = false
        };

        await _db.AddOrUpdateItemAsync(item);
    }

    /// <summary>
    /// Copies an item back to the clipboard without triggering a duplicate capture.
    /// </summary>
    public void CopyToClipboard(ClipboardItem item)
    {
        try
        {
            _suppressNext = true;

            if (item.Type == ClipboardItemType.Image && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
            {
                using var img = System.Drawing.Image.FromFile(item.ImagePath);
                FormsClipboard.SetImage(img);
            }
            else if (item.Type == ClipboardItemType.Files && !string.IsNullOrEmpty(item.FilesJson))
            {
                var files = JsonSerializer.Deserialize<string[]>(item.FilesJson);
                if (files != null && files.Length > 0)
                {
                    var fileCollection = new System.Collections.Specialized.StringCollection();
                    fileCollection.AddRange(files);
                    WpfClipboard.SetFileDropList(fileCollection);
                }
            }
            else
            {
                WpfClipboard.SetText(item.ContentText ?? string.Empty);
            }

            _lastSequenceNumber = NativeMethods.GetClipboardSequenceNumber();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to copy item to clipboard", ex);
        }
    }

    private void CheckSensitiveWipe()
    {
        try
        {
            var wipeAge = TimeSpan.FromMinutes(_settings.SensitiveWipeMinutes);

            // Wipe unpinned sensitive items from memory store
            var memWiped = _memoryStore.WipeOlderThan(wipeAge);

            // Wipe unpinned sensitive items from database
            _ = _db.WipeSensitiveAsync(wipeAge);

            if (memWiped > 0)
            {
                ItemsUpdated?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error during sensitive wipe check", ex);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _debounceTimer.Dispose();
        _sensitiveWipeTimer.Dispose();

        if (_hwnd != IntPtr.Zero)
        {
            try
            {
                NativeMethods.RemoveClipboardFormatListener(_hwnd);
            }
            catch { }
        }

        _hwndSource?.Dispose();
    }
}
