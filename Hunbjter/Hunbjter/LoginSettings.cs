namespace Hunbjter;

public sealed class LoginSettings
{
    public string LoginUrl { get; set; } = "";

    public string UserId { get; set; } = "";

    public string EncryptedPassword { get; set; } = "";

    public string RecordingDirectory { get; set; } = "";

    public string FfmpegPath { get; set; } = "";

    public int ModelCheckIntervalSeconds { get; set; } = 300;

    public int RecordingStopAfterOfflineChecks { get; set; } = 2;

    public int HighlightCaptureSeconds { get; set; } = 60;

    /// <summary>
    /// Main window's last normal (non-maximized/minimized) position. Null means "never saved
    /// yet" (fresh install or an older settings.json) - Form1 leaves the OS to pick a default
    /// position in that case instead of forcing (0,0).
    /// </summary>
    public int? WindowX { get; set; }

    public int? WindowY { get; set; }
}
