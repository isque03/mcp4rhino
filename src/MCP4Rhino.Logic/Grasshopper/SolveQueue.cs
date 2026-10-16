namespace MCP4Rhino.Logic.Grasshopper;

public sealed class SolveContext
{
    private int _cancelled;
    private int _timedOut;
    public bool CancellationRequested => Volatile.Read(ref _cancelled) != 0;
    public bool TimedOut => Volatile.Read(ref _timedOut) != 0;
    public Action? Abort { get; set; }

    public void Cancel(bool timeout = false)
    {
        if (timeout) Interlocked.Exchange(ref _timedOut, 1);
        Interlocked.Exchange(ref _cancelled, 1);
        Abort?.Invoke();
    }
}

public sealed record JobStatus(string JobId, string SessionId, string State, bool CancellationRequested,
    object? Result, string? Error);

/// <summary>One process-wide UI worker. Cancellation never releases a running job's slot.</summary>
public sealed class SolveQueue(Action<Action> post) : IDisposable
{
    private sealed class Job(string sessionId, Func<SolveContext, object> work, int timeoutSeconds)
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string SessionId { get; } = sessionId;
        public Func<SolveContext, object>? Work { get; set; } = work;
        public int TimeoutSeconds { get; } = timeoutSeconds;
        public SolveContext Context { get; } = new();
        public string State { get; set; } = "queued";
        public object? Result { get; set; }
        public string? Error { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Job> _jobs = new();
    private readonly Queue<Job> _pending = new();
    private bool _scheduled;
    private bool _disposed;

    public bool Busy { get { lock (_gate) return _scheduled; } }

    public JobStatus Enqueue(string sessionId, Func<SolveContext, object> work, int timeoutSeconds = 60)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pending.Count >= 32) throw new GhException("busy", "Solve queue is full.");
            if (_jobs.Count >= 256) Prune();
            var job = new Job(sessionId, work, Math.Clamp(timeoutSeconds, 1, 3600));
            _jobs.Add(job.Id, job);
            _pending.Enqueue(job);
            if (!_scheduled) { _scheduled = true; post(RunNext); }
            return Snapshot(job);
        }
    }

    public JobStatus Get(string id)
    {
        lock (_gate) return Snapshot(Find(id));
    }

    public JobStatus Cancel(string id)
    {
        lock (_gate)
        {
            var job = Find(id);
            if (job.State is "queued" or "running")
            {
                job.Context.Cancel();
                if (job.State == "queued") job.State = "cancelled";
            }
            return Snapshot(job);
        }
    }

    private void RunNext()
    {
        Job? job;
        lock (_gate)
        {
            while (_pending.TryPeek(out var head) && head.State == "cancelled") _pending.Dequeue();
            if (_disposed || !_pending.TryDequeue(out job)) { _scheduled = false; return; }
            job.State = "running";
        }
        using var deadline = new Timer(_ => Deadline(job), null,
            TimeSpan.FromSeconds(job.TimeoutSeconds), Timeout.InfiniteTimeSpan);
        try
        {
            var result = job.Work!(job.Context);
            lock (_gate)
            {
                job.Result = job.Context.CancellationRequested ? null : result;
                job.State = job.Context.TimedOut ? "timed_out" : job.Context.CancellationRequested ? "cancelled" : "completed";
            }
        }
        catch (Exception ex)
        {
            lock (_gate) { job.Error = ex.Message; job.State = "failed"; }
        }
        finally
        {
            job.Context.Abort = null;
            job.Work = null;
            lock (_gate)
            {
                if (_disposed) _scheduled = false;
                else post(RunNext);
            }
        }
    }

    private void Deadline(Job job)
    {
        lock (_gate) if (job.State == "running") job.Context.Cancel(timeout: true);
    }

    private Job Find(string id) => _jobs.GetValueOrDefault(id) ??
        throw new GhException("job_not_found", "Job is unknown or its status has expired.");

    private static JobStatus Snapshot(Job job) => new(job.Id, job.SessionId, job.State,
        job.Context.CancellationRequested, job.Result, job.Error);

    private void Prune()
    {
        foreach (var id in _jobs.Where(p => p.Value.State is not ("queued" or "running")).Take(128).Select(p => p.Key).ToArray())
            _jobs.Remove(id);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (var job in _jobs.Values.Where(j => j.State is "queued" or "running"))
            {
                job.Context.Cancel();
                if (job.State == "queued") job.State = "cancelled";
            }
        }
    }
}
