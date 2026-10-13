using System.Diagnostics;

namespace DeeClipper.App.Services;

public static class SourceAppResolver
{
    public static string? GetActiveSourceApp()
    {
        try
        {
            var ownerHwnd = NativeMethods.GetClipboardOwner();
            if (ownerHwnd == IntPtr.Zero)
            {
                return null;
            }

            NativeMethods.GetWindowThreadProcessId(ownerHwnd, out var processId);
            if (processId == 0)
            {
                return null;
            }

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
