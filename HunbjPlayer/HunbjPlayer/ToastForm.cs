namespace HunbjPlayer;

/// <summary>
/// 영상 왼쪽 위에 잠깐 떠 있다가 저절로 사라지는 알림 창 (오류 안내용). 메시지 박스와 달리 작업을 막지 않고,
/// 클릭/포커스를 가로채지 않으며, 영상이 멈춰 있어도 보임(mpv 화면 안내는 재생 중에만 보이므로).
/// </summary>
public sealed class ToastForm : Form
{
    const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000;

    readonly Label _label = new()
    {
        AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent,
        Font = new Font("Malgun Gothic", 12f, FontStyle.Bold)
    };
    readonly System.Windows.Forms.Timer _hide = new();

    public ToastForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Opacity = 0.92;
        Padding = new Padding(16, 10, 16, 10);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Controls.Add(_label);
        _hide.Tick += (_, _) => { _hide.Stop(); Hide(); };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED;
            return cp;
        }
    }

    /// <summary>area(화면 좌표)의 왼쪽 위에 표시. 오류는 붉은 바탕, 일반 안내는 검정 바탕</summary>
    public void ShowText(Form owner, Rectangle area, string text, int ms, bool error)
    {
        BackColor = error ? Color.FromArgb(150, 30, 30) : Color.Black;
        // 긴 경로/원인이 한 줄로 화면을 넘지 않도록 영상 폭의 70%에서 줄바꿈
        _label.MaximumSize = new Size(Math.Max(300, (int)(area.Width * 0.7)), 0);
        _label.Text = text;
        PerformLayout();
        int m = LogicalToDeviceUnits(16);
        Location = new Point(area.X + m, area.Y + m);
        if (!Visible) Show(owner); else Invalidate();
        _hide.Stop();
        _hide.Interval = Math.Clamp(ms, 1000, 3000); // 아무리 길게 요청해도 최대 3초만 표시
        _hide.Start();
    }

    public void Dismiss() { _hide.Stop(); Hide(); }
}
