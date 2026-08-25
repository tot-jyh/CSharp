using System.Collections.Concurrent;
using System.Text.Json;

namespace Hunbjter;

/// <summary>
/// Shared JSON persistence for the app's settings files.
///
/// Two hazards this exists to close:
///  - a bare <c>File.WriteAllText</c> is not atomic, so an interrupted write leaves a truncated
///    file, and the previous loader turned that into an empty document — i.e. a silent wipe of
///    the whole roster;
///  - several store instances target the same path (a FavoriteStore is constructed in Form1,
///    ModelManagementForm and SiteManagementForm), so writes can interleave.
/// </summary>
internal static class JsonFileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static readonly ConcurrentDictionary<string, object> Locks = new(StringComparer.OrdinalIgnoreCase);

    public static string ResolvePath(string fileName)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hunbjter",
            fileName);
    }

    /// <summary>
    /// Falls back to <paramref name="createDefault"/> when the file is missing or unreadable.
    /// A file that exists but cannot be parsed is moved aside and reported through
    /// <paramref name="failure"/> instead of being discarded silently.
    /// </summary>
    public static T Load<T>(string path, Func<T> createDefault, out string? failure)
        where T : class
    {
        failure = null;

        if (!File.Exists(path))
        {
            return createDefault();
        }

        lock (LockFor(path))
        {
            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? createDefault();
            }
            catch (Exception ex)
            {
                failure = Quarantine(path, ex);
                return createDefault();
            }
        }
    }

    public static void Save<T>(string path, T document)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(document, Options);

        lock (LockFor(path))
        {
            // Write beside the target then swap, so a reader never observes a partial file.
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite: true);
        }
    }

    /// <summary>Keeps the damaged file so the user can recover it by hand.</summary>
    private static string Quarantine(string path, Exception ex)
    {
        try
        {
            var backupPath = $"{path}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(path, backupPath, overwrite: true);
            return $"{Path.GetFileName(path)} 파일을 읽지 못했습니다 ({ex.Message}). {Path.GetFileName(backupPath)} 로 백업했습니다.";
        }
        catch (Exception moveFailure)
        {
            return $"{Path.GetFileName(path)} 파일을 읽지 못했습니다 ({ex.Message}). 백업도 실패했습니다: {moveFailure.Message}";
        }
    }

    private static object LockFor(string path)
    {
        return Locks.GetOrAdd(Path.GetFullPath(path), _ => new object());
    }
}
