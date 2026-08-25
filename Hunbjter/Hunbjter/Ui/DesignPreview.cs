#if DEBUG
using System.Diagnostics;

namespace Hunbjter;

/// <summary>
/// Synthetic data for reviewing the UI without a live pandalive session.
/// Only reachable when HUNBJTER_UI_PREVIEW=1 and only in DEBUG builds — a normal launch
/// never touches this, and it never reads or writes the real favorites store.
/// </summary>
internal static class DesignPreview
{
    public const string EnvironmentFlag = "HUNBJTER_UI_PREVIEW";

    public static bool IsEnabled =>
        Environment.GetEnvironmentVariable(EnvironmentFlag) == "1";

    /// <summary>Ids that should appear as actively recording.</summary>
    public static readonly string[] RecordingIds = ["팬더:yuriyuri413", "팬더:cuee66"];

    public static readonly string[] SampleLog =
    [
        "목록 12개를 불러왔습니다.",
        "앱 시작 방송 확인 중: 7개",
        "하율: 방송중 1920x1080",
        "하율: 최고 화질 녹화 시작 - D:\\Rec\\하율_20260824_094512.ts",
        "루미: 확인 실패 - 팬방송 입장을 위해 하트 130개를 사용하시겠습니까?",
        "백민설: 오프라인",
        "세연: 확인 실패 - 로그인이 필요합니다",
        "정pd: 녹화 종료 (코드 0)",
        "앱 시작 방송 확인 완료: 7개"
    ];

    public static FavoritesDocument CreateFavorites()
    {
        var now = DateTimeOffset.Now;
        var document = new FavoritesDocument();

        document.Items.Add(Build("yuriyuri413", "하율", now, enabled: true,
            liveStatus: "live", liveMessage: "방송중", resolution: "1920x1080", lastSeen: now));

        document.Items.Add(Build("cuee66", "루미", now, enabled: true,
            liveStatus: "live", liveMessage: "방송중", resolution: "1280x720",
            lastSeen: now.AddMinutes(-3)));

        document.Items.Add(Build("onlysu0912", "백민설", now, enabled: true,
            liveStatus: "live", liveMessage: "방송중", resolution: "1920x1080",
            lastSeen: now.AddMinutes(-12)));

        document.Items.Add(Build("dkdlfjqm758", "세연", now, enabled: true,
            liveStatus: "error", liveMessage: "팬방송 입장을 위해 하트 130개를 사용하시겠습니까?",
            lastSeen: now.AddHours(-5)));

        document.Items.Add(Build("s2s20121", "다여운", now, enabled: true,
            liveStatus: "error", liveMessage: "로그인이 필요합니다",
            lastSeen: now.AddHours(-9)));

        document.Items.Add(Build("yoy12345", "유제인", now, enabled: true,
            liveStatus: "offline", liveMessage: "오프라인", lastSeen: now.AddDays(-1)));

        document.Items.Add(Build("wjdqn820", "윤나희", now, enabled: true,
            liveStatus: "offline", liveMessage: "종료된 방송입니다", lastSeen: now.AddDays(-2)));

        document.Items.Add(Build("mini10062", "윤이(괄호포함)", now, enabled: false,
            liveStatus: "offline", liveMessage: "오프라인", lastSeen: now.AddDays(-3)));

        document.Items.Add(Build("charming09", "이유", now, enabled: false,
            liveStatus: "offline", liveMessage: "오프라인", lastSeen: now.AddDays(-4)));

        document.Items.Add(Build("seoltang03", "임설", now, enabled: true,
            liveStatus: "unsupported", liveMessage: "", lastSeen: now.AddDays(-6)));

        document.Items.Add(Build("1makemyself", "정pd", now, enabled: true,
            liveStatus: "error", liveMessage: "확인 실패", lastSeen: now.AddDays(-7)));

        document.Items.Add(Build("znvvlv00", "하예주", now, enabled: true,
            lastSeen: now.AddDays(-9)));

        return document;
    }

    /// <summary>
    /// A session whose process was never started. Nothing on the preview path reads
    /// <see cref="Process.HasExited"/>, and the one caller that would — the shutdown sweep —
    /// already swallows the exception.
    /// </summary>
    public static RecordingSession CreateIdleSession()
    {
        return new RecordingSession(new Process(), Environment.ProcessPath ?? "", "");
    }

    private static FavoriteItem Build(
        string userId,
        string displayName,
        DateTimeOffset now,
        bool enabled = true,
        string liveStatus = "",
        string liveMessage = "",
        string resolution = "",
        DateTimeOffset? lastSeen = null)
    {
        var item = new FavoriteItem
        {
            Id = $"팬더:{userId}",
            Platform = "팬더",
            PlatformUserId = userId,
            DisplayName = displayName,
            ProfileUrl = $"https://www.pandalive.co.kr/{userId}",
            Enabled = enabled,
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now,
            LastSeenAt = lastSeen
        };

        if (!string.IsNullOrEmpty(liveStatus))
        {
            item.Metadata["liveStatus"] = liveStatus;
        }

        if (!string.IsNullOrEmpty(liveMessage))
        {
            item.Metadata["liveMessage"] = liveMessage;
        }

        if (!string.IsNullOrEmpty(resolution))
        {
            item.Metadata["resolution"] = resolution;
        }

        item.Metadata["lastCheckedAt"] = now.AddMinutes(-2).ToString("O");
        return item;
    }
}
#endif
