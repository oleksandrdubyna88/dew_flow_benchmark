namespace Bench.Tests.Ui;

/// <summary>A clock a test moves by hand — the seam that makes a <see cref="PeriodicTimer"/> poll deterministic. Timers fire only
/// inside <see cref="Advance"/>, on the calling thread, once per elapsed period; a disposed timer is forgotten, so
/// <see cref="ActiveTimers"/> is how a test sees that a poll stopped rather than inferring it from silence.</summary>
internal sealed class ManualClock : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public int ActiveTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(t => t.Armed);
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Arm(_now, dueTime, period);
            _timers.Add(timer);
            return timer;
        }
    }

    /// <summary>Moves the clock and fires every timer whose due time passed — once per elapsed period.</summary>
    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _now += by;
            due = [.. _timers.SelectMany(t => Enumerable.Repeat(t, t.Elapse(_now)))];
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    private void Forget(ManualTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private void Rearm(ManualTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            timer.Arm(_now, dueTime, period);
        }
    }

    private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
    {
        private DateTimeOffset _next = DateTimeOffset.MaxValue;
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public bool Armed => _next != DateTimeOffset.MaxValue;

        public void Arm(DateTimeOffset now, TimeSpan dueTime, TimeSpan period)
        {
            _next = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : now + dueTime;
            _period = period;
        }

        /// <summary>How many times the timer is due by <paramref name="now"/>; advances its next due time past it.</summary>
        public int Elapse(DateTimeOffset now)
        {
            var fires = 0;
            while (_next <= now)
            {
                fires++;
                _next = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero ? DateTimeOffset.MaxValue : _next + _period;
            }

            return fires;
        }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            clock.Rearm(this, dueTime, period);
            return true;
        }

        public void Dispose() => clock.Forget(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
