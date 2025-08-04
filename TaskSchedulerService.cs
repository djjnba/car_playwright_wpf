public class TaskSchedulerService : IDisposable
{
    private Timer? _timer;
    private readonly Func<Task> _action;
    private readonly TimeSpan _interval;
    private readonly DateTime _startBase;
    private int _runCount = 0;
    private DateTime _nextRun;
    public DateTime NextRun => _nextRun;

    public TaskSchedulerService(DateTime firstRun, TimeSpan interval, Func<Task> action)
    {
        _action = action;
        _interval = interval;
        _startBase = firstRun;
        _nextRun = firstRun;

        var due = _nextRun - DateTime.Now;
        if (due < TimeSpan.Zero)
            due = TimeSpan.Zero;

        _timer = new Timer(OnTimer, null, due, interval);
    }

    private async void OnTimer(object? state)
    {
        try
        {
            await _action();
        }
        catch
        {
            // 忽略异常
        }
        finally
        {
            _runCount++;
            _nextRun = _startBase.AddTicks(_interval.Ticks * _runCount);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
