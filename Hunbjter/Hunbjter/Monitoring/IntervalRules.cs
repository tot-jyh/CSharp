namespace Hunbjter;

/// <summary>
/// One scheduling policy. Rules are evaluated in order and the first non-null interval wins,
/// so adding "quiet hours" or "weekends only" later is a new class here and nothing else.
/// </summary>
public interface IIntervalRule
{
    string Name { get; }

    TimeSpan? Evaluate(ModelMonitor monitor, DateTimeOffset now);
}

/// <summary>The model's own interval, when the user has set one.</summary>
public sealed class PerModelIntervalRule : IIntervalRule
{
    public string Name => "모델 설정";

    public TimeSpan? Evaluate(ModelMonitor monitor, DateTimeOffset now)
    {
        var seconds = monitor.Favorite.CheckIntervalSeconds;
        return seconds is > 0
            ? TimeSpan.FromSeconds(Math.Clamp(seconds.Value, ModelMonitor.MinimumIntervalSeconds, ModelMonitor.MaximumIntervalSeconds))
            : null;
    }
}

/// <summary>
/// A paid-entry room: the stream exists but needs a ticket, so it is worth re-checking soon.
/// </summary>
public sealed class PaidRoomRetryRule : IIntervalRule
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    public string Name => "풀방 재시도";

    public TimeSpan? Evaluate(ModelMonitor monitor, DateTimeOffset now)
    {
        return PandaMessages.IsPaidRoomTicket(monitor.StatusMessage) ? Interval : null;
    }
}

/// <summary>Seen live very recently but currently not live — likely a brief drop, so poll faster.</summary>
public sealed class RecentlySeenRule : IIntervalRule
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(20);
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public string Name => "최근 방송";

    public TimeSpan? Evaluate(ModelMonitor monitor, DateTimeOffset now)
    {
        if (monitor.State == LiveState.Live)
        {
            return null;
        }

        return monitor.LastSeenLiveAt is { } seen && seen >= now - Window ? Interval : null;
    }
}

/// <summary>
/// Backs a repeatedly failing model off instead of hammering it at full rate forever.
/// </summary>
public sealed class FailureBackoffRule : IIntervalRule
{
    public static readonly TimeSpan Ceiling = TimeSpan.FromMinutes(30);

    public string Name => "실패 대기";

    public TimeSpan? Evaluate(ModelMonitor monitor, DateTimeOffset now)
    {
        if (monitor.ConsecutiveFailures <= 0)
        {
            return null;
        }

        var multiplier = 1 << Math.Min(monitor.ConsecutiveFailures, 4);
        var backoff = monitor.ConfiguredInterval * multiplier;
        return backoff > Ceiling ? Ceiling : backoff;
    }
}
