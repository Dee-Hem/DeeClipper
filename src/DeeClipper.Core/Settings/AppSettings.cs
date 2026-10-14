namespace DeeClipper.Core.Settings;

public class AppSettings
{
    /// <summary>
    /// History limit for unpinned items: 50, 100, 500, 1000, or -1 (unlimited). Default is 500.
    /// </summary>
    public int HistoryLimit { get; set; } = 500;

    /// <summary>
    /// Maximum age in days for unpinned items. Null means never.
    /// </summary>
    public int? MaxAgeDays { get; set; } = null;

    /// <summary>
    /// Maximum text size in bytes to capture (default 1 MB = 1048576 bytes).
    /// </summary>
    public long MaxTextSizeBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Maximum image size in bytes to capture (default 25 MB = 26214400 bytes).
    /// </summary>
    public long MaxImageSizeBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>
    /// Whether sensitive content detection is enabled. Default true.
    /// </summary>
    public bool SensitiveDetectionEnabled { get; set; } = true;

    /// <summary>
    /// Auto wipe unpinned sensitive items after N minutes. Default 5.
    /// </summary>
    public int SensitiveWipeMinutes { get; set; } = 5;

    /// <summary>
    /// Global popup hotkey. Default Ctrl+Alt+V.
    /// </summary>
    public string PopupHotkey { get; set; } = "Ctrl+Alt+V";

    /// <summary>
    /// Paste directly into active window on selection. Default true.
    /// </summary>
    public bool PasteDirectly { get; set; } = true;

    /// <summary>
    /// Quick-paste slots (1-9) enabled. Default false.
    /// </summary>
    public bool QuickPasteSlots { get; set; } = false;

    /// <summary>
    /// Theme mode: System, Light, Dark. Default System.
    /// </summary>
    public string Theme { get; set; } = "System";

    /// <summary>
    /// Start app with Windows. Default false.
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Whether popup opens near the mouse cursor (true) or centered on the monitor (false). Default true.
    /// </summary>
    public bool PopupPositionNearCursor { get; set; } = true;

    /// <summary>
    /// Popup window width in device-independent pixels. Default 680.
    /// </summary>
    public double PopupWidth { get; set; } = 680;

    /// <summary>
    /// Popup window height in device-independent pixels. Default 500.
    /// </summary>
    public double PopupHeight { get; set; } = 500;

    /// <summary>
    /// Whether the preview pane in the popup is visible. Default true.
    /// </summary>
    public bool ShowPreviewPane { get; set; } = true;

    /// <summary>
    /// Whether first run onboarding wizard has been completed. Default false.
    /// </summary>
    public bool FirstRunCompleted { get; set; } = false;

    /// <summary>
    /// Whether clipboard monitoring is currently paused. Default false.
    /// </summary>
    public bool IsMonitoringPaused { get; set; } = false;

    /// <summary>
    /// Whether compact item list view mode is enabled. Default false.
    /// </summary>
    public bool CompactMode { get; set; } = false;

    /// <summary>
    /// Base font size for UI preview and list items. Default 13.
    /// </summary>
    public double FontSize { get; set; } = 13.0;

    /// <summary>
    /// Blocklist of executable names (without .exe) to ignore when copied from.
    /// </summary>
    public List<string> AppBlocklist { get; set; } = new()
    {
        "keepass",
        "keepassxc",
        "1password",
        "bitwarden",
        "lastpass",
        "dashlane",
        "enpass",
        "nordpass"
    };

    public bool IsAppBlocked(string? appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            return false;

        var clean = appName.Trim().ToLowerInvariant();
        if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            clean = clean[..^4];

        return AppBlocklist.Any(b => string.Equals(b.Trim(), clean, StringComparison.OrdinalIgnoreCase));
    }
}
