using System.Text.Json;

namespace Hunbjter;

public sealed class FavoriteStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string favoritesPath;

    public FavoriteStore()
    {
        favoritesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hunbjter",
            "favorites.json");
    }

    public FavoritesDocument Load()
    {
        if (!File.Exists(favoritesPath))
        {
            return new FavoritesDocument();
        }

        try
        {
            var json = File.ReadAllText(favoritesPath);
            return JsonSerializer.Deserialize<FavoritesDocument>(json) ?? new FavoritesDocument();
        }
        catch
        {
            return new FavoritesDocument();
        }
    }

    public void Save(FavoritesDocument document)
    {
        var directory = Path.GetDirectoryName(favoritesPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(favoritesPath, JsonSerializer.Serialize(document, JsonOptions));
    }
}
