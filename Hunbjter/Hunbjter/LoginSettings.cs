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
}
