using System.Diagnostics;
using System.Text;

namespace Hunbjter;

/// <summary>
/// ffmpeg-backed segment extraction/concatenation for the Clip tool. Follows the same raw-Process
/// pattern as <see cref="RecordingService.CreateHighlightAsync"/> - no Xabe.FFmpeg dependency,
/// just the user's own configured <c>settings.FfmpegPath</c>.
/// </summary>
public sealed class ClipService
{
    public async Task ExtractSegmentAsync(string ffmpegPath, string sourcePath, TimeSpan start, TimeSpan duration, string outputPath)
    {
        if (!File.Exists(ffmpegPath))
        {
            throw new FileNotFoundException("선택한 ffmpeg.exe 파일을 찾을 수 없습니다.");
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("구간 소스 파일을 찾을 수 없습니다.", sourcePath);
        }

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

        AddExtractArguments(process.StartInfo, sourcePath, outputPath, start, duration);
        if (!process.Start())
        {
            throw new InvalidOperationException("ffmpeg를 시작하지 못했습니다.");
        }

        var errorText = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"구간 추출 실패 (코드 {process.ExitCode}): {TrimFfmpegError(errorText)}");
        }
    }

    public async Task ConcatAsync(string ffmpegPath, IReadOnlyList<string> segmentPaths, string outputPath)
    {
        if (!File.Exists(ffmpegPath))
        {
            throw new FileNotFoundException("선택한 ffmpeg.exe 파일을 찾을 수 없습니다.");
        }

        if (segmentPaths.Count == 0)
        {
            throw new InvalidOperationException("합칠 구간이 없습니다.");
        }

        var listFile = Path.Combine(Path.GetTempPath(), $"clipm_concat_{Guid.NewGuid():N}.txt");
        var listLines = segmentPaths.Select(path => $"file '{path.Replace("'", "'\\''")}'");
        await File.WriteAllTextAsync(listFile, string.Join(Environment.NewLine, listLines), new UTF8Encoding(false));

        try
        {
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

            AddConcatArguments(process.StartInfo, listFile, outputPath);
            if (!process.Start())
            {
                throw new InvalidOperationException("ffmpeg를 시작하지 못했습니다.");
            }

            var errorText = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"구간 합치기 실패 (코드 {process.ExitCode}): {TrimFfmpegError(errorText)}");
            }
        }
        finally
        {
            try
            {
                if (File.Exists(listFile))
                {
                    File.Delete(listFile);
                }
            }
            catch
            {
                // Best-effort cleanup - a leftover temp list file is harmless.
            }
        }
    }

    private static void AddExtractArguments(ProcessStartInfo startInfo, string sourcePath, string outputPath, TimeSpan start, TimeSpan duration)
    {
        foreach (var argument in new[]
        {
            "-hide_banner",
            "-y",
            "-ss",
            start.ToString(@"hh\:mm\:ss"),
            "-i",
            sourcePath,
            "-t",
            duration.ToString(@"hh\:mm\:ss"),
            "-c",
            "copy",
            outputPath
        })
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static void AddConcatArguments(ProcessStartInfo startInfo, string listFile, string outputPath)
    {
        foreach (var argument in new[]
        {
            "-hide_banner",
            "-y",
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            listFile,
            "-c",
            "copy",
            outputPath
        })
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static string TrimFfmpegError(string value)
    {
        value = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length <= 500 ? value : value[^500..];
    }
}
