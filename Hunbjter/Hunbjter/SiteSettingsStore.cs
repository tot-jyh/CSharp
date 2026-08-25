namespace Hunbjter;

public sealed class SiteSettingsStore
{
    private readonly string settingsPath = JsonFileStore.ResolvePath("site-settings.json");

    public string? LastLoadFailure { get; private set; }

    public SiteSettingsDocument Load()
    {
        var document = JsonFileStore.Load(settingsPath, static () => new SiteSettingsDocument(), out var failure);
        LastLoadFailure = failure;
        return document;
    }

    public void Save(SiteSettingsDocument document)
    {
        JsonFileStore.Save(settingsPath, document);
    }
}
