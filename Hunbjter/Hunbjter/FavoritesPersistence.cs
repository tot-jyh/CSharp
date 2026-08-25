namespace Hunbjter;

/// <summary>
/// Debounces writes to favorites.json.
///
/// With one shared timer the app saved once per sweep. Now that every model checks on its own
/// schedule, an undebounced save would rewrite the whole document once per model per interval.
/// Structural edits (add, delete, watch toggle) still go through <see cref="Flush"/> so they
/// are never at risk of being lost.
/// </summary>
public sealed class FavoritesPersistence : IDisposable
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaximumLatency = TimeSpan.FromSeconds(15);

    private readonly FavoriteStore store;
    private readonly System.Windows.Forms.Timer timer = new();

    private FavoritesDocument? pending;
    private DateTime firstDirtyAt;

    public FavoritesPersistence(FavoriteStore store)
    {
        this.store = store;

        // A WinForms timer keeps the callback on the UI thread, which is where the document is
        // mutated — no locking required.
        timer.Interval = (int)IdleDelay.TotalMilliseconds;
        timer.Tick += (_, _) => OnTick();
    }

    public event EventHandler<MonitorLogEventArgs>? SaveFailed;

    public void MarkDirty(FavoritesDocument document)
    {
        if (pending is null)
        {
            firstDirtyAt = DateTime.UtcNow;
        }

        pending = document;
        timer.Stop();
        timer.Start();
    }

    /// <summary>Writes immediately. Use before a modal dialog reloads the file, and on shutdown.</summary>
    public void Flush()
    {
        timer.Stop();

        if (pending is not { } document)
        {
            return;
        }

        pending = null;
        Write(document);
    }

    private void OnTick()
    {
        // Cap the total latency so a model checking every 10s cannot defer the write forever.
        if (pending is not null && DateTime.UtcNow - firstDirtyAt < MaximumLatency)
        {
            timer.Stop();
            timer.Start();

            if (DateTime.UtcNow - firstDirtyAt < IdleDelay)
            {
                return;
            }
        }

        Flush();
    }

    private void Write(FavoritesDocument document)
    {
        try
        {
            store.Save(document);
        }
        catch (Exception ex)
        {
            SaveFailed?.Invoke(this, new MonitorLogEventArgs($"목록 저장 실패: {ex.Message}"));
        }
    }

    public void Dispose()
    {
        Flush();
        timer.Dispose();
    }
}
