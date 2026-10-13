using Microsoft.Win32;

namespace DeeClipper.App.Services;

public class PowerAndSessionWatcher : IDisposable
{
    private readonly Action _onReRegister;
    private bool _isDisposed;

    public PowerAndSessionWatcher(Action onReRegister)
    {
        _onReRegister = onReRegister;

        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to hook SystemEvents for power/session monitoring", ex);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Logger.LogInfo("System resumed from sleep/suspend. Re-registering clipboard listener...");
            _onReRegister();
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            Logger.LogInfo("Session unlocked. Re-registering clipboard listener...");
            _onReRegister();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
        }
        catch { }
    }
}
