using System.Windows;
using DeeClipper.App.Services;
using DeeClipper.App.ViewModels;
using DeeClipper.Core.Common;
using DeeClipper.Core.Settings;
using DeeClipper.Core.Storage;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace DeeClipper.App;

public class App : WpfApplication
{
    private static SingleInstanceManager? _singleInstanceManager;
    private static TrayManager? _trayManager;
    private static ClipboardEngine? _clipboardEngine;
    private static PowerAndSessionWatcher? _powerWatcher;
    private static MainWindow? _mainWindow;
    private static PopupWindow? _popupWindow;
    private static PasteEngine? _pasteEngine;
    private static HotkeyManager? _hotkeyManager;
    private static DatabaseService? _databaseService;
    private static SettingsManager? _settingsManager;
    private static DatabasePaths? _paths;
    private static Dialogs.SettingsWindow? _settingsWindow;

    public static void OpenSettingsWindow()
    {
        Current?.Dispatcher?.Invoke(() =>
        {
            if (_settingsWindow != null && _settingsWindow.IsVisible)
            {
                _settingsWindow.Activate();
                _settingsWindow.Focus();
                return;
            }

            if (_settingsManager != null && _databaseService != null && _paths != null &&
                _clipboardEngine != null && _hotkeyManager != null)
            {
                _settingsWindow = new Dialogs.SettingsWindow(
                    _settingsManager,
                    _databaseService,
                    _paths,
                    _clipboardEngine,
                    _hotkeyManager,
                    owner: _mainWindow?.IsVisible == true ? _mainWindow : null
                );
                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
                _settingsWindow.Show();
            }
        });
    }

    [STAThread]
    public static void Main(string[] args)
    {
        var paths = new DatabasePaths();
        paths.EnsureDirectoriesExist();
        Logger.Initialize(paths);

        // Setup Global Exception Handlers so the app never closes silently
        SetupGlobalExceptionHandling();

        var isBackground = args.Any(a => string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase));

        _singleInstanceManager = new SingleInstanceManager();
        var isFirstInstance = _singleInstanceManager.CheckSingleInstance(() =>
        {
            // Second instance signaled activation
            Current?.Dispatcher?.Invoke(() =>
            {
                BringMainWindowToFront();
            });
        });

        if (!isFirstInstance)
        {
            // Another instance is already running; exit silently
            return;
        }

        var app = new App();
        app.InitializeAndRun(paths, isBackground);
    }

    private void InitializeAndRun(DatabasePaths paths, bool startInBackground)
    {
        try
        {
            _paths = paths;
            _settingsManager = new SettingsManager(paths.SettingsPath);
            var settings = _settingsManager.Current;

            _databaseService = new DatabaseService(paths, settings);
            _ = _databaseService.InitializeAsync();

            var memoryStore = new MemorySensitiveStore();
            _clipboardEngine = new ClipboardEngine(_databaseService, settings, memoryStore);

            // Re-register listener on resume from sleep and session unlock
            _powerWatcher = new PowerAndSessionWatcher(() =>
            {
                _clipboardEngine.ReRegisterListener();
            });

            _pasteEngine = new PasteEngine(_databaseService, _clipboardEngine, settings, memoryStore);

            var popupViewModel = new PopupViewModel(_databaseService, memoryStore, _clipboardEngine, _pasteEngine, settings);
            _popupWindow = new PopupWindow(popupViewModel, _pasteEngine, settings);

            // Pre-warm popup window so it opens in under 100ms
            _popupWindow.Show();
            _popupWindow.Hide();

            var mainViewModel = new MainViewModel(_databaseService, memoryStore, _clipboardEngine, settings, _pasteEngine);
            _mainWindow = new MainWindow(mainViewModel);

            _trayManager = new TrayManager(
                onOpenWindow: BringMainWindowToFront,
                onOpenSettings: OpenSettingsWindow,
                onTogglePause: paused => _clipboardEngine.SetPaused(paused),
                onPrivateMode: minutes => _clipboardEngine.SetPrivateMode(minutes),
                onExit: () =>
                {
                    if (_mainWindow != null)
                    {
                        _mainWindow.IsExplicitExit = true;
                    }
                    Shutdown();
                }
            );

            _clipboardEngine.PauseStateChanged += isPaused =>
            {
                _trayManager.UpdatePauseState(isPaused);
            };

            // Initialize global hotkey listener
            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.HotkeyPressed += () =>
            {
                Current?.Dispatcher?.Invoke(() =>
                {
                    _popupWindow?.OpenPopup();
                });
            };
            _hotkeyManager.RegistrationFailed += msg =>
            {
                _trayManager?.ShowBalloon(msg, "Hotkey Warning");
            };

            _hotkeyManager.Register(settings.PopupHotkey);

            if (settings.QuickPasteSlots)
            {
                _hotkeyManager.RegisterQuickPasteSlots();
            }
            _hotkeyManager.QuickPasteSlotPressed += slot =>
            {
                Current?.Dispatcher?.Invoke(async () =>
                {
                    try
                    {
                        var item = await _databaseService.GetPinnedItemBySlotAsync(slot);
                        if (item != null && _pasteEngine != null)
                        {
                            await _pasteEngine.ExecutePasteAsync(item, PasteMode.DirectPaste);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error executing quick-paste for slot {slot}", ex);
                    }
                });
            };

            if (!startInBackground)
            {
                _mainWindow.Show();

                // Show onboarding on first run
                if (!settings.FirstRunCompleted)
                {
                    var onboarding = new Dialogs.OnboardingWindow(_settingsManager, _mainWindow);
                    onboarding.ShowDialog();
                }
            }

            Run();
        }
        catch (Exception ex)
        {
            Logger.LogError("Fatal error during application startup", ex);
            WpfMessageBox.Show(
                $"DeeClipper encountered a fatal error during startup:\n\n{ex.Message}\n\nCheck logs at: {paths.LogsDirectory}",
                "DeeClipper Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        finally
        {
            Cleanup();
        }
    }

    private static void BringMainWindowToFront()
    {
        if (_mainWindow == null) return;

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    private static void SetupGlobalExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Logger.LogError("AppDomain Unhandled Exception", ex);
            WpfMessageBox.Show(
                $"An unhandled error occurred:\n\n{ex?.Message}\n\nDetails written to logs folder.",
                "DeeClipper Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.LogError("TaskScheduler Unobserved Task Exception", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.LogError("Dispatcher Unhandled Exception", args.Exception);
            WpfMessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nDetails written to logs folder.",
                "DeeClipper Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            args.Handled = true;
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup();
        base.OnExit(e);
    }

    private static void Cleanup()
    {
        _hotkeyManager?.Dispose();
        _popupWindow?.Close();
        _trayManager?.Dispose();
        _clipboardEngine?.Dispose();
        _powerWatcher?.Dispose();
        _singleInstanceManager?.Dispose();
        if (_databaseService != null)
        {
            _ = _databaseService.DisposeAsync();
        }
    }
}
