using System.Text.Json;

namespace Hunbjter;

public sealed class SiteSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string settingsPath;

    public SiteSettingsStore()
    {
        settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hunbjter",
            "site-settings.json");
    }

    public SiteSettingsDocument Load()
    {
        if (!File.Exists(settingsPath))
        {
            return new SiteSettingsDocument();
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<SiteSettingsDocument>(json) ?? new SiteSettingsDocument();
        }
        catch
        {
            return new SiteSettingsDocument();
        }
    }

    public void Save(SiteSettingsDocument document)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(settingsPath, JsonSerializer.Serialize(document, JsonOptions));
    }
}
