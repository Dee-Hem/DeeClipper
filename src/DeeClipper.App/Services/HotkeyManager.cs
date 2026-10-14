using System.Windows.Interop;

namespace DeeClipper.App.Services;

public class HotkeyManager : IDisposable
{
    private const int HotkeyId = 9001;

    private HwndSource? _hwndSource;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _isRegistered;
    private bool _isDisposed;

    public event Action? HotkeyPressed;
    public event Action<string>? RegistrationFailed;

    public bool IsRegistered => _isRegistered;

    public HotkeyManager()
    {
        InitializeWindow();
    }

    private void InitializeWindow()
    {
        try
        {
            var parameters = new HwndSourceParameters("DeeClipperHotkeyListener")
            {
                ParentWindow = NativeMethods.HWND_MESSAGE
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);
            _hwnd = _hwndSource.Handle;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to initialize HotkeyManager message window", ex);
        }
    }

    public bool Register(string hotkeyString)
    {
        if (_hwnd == IntPtr.Zero) return false;

        Unregister();

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        if (!ParseHotkey(hotkeyString, out var modifiers, out var vk))
        {
            Logger.LogWarning($"Invalid hotkey string: {hotkeyString}");
            RegistrationFailed?.Invoke($"Invalid hotkey format: {hotkeyString}");
            return false;
        }

        // Win+V is reserved by Windows
        if ((modifiers & NativeMethods.MOD_WIN) != 0 && vk == NativeMethods.VK_V)
        {
            var msg = "Win+V is reserved by Windows. Please configure an alternative hotkey like Ctrl+Alt+V.";
            Logger.LogWarning(msg);
            RegistrationFailed?.Invoke(msg);
            return false;
        }

        var success = NativeMethods.RegisterHotKey(
            _hwnd,
            HotkeyId,
            modifiers | NativeMethods.MOD_NOREPEAT,
            vk
        );

        if (!success)
        {
            var msg = $"Hotkey '{hotkeyString}' is already in use by another application. DeeClipper will keep running.";
            Logger.LogWarning(msg);
            RegistrationFailed?.Invoke(msg);
            _isRegistered = false;
            return false;
        }

        _isRegistered = true;
        Logger.LogInfo($"Hotkey registered successfully: {hotkeyString}");
        return true;
    }

    private const int QuickSlotBaseId = 9100;
    private readonly List<int> _registeredQuickSlots = new();

    public event Action<int>? QuickPasteSlotPressed;

    public void RegisterQuickPasteSlots()
    {
        if (_hwnd == IntPtr.Zero) return;

        UnregisterQuickPasteSlots();

        for (var slot = 1; slot <= 9; slot++)
        {
            var id = QuickSlotBaseId + slot;
            var vk = (uint)('0' + slot);
            var success = NativeMethods.RegisterHotKey(
                _hwnd,
                id,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
                vk
            );

            if (success)
            {
                _registeredQuickSlots.Add(id);
            }
            else
            {
                Logger.LogWarning($"Could not register quick-paste slot Ctrl+Alt+{slot} (already in use).");
            }
        }
    }

    public void UnregisterQuickPasteSlots()
    {
        if (_hwnd == IntPtr.Zero) return;

        foreach (var id in _registeredQuickSlots)
        {
            try
            {
                NativeMethods.UnregisterHotKey(_hwnd, id);
            }
            catch { }
        }
        _registeredQuickSlots.Clear();
    }

    public void Unregister()
    {
        if (_isRegistered && _hwnd != IntPtr.Zero)
        {
            try
            {
                NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            }
            catch { }
            _isRegistered = false;
        }

        UnregisterQuickPasteSlots();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (id == HotkeyId)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            else if (id is >= QuickSlotBaseId + 1 and <= QuickSlotBaseId + 9)
            {
                var slotNumber = id - QuickSlotBaseId;
                QuickPasteSlotPressed?.Invoke(slotNumber);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public static bool ParseHotkey(string input, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = input.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        foreach (var p in parts)
        {
            var lower = p.ToLowerInvariant();
            switch (lower)
            {
                case "ctrl":
                case "control":
                    modifiers |= NativeMethods.MOD_CONTROL;
                    break;
                case "alt":
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case "shift":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                    modifiers |= NativeMethods.MOD_WIN;
                    break;
                default:
                    if (lower.Length == 1 && char.IsLetterOrDigit(lower[0]))
                    {
                        vk = (uint)char.ToUpperInvariant(lower[0]);
                    }
                    else if (lower == "v")
                    {
                        vk = (uint)NativeMethods.VK_V;
                    }
                    else if (lower == "insert")
                    {
                        vk = (uint)NativeMethods.VK_INSERT;
                    }
                    break;
            }
        }

        return vk != 0;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Unregister();
        _hwndSource?.Dispose();
    }
}
