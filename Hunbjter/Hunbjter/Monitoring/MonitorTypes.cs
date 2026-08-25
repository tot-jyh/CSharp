namespace Hunbjter;

/// <summary>Replaces the stringly-typed <c>Metadata["liveStatus"]</c> values.</summary>
public enum LiveState
{
    Unknown,
    Live,
    Offline,
    Error,
    Unsupported
}

public enum CheckTrigger
{
    Automatic,
    Manual,
    Startup,
    NewModel,
    AfterRecordingExit,
    BeforeRecording
}

/// <summary>What a completed check means for the recorder.</summary>
public sealed record CheckOutcome(LiveState State, bool StateChanged, bool ShouldStartRecording)
{
    public static readonly CheckOutcome Skipped = new(LiveState.Unknown, false, false);
}

public sealed class ModelStatusChangedEventArgs(ModelMonitor monitor, bool requiresRegroup) : EventArgs
{
    public ModelMonitor Monitor { get; } = monitor;

    /// <summary>The model crossed the live/standby boundary, so the two grids must be rebuilt.</summary>
    public bool RequiresRegroup { get; } = requiresRegroup;
}

public sealed class MonitorLogEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}

/// <summary>Asks the site whether one model is live.</summary>
public interface ILiveStatusProbe
{
    Task<PandaLiveStatus> ProbeAsync(WebViewLease lease, FavoriteItem favorite, CancellationToken cancellationToken);

    Task PrepareSessionAsync(WebViewLease lease, string reason, CancellationToken cancellationToken);
}

/// <summary>
/// The recorder, as the monitor sees it. Keeping this an interface stops the monitor from
/// reaching into Form1 — and stops the recorder from ever opening a modal dialog on its behalf.
/// </summary>
public interface IRecordingCoordinator
{
    bool IsRecording(string modelId);

    Task StartAsync(FavoriteItem favorite, WebViewLease lease, CancellationToken cancellationToken);

    void StopForOfflineBroadcast(FavoriteItem favorite);
}

/// <summary>Everything a <see cref="ModelMonitor"/> needs, injected so it can be tested headlessly.</summary>
public sealed record MonitorContext(
    ILiveStatusProbe Probe,
    WebViewGate Gate,
    IRecordingCoordinator Recording,
    IReadOnlyList<IIntervalRule> Rules,
    Func<LoginSettings> GetSettings,
    TimeProvider Clock)
{
    /// <summary>
    /// Suspends automatic checks while a modal management dialog owns the settings files.
    /// Without this, a check could write favorites.json between the dialog's load and its save.
    /// </summary>
    public Func<bool> IsSuspended { get; init; } = static () => false;
}
