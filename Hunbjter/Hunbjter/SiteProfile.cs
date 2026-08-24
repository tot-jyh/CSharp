namespace Hunbjter;

public sealed class SiteProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    public string LoginUrl { get; set; } = "";

    public string UserId { get; set; } = "";

    public string EncryptedPassword { get; set; } = "";
}
