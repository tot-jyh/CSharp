namespace HunbjPlayer;

/// <summary>
/// 전체화면에서 마우스를 화면 아래쪽으로 가져가면 영상 위에 겹쳐 나타나는 진행바 창 (진행바 + 현재시간/전체시간).
/// 영상(mpv)은 별도의 네이티브 창이라 일반 컨트롤은 그 위에 그릴 수 없으므로, 포커스를 가져가지 않는 별도 창으로 띄움.
/// </summary>
public sealed class FsBar : Form
{
    const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x80;

    public SeekBar Seek { get; } = new() { Dock = DockStyle.Top };
    public Label TimeLabel { get; } = new()
    {
        Dock = DockStyle.Fill, ForeColor = Color.Gainsboro, BackColor = Color.FromArgb(30, 30, 30),
        TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Malgun Gothic", 11f, FontStyle.Regular)
    };

    public FsBar()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(30, 30, 30);
        Opacity = 0.92;
        TimeLabel.Padding = new Padding(12, 0, 0, 0);
        Controls.Add(TimeLabel);
        Controls.Add(Seek); // 나중에 추가된 Seek이 먼저 도킹되어 위쪽에 놓임
    }

    // 클릭해도 활성 창이 바뀌지 않아야 본 창의 단축키(방향키, Enter 등)가 계속 동작함
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }
}
