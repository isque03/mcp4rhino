using MCP4Rhino.Contracts;

namespace MCP4Rhino.Logic.Telemetry;

/// <summary>
/// Bounded in-memory queue. Flushes on a background task every 30 seconds or at 20 events.
/// Delivery failure discards the batch. No persistent retry.
/// </summary>
public sealed class TelemetryService : ITelemetry, IDisposable
{
    public const int QueueCapacity = 100;
    public const int FlushThreshold = 20;
    public const int MaxBatchSize = 100;
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly Queue<TelemetryEvent> _queue = new();
    private ITelemetryTransport? _transport;
    private Func<DateTimeOffset> _clock = () => DateTimeOffset.UtcNow;
    private Action<string>? _log;
    private string _pluginVersion = McpJsonRpc.ServerVersion;
    private string _rhinoVersion = "8";
    private string _os = "macOS";
    private string? _installationId;
    private bool _enabled;
    private bool _flushing;
    private bool _dropLogged;
    private CancellationTokenSource _sendCts = new();
    private Timer? _timer;
    private Task _worker = Task.CompletedTask;
    private bool _disposed;

    public int QueuedCount
    {
        get
        {
            lock (_gate)
                return _queue.Count;
        }
    }

    public void Configure(TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Timer? oldTimer;
        ITelemetryTransport? oldTransport;
        CancellationTokenSource previous;
        lock (_gate)
        {
            if (_disposed)
                return;

            oldTimer = _timer;
            _timer = null;
            oldTransport = _transport;
            previous = _sendCts;
            _sendCts = new CancellationTokenSource();

            _pluginVersion = Blank(options.PluginVersion, McpJsonRpc.ServerVersion);
            _rhinoVersion = Blank(options.RhinoVersion, "8");
            _os = Blank(options.Os, TelemetryEnvironment.CurrentOs());
            _clock = options.UtcNow ?? (() => DateTimeOffset.UtcNow);
            _log = options.Log;
            _installationId = string.IsNullOrWhiteSpace(options.InstallationId) ? null : options.InstallationId.Trim();
            _transport = options.Transport;
            _enabled = options.ConsentGranted && _transport is not null && _installationId is not null;
            _dropLogged = false;
            if (!_enabled)
                _queue.Clear();
            else if (options.UseTimer)
                _timer = new Timer(_ => NotifyFlushInterval(), null, FlushInterval, FlushInterval);
        }

        DisposeQuiet(oldTimer);
        try { previous.Cancel(); } catch { /* already disposed or canceled */ }
        if (!ReferenceEquals(oldTransport, options.Transport))
            DisposeQuiet(oldTransport);
    }

    public void Revoke()
    {
        Timer? timer;
        CancellationTokenSource previous;
        lock (_gate)
        {
            _enabled = false;
            _queue.Clear();
            timer = _timer;
            _timer = null;
            previous = _sendCts;
            _sendCts = new CancellationTokenSource();
        }

        DisposeQuiet(timer);
        try { previous.Cancel(); } catch { /* in-flight send observes the cancel */ }
    }

    public void Shutdown()
    {
        Timer? timer;
        lock (_gate)
        {
            timer = _timer;
            _timer = null;
        }

        DisposeQuiet(timer);
    }

    public void Track(string eventName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(eventName) || !TrySnapshot(out var snap))
                return;
            Enqueue(snap.Create(eventName.Trim(), tool: null, durationMs: null, success: null));
        }
        catch (Exception ex)
        {
            Log($"telemetry track failed ({ex.GetType().Name})");
        }
    }

    public void TrackTool(string toolName, long durationMs, bool success)
        => TrackTool(toolName, durationMs, success, null);

    public void TrackTool(string toolName, long durationMs, bool success, ToolDiagnostics? diagnostics)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(toolName) || !TrySnapshot(out var snap))
                return;
            if (durationMs < 0)
                durationMs = 0;
            var wire = ToolDiagnosticsWire.Create(toolName.Trim(), success, diagnostics);
            Enqueue(snap.Create(TelemetryEvents.ToolExecuted, toolName.Trim(), durationMs, success, wire));
        }
        catch (Exception ex)
        {
            Log($"telemetry track failed ({ex.GetType().Name})");
        }
    }

    /// <summary>Same path the 30-second timer uses. Does not block.</summary>
    public void NotifyFlushInterval()
    {
        try
        {
            ScheduleFlush();
        }
        catch (Exception ex)
        {
            Log($"telemetry flush failed ({ex.GetType().Name})");
        }
    }

    public Task WaitForFlushAsync()
    {
        lock (_gate)
            return _worker;
    }

    public void Dispose()
    {
        Timer? timer;
        ITelemetryTransport? transport;
        lock (_gate)
        {
            _disposed = true;
            _enabled = false;
            _queue.Clear();
            timer = _timer;
            _timer = null;
            transport = _transport;
            _transport = null;
        }

        DisposeQuiet(timer);
        DisposeQuiet(transport);
    }

    private bool TrySnapshot(out Snapshot snapshot)
    {
        lock (_gate)
        {
            if (!_enabled || _transport is null || string.IsNullOrWhiteSpace(_installationId))
            {
                snapshot = default;
                return false;
            }

            snapshot = new Snapshot(_installationId, _pluginVersion, _rhinoVersion, _os, _clock);
            return true;
        }
    }

    private void Enqueue(TelemetryEvent evt)
    {
        string? dropMessage = null;
        lock (_gate)
        {
            if (!_enabled || _transport is null)
                return;
            if (_queue.Count >= QueueCapacity)
            {
                if (!_dropLogged)
                {
                    _dropLogged = true;
                    dropMessage = "telemetry queue full; dropping events";
                }
            }
            else
            {
                _queue.Enqueue(evt);
                if (!_flushing && _queue.Count >= FlushThreshold)
                {
                    _flushing = true;
                    _worker = Task.Run(FlushWorkerAsync);
                }
            }
        }

        if (dropMessage is not null)
            Log(dropMessage);
    }

    private void ScheduleFlush()
    {
        lock (_gate)
        {
            if (!_enabled || _transport is null || _flushing || _queue.Count == 0)
                return;
            _flushing = true;
            _worker = Task.Run(FlushWorkerAsync);
        }
    }

    private async Task FlushWorkerAsync()
    {
        try
        {
            while (true)
            {
                List<TelemetryEvent> batch;
                CancellationToken token;
                ITelemetryTransport? transport;
                lock (_gate)
                {
                    if (!_enabled || _transport is null || _queue.Count == 0)
                    {
                        if (!_enabled)
                            _queue.Clear();
                        _flushing = false;
                        return;
                    }

                    var take = Math.Min(MaxBatchSize, _queue.Count);
                    batch = new List<TelemetryEvent>(take);
                    for (var i = 0; i < take; i++)
                        batch.Add(_queue.Dequeue());
                    token = _sendCts.Token;
                    transport = _transport;
                }

                try
                {
                    var ok = await transport.SendAsync(batch, token).ConfigureAwait(false);
                    if (!ok)
                        Log("telemetry delivery failed; discarding batch");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    Log("telemetry delivery canceled; discarding batch");
                }
                catch (Exception ex)
                {
                    Log($"telemetry delivery failed; discarding batch ({ex.GetType().Name})");
                }

                lock (_gate)
                {
                    if (!_enabled)
                    {
                        _queue.Clear();
                        _flushing = false;
                        return;
                    }

                    if (_queue.Count < FlushThreshold)
                    {
                        _flushing = false;
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"telemetry flush stopped ({ex.GetType().Name})");
            lock (_gate)
                _flushing = false;
        }
    }

    private void Log(string message)
    {
        try { _log?.Invoke(message); }
        catch { /* logging must not affect callers */ }
    }

    private static void DisposeQuiet(IDisposable? disposable)
    {
        try { disposable?.Dispose(); }
        catch { /* shutdown and reconfigure stay best-effort */ }
    }

    private static string Blank(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private readonly struct Snapshot(
        string installationId,
        string pluginVersion,
        string rhinoVersion,
        string os,
        Func<DateTimeOffset> clock)
    {
        public TelemetryEvent Create(string eventName, string? tool, long? durationMs, bool? success,
            ToolDiagnosticsWire diagnostics = default) => new()
        {
            InstallationId = installationId,
            EventName = eventName,
            Timestamp = TelemetryEvent.FormatTimestamp(clock()),
            PluginVersion = pluginVersion,
            RhinoVersion = rhinoVersion,
            Os = os,
            Tool = tool,
            DurationMs = durationMs,
            Success = success,
            ErrorCode = diagnostics.ErrorCode,
            ErrorType = diagnostics.ErrorType,
            ErrorStage = diagnostics.ErrorStage,
            RhinoCommandName = diagnostics.RhinoCommandName,
        };
    }
}
