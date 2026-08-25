namespace Hunbjter;

/// <summary>
/// Maps the status text produced by <c>Form1.GetFavoriteStatusText</c> onto badge colors.
/// Keeping this separate means the status wording stays in one place and only the
/// presentation lives here.
/// </summary>
internal static class StatusVisual
{
    public static (Color Foreground, Color Background) ForStatus(string text)
    {
        return text switch
        {
            "방송중" => Tint(Theme.Live),
            "OFF LINE" => Tint(Theme.Offline),
            "watch-off" => (Theme.TextMuted, Theme.SurfaceAlt),
            "로그인 체크" => Tint(Theme.Warning),
            "확인실패" => Tint(Theme.Danger),
            "미지원" => (Theme.TextMuted, Theme.SurfaceAlt),
            "-" or "" => (Theme.TextMuted, Color.Transparent),

            // Anything else is a message straight from the site, e.g. "풀방 입장권 ...".
            _ => Tint(Theme.Warning)
        };
    }

    private static (Color Foreground, Color Background) Tint(Color color)
    {
        return (color, Theme.Blend(color, Theme.Surface, 0.16));
    }
}
