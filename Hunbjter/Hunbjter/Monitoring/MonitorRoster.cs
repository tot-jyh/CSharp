namespace Hunbjter;

/// <summary>
/// Owns one <see cref="ModelMonitor"/> per model and keeps that set in step with the stored
/// favorites document. Each monitor runs its own loop; this type only creates, retires and
/// coordinates them.
/// </summary>
public sealed class MonitorRoster : IAsyncDisposable
{
    private readonly Dictionary<string, ModelMonitor> monitors = new(StringComparer.OrdinalIgnoreCase);
    private readonly MonitorContext context;

    private bool started;

    public MonitorRoster(MonitorContext context)
    {
        // Bound here rather than by the caller: the roster owns the flag, and wiring it at the
        // call site would mean reading a field that is still being assigned.
        this.context = context with { IsSuspended = () => IsSuspended };
    }

    public event EventHandler<ModelStatusChangedEventArgs>? StatusChanged;

    public event EventHandler<MonitorLogEventArgs>? LogRequested;

    /// <summary>Snapshot: callers iterate across awaits, so this must not be the live dictionary.</summary>
    public IReadOnlyList<ModelMonitor> Monitors => monitors.Values.ToList();

    public bool IsSuspended { get; private set; }

    public ModelMonitor? Find(string id)
    {
        return monitors.TryGetValue(id, out var monitor) ? monitor : null;
    }

    /// <summary>
    /// Reconciles against a freshly loaded document: existing monitors keep their runtime state
    /// and are rebound to the new <see cref="FavoriteItem"/>, new models get a monitor, and
    /// removed models are retired so any in-flight check discards its result.
    /// </summary>
    public void Sync(FavoritesDocument document)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var favorite in document.Items)
        {
            seen.Add(favorite.Id);

            if (monitors.TryGetValue(favorite.Id, out var existing))
            {
                existing.Rebind(favorite);
                continue;
            }

            var monitor = Create(favorite);
            monitors[favorite.Id] = monitor;

            if (started)
            {
                monitor.Start();
                monitor.RequestImmediate("신규 모델");
            }
        }

        foreach (var id in monitors.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            monitors[id].Retire();
            monitors.Remove(id);
        }
    }

    private ModelMonitor Create(FavoriteItem favorite)
    {
        var monitor = new ModelMonitor(favorite, context);
        monitor.StatusChanged += (_, e) => StatusChanged?.Invoke(this, e);
        monitor.LogRequested += (_, e) => LogRequested?.Invoke(this, e);
        return monitor;
    }

    public void Start()
    {
        started = true;
        foreach (var monitor in monitors.Values)
        {
            monitor.Start();
        }
    }

    /// <summary>Cancel and abandon. Never awaited from the UI thread during shutdown.</summary>
    public async Task StopAsync()
    {
        started = false;
        await Task.WhenAll(monitors.Values.Select(monitor => monitor.StopAsync()));
    }

    /// <summary>
    /// Cancels every loop without waiting. Used on shutdown, where blocking the UI thread on
    /// continuations that need the message pump would deadlock.
    /// </summary>
    public void Retire()
    {
        started = false;
        foreach (var monitor in monitors.Values)
        {
            monitor.Retire();
        }
    }

    /// <summary>Held while a modal dialog owns the settings files.</summary>
    public void Suspend() => IsSuspended = true;

    public void Resume()
    {
        IsSuspended = false;
        foreach (var monitor in monitors.Values)
        {
            monitor.Reschedule();
        }
    }

    /// <summary>
    /// A user-initiated sweep. Runs at <see cref="GatePriority.Manual"/> so it overtakes the
    /// automatic loops rather than queueing behind them.
    /// </summary>
    public async Task RunManualAsync(
        IReadOnlyList<ModelMonitor> targets,
        string reason,
        CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
        {
            return;
        }

        using (var lease = await context.Gate.AcquireAsync(GatePriority.Manual, cancellationToken))
        {
            await context.Probe.PrepareSessionAsync(lease, $"{reason} 전 세션 준비", cancellationToken);
        }

        foreach (var monitor in targets)
        {
            if (cancellationToken.IsCancellationRequested || monitor.IsRetired)
            {
                break;
            }

            // Re-acquired per model so the sweep stays interruptible.
            using var lease = await context.Gate.AcquireAsync(GatePriority.Manual, cancellationToken);
            var outcome = await monitor.RunCheckAsync(CheckTrigger.Manual, lease, cancellationToken);

            if (outcome.ShouldStartRecording)
            {
                await context.Recording.StartAsync(monitor.Favorite, lease, cancellationToken);
            }

            // Unconditional: RunCheckAsync's own finally already recomputed NextDueAt, but it
            // does not Nudge() the model's automatic loop. Without this, a loop that was asleep
            // waiting for IsCheckInFlight to clear only wakes up on its own ParkedPollInterval
            // timer (up to a minute later) instead of resuming right away.
            monitor.Reschedule();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var monitor in monitors.Values)
        {
            monitor.Retire();
        }

        await StopAsync();
        monitors.Clear();
    }
}
