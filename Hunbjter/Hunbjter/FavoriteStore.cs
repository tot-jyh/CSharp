namespace Hunbjter;

public sealed class FavoriteStore
{
    private readonly string favoritesPath = JsonFileStore.ResolvePath("favorites.json");

    /// <summary>
    /// Set when the last <see cref="Load"/> found an unreadable file. Callers surface this to
    /// the log so a corrupted roster is visible rather than looking like an empty one.
    /// </summary>
    public string? LastLoadFailure { get; private set; }

    public FavoritesDocument Load()
    {
        var document = JsonFileStore.Load(favoritesPath, static () => new FavoritesDocument(), out var failure);
        LastLoadFailure = failure;
        return document;
    }

    public void Save(FavoritesDocument document)
    {
        JsonFileStore.Save(favoritesPath, document);
    }
}
