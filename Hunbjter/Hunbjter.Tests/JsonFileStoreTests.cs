using System.Text.Json;
using Hunbjter;
using Xunit;

namespace Hunbjter.Tests;

/// <summary>
/// Guards the store against the failure that used to turn a truncated file into an empty
/// roster — indistinguishable, to the caller, from "the user has no models".
/// </summary>
public sealed class JsonFileStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), "hunbjter-tests-" + Guid.NewGuid().ToString("N"));

    public JsonFileStoreTests() => Directory.CreateDirectory(directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the run.
        }
    }

    private string PathFor(string name) => Path.Combine(directory, name);

    [Fact]
    public void MissingFileYieldsTheDefaultWithoutReportingFailure()
    {
        var loaded = JsonFileStore.Load(PathFor("absent.json"), () => new FavoritesDocument(), out var failure);

        Assert.Null(failure);
        Assert.Empty(loaded.Items);
    }

    [Fact]
    public void SaveThenLoadRoundTripsKoreanText()
    {
        var path = PathFor("favorites.json");
        var document = new FavoritesDocument();
        document.Items.Add(new FavoriteItem
        {
            Id = "팬더:cuee66",
            Platform = "팬더",
            PlatformUserId = "cuee66",
            DisplayName = "루미(괄호)",
            Memo = "풀방 입장권 확인 필요",
        });

        JsonFileStore.Save(path, document);
        var loaded = JsonFileStore.Load(path, () => new FavoritesDocument(), out var failure);

        Assert.Null(failure);
        var item = Assert.Single(loaded.Items);
        Assert.Equal("팬더:cuee66", item.Id);
        Assert.Equal("루미(괄호)", item.DisplayName);
        Assert.Equal("풀방 입장권 확인 필요", item.Memo);
    }

    [Fact]
    public void SaveLeavesNoTemporaryFileBehind()
    {
        var path = PathFor("favorites.json");

        JsonFileStore.Save(path, new FavoritesDocument());

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void CorruptFileIsReportedAndQuarantinedRatherThanSilentlyDiscarded()
    {
        var path = PathFor("favorites.json");
        File.WriteAllText(path, "{ this is not json");

        var loaded = JsonFileStore.Load(path, () => new FavoritesDocument(), out var failure);

        Assert.NotNull(failure);
        Assert.Contains("favorites.json", failure);
        Assert.Empty(loaded.Items);

        // The damaged content is preserved so the user can recover it by hand.
        var quarantined = Directory.GetFiles(directory, "favorites.json.bad-*");
        Assert.Single(quarantined);
        Assert.Contains("this is not json", File.ReadAllText(quarantined[0]));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void OverwritingAnExistingFileKeepsItParseable()
    {
        var path = PathFor("favorites.json");
        JsonFileStore.Save(path, new FavoritesDocument());

        var second = new FavoritesDocument();
        second.Items.Add(new FavoriteItem { Id = "팬더:a", DisplayName = "가" });
        JsonFileStore.Save(path, second);

        var reloaded = JsonSerializer.Deserialize<FavoritesDocument>(File.ReadAllText(path));
        Assert.NotNull(reloaded);
        Assert.Single(reloaded.Items);
    }

    [Fact]
    public async Task ConcurrentSavesNeverProduceAPartialFile()
    {
        var path = PathFor("favorites.json");

        await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() =>
        {
            var document = new FavoritesDocument();
            for (var n = 0; n <= i; n++)
            {
                document.Items.Add(new FavoriteItem { Id = $"팬더:{n}", DisplayName = $"모델{n}" });
            }

            JsonFileStore.Save(path, document);
        })));

        // Whatever ordering won, the file on disk must still be a complete document.
        var final = JsonFileStore.Load(path, () => new FavoritesDocument(), out var failure);
        Assert.Null(failure);
        Assert.NotEmpty(final.Items);
    }
}
