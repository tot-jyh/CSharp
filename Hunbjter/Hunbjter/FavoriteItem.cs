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

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? LastSeenAt { get; set; }

    public DateTimeOffset? LastLiveAt { get; set; }

    public string LastBroadcastTitle { get; set; } = "";

    public string LastKnownUrl { get; set; } = "";

    public Dictionary<string, string> Metadata { get; set; } = [];
}
