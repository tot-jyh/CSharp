using System.Text.Json;

namespace HunbjPlayer;

/// <summary>편집(Clip) 목록의 한 행 (저장용)</summary>
public class ClipRowData
{
    public string FilePath { get; set; } = "";
    public double Start { get; set; }
    public double End { get; set; }
    public bool Excluded { get; set; }
}

/// <summary>종료 시 저장되는 창 위치/크기, 볼륨, 패널·목록 상태. %AppData%\HunbjPlayer\settings.json</summary>
public class PlayerSettings
{
    // 환경변수 HUNBJ_SETTINGS_DIR 로 설정 폴더를 바꿀 수 있음 (테스트가 사용자의 실제 설정을 건드리지 않게 하기 위함)
    public static string Dir => Path.GetDirectoryName(FilePath)!;

    static readonly string FilePath = Path.Combine(
        Environment.GetEnvironmentVariable("HUNBJ_SETTINGS_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HunbjPlayer"),
        "settings.json");

    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Maximized { get; set; }
    public bool HasBounds { get; set; }
    public int Volume { get; set; } = 80;
    public bool TopMost { get; set; }

    // 건너뛰기 시간(초): ←/→, Ctrl+←/→, Shift+←/→
    public int SkipShort { get; set; } = 5;
    public int SkipMedium { get; set; } = 30;
    public int SkipLong { get; set; } = 60;
    /// <summary>재생창에 파일 1개를 드롭하면 파일명 앞부분이 이 글자 수만큼 같은 파일을 함께 추가 (0이면 사용 안 함)</summary>
    public int SimilarPrefix { get; set; } = 5;

    // 종료 시점의 패널/목록 상태 (다음 실행 때 그대로 복원)
    public bool ListOpen { get; set; }
    public bool ClipOpen { get; set; }
    public List<string> Playlist { get; set; } = new();
    public int PlaylistIndex { get; set; } = -1;
    /// <summary>재생목록 정렬 기준 (Form1.SortMode: 0 이름↑, 1 이름↓, 2 최근↑, 3 최근↓, 4 정렬없음)</summary>
    public int PlaylistSort { get; set; } = 4;
    public List<ClipRowData> ClipRows { get; set; } = new();
    /// <summary>파일별 재생시간(초) 캐시 - 재생목록에 시간을 표시하려고 매번 다시 읽지 않기 위함</summary>
    public Dictionary<string, double> Durations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>비어 있으면 Hunbjter 설정/실행 파일 옆/PATH 순으로 자동 탐색</summary>
    public string FfmpegPath { get; set; } = "";

    public static PlayerSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<PlayerSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* 손상된 설정은 무시하고 기본값 사용 */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 저장 실패는 치명적이지 않음 */ }
    }
}
