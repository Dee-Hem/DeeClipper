using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DeeClipper.App.Services;

public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "DeeClipper";

    public static bool IsAutoStartEnabled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (key == null) return false;

            var val = key.GetValue(AppName) as string;
            return !string.IsNullOrEmpty(val);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to check auto-start registry: {ex.Message}");
            return false;
        }
    }

    public static bool SetAutoStart(bool enable)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return false;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    try
                    {
                        exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(exePath))
                {
                    Logger.LogWarning("Could not determine current executable path for auto-start registration.");
                    return false;
                }

                // Launch with --background flag so it starts directly in system tray
                var command = $"\"{exePath}\" --background";
                key.SetValue(AppName, command);
                Logger.LogInfo($"Registered HKCU auto-start: {command}");
            }
            else
            {
                if (key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                    Logger.LogInfo("Removed HKCU auto-start registration.");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to update auto-start registry to {enable}", ex);
            return false;
        }
    }
}
