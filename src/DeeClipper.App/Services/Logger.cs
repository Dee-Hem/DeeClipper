using System.IO;
using System.Text;
using DeeClipper.Core.Common;

namespace DeeClipper.App.Services;

public static class Logger
{
    private static readonly object Lock = new();
    private static string? _logDirectory;

    public static void Initialize(DatabasePaths paths)
    {
        _logDirectory = paths.LogsDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public static void LogInfo(string message) => Log("INFO", message);
    public static void LogWarning(string message) => Log("WARN", message);
    public static void LogError(string message, Exception? ex = null)
    {
        var sb = new StringBuilder(message);
        if (ex != null)
        {
            sb.AppendLine();
            sb.AppendLine(ex.ToString());
        }
        Log("ERROR", sb.ToString());
    }

    private static void Log(string level, string message)
    {
        try
        {
            var dir = _logDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeeClipper", "logs"
            );
            Directory.CreateDirectory(dir);

            var fileName = $"deeclipper_{DateTime.UtcNow:yyyyMMdd}.log";
            var filePath = Path.Combine(dir, fileName);

            var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";

            lock (Lock)
            {
                File.AppendAllText(filePath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Do not crash if logging fails
        }
    }
}
