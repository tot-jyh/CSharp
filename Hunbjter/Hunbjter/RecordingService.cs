using System.Diagnostics;
using System.Collections.Concurrent;

namespace Hunbjter;

public sealed class RecordingService
{
    private static readonly HttpClient PlaylistClient = new();

    public async Task<RecordingSession> StartAsync(
        FavoriteItem favorite,
        string masterPlaylistUrl,
        string outputDirectory,
        string ffmpegPath,
        RecordingHttpContext httpContext)
    {
        if (!File.Exists(ffmpegPath))
        {
            throw new FileNotFoundException("선택한 ffmpeg.exe 파일을 찾을 수 없습니다.");
        }

        var streamUrl = await SelectBestQualityAsync(masterPlaylistUrl, httpContext);
        var outputPath = BuildOutputPath(favorite, outputDirectory);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            },
            EnableRaisingEvents = true
        };
        AddFfmpegArguments(process.StartInfo, streamUrl, outputPath, httpContext);

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("ffmpeg를 시작하지 못했습니다.");
        }

        var session = new RecordingSession(process, outputPath, streamUrl);
        process.ErrorDataReceived += (_, eventArgs) => session.AppendError(eventArgs.Data);
        process.BeginErrorReadLine();
        return session;
    }

    public async Task<string> CreateHighlightAsync(
        FavoriteItem favorite,
        string sourcePath,
        string outputDirectory,
        string ffmpegPath,
        int seconds)
    {
        if (!File.Exists(ffmpegPath))
        {
            throw new FileNotFoundException("선택한 ffmpeg.exe 파일을 찾을 수 없습니다.");
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("녹화 원본 파일을 찾을 수 없습니다.", sourcePath);
        }

        seconds = Math.Clamp(seconds, 5, 3600);
        var outputPath = BuildHighlightOutputPath(favorite, outputDirectory, seconds);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            }
        };

        AddHighlightArguments(process.StartInfo, sourcePath, outputPath, seconds);
        if (!process.Start())
        {
            throw new InvalidOperationException("ffmpeg를 시작하지 못했습니다.");
        }

        var errorText = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"하이라이트 캡쳐 실패 (코드 {process.ExitCode}): {TrimFfmpegError(errorText)}");
        }

        return outputPath;
    }

    private static async Task<string> SelectBestQualityAsync(string playlistUrl, RecordingHttpContext httpContext)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, playlistUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", httpContext.UserAgent);
            foreach (var header in ParseHeaderLines(httpContext.HeaderText))
            {
                request.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }

            using var response = await PlaylistClient.SendAsync(request);
            var playlist = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? HlsVariantSelector.SelectBestVariantUrl(playlistUrl, playlist)
                : playlistUrl;
        }
        catch
        {
            return playlistUrl;
        }
    }

    private static void AddFfmpegArguments(
        ProcessStartInfo startInfo,
        string streamUrl,
        string outputPath,
        RecordingHttpContext httpContext)
    {
        foreach (var argument in new[]
        {
            "-hide_banner",
            "-loglevel",
            "warning",
            "-user_agent",
            httpContext.UserAgent,
            "-headers",
            httpContext.HeaderText,
            "-i",
            streamUrl,
            "-map",
            "0:v?",
            "-map",
            "0:a?",
            "-dn",
            "-c",
            "copy",
            "-f",
            "mpegts",
            outputPath
        })
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static void AddHighlightArguments(
        ProcessStartInfo startInfo,
        string sourcePath,
        string outputPath,
        int seconds)
    {
        foreach (var argument in new[]
        {
            "-hide_banner",
            "-y",
            "-sseof",
            $"-{seconds}",
            "-i",
            sourcePath,
            "-c",
            "copy",
            "-f",
            "mpegts",
            outputPath
        })
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static IEnumerable<(string Name, string Value)> ParseHeaderLines(string headerText)
    {
        foreach (var line in headerText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            yield return (line[..separatorIndex].Trim(), line[(separatorIndex + 1)..].Trim());
        }
    }

    private static string BuildOutputPath(FavoriteItem favorite, string outputDirectory)
    {
        var name = SanitizeFileName(favorite);
        var modelDirectory = BuildModelDirectory(outputDirectory, name);
        return Path.Combine(modelDirectory, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.ts");
    }

    private static string BuildHighlightOutputPath(FavoriteItem favorite, string outputDirectory, int seconds)
    {
        var name = SanitizeFileName(favorite);
        var modelDirectory = BuildModelDirectory(outputDirectory, name);
        return Path.Combine(modelDirectory, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}_highlight_{seconds}s.ts");
    }

    /// <summary>
    /// Files now land in {녹화저장 폴더}/{모델명}/... instead of flat in the root, so recordings
    /// stay grouped per model as the folder fills up with many models over time.
    /// </summary>
    private static string BuildModelDirectory(string outputDirectory, string sanitizedName)
    {
        var modelDirectory = Path.Combine(outputDirectory, sanitizedName);
        Directory.CreateDirectory(modelDirectory);
        return modelDirectory;
    }

    private static string SanitizeFileName(FavoriteItem favorite)
    {
        var name = string.Concat(favorite.DisplayName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return string.IsNullOrWhiteSpace(name) ? favorite.PlatformUserId : name;
    }

    private static string TrimFfmpegError(string value)
    {
        value = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= 500 ? value : value[^500..];
    }

}

public sealed class RecordingSession : IDisposable
{
    private readonly ConcurrentQueue<string> errorLines = new();
    public RecordingSession(Process process, string outputPath, string streamUrl)
    {
        Process = process;
        OutputPath = outputPath;
        StreamUrl = streamUrl;
    }

    public Process Process { get; }

    public string OutputPath { get; }

    public string StreamUrl { get; }

    public bool IsRunning => !Process.HasExited;

    public int ExitCode => Process.HasExited ? Process.ExitCode : 0;

    public string ErrorSummary => string.Join(" | ", errorLines);

    public void AppendError(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        errorLines.Enqueue(line.Trim());
        while (errorLines.Count > 8)
        {
            errorLines.TryDequeue(out _);
        }
    }

    public void Stop()
    {
        if (Process.HasExited)
        {
            return;
        }

        try
        {
            Process.StandardInput.WriteLine("q");
            Process.StandardInput.Flush();
        }
        catch
        {
        }

        if (Process.WaitForExit(5000))
        {
            return;
        }

        if (!Process.HasExited)
        {
            Process.Kill(entireProcessTree: true);
            Process.WaitForExit(5000);
        }
    }

    public void Dispose()
    {
        Process.Dispose();
    }
}
