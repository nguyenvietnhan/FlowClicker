using System.Windows;

namespace FlowClicker.Core.Engine;

internal sealed class MouseWorker : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellation;
    private Thread? _thread;
    private bool _disposed;

    public bool IsRunning => Volatile.Read(ref _cancellation) is not null;

    public void Start(string name, ThreadPriority priority, Action<CancellationToken> run, Action<bool> stateChanged)
    {
        lock (_gate)
        {
            if (_disposed || IsRunning)
                return;

            var cancellation = new CancellationTokenSource();
            Volatile.Write(ref _cancellation, cancellation);
            _thread = new Thread(() =>
            {
                try
                {
                    run(cancellation.Token);
                }
                finally
                {
                    lock (_gate)
                    {
                        cancellation.Dispose();
                        Volatile.Write(ref _cancellation, null);
                        Publish(stateChanged, false);
                    }
                }
            }) { IsBackground = true, Name = name, Priority = priority };

            _thread.Start();
            Publish(stateChanged, true);
        }
    }

    public void Stop()
    {
        lock (_gate)
            _cancellation?.Cancel();
    }

    private static void Publish(Action<bool> stateChanged, bool running)
    {
        if (Application.Current is { } app)
            app.Dispatcher.BeginInvoke(() => stateChanged(running));
        else
            stateChanged(running);
    }

    public void Dispose()
    {
        Thread? thread;
        lock (_gate)
        {
            _disposed = true;
            _cancellation?.Cancel();
            thread = _thread;
        }

        if (thread != Thread.CurrentThread)
            thread?.Join();
    }
}
