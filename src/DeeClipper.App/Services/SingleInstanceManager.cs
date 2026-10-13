namespace DeeClipper.App.Services;

public class SingleInstanceManager : IDisposable
{
    private const string MutexName = "Local\\DeeClipper_SingleInstance_Mutex";
    private const string EventName = "Local\\DeeClipper_ShowWindow_Event";

    private Mutex? _mutex;
    private EventWaitHandle? _eventWaitHandle;
    private bool _hasHandle;
    private bool _isDisposed;

    public bool IsFirstInstance => _hasHandle;

    public bool CheckSingleInstance(Action onActivateRequested)
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out _hasHandle);
            if (!_hasHandle)
            {
                // Second instance: signal the existing instance to activate and bring window to front
                try
                {
                    using var existingEvent = EventWaitHandle.OpenExisting(EventName);
                    existingEvent.Set();
                }
                catch
                {
                    // Fallback
                }
                return false;
            }

            // First instance: create wait handle and listen for activation requests
            _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            var thread = new Thread(() =>
            {
                while (!_isDisposed)
                {
                    try
                    {
                        if (_eventWaitHandle.WaitOne())
                        {
                            if (!_isDisposed)
                            {
                                onActivateRequested();
                            }
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            })
            {
                IsBackground = true,
                Name = "DeeClipper_SingleInstance_Listener"
            };
            thread.Start();

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error checking single instance", ex);
            return true;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_hasHandle && _mutex != null)
        {
            try { _mutex.ReleaseMutex(); } catch { }
            _mutex.Dispose();
        }

        _eventWaitHandle?.Dispose();
    }
}
