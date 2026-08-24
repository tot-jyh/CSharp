namespace Hunbjter;

public sealed class PandaLiveStatus
{
    public bool Success { get; init; }

    public bool IsLive { get; init; }

    public string Message { get; init; } = "";

    public string UserId { get; init; } = "";

    public string Nickname { get; init; } = "";

    public string Title { get; init; } = "";

    public int Width { get; init; }

    public int Height { get; init; }

    public string StreamUrl { get; init; } = "";
}
