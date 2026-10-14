using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using DeeClipper.Core.Models;
using DeeClipper.Core.Settings;
using DeeClipper.Core.Storage;
using DeeClipper.Core.Text;

namespace DeeClipper.App.Services;

public enum PasteMode
{
    DirectPaste,
    PasteAsPlainText,
    CopyOnly
}

public enum PasteAsTransform
{
    PlainText,
    UpperCase,
    LowerCase,
    TitleCase,
    SingleLine,
    Trimmed,
    CleanLink
}

public class PasteEngine
{
    private readonly DatabaseService _db;
    private readonly ClipboardEngine _clipboardEngine;
    private readonly AppSettings _settings;
    private readonly MemorySensitiveStore _memoryStore;

    private IntPtr _previousForegroundWindow = IntPtr.Zero;

    public PasteEngine(
        DatabaseService db,
        ClipboardEngine clipboardEngine,
        AppSettings settings,
        MemorySensitiveStore memoryStore)
    {
        _db = db;
        _clipboardEngine = clipboardEngine;
        _settings = settings;
        _memoryStore = memoryStore;
    }

    /// <summary>
    /// Remembers the current foreground window before showing the popup.
    /// </summary>
    public void RememberForegroundWindow()
    {
        _previousForegroundWindow = NativeMethods.GetForegroundWindow();
        Logger.LogInfo($"Remembered foreground window: {_previousForegroundWindow}");
    }

    /// <summary>
    /// Executes paste with an on-the-fly text transformation without modifying the stored database item.
    /// </summary>
    public async Task ExecutePasteAsAsync(ClipboardItem item, PasteAsTransform transform)
    {
        if (string.IsNullOrEmpty(item.ContentText))
        {
            await ExecutePasteAsync(item, PasteMode.DirectPaste);
            return;
        }

        var transformed = transform switch
        {
            PasteAsTransform.PlainText => TextTransforms.ToPlainText(item.ContentText),
            PasteAsTransform.UpperCase => TextTransforms.ToUpperCase(item.ContentText),
            PasteAsTransform.LowerCase => TextTransforms.ToLowerCase(item.ContentText),
            PasteAsTransform.TitleCase => TextTransforms.ToTitleCase(item.ContentText),
            PasteAsTransform.SingleLine => TextTransforms.ToSingleLine(item.ContentText),
            PasteAsTransform.Trimmed => TextTransforms.Trim(item.ContentText),
            PasteAsTransform.CleanLink => TextTransforms.StripTrackingParameters(item.ContentText),
            _ => item.ContentText
        };

        var transientItem = new ClipboardItem
        {
            Id = item.Id,
            Type = ClipboardItemType.Text,
            ContentText = transformed,
            ContentHash = item.ContentHash,
            Preview = ClipboardItem.GeneratePreview(transformed),
            ByteSize = Encoding.UTF8.GetByteCount(transformed),
            SourceApp = item.SourceApp,
            IsPinned = item.IsPinned,
            IsSensitive = item.IsSensitive
        };

        await ExecutePasteAsync(transientItem, PasteMode.DirectPaste);
    }

    /// <summary>
    /// Executes the paste or copy action for the chosen item.
    /// </summary>
    public async Task ExecutePasteAsync(ClipboardItem item, PasteMode mode)
    {
        var targetHwnd = _previousForegroundWindow;
        var shouldPasteDirectly = _settings.PasteDirectly && mode != PasteMode.CopyOnly;

        try
        {
            // 1. Prepare item content (plain text if requested)
            var itemToCopy = item;
            if (mode == PasteMode.PasteAsPlainText && !string.IsNullOrEmpty(item.ContentText))
            {
                itemToCopy = new ClipboardItem
                {
                    Id = item.Id,
                    Type = ClipboardItemType.Text,
                    ContentText = TextTransforms.ToPlainText(item.ContentText),
                    ContentHash = item.ContentHash,
                    Preview = item.Preview,
                    ByteSize = item.ByteSize,
                    SourceApp = item.SourceApp,
                    IsPinned = item.IsPinned,
                    IsSensitive = item.IsSensitive
                };
            }

            // 2. Write item to clipboard with loopback suppression
            _clipboardEngine.CopyToClipboard(itemToCopy);

            // 3. Update last_used_at and use_count
            await UpdateItemUsageAsync(item);

            // 4. If copy-only was requested or paste-directly is off
            if (!shouldPasteDirectly)
            {
                ToastNotification.Show("Copied to clipboard");
                return;
            }

            // 5. Restore target window
            if (targetHwnd == IntPtr.Zero)
            {
                ToastNotification.Show("Copied. Press Ctrl+V.");
                return;
            }

            var activated = NativeMethods.SetForegroundWindow(targetHwnd);
            if (!activated)
            {
                Logger.LogWarning($"SetForegroundWindow returned false for hWnd {targetHwnd}. Target may be elevated.");
                ToastNotification.Show("Copied. Press Ctrl+V.");
                return;
            }

            // 6. Wait until hotkey modifiers are physically released (up to 500 ms)
            await WaitForModifiersReleasedAsync(500);

            // 7. Check target window class
            var sb = new StringBuilder(256);
            NativeMethods.GetClassName(targetHwnd, sb, sb.Capacity);
            var className = sb.ToString();
            var isConsole = string.Equals(className, "ConsoleWindowClass", StringComparison.OrdinalIgnoreCase);

            // 8. Inject keystrokes via SendInput
            var sent = SendPasteKeystrokes(isConsole);
            if (!sent)
            {
                Logger.LogWarning("SendInput failed to send paste keystrokes.");
                ToastNotification.Show("Copied. Press Ctrl+V.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error in ExecutePasteAsync", ex);
            ToastNotification.Show("Copied. Press Ctrl+V.");
        }
    }

    private async Task UpdateItemUsageAsync(ClipboardItem item)
    {
        try
        {
            if (item.Id < 0)
            {
                // In-memory sensitive item
                item.LastUsedAt = DateTimeOffset.UtcNow;
                item.UseCount++;
            }
            else
            {
                // Database item
                await _db.TouchItemUsageAsync(item.Id);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update item usage count", ex);
        }
    }

    private static async Task WaitForModifiersReleasedAsync(int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var ctrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
            var alt = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;
            var shift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
            var lwin = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0;
            var rwin = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;

            if (!ctrl && !alt && !shift && !lwin && !rwin)
            {
                break;
            }

            await Task.Delay(15);
        }
    }

    private static bool SendPasteKeystrokes(bool isConsole)
    {
        try
        {
            NativeMethods.INPUT[] inputs;

            if (isConsole)
            {
                // Shift+Insert for classic console windows
                inputs = new NativeMethods.INPUT[4];

                // Shift Down
                inputs[0].type = NativeMethods.INPUT_KEYBOARD;
                inputs[0].u.ki.wVk = (ushort)NativeMethods.VK_SHIFT;

                // Insert Down
                inputs[1].type = NativeMethods.INPUT_KEYBOARD;
                inputs[1].u.ki.wVk = (ushort)NativeMethods.VK_INSERT;

                // Insert Up
                inputs[2].type = NativeMethods.INPUT_KEYBOARD;
                inputs[2].u.ki.wVk = (ushort)NativeMethods.VK_INSERT;
                inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

                // Shift Up
                inputs[3].type = NativeMethods.INPUT_KEYBOARD;
                inputs[3].u.ki.wVk = (ushort)NativeMethods.VK_SHIFT;
                inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
            }
            else
            {
                // Ctrl+V for standard Windows applications
                inputs = new NativeMethods.INPUT[4];

                // Ctrl Down
                inputs[0].type = NativeMethods.INPUT_KEYBOARD;
                inputs[0].u.ki.wVk = (ushort)NativeMethods.VK_CONTROL;

                // V Down
                inputs[1].type = NativeMethods.INPUT_KEYBOARD;
                inputs[1].u.ki.wVk = (ushort)NativeMethods.VK_V;

                // V Up
                inputs[2].type = NativeMethods.INPUT_KEYBOARD;
                inputs[2].u.ki.wVk = (ushort)NativeMethods.VK_V;
                inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

                // Ctrl Up
                inputs[3].type = NativeMethods.INPUT_KEYBOARD;
                inputs[3].u.ki.wVk = (ushort)NativeMethods.VK_CONTROL;
                inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
            }

            var size = Marshal.SizeOf<NativeMethods.INPUT>();
            var result = NativeMethods.SendInput((uint)inputs.Length, inputs, size);
            return result == inputs.Length;
        }
        catch (Exception ex)
        {
            Logger.LogError("Exception in SendPasteKeystrokes", ex);
            return false;
        }
    }
}
