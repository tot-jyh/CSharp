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
            throw new FileNotFoundException("?좏깮??ffmpeg.exe ?뚯씪??李얠쓣 ???놁뒿?덈떎.");
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
            throw new InvalidOperationException("ffmpeg瑜??쒖옉?섏? 紐삵뻽?듬땲??");
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
            throw new FileNotFoundException("?좏깮??ffmpeg.exe ?뚯씪??李얠쓣 ???놁뒿?덈떎.");
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("?뱁솕 ?먮낯 ?뚯씪??李얠쓣 ???놁뒿?덈떎.", sourcePath);
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
            throw new InvalidOperationException("ffmpeg瑜??쒖옉?섏? 紐삵뻽?듬땲??");
        }

        var errorText = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"?섏씠?쇱씠??罹≪퀜 ?ㅽ뙣 (肄붾뱶 {process.ExitCode}): {TrimFfmpegError(errorText)}");
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
        Directory.CreateDirectory(outputDirectory);

        var name = string.Concat(favorite.DisplayName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        if (string.IsNullOrWhiteSpace(name))
        {
            name = favorite.PlatformUserId;
        }

        return Path.Combine(outputDirectory, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.ts");
    }

    private static string BuildHighlightOutputPath(FavoriteItem favorite, string outputDirectory, int seconds)
    {
        Directory.CreateDirectory(outputDirectory);

        var name = string.Concat(favorite.DisplayName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        if (string.IsNullOrWhiteSpace(name))
        {
            name = favorite.PlatformUserId;
        }

        return Path.Combine(outputDirectory, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}_highlight_{seconds}s.ts");
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

