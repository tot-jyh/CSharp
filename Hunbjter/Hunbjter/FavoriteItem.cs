namespace Hunbjter;

public sealed class FavoriteItem
{
    public string Id { get; set; } = "";

    public string Platform { get; set; } = "";

    public string PlatformUserId { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string ProfileUrl { get; set; } = "";

    public string ThumbnailUrl { get; set; } = "";

    public string Memo { get; set; } = "";

    public List<string> Tags { get; set; } = [];

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// True when the user manually clicked "녹화 종료" while watched/live - keeps the automatic
    /// recheck paths (loop, after-recording-exit, manual check) from immediately restarting
    /// recording. Cleared by a manual "녹화 시작" or by toggling Watch off/on. Does NOT apply to
    /// the offline-backoff auto-stop (StopForOfflineBroadcast), which should keep auto-resuming.
    /// </summary>
    public bool RecordingPaused { get; set; }

    /// <summary>
    /// Per-model check interval. Null means "use the interval from 환경설정".
    /// Additive only: older favorites.json files simply leave this null.
    /// </summary>
    public int? CheckIntervalSeconds { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? LastSeenAt { get; set; }

    public DateTimeOffset? LastLiveAt { get; set; }

    public string LastBroadcastTitle { get; set; } = "";

    public string LastKnownUrl { get; set; } = "";

    public Dictionary<string, string> Metadata { get; set; } = [];
}
