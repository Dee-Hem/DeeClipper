using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DeeClipper.App.Services;

public class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _pauseMenuItem;
    private readonly Action _onOpenWindow;
    private readonly Action _onOpenSettings;
    private readonly Action<bool> _onTogglePause;
    private readonly Action<int> _onPrivateMode;
    private readonly Action _onExit;

    private Icon? _activeIcon;
    private Icon? _pausedIcon;
    private bool _isDisposed;

    public TrayManager(
        Action onOpenWindow,
        Action onOpenSettings,
        Action<bool> onTogglePause,
        Action<int> onPrivateMode,
        Action onExit)
    {
        _onOpenWindow = onOpenWindow;
        _onOpenSettings = onOpenSettings;
        _onTogglePause = onTogglePause;
        _onPrivateMode = onPrivateMode;
        _onExit = onExit;

        LoadIcons();

        _contextMenu = new ContextMenuStrip();

        // 1. Open
        var openItem = new ToolStripMenuItem("Open DeeClipper", null, (_, _) => _onOpenWindow())
        {
            Font = new Font(_contextMenu.Font ?? SystemFonts.DefaultFont, FontStyle.Bold)
        };
        _contextMenu.Items.Add(openItem);

        // Settings item
        var settingsItem = new ToolStripMenuItem("Settings...", null, (_, _) => _onOpenSettings());
        _contextMenu.Items.Add(settingsItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // 2. Pause monitoring (toggle)
        _pauseMenuItem = new ToolStripMenuItem("Pause monitoring");
        _pauseMenuItem.Click += (_, _) =>
        {
            var newState = !_pauseMenuItem.Checked;
            _pauseMenuItem.Checked = newState;
            _onTogglePause(newState);
        };
        _pauseMenuItem.CheckOnClick = false;
        _contextMenu.Items.Add(_pauseMenuItem);

        // 3. Private mode (submenu: 5, 15, 60 minutes)
        var privateModeMenu = new ToolStripMenuItem("Private mode");
        privateModeMenu.DropDownItems.Add(new ToolStripMenuItem("Pause for 5 minutes", null, (_, _) => _onPrivateMode(5)));
        privateModeMenu.DropDownItems.Add(new ToolStripMenuItem("Pause for 15 minutes", null, (_, _) => _onPrivateMode(15)));
        privateModeMenu.DropDownItems.Add(new ToolStripMenuItem("Pause for 60 minutes", null, (_, _) => _onPrivateMode(60)));
        _contextMenu.Items.Add(privateModeMenu);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // 4. Exit
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => _onExit());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Text = "DeeClipper",
            Icon = _activeIcon ?? SystemIcons.Application,
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.MouseClick += (sender, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _onOpenWindow();
            }
        };
    }

    private void LoadIcons()
    {
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var appIcoPath = Path.Combine(appDir, "Assets", "app.ico");
            var pausedIcoPath = Path.Combine(appDir, "Assets", "paused.ico");

            if (File.Exists(appIcoPath))
            {
                _activeIcon = new Icon(appIcoPath);
            }
            if (File.Exists(pausedIcoPath))
            {
                _pausedIcon = new Icon(pausedIcoPath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load tray icons", ex);
        }
    }

    public void UpdatePauseState(bool isPaused)
    {
        _pauseMenuItem.Checked = isPaused;
        _notifyIcon.Icon = isPaused
            ? (_pausedIcon ?? _activeIcon ?? SystemIcons.Application)
            : (_activeIcon ?? SystemIcons.Application);

        _notifyIcon.Text = isPaused ? "DeeClipper (Paused)" : "DeeClipper";
    }

    public void ShowBalloon(string message, string title = "DeeClipper", ToolTipIcon icon = ToolTipIcon.Warning)
    {
        try
        {
            _notifyIcon.ShowBalloonTip(4000, title, message, icon);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _activeIcon?.Dispose();
        _pausedIcon?.Dispose();
    }
}
