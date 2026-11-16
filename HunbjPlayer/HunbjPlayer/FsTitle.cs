namespace HunbjPlayer;

/// <summary>
/// 전체화면에서 마우스를 화면 위쪽으로 가져가면 영상 위에 겹쳐 나타나는 파일명 표시 창.
/// FsBar와 같은 이유(영상은 별도 네이티브 창)로 포커스를 가져가지 않는 별도 창이며, 클릭은 아래로 통과시킴.
/// </summary>
public sealed class FsTitle : Form
{
    const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000;

    public Label TitleLabel { get; } = new()
    {
        Dock = DockStyle.Fill, ForeColor = Color.Gainsboro, BackColor = Color.FromArgb(30, 30, 30),
        TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Font = new Font("Malgun Gothic", 12f, FontStyle.Regular)
    };

    public FsTitle()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(30, 30, 30);
        Opacity = 0.92;
        TitleLabel.Padding = new Padding(16, 0, 16, 0);
        Controls.Add(TitleLabel);
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
}
