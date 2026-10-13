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
    private static DatabaseService? _databaseService;

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
            var settingsManager = new SettingsManager(paths.SettingsPath);
            var settings = settingsManager.Current;

            _databaseService = new DatabaseService(paths, settings);
            _ = _databaseService.InitializeAsync();

            var memoryStore = new MemorySensitiveStore();
            _clipboardEngine = new ClipboardEngine(_databaseService, settings, memoryStore);

            // Re-register listener on resume from sleep and session unlock
            _powerWatcher = new PowerAndSessionWatcher(() =>
            {
                _clipboardEngine.ReRegisterListener();
            });

            var mainViewModel = new MainViewModel(_databaseService, memoryStore, _clipboardEngine);
            _mainWindow = new MainWindow(mainViewModel);

            _trayManager = new TrayManager(
                onOpenWindow: BringMainWindowToFront,
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

            if (!startInBackground)
            {
                _mainWindow.Show();
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
