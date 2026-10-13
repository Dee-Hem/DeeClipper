namespace DeeClipper.Core.Common;

public class DatabasePaths
{
    public string RootDirectory { get; }
    public string DatabasePath { get; }
    public string ImagesDirectory { get; }
    public string LogsDirectory { get; }
    public string SettingsPath { get; }

    public DatabasePaths(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeeClipper"
        );

        DatabasePath = Path.Combine(RootDirectory, "deeclipper.db");
        ImagesDirectory = Path.Combine(RootDirectory, "images");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        SettingsPath = Path.Combine(RootDirectory, "settings.json");
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ImagesDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
