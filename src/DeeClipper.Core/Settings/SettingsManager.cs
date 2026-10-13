using System.Text.Json;

namespace DeeClipper.Core.Settings;

public class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private readonly object _syncLock = new();
    private AppSettings _currentSettings;

    public SettingsManager(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
        _currentSettings = Load();
    }

    public AppSettings Current
    {
        get
        {
            lock (_syncLock)
            {
                return _currentSettings;
            }
        }
    }

    public AppSettings Load()
    {
        lock (_syncLock)
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var deserialized = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (deserialized != null)
                    {
                        _currentSettings = deserialized;
                        return _currentSettings;
                    }
                }
            }
            catch
            {
                // Fall back to default settings on error
            }

            _currentSettings = new AppSettings();
            return _currentSettings;
        }
    }

    /// <summary>
    /// Atomically saves the settings to settings.json using a temp file swap.
    /// </summary>
    public void Save(AppSettings settings)
    {
        lock (_syncLock)
        {
            _currentSettings = settings ?? new AppSettings();
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tempFilePath = _settingsFilePath + ".tmp." + Guid.NewGuid().ToString("N");
            var json = JsonSerializer.Serialize(_currentSettings, JsonOptions);

            File.WriteAllText(tempFilePath, json);

            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    File.Replace(tempFilePath, _settingsFilePath, null);
                }
                else
                {
                    File.Move(tempFilePath, _settingsFilePath);
                }
            }
            catch
            {
                // Fallback for filesystems that do not support File.Replace
                File.Move(tempFilePath, _settingsFilePath, overwrite: true);
            }
        }
    }

    public void Update(Action<AppSettings> updateAction)
    {
        lock (_syncLock)
        {
            updateAction(_currentSettings);
            Save(_currentSettings);
        }
    }
}
