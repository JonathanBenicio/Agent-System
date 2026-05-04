using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Notificador de mudanças para hot-reload via IOptionsMonitor pattern.
/// </summary>
public class ConfigReloadNotifier : IConfigReloadNotifier
{
    private readonly List<Action<string>> _listeners = new();
    private readonly object _lock = new();

    public void NotifyChange(string key)
    {
        List<Action<string>> snapshot;
        lock (_lock)
        {
            snapshot = new List<Action<string>>(_listeners);
        }
        foreach (var listener in snapshot)
        {
            listener(key);
        }
    }

    public IDisposable OnChange(Action<string> listener)
    {
        lock (_lock)
        {
            _listeners.Add(listener);
        }
        return new ChangeSubscription(this, listener);
    }

    private void RemoveListener(Action<string> listener)
    {
        lock (_lock)
        {
            _listeners.Remove(listener);
        }
    }

    private sealed class ChangeSubscription : IDisposable
    {
        private readonly ConfigReloadNotifier _notifier;
        private readonly Action<string> _listener;

        public ChangeSubscription(ConfigReloadNotifier notifier, Action<string> listener)
        {
            _notifier = notifier;
            _listener = listener;
        }

        public void Dispose() => _notifier.RemoveListener(_listener);
    }
}
