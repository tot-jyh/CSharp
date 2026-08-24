namespace Hunbjter;

public sealed class FavoritesDocument
{
    public int Version { get; set; } = 1;

    public List<FavoriteItem> Items { get; set; } = [];
}
