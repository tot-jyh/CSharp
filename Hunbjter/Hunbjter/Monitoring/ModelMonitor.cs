namespace Hunbjter;

/// <summary>
/// One model's monitor: it owns that model's runtime state, decides its own next check time,
/// and runs its own wait loop.
///
/// The loop is an <c>async</c> state machine, not a thread. Every check funnels through
/// <see cref="WebViewGate"/> because the whole app shares a single WebView2, so real
/// concurrency here would be incorrect rather than merely slow. Continuations must resume on
/// the UI thread — there is deliberately no <c>ConfigureAwait(false)</c> anywhere in this file.
/// </summary>
public sealed class ModelMonitor : IAsyncDisposable
{
    public const int MinimumIntervalSeconds = 10;
    public const int MaximumIntervalSeconds = 86400;
    public const int DefaultIntervalSeconds = 300;

    /// <summary>A single check may not hold the shared WebView2 longer than this.</summary>
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(45);

    /// <summary>Parked models still re-evaluate periodically so they recover without an explicit poke.</summary>
    private static readonly TimeSpan ParkedPollInterval = TimeSpan.FromSeconds(60);

    private readonly MonitorContext context;

    private TaskCompletionSource wake = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? loopCancellation;
    private Task? loop;

    public ModelMonitor(FavoriteItem favorite, MonitorContext context)
    {
        Favorite = favorite;
        this.context = context;

        HydrateFromFavorite();
        ComputeNextDue();
    }

    public event EventHandler<ModelStatusChangedEventArgs>? StatusChanged;

    public event EventHandler<MonitorLogEventArgs>? LogRequested;

    public string Id => Favorite.Id;

    public FavoriteItem Favorite { get; private set; }

    public LiveState State { get; private set; }

    public string StatusMessage { get; private set; } = "";

    public string StreamUrl { get; private set; } = "";

    public string Resolution { get; private set; } = "";

    public DateTimeOffset? LastCheckedAt { get; private set; }

    public DateTimeOffset? LastSeenLiveAt { get; private set; }

    public int OfflineStrikes { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public bool IsCheckInFlight { get; private set; }

    public bool IsRetired { get; private set; }

    public bool IsParked { get; private set; }

    public TimeSpan CurrentInterval { get; private set; }

    public DateTimeOffset NextDueAt { get; private set; }

    /// <summary>Why the current interval was chosen — surfaced in the log so cadence is explainable.</summary>
    public string ScheduleReason { get; private set; } = "";

    public bool IsRecording => context.Recording.IsRecording(Id);

    public bool IsLive => State == LiveState.Live;

    /// <summary>The global fallback interval, used when no rule claims this model.</summary>
    public TimeSpan ConfiguredInterval
    {
        get
        {
            var configured = context.GetSettings().ModelCheckIntervalSeconds;
            var seconds = configured > 0 ? configured : DefaultIntervalSeconds;
            return TimeSpan.FromSeconds(Math.Clamp(seconds, MinimumIntervalSeconds, MaximumIntervalSeconds));
        }
    }

    // ---------------------------------------------------------------- lifecycle

    public void Start()
    {
        if (loop is not null || IsRetired)
        {
            return;
        }

        loopCancellation = new CancellationTokenSource();
        loop = RunLoopAsync(loopCancellation.Token);
    }

    /// <summary>Cancels and abandons. Callers must not block the UI thread waiting for this.</summary>
    public async Task StopAsync()
    {
        loopCancellation?.Cancel();
        Nudge();

        if (loop is { } running)
        {
            try
            {
                await running;
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        loop = null;
        loopCancellation?.Dispose();
        loopCancellation = null;
    }

    public void Retire()
    {
        IsRetired = true;
        loopCancellation?.Cancel();
        Nudge();
    }

    public async ValueTask DisposeAsync()
    {
        Retire();
        await StopAsync();
    }

    /// <summary>Wakes the loop now instead of at <see cref="NextDueAt"/>.</summary>
    public void RequestImmediate(string reason)
    {
        if (!string.IsNullOrEmpty(reason))
        {
            ScheduleReason = reason;
        }

        NextDueAt = context.Clock.GetLocalNow();
        IsParked = false;
        Nudge();
    }

    /// <summary>
    /// Points this monitor at the freshly loaded item for the same model, keeping runtime state.
    /// The management dialogs reload the whole document, so object identity changes even when
    /// nothing about the model did.
    /// </summary>
    public void Rebind(FavoriteItem favorite)
    {
        if (!Id.Equals(favorite.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"모니터 대상이 다릅니다: {Id} != {favorite.Id}", nameof(favorite));
        }

        Favorite = favorite;
        SyncToFavorite();
        Reschedule();
    }

    /// <summary>Re-evaluates the schedule after something outside the loop changed (watch toggle, recording state).</summary>
    public void Reschedule()
    {
        ComputeNextDue();
        Nudge();
    }

    // ---------------------------------------------------------------- the loop

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !IsRetired)
        {
            try
            {
                await WaitUntilDueAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested || IsRetired || !ShouldCheckNow())
                {
                    continue;
                }

                using var perCheck = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                perCheck.CancelAfter(CheckTimeout);

                using var lease = await context.Gate.AcquireAsync(GatePriority.Normal, perCheck.Token);

                // The roster can retire this monitor while it waits at the gate.
                if (IsRetired)
                {
                    continue;
                }

                var outcome = await RunCheckAsync(CheckTrigger.Automatic, lease, perCheck.Token);

                if (outcome.ShouldStartRecording && !IsRetired)
                {
                    await context.Recording.StartAsync(Favorite, lease, perCheck.Token);
                    Reschedule();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                // The per-check timeout fired. RunCheckAsync already recorded the failure.
            }
            catch (Exception ex)
            {
                RaiseLog($"{Favorite.DisplayName}: 확인 처리 오류 - {ex.Message}");
                RegisterFailure();
            }
        }
    }

    private async Task WaitUntilDueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !IsRetired)
        {
            var now = context.Clock.GetLocalNow();

            // Every one of these must match what ShouldCheckNow() below will reject, or the
            // same freeze recurs: if this returns "due" while ShouldCheckNow() is about to say
            // no, RunLoopAsync just loops straight back here with zero real awaits anywhere in
            // the cycle, which never yields to the message pump and hangs the whole app.
            //  - IsParked / IsSuspended: a modal management dialog is open.
            //  - IsCheckInFlight: this exact model is already being checked *right now* by a
            //    concurrent manual "방송 확인" (MonitorRoster.RunManualAsync calls RunCheckAsync
            //    directly, independently of this loop). That check can legitimately run for tens
            //    of seconds - retries, timeouts - and this loop would otherwise spin the whole
            //    time waiting for it to clear.
            var waiting = IsParked || context.IsSuspended() || IsCheckInFlight;

            if (!waiting && now >= NextDueAt)
            {
                return;
            }

            var remaining = waiting ? ParkedPollInterval : NextDueAt - now;
            if (remaining > ParkedPollInterval)
            {
                // Wake early enough that a settings change takes effect without a restart.
                remaining = ParkedPollInterval;
            }

            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            var pending = Volatile.Read(ref wake);
            var delay = Task.Delay(remaining, context.Clock, cancellationToken);
            await Task.WhenAny(delay, pending.Task);

            if (pending.Task.IsCompleted)
            {
                Interlocked.CompareExchange(
                    ref wake,
                    new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                    pending);
            }

            // A parked model re-evaluates in case Watch was toggled or recording finished.
            if (IsParked)
            {
                ComputeNextDue();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void Nudge() => Volatile.Read(ref wake).TrySetResult();

    private bool ShouldCheckNow()
    {
        return Favorite.Enabled && !IsRecording && !IsCheckInFlight && !context.IsSuspended();
    }

    // ---------------------------------------------------------------- the check

    public async Task<CheckOutcome> RunCheckAsync(CheckTrigger trigger, WebViewLease lease, CancellationToken cancellationToken)
    {
        if (IsRetired)
        {
            return CheckOutcome.Skipped;
        }

        if (!PandaMessages.IsPandaPlatform(Favorite.Platform, Favorite.ProfileUrl)
            && !PandaMessages.IsStripchatPlatform(Favorite.Platform, Favorite.ProfileUrl))
        {
            return MarkUnsupported();
        }

        var wasLive = IsLive;
        IsCheckInFlight = true;

        try
        {
            var status = await context.Probe.ProbeAsync(lease, Favorite, cancellationToken);
            return ApplyStatus(status, wasLive);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LastCheckedAt = context.Clock.GetLocalNow();
            RegisterFailure();
            SetErrorState("확인 시간 초과");
            RaiseLog($"{Favorite.DisplayName}: 확인 시간 초과");
            return Finish(wasLive, false);
        }
        catch (Exception ex)
        {
            LastCheckedAt = context.Clock.GetLocalNow();
            RegisterFailure();
            SetErrorState(ex.Message);
            RaiseLog($"{Favorite.DisplayName}: 확인 실패 - {ex.Message}");
            return Finish(wasLive, false);
        }
        finally
        {
            IsCheckInFlight = false;

            // Note: LastCheckedAt is stamped on each path *before* Finish, because Finish
            // mirrors state into Metadata — doing it here would leave the stored lastCheckedAt
            // one check behind.
            ComputeNextDue();
        }
    }

    private CheckOutcome ApplyStatus(PandaLiveStatus status, bool wasLive)
    {
        var now = context.Clock.GetLocalNow();
        LastCheckedAt = now;
        StatusMessage = status.Message;

        if (!status.Success)
        {
            // pandalive answers "종료된 방송입니다" (ended broadcast) with result:false - a real
            // API-level failure shape, but it just means the model isn't broadcasting right now,
            // not that anything actually went wrong. Treating it as a genuine failure fed
            // ConsecutiveFailures into FailureBackoffRule's exponential wait (up to 30 minutes),
            // so a model that came back live only a few minutes after RecentlySeenRule's 20-
            // minute window closed could sit unchecked long enough to miss the restart entirely.
            if (PandaMessages.IsOfflineBroadcast(status.Message))
            {
                ConsecutiveFailures = 0;
                State = LiveState.Offline;
                ClearPlayback();
                RaiseLog($"{Favorite.DisplayName}: 오프라인 ({status.Message})");
                ApplyOfflineStrike();
                return Finish(wasLive, false);
            }

            RegisterFailure();
            State = LiveState.Error;
            ClearPlayback();
            RaiseLog($"{Favorite.DisplayName}: 확인 실패 - {status.Message}");
            ApplyOfflineStrike();
            return Finish(wasLive, false);
        }

        ConsecutiveFailures = 0;

        var resolution = status.Width > 0 && status.Height > 0 ? $"{status.Width}x{status.Height}" : "";

        if (status.IsLive)
        {
            State = LiveState.Live;
            StreamUrl = status.StreamUrl;
            Resolution = resolution;
            OfflineStrikes = 0;
            LastSeenLiveAt = now;
            Favorite.LastSeenAt = now;
            Favorite.LastLiveAt = now;
        }
        else
        {
            State = LiveState.Offline;
            ClearPlayback();
            ApplyOfflineStrike();
        }

        if (!string.IsNullOrWhiteSpace(status.Title))
        {
            Favorite.LastBroadcastTitle = status.Title;
        }

        RaiseLog($"{Favorite.DisplayName}: {(status.IsLive ? "방송중" : "오프라인")} {resolution}".TrimEnd());

        var shouldStart = status.IsLive && !IsRecording && Favorite.Enabled && !Favorite.RecordingPaused;
        return Finish(wasLive, shouldStart);
    }

    /// <summary>
    /// A recording only stops after the site has reported not-live several times in a row, so a
    /// single hiccup does not kill an in-progress capture.
    /// </summary>
    private void ApplyOfflineStrike()
    {
        if (!IsRecording)
        {
            return;
        }

        OfflineStrikes++;

        var configured = context.GetSettings().RecordingStopAfterOfflineChecks;
        var threshold = Math.Clamp(configured > 0 ? configured : 2, 1, 10);

        RaiseLog($"{Favorite.DisplayName}: 방송 종료 판단 {OfflineStrikes}/{threshold}");

        if (OfflineStrikes >= threshold)
        {
            context.Recording.StopForOfflineBroadcast(Favorite);
            RaiseLog($"{Favorite.DisplayName}: 방송 종료 판단으로 녹화 종료");
        }
    }

    private CheckOutcome MarkUnsupported()
    {
        var changed = State != LiveState.Unsupported;

        State = LiveState.Unsupported;
        StatusMessage = "";
        ClearPlayback();
        LastCheckedAt = context.Clock.GetLocalNow();

        if (changed)
        {
            // Logged once rather than on every tick: the old code returned before stamping
            // lastCheckedAt, so an unsupported model re-checked forever.
            RaiseLog($"지원하지 않는 사이트: {Favorite.DisplayName} / {Favorite.Platform}");
        }

        ComputeNextDue();
        RaiseStatusChanged(changed);
        return new CheckOutcome(State, changed, false);
    }

    private CheckOutcome Finish(bool wasLive, bool shouldStartRecording)
    {
        var changed = wasLive != IsLive;
        RaiseStatusChanged(changed);
        return new CheckOutcome(State, changed, shouldStartRecording);
    }

    private void SetErrorState(string message)
    {
        State = LiveState.Error;
        StatusMessage = message;
        ClearPlayback();
    }

    private void RegisterFailure()
    {
        if (ConsecutiveFailures < int.MaxValue)
        {
            ConsecutiveFailures++;
        }
    }

    private void ClearPlayback()
    {
        StreamUrl = "";
        Resolution = "";
    }

    // ---------------------------------------------------------------- scheduling

    private void ComputeNextDue()
    {
        var now = context.Clock.GetLocalNow();

        if (!Favorite.Enabled)
        {
            Park("Watch Off");
            return;
        }

        if (IsRecording)
        {
            // While ffmpeg is running it, not the site, is the source of truth.
            Park("녹화중");
            return;
        }

        if (State == LiveState.Unsupported)
        {
            Park("미지원");
            return;
        }

        foreach (var rule in context.Rules)
        {
            if (rule.Evaluate(this, now) is { } interval)
            {
                Schedule(interval, rule.Name, now);
                return;
            }
        }

        Schedule(ConfiguredInterval, "기본", now);
    }

    private void Park(string reason)
    {
        IsParked = true;
        ScheduleReason = reason;
        CurrentInterval = Timeout.InfiniteTimeSpan;
        NextDueAt = DateTimeOffset.MaxValue;
    }

    private void Schedule(TimeSpan interval, string reason, DateTimeOffset now)
    {
        IsParked = false;
        CurrentInterval = interval;
        ScheduleReason = reason;
        NextDueAt = (LastCheckedAt ?? now) + interval + Jitter(interval);
    }

    /// <summary>
    /// Deterministic ±10% spread derived from the id, so a roster that all starts at once does
    /// not converge into one thundering herd every interval.
    /// </summary>
    private TimeSpan Jitter(TimeSpan interval)
    {
        var offset = (StableHash(Id) % 201) / 100.0 - 1.0;
        return TimeSpan.FromMilliseconds(interval.TotalMilliseconds * 0.1 * offset);
    }

    /// <summary>FNV-1a: unlike string.GetHashCode it is stable across processes, so tests are repeatable.</summary>
    private static uint StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }

    // ---------------------------------------------------------------- persistence bridge

    /// <summary>Reads the runtime state that survives in the stored document.</summary>
    public void HydrateFromFavorite()
    {
        LastSeenLiveAt = Favorite.LastSeenAt;

        if (Favorite.Metadata.TryGetValue("lastCheckedAt", out var raw)
            && DateTimeOffset.TryParse(raw, out var checkedAt))
        {
            LastCheckedAt = checkedAt;
        }
    }

    /// <summary>
    /// Mirrors state back into <see cref="FavoriteItem.Metadata"/>. The view and the JSON format
    /// still read those keys, so this keeps both working while the migration is in progress.
    /// </summary>
    public void SyncToFavorite()
    {
        SetOrRemove("liveStatus", State switch
        {
            LiveState.Live => "live",
            LiveState.Offline => "offline",
            LiveState.Error => "error",
            LiveState.Unsupported => "unsupported",
            _ => null
        });

        SetOrRemove("liveMessage", string.IsNullOrEmpty(StatusMessage) ? null : StatusMessage);
        SetOrRemove("streamUrl", string.IsNullOrEmpty(StreamUrl) ? null : StreamUrl);
        SetOrRemove("resolution", string.IsNullOrEmpty(Resolution) ? null : Resolution);
        SetOrRemove("offlineCheckCount", OfflineStrikes > 0 ? OfflineStrikes.ToString() : null);
        SetOrRemove("lastCheckedAt", LastCheckedAt?.ToString("O"));

        Favorite.UpdatedAt = context.Clock.GetLocalNow();
    }

    private void SetOrRemove(string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            Favorite.Metadata.Remove(key);
            return;
        }

        Favorite.Metadata[key] = value;
    }

    private void RaiseLog(string message) => LogRequested?.Invoke(this, new MonitorLogEventArgs(message));

    private void RaiseStatusChanged(bool requiresRegroup)
    {
        SyncToFavorite();
        StatusChanged?.Invoke(this, new ModelStatusChangedEventArgs(this, requiresRegroup));
    }
}
