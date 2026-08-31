namespace Hunbjter;

/// <summary>
/// Platform and message predicates for pandalive (and, where noted, stripchat). Pure string
/// logic in one place, so the monitor, the recorder and the view all agree on what a given site
/// message means.
/// </summary>
internal static class PandaMessages
{
    public static bool IsPandaPlatform(string platform, string profileUrl)
    {
        return platform.Contains("팬더", StringComparison.OrdinalIgnoreCase)
            || platform.Contains("panda", StringComparison.OrdinalIgnoreCase)
            || profileUrl.Contains("pandalive.co.kr", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsStripchatPlatform(string platform, string profileUrl)
    {
        return platform.Contains("스챗", StringComparison.OrdinalIgnoreCase)
            || platform.Contains("stripchat", StringComparison.OrdinalIgnoreCase)
            || profileUrl.Contains("stripchat.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAdultSessionDelay(string message)
    {
        return message.Contains("성인", StringComparison.OrdinalIgnoreCase)
            || message.Contains("adult", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The session looks stale or unauthorized, so re-preparing it is worth a retry.</summary>
    public static bool IsSessionRelatedFailure(string message)
    {
        return IsAdultSessionDelay(message)
            || message.Contains("로그인", StringComparison.OrdinalIgnoreCase)
            || message.Contains("login", StringComparison.OrdinalIgnoreCase)
            || message.Contains("권한", StringComparison.OrdinalIgnoreCase)
            || message.Contains("인증", StringComparison.OrdinalIgnoreCase)
            || message.Contains("403", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsOfflineBroadcast(string message)
    {
        return message.Contains("종료된 방송", StringComparison.OrdinalIgnoreCase)
            || message.Contains("종료되거나", StringComparison.OrdinalIgnoreCase)
            || message.Contains("offline", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A paid-entry room: the stream exists but needs a ticket, so retry sooner.</summary>
    public static bool IsPaidRoomTicket(string message)
    {
        return message.Contains("풀방 입장권", StringComparison.OrdinalIgnoreCase)
            || message.Contains("풀방입장권", StringComparison.OrdinalIgnoreCase);
    }

    public static string HostForLog(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "unknown";
    }
}
