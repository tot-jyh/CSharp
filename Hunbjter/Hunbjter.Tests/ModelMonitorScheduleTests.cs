using Hunbjter;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Hunbjter.Tests;

/// <summary>
/// Scheduling policy, exercised entirely offline: no browser, no network, no wall-clock waits.
/// These are the rules that used to live as four tangled helpers on Form1.
/// </summary>
public sealed class ModelMonitorScheduleTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 24, 12, 0, 0, TimeSpan.FromHours(9));

    private sealed class ScriptedProbe : ILiveStatusProbe
    {
        public PandaLiveStatus Next { get; set; } = new() { Success = true, IsLive = false, Message = "오프라인" };

        public int ProbeCount { get; private set; }

        /// <summary>Optional hook a test can use to hold ProbeAsync open, simulating a slow check.</summary>
        public Func<Task>? BeforeProbe { get; set; }

        public async Task<PandaLiveStatus> ProbeAsync(WebViewLease lease, FavoriteItem favorite, CancellationToken cancellationToken)
        {
            ProbeCount++;

            if (BeforeProbe is { } beforeProbe)
            {
                await beforeProbe();
            }

            return Next;
        }

        public Task PrepareSessionAsync(WebViewLease lease, string reason, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeRecorder : IRecordingCoordinator
    {
        public HashSet<string> Recording { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Started { get; } = [];

        public List<string> StoppedOffline { get; } = [];

        public bool IsRecording(string modelId) => Recording.Contains(modelId);

        public Task StartAsync(FavoriteItem favorite, WebViewLease lease, CancellationToken cancellationToken)
        {
            Started.Add(favorite.Id);
            Recording.Add(favorite.Id);
            return Task.CompletedTask;
        }

        public void StopForOfflineBroadcast(FavoriteItem favorite)
        {
            StoppedOffline.Add(favorite.Id);
            Recording.Remove(favorite.Id);
        }
    }

    private sealed class Harness
    {
        public FakeTimeProvider Clock { get; } = new(Start);

        public ScriptedProbe Probe { get; } = new();

        public FakeRecorder Recorder { get; } = new();

        public LoginSettings Settings { get; } = new() { ModelCheckIntervalSeconds = 300, RecordingStopAfterOfflineChecks = 2 };

        /// <summary>Mirrors MonitorRoster.IsSuspended - true while a modal management dialog is open.</summary>
        public bool Suspended { get; set; }

        public FavoriteItem Favorite { get; } = new()
        {
            Id = "팬더:cuee66",
            Platform = "팬더",
            PlatformUserId = "cuee66",
            DisplayName = "루미",
            ProfileUrl = "https://www.pandalive.co.kr/cuee66",
            Enabled = true
        };

        public ModelMonitor CreateMonitor()
        {
            var context = new MonitorContext(
                Probe,
                new WebViewGate(() => null!),
                Recorder,
                [
                    new PerModelIntervalRule(),
                    new PaidRoomRetryRule(),
                    new RecentlySeenRule(),
                    new FailureBackoffRule()
                ],
                () => Settings,
                Clock)
            {
                IsSuspended = () => Suspended
            };

            return new ModelMonitor(Favorite, context);
        }

        /// <summary>Runs one check with a lease, exactly as the loop would.</summary>
        public async Task<CheckOutcome> CheckAsync(ModelMonitor monitor)
        {
            var gate = new WebViewGate(() => null!);
            using var lease = await gate.AcquireAsync(GatePriority.Manual);
            return await monitor.RunCheckAsync(CheckTrigger.Automatic, lease, CancellationToken.None);
        }
    }

    [Fact]
    public async Task OfflineModelFallsBackToTheConfiguredInterval()
    {
        var harness = new Harness();
        var monitor = harness.CreateMonitor();

        await harness.CheckAsync(monitor);

        Assert.Equal(LiveState.Offline, monitor.State);
        Assert.Equal(TimeSpan.FromSeconds(300), monitor.CurrentInterval);
        Assert.Equal("기본", monitor.ScheduleReason);
    }

    [Fact]
    public async Task PaidRoomMessageRetriesEveryTenSeconds()
    {
        var harness = new Harness();
        harness.Probe.Next = new PandaLiveStatus
        {
            Success = false,
            Message = "팬방송 입장을 위해 하트 130개를 사용하시겠습니까? 풀방 입장권"
        };

        var monitor = harness.CreateMonitor();
        await harness.CheckAsync(monitor);

        Assert.Equal(PaidRoomRetryRule.Interval, monitor.CurrentInterval);
        Assert.Equal("풀방 재시도", monitor.ScheduleReason);
    }

    [Fact]
    public async Task RecentlyLiveButNowOfflinePollsEveryThirtySeconds()
    {
        var harness = new Harness();
        var monitor = harness.CreateMonitor();

        harness.Probe.Next = new PandaLiveStatus { Success = true, IsLive = true, Message = "방송중", Width = 1920, Height = 1080 };
        await harness.CheckAsync(monitor);
        Assert.Equal(LiveState.Live, monitor.State);

        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        harness.Probe.Next = new PandaLiveStatus { Success = true, IsLive = false, Message = "오프라인" };
        await harness.CheckAsync(monitor);

        Assert.Equal(RecentlySeenRule.Interval, monitor.CurrentInterval);
        Assert.Equal("최근 방송", monitor.ScheduleReason);
    }

    [Fact]
    public async Task ARecentSightingStopsCountingOnceTheWindowPasses()
    {
        var harness = new Harness();
        var monitor = harness.CreateMonitor();

        harness.Probe.Next = new PandaLiveStatus { Success = true, IsLive = true, Message = "방송중" };
        await harness.CheckAsync(monitor);

        harness.Clock.Advance(RecentlySeenRule.Window + TimeSpan.FromMinutes(1));
        harness.Probe.Next = new PandaLiveStatus { Success = true, IsLive = false, Message = "오프라인" };
        await harness.CheckAsync(monitor);

        Assert.Equal("기본", monitor.ScheduleReason);
    }

    [Fact]
    public async Task PerModelIntervalOutranksEverythingElse()
    {
        var harness = new Harness();
        harness.Favorite.CheckIntervalSeconds = 45;

        var monitor = harness.CreateMonitor();
        await harness.CheckAsync(monitor);

        Assert.Equal(TimeSpan.FromSeconds(45), monitor.CurrentInterval);
        Assert.Equal("모델 설정", monitor.ScheduleReason);
    }

    [Fact]
    public async Task PerModelIntervalIsClampedToTheAllowedRange()
    {
        var harness = new Harness();
        harness.Favorite.CheckIntervalSeconds = 1;

        var monitor = harness.CreateMonitor();
        await harness.CheckAsync(monitor);

        Assert.Equal(TimeSpan.FromSeconds(ModelMonitor.MinimumIntervalSeconds), monitor.CurrentInterval);
    }

    [Fact]
    public async Task RepeatedFailuresBackOffExponentiallyUpToACeiling()
    {
        var harness = new Harness();
        harness.Probe.Next = new PandaLiveStatus { Success = false, Message = "확인 실패" };

        var monitor = harness.CreateMonitor();

        await harness.CheckAsync(monitor);
        Assert.Equal(TimeSpan.FromSeconds(600), monitor.CurrentInterval);
        Assert.Equal("실패 대기", monitor.ScheduleReason);

        await harness.CheckAsync(monitor);
        Assert.Equal(TimeSpan.FromSeconds(1200), monitor.CurrentInterval);

        for (var i = 0; i < 6; i++)
        {
            await harness.CheckAsync(monitor);
        }

        Assert.Equal(FailureBackoffRule.Ceiling, monitor.CurrentInterval);
    }

    [Fact]
    public async Task ASuccessfulCheckClearsTheBackoff()
    {
        var harness = new Harness();
        harness.Probe.Next = new PandaLiveStatus { Success = false, Message = "확인 실패" };
        var monitor = harness.CreateMonitor();
        await harness.CheckAsync(monitor);
        Assert.Equal("실패 대기", monitor.ScheduleReason);

        harness.Probe.Next = new PandaLiveStatus { Success = true, IsLive = false, Message = "오프라인" };
        await harness.CheckAsync(monitor);

        Assert.Equal(0, monitor.ConsecutiveFailures);
        Assert.Equal("기본", monitor.ScheduleReason);
    }

    [Fact]
    public void WatchOffParksTheModel()
    {
        var harness = new Harness();
        harness.Favorite.Enabled = false;

        var monitor = harness.CreateMonitor();

        Assert.True(monitor.IsParked);
        Assert.Equal("Watch Off", monitor.ScheduleReason);
    }

    [Fact]
    public void ARecordingModelIsParkedBecauseFfmpegIsTheSourceOfTruth()
    {
        var harness = new Harness();
        harness.Recorder.Recording.Add(harness.Favorite.Id);

        var monitor = harness.CreateMonitor();

        Assert.True(monitor.IsParked);
        Assert.Equal("녹화중", monitor.ScheduleReason);
    }

    /// <summary>
    /// Regression: the old code returned before stamping lastCheckedAt for a non-panda model, so
    /// it re-checked and re-logged on every single tick, forever.
    /// </summary>
    [Fact]
    public async Task UnsupportedPlatformIsParkedInsteadOfRecheckingForever()
    {
        var harness = new Harness();
        harness.Favorite.Platform = "치지직";
        harness.Favorite.ProfileUrl = "https://chzzk.naver.com/abc";

        var monitor = harness.CreateMonitor();
        await harness.CheckAsync(monitor);

        Assert.Equal(LiveState.Unsupported, monitor.State);
        Assert.True(monitor.IsParked);
        Assert.Equal("미지원", monitor.ScheduleReason);
        Assert.Equal(0, harness.Probe.ProbeCount);
    }

    [Fact]
    public async Task GoingLiveAsksForRecordingToStart()
    {
        var harness = new Harness();
        harness.Probe.Next = new PandaLiveStatus { Success = true, IsLive = true, Message = "방송중", Width = 1280, Height = 720 };

        var monitor = harness.CreateMonitor();
        var outcome = await harness.CheckAsync(monitor);

        Assert.True(outcome.ShouldStartRecording);
        Assert.True(outcome.StateChanged);
        Assert.Equal("1280x720", monitor.Resolution);
    }

    [Fact]
    public async Task AnAlreadyRecordingModelIsNotAskedToStartAgain()
    {
        var harness = new Harness();
        harness.Recorder.Recording.Add(harness.Favorite.Id);
        harness.Probe.Next = new PandaLiveStatus { Success = true, IsLive = true, Message = "방송중" };

        var monitor = harness.CreateMonitor();
        var outcome = await harness.CheckAsync(monitor);

        Assert.False(outcome.ShouldStartRecording);
    }

    [Fact]
    public async Task RecordingStopsOnlyAfterTheConfiguredNumberOfOfflineChecks()
    {
        var harness = new Harness();
        harness.Recorder.Recording.Add(harness.Favorite.Id);
        harness.Probe.Next = new PandaLiveStatus { Success = true, IsLive = false, Message = "오프라인" };

        var monitor = harness.CreateMonitor();

        await harness.CheckAsync(monitor);
        Assert.Empty(harness.Recorder.StoppedOffline);

        await harness.CheckAsync(monitor);
        Assert.Equal([harness.Favorite.Id], harness.Recorder.StoppedOffline);
    }

    [Fact]
    public async Task StateIsMirroredBackIntoMetadataForTheStoredDocument()
    {
        var harness = new Harness();
        harness.Probe.Next = new PandaLiveStatus { Success = true, IsLive = true, Message = "방송중", Width = 1920, Height = 1080, StreamUrl = "https://example/stream.m3u8" };

        var monitor = harness.CreateMonitor();
        await harness.CheckAsync(monitor);

        Assert.Equal("live", harness.Favorite.Metadata["liveStatus"]);
        Assert.Equal("방송중", harness.Favorite.Metadata["liveMessage"]);
        Assert.Equal("1920x1080", harness.Favorite.Metadata["resolution"]);
        Assert.Equal("https://example/stream.m3u8", harness.Favorite.Metadata["streamUrl"]);
        Assert.True(harness.Favorite.Metadata.ContainsKey("lastCheckedAt"));
    }

    /// <summary>
    /// Regression for a real production bug: while a modal management dialog is open
    /// (MonitorRoster.Suspend), WaitUntilDueAsync used to treat "already overdue" as if it were
    /// not suspended at all and return synchronously with no await anywhere in the call chain.
    /// RunLoopAsync would then see ShouldCheckNow() reject the check and loop straight back to
    /// WaitUntilDueAsync, forever, with zero real await points — which never yields to the
    /// message pump and freezes the whole app for as long as the dialog stays open. Since Start()
    /// invokes the loop synchronously, that bug makes Start() itself hang, which is exactly what
    /// this test would observe as a timeout.
    /// </summary>
    [Fact]
    public async Task SuspensionWaitsInsteadOfBusyLoopingWhenAlreadyOverdue()
    {
        var harness = new Harness();
        var monitor = harness.CreateMonitor();

        // Establish a real LastCheckedAt so the schedule anchors to it (Schedule uses
        // LastCheckedAt ?? now), matching how a model that has already been checked at least
        // once behaves in production.
        await harness.CheckAsync(monitor);
        Assert.Equal(1, harness.Probe.ProbeCount);
        var interval = monitor.CurrentInterval;

        // Move well past NextDueAt (interval plus up to 10% jitter) and suspend before the loop
        // ever gets a chance to run - mirroring a dialog that is opened and left open past the
        // model's next due time.
        harness.Clock.Advance(interval + TimeSpan.FromSeconds(interval.TotalSeconds));
        harness.Suspended = true;

        var started = Task.Run(monitor.Start);
        var startedOrTimedOut = await Task.WhenAny(started, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.True(started.IsCompletedSuccessfully, "Start() hung - WaitUntilDueAsync is busy-looping instead of awaiting.");
        Assert.Same(started, startedOrTimedOut);
        Assert.Equal(1, harness.Probe.ProbeCount); // No automatic check ran while suspended.

        // Clearing suspension must let the still-overdue check run promptly, the same way
        // MonitorRoster.Resume() calls Reschedule() on every monitor.
        var statusChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.StatusChanged += (_, _) => statusChanged.TrySetResult();

        harness.Suspended = false;
        monitor.Reschedule();

        var signaledOrTimedOut = await Task.WhenAny(statusChanged.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(statusChanged.Task, signaledOrTimedOut);
        Assert.Equal(2, harness.Probe.ProbeCount);

        await monitor.DisposeAsync();
    }

    /// <summary>
    /// Regression for the second half of the same freeze family: a concurrent manual check
    /// (MonitorRoster.RunManualAsync calling RunCheckAsync directly) sets IsCheckInFlight on the
    /// monitor while that monitor's own automatic loop is independently trying to run. Before the
    /// fix, WaitUntilDueAsync did not know about IsCheckInFlight, so an already-overdue automatic
    /// loop would return synchronously, get rejected by ShouldCheckNow()'s IsCheckInFlight check,
    /// and loop straight back with zero real awaits - freezing the UI thread for as long as the
    /// concurrent manual check ran (which, with session-failure retries, can be tens of seconds).
    /// </summary>
    [Fact]
    public async Task ConcurrentInFlightCheckDoesNotBusyLoopTheAutomaticLoop()
    {
        var harness = new Harness();
        var monitor = harness.CreateMonitor();

        // Establish a real LastCheckedAt so the schedule anchors to it, then push the clock past
        // it - matching a model whose automatic loop is legitimately due to run right now.
        await harness.CheckAsync(monitor);
        Assert.Equal(1, harness.Probe.ProbeCount);
        harness.Clock.Advance(monitor.CurrentInterval + TimeSpan.FromSeconds(monitor.CurrentInterval.TotalSeconds));

        // Simulate a concurrent manual check that is slow to respond, exactly like a session
        // re-preparation retry would be - held open until the test releases it.
        var releaseManualCheck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Probe.BeforeProbe = () => releaseManualCheck.Task;

        var gate = new WebViewGate(() => null!);
        using var manualLease = await gate.AcquireAsync(GatePriority.Manual);
        var manualCheck = monitor.RunCheckAsync(CheckTrigger.Manual, manualLease, CancellationToken.None);

        // IsCheckInFlight is now true on the monitor, held there by the still-running manual check,
        // which has already made its own probe call (the 2nd overall, after CheckAsync's 1st).
        Assert.True(monitor.IsCheckInFlight);
        Assert.Equal(2, harness.Probe.ProbeCount);

        var started = Task.Run(monitor.Start);
        var startedOrTimedOut = await Task.WhenAny(started, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.True(started.IsCompletedSuccessfully, "Start() hung - the automatic loop is busy-looping against the in-flight manual check.");
        Assert.Same(started, startedOrTimedOut);
        Assert.Equal(2, harness.Probe.ProbeCount); // The automatic loop did not sneak a 3rd probe call in while suspended.

        releaseManualCheck.TrySetResult();
        await manualCheck;

        await monitor.DisposeAsync();
    }

    [Fact]
    public async Task JitterStaysWithinTenPercentAndIsStableForAGivenId()
    {
        var harness = new Harness();
        var monitor = harness.CreateMonitor();
        await harness.CheckAsync(monitor);

        var offset = monitor.NextDueAt - (monitor.LastCheckedAt!.Value + monitor.CurrentInterval);

        Assert.True(offset.Duration() <= monitor.CurrentInterval * 0.1,
            $"jitter {offset} exceeded 10% of {monitor.CurrentInterval}");

        // Same id, fresh monitor: the spread must be reproducible, not random per process.
        var second = new Harness();
        var secondMonitor = second.CreateMonitor();
        await second.CheckAsync(secondMonitor);
        var secondOffset = secondMonitor.NextDueAt - (secondMonitor.LastCheckedAt!.Value + secondMonitor.CurrentInterval);

        Assert.Equal(offset, secondOffset);
    }
}
