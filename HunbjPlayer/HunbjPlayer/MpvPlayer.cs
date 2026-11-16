using System.Runtime.InteropServices;
using System.Text;

namespace HunbjPlayer;

/// <summary>libmpv 최소 래퍼. 지정한 창(wid) 안에 영상을 그린다.</summary>
public sealed class MpvPlayer : IDisposable
{
    const string Lib = "libmpv-2.dll";
    const int FormatString = 1, FormatFlag = 3, FormatDouble = 5;
    const int EventShutdown = 1, EventEndFile = 7, EventClientMessage = 16, EventPlaybackRestart = 21;
    const int EndReasonEof = 0, EndReasonError = 4;

    [DllImport(Lib)] static extern IntPtr mpv_create();
    [DllImport(Lib)] static extern int mpv_initialize(IntPtr ctx);
    [DllImport(Lib)] static extern void mpv_terminate_destroy(IntPtr ctx);
    [DllImport(Lib)] static extern int mpv_set_option_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Lib)] static extern int mpv_set_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Lib)] static extern int mpv_get_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format, out double data);
    [DllImport(Lib)] static extern int mpv_get_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format, out int data);
    [DllImport(Lib)] static extern IntPtr mpv_get_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Lib)] static extern void mpv_free(IntPtr data);
    [DllImport(Lib)] static extern int mpv_command(IntPtr ctx, IntPtr[] args);
    [DllImport(Lib)] static extern int mpv_command_async(IntPtr ctx, ulong userdata, IntPtr[] args);
    [DllImport(Lib)] static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

    [StructLayout(LayoutKind.Sequential)]
    struct MpvEvent { public int EventId; public int Error; public ulong ReplyUserdata; public IntPtr Data; }

    [StructLayout(LayoutKind.Sequential)]
    struct MpvEventEndFile { public int Reason; public int Error; }

    [StructLayout(LayoutKind.Sequential)]
    struct MpvEventClientMessage { public int NumArgs; public IntPtr Args; }

    IntPtr _ctx;
    readonly Thread _eventThread;

    /// <summary>파일이 끝까지 재생됨 (이벤트 스레드에서 호출)</summary>
    public event Action? EndReached;
    /// <summary>파일을 열거나 재생하지 못함. 인자는 사람이 읽을 수 있는 원인 (이벤트 스레드에서 호출)</summary>
    public event Action<string>? LoadFailed;

    [DllImport(Lib)] static extern IntPtr mpv_error_string(int error);

    // mpv 오류 코드를 한국어 설명 + mpv 원문으로 바꿈
    static string DescribeError(int code)
    {
        var raw = Marshal.PtrToStringUTF8(mpv_error_string(code)) ?? "unknown error";
        var ko = code switch
        {
            -13 => "파일을 열 수 없습니다 (없거나, 다른 프로그램이 사용 중이거나, 읽을 권한이 없습니다)",
            -16 => "재생할 영상/소리가 없습니다",
            -17 => "알 수 없는 파일 형식입니다 (손상되었거나, 아직 녹화 중이거나, 지원하지 않는 형식입니다)",
            -14 => "오디오 장치를 열지 못했습니다",
            -15 => "영상 출력을 시작하지 못했습니다",
            _ => null,
        };
        return ko == null ? $"{raw} (오류 {code})" : $"{ko} - {raw} (오류 {code})";
    }
    /// <summary>시크 등 이후 재생이 다시 시작됨 (이벤트 스레드에서 호출)</summary>
    public event Action? PlaybackRestart;
    /// <summary>script-message 수신 (이벤트 스레드에서 호출)</summary>
    public event Action<string[]>? ClientMessage;

    public MpvPlayer(IntPtr wid)
    {
        _ctx = mpv_create();
        if (_ctx == IntPtr.Zero) throw new InvalidOperationException("libmpv 초기화 실패");

        Opt("wid", wid.ToInt64().ToString());
        Opt("config", "no");             // 사용자 mpv.conf 무시
        Opt("load-scripts", "no");
        Opt("osc", "no");
        Opt("ytdl", "no");
        Opt("terminal", "no");
        Opt("idle", "yes");
        Opt("keep-open", "no");
        Opt("input-default-bindings", "no");
        Opt("input-vo-keyboard", "yes"); // 영상 창에 포커스가 있을 때 키를 받아 script-message로 넘김
        Opt("drag-and-drop", "no");      // 드롭은 부모 폼이 처리(재생목록 추가)
        Opt("hwdec", "auto-safe");
        Opt("volume", "100"); // 시작 직후 소리 크기는 곧바로 Volume 속성(설정 파일 값)으로 덮어씀
        Opt("volume-max", "100");

        // 끊김 없는 건너뛰기: 화살표 시크는 키프레임 단위 + 넉넉한 앞/뒤 캐시
        // → 캐시 범위 안의 이동은 파일을 다시 읽지 않고 즉시 처리됨
        Opt("hr-seek", "default");
        Opt("cache", "yes");
        Opt("demuxer-max-bytes", "256MiB");
        Opt("demuxer-max-back-bytes", "128MiB");
        Opt("demuxer-readahead-secs", "60");
        Opt("demuxer-seekable-cache", "yes");

        // OSD (왼쪽 상단 안내 문구)
        Opt("osd-align-x", "left");
        Opt("osd-align-y", "top");
        Opt("osd-margin-x", "20");
        Opt("osd-margin-y", "20");
        Opt("osd-font", "Malgun Gothic");
        Opt("osd-font-size", "30");
        Opt("osd-bold", "yes");
        Opt("osd-border-style", "background-box");
        Opt("osd-back-color", "#B0000000");
        Opt("osd-bar", "no");
        Opt("osd-duration", "1500");

        // 진단용: 환경변수 HUNBJ_MPV_LOG 에 경로를 주면 mpv 로그 기록
        var log = Environment.GetEnvironmentVariable("HUNBJ_MPV_LOG");
        if (!string.IsNullOrEmpty(log)) Opt("log-file", log);

        if (mpv_initialize(_ctx) < 0) throw new InvalidOperationException("libmpv 초기화 실패");

        _eventThread = new Thread(EventLoop) { IsBackground = true, Name = "mpv-events" };
        _eventThread.Start();
    }

    void Opt(string name, string value) => mpv_set_option_string(_ctx, name, value);

    public void Command(params string[] args) => Run(args, async: false);
    public void CommandAsync(params string[] args) => Run(args, async: true);

    void Run(string[] args, bool async)
    {
        if (_ctx == IntPtr.Zero) return;
        var ptrs = new IntPtr[args.Length + 1];
        try
        {
            for (int i = 0; i < args.Length; i++) ptrs[i] = Utf8(args[i]);
            if (async) mpv_command_async(_ctx, 0, ptrs);
            else mpv_command(_ctx, ptrs);
        }
        finally
        {
            foreach (var p in ptrs) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
        }
    }

    static IntPtr Utf8(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        var p = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, p, bytes.Length);
        Marshal.WriteByte(p, bytes.Length, 0);
        return p;
    }

    public void SetProperty(string name, string value)
    {
        if (_ctx != IntPtr.Zero) mpv_set_property_string(_ctx, name, value);
    }

    /// <summary>문자열 속성(예: "path" = 지금 실제로 재생 중인 파일 경로)</summary>
    public string? GetString(string name)
    {
        if (_ctx == IntPtr.Zero) return null;
        var p = mpv_get_property_string(_ctx, name);
        if (p == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUTF8(p); }
        finally { mpv_free(p); }
    }

    public double GetDouble(string name, double fallback = 0)
        => _ctx != IntPtr.Zero && mpv_get_property(_ctx, name, FormatDouble, out double v) >= 0 ? v : fallback;

    public bool GetFlag(string name)
        => _ctx != IntPtr.Zero && mpv_get_property(_ctx, name, FormatFlag, out int v) >= 0 && v != 0;

    public void Load(string path, double start = 0) =>
        Command("loadfile", path, "replace", "-1",
            "start=" + start.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
    public void Stop() => Command("stop");
    public void ShowText(string text, int ms = 1500) => CommandAsync("show-text", text, ms.ToString());

    /// <summary>상대 이동(초). 키프레임 단위라 빠르고, 연속 입력도 mpv가 누적 처리</summary>
    public void SeekRelative(double seconds) => CommandAsync("seek", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "relative");

    /// <summary>정확한 위치로 이동 (구간반복 시작점 등)</summary>
    public void SeekExact(double seconds) =>
        CommandAsync("seek", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "absolute+exact");

    public void SeekAbsolute(double seconds) =>
        CommandAsync("seek", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "absolute+keyframes");

    public void FrameStep(bool forward) => Command(forward ? "frame-step" : "frame-back-step");

    public double Speed
    {
        get => GetDouble("speed", 1.0);
        set => SetProperty("speed", value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
    }

    public bool Pause { get => GetFlag("pause"); set => SetProperty("pause", value ? "yes" : "no"); }
    public bool Mute { get => GetFlag("mute"); set => SetProperty("mute", value ? "yes" : "no"); }
    /// <summary>
    /// 화면에 보이는 볼륨(0~100, 소리 크기(진폭)에 비례하는 선형 값).
    /// mpv의 volume 속성은 세제곱 곡선((v/100)^3)이라 40을 주면 실제로는 0.064배(-24dB)로 크게 작아지므로,
    /// 다른 플레이어(팟플레이어 등)와 비슷한 느낌이 되도록 mpv에는 세제곱근을 취한 값을 넘김.
    /// 예) 40% → mpv 73.7 → 정확히 0.4배(-8dB)
    /// </summary>
    public int Volume
    {
        get
        {
            double mpvValue = Math.Clamp(GetDouble("volume", 100), 0, 100);
            return (int)Math.Round(100 * Math.Pow(mpvValue / 100, 3));
        }
        set
        {
            double linear = Math.Clamp(value, 0, 100) / 100.0;
            SetProperty("volume", (100 * Math.Cbrt(linear)).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
    public double TimePos => GetDouble("time-pos");
    public double Duration => GetDouble("duration");
    public bool Idle => GetFlag("idle-active");

    void EventLoop()
    {
        while (true)
        {
            var ev = Marshal.PtrToStructure<MpvEvent>(mpv_wait_event(_ctx, -1));
            switch (ev.EventId)
            {
                case EventShutdown:
                    return;
                case EventEndFile:
                    var end = Marshal.PtrToStructure<MpvEventEndFile>(ev.Data);
                    if (end.Reason == EndReasonEof) EndReached?.Invoke();
                    else if (end.Reason == EndReasonError) LoadFailed?.Invoke(DescribeError(end.Error));
                    break;
                case EventPlaybackRestart:
                    PlaybackRestart?.Invoke();
                    break;
                case EventClientMessage:
                    var msg = Marshal.PtrToStructure<MpvEventClientMessage>(ev.Data);
                    var args = new string[msg.NumArgs];
                    for (int i = 0; i < msg.NumArgs; i++)
                        args[i] = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(msg.Args, i * IntPtr.Size)) ?? "";
                    ClientMessage?.Invoke(args);
                    break;
            }
        }
    }

    public void Dispose()
    {
        if (_ctx == IntPtr.Zero) return;
        // 이벤트 스레드가 SHUTDOWN을 받고 빠져나온 뒤에 파괴해야 안전
        Command("quit");
        _eventThread.Join(3000);
        var ctx = _ctx;
        _ctx = IntPtr.Zero;
        mpv_terminate_destroy(ctx);
    }
}
