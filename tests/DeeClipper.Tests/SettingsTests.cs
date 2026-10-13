using DeeClipper.Core.Settings;
using Xunit;

namespace DeeClipper.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public SettingsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeeClipper_SettingsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore
        }
    }

    [Fact]
    public void AppSettings_HasAllRequiredDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal(500, settings.HistoryLimit);
        Assert.Null(settings.MaxAgeDays);
        Assert.Equal(1024 * 1024, settings.MaxTextSizeBytes); // 1 MB
        Assert.Equal(25 * 1024 * 1024, settings.MaxImageSizeBytes); // 25 MB
        Assert.True(settings.SensitiveDetectionEnabled);
        Assert.Equal(5, settings.SensitiveWipeMinutes);
        Assert.Equal("Ctrl+Alt+V", settings.PopupHotkey);
        Assert.True(settings.PasteDirectly);
        Assert.False(settings.QuickPasteSlots);
        Assert.Equal("System", settings.Theme);
        Assert.False(settings.StartWithWindows);

        // Blocklist defaults: keepass, keepassxc, 1password, bitwarden, lastpass, dashlane, enpass, nordpass
        var expectedBlocklist = new[]
        {
            "keepass", "keepassxc", "1password", "bitwarden", "lastpass", "dashlane", "enpass", "nordpass"
        };
        foreach (var expectedApp in expectedBlocklist)
        {
            Assert.Contains(settings.AppBlocklist, a => string.Equals(a, expectedApp, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void IsAppBlocked_MatchesBlocklistCaseInsensitivelyAndIgnoresExeExtension()
    {
        var settings = new AppSettings();

        Assert.True(settings.IsAppBlocked("keepass.exe"));
        Assert.True(settings.IsAppBlocked("KeePass"));
        Assert.True(settings.IsAppBlocked("1password.exe"));
        Assert.True(settings.IsAppBlocked("Bitwarden"));
        Assert.True(settings.IsAppBlocked("keepassxc.EXE"));
        Assert.True(settings.IsAppBlocked("LastPass"));
        Assert.True(settings.IsAppBlocked("dashlane"));
        Assert.True(settings.IsAppBlocked("Enpass.exe"));
        Assert.True(settings.IsAppBlocked("NordPass"));

        Assert.False(settings.IsAppBlocked("notepad.exe"));
        Assert.False(settings.IsAppBlocked("code.exe"));
        Assert.False(settings.IsAppBlocked("chrome"));
    }

    [Fact]
    public void SettingsManager_SavesAtomicallyAndReloadsSuccessfully()
    {
        var manager = new SettingsManager(_settingsPath);

        // Initial default
        Assert.Equal(500, manager.Current.HistoryLimit);

        // Update settings
        manager.Update(s =>
        {
            s.HistoryLimit = 1000;
            s.Theme = "Dark";
            s.MaxAgeDays = 30;
        });

        Assert.True(File.Exists(_settingsPath));

        // Create new manager instance reading the file
        var freshManager = new SettingsManager(_settingsPath);
        Assert.Equal(1000, freshManager.Current.HistoryLimit);
        Assert.Equal("Dark", freshManager.Current.Theme);
        Assert.Equal(30, freshManager.Current.MaxAgeDays);
    }
}
