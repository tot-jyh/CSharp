using System.Diagnostics;
using System.IO.Compression;

namespace HunbjPlayer;

/// <summary>
/// ffmpeg.exe를 찾을 수 없을 때 공개 빌드(zip)를 내려받아 실행 파일 옆에 설치.
/// zip 안에서 ffmpeg.exe / ffprobe.exe만 꺼내 쓰고 나머지는 버림.
/// </summary>
public static class FfmpegInstaller
{
    // 위에서부터 차례로 시도. HUNBJ_FFMPEG_URL 환경변수로 주소를 직접 지정할 수도 있음 (테스트용)
    static IEnumerable<string> Urls()
    {
        var custom = Environment.GetEnvironmentVariable("HUNBJ_FFMPEG_URL");
        if (!string.IsNullOrWhiteSpace(custom)) { yield return custom; yield break; }
        yield return "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";                    // 약 110MB
        yield return "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"; // 약 190MB
    }

    /// <summary>설치 폴더: 실행 파일 옆(쓸 수 있으면), 아니면 %AppData%\HunbjPlayer\ffmpeg</summary>
    public static string InstallDir
    {
        get
        {
            var beside = AppContext.BaseDirectory;
            try
            {
                var probe = Path.Combine(beside, $".write_test_{Guid.NewGuid():N}");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return beside;
            }
            catch { return AppDataDir; }
        }
    }

    /// <summary>Program Files처럼 실행 파일 옆에 쓸 수 없을 때 쓰는 폴더 (ffmpeg 탐색 경로에도 포함됨)</summary>
    public static string AppDataDir => Path.Combine(PlayerSettings.Dir, "ffmpeg");

    /// <summary>내려받아 설치하고 ffmpeg.exe 전체 경로를 돌려줌. 실패하면 예외</summary>
    public static async Task<string> DownloadAsync(IProgress<(long Done, long Total)>? progress, CancellationToken ct = default)
    {
        var dir = InstallDir;
        Directory.CreateDirectory(dir);
        var zip = Path.Combine(Path.GetTempPath(), $"hunbjplayer_ffmpeg_{Guid.NewGuid():N}.zip");
        var errors = new List<string>();

        try
        {
            foreach (var url in Urls())
            {
                try
                {
                    await DownloadFileAsync(url, zip, progress, ct);
                    return ExtractAndVerify(zip, dir);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    errors.Add($"{new Uri(url).Host}: {ex.Message}");
                }
            }
            throw new InvalidOperationException("모든 내려받기 주소에서 실패했습니다. " + string.Join(" / ", errors));
        }
        finally
        {
            try { if (File.Exists(zip)) File.Delete(zip); } catch { /* 임시 파일 정리 실패는 무시 */ }
        }
    }

    static async Task DownloadFileAsync(string url, string path, IProgress<(long, long)>? progress, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("HunbjPlayer");
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? -1;

        await using var src = await response.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(path);
        var buffer = new byte[256 * 1024];
        long done = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            done += read;
            progress?.Report((done, total));
        }
    }

    // zip에서 ffmpeg.exe(필수)와 ffprobe.exe(있으면)를 꺼내 설치하고, 실제로 실행되는지 확인
    static string ExtractAndVerify(string zipPath, string dir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        // 일부 도구가 만든 zip은 경로 구분자가 \ 이므로 / 로 통일해서 비교
        string Norm(ZipArchiveEntry e) => e.FullName.Replace((char)92, (char)47);
        var ffmpegEntry = archive.Entries.FirstOrDefault(e => Norm(e).EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
                                                              || Norm(e).Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("내려받은 파일에서 ffmpeg.exe를 찾지 못했습니다.");

        var target = Path.Combine(dir, "ffmpeg.exe");
        Install(ffmpegEntry, target);

        var probeEntry = archive.Entries.FirstOrDefault(e => Norm(e).EndsWith("/bin/ffprobe.exe", StringComparison.OrdinalIgnoreCase)
                                                             || Norm(e).Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase));
        if (probeEntry != null) Install(probeEntry, Path.Combine(dir, "ffprobe.exe"));

        // 깨진 파일을 설치해 두지 않도록 한 번 실행해 봄
        using var p = Process.Start(new ProcessStartInfo(target, "-version")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        })!;
        if (!p.WaitForExit(15000) || p.ExitCode != 0)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
            try { File.Delete(target); } catch { }
            throw new InvalidDataException("내려받은 ffmpeg.exe가 실행되지 않습니다.");
        }
        return target;
    }

    // 임시 이름으로 풀고 교체: 이미 실행 중인 ffmpeg가 있어도 설치 도중 반쯤 쓰인 파일이 남지 않음
    static void Install(ZipArchiveEntry entry, string target)
    {
        var temp = target + ".download";
        entry.ExtractToFile(temp, overwrite: true);
        File.Move(temp, target, overwrite: true);
    }
}
