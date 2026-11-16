namespace HunbjPlayer;

/// <summary>팟플레이어 스타일의 얇은 시크바. Value는 0..1 비율.</summary>
public class SeekBar : Control
{
    static readonly Color Track = Color.FromArgb(70, 70, 70);
    static readonly Color Fill = Color.FromArgb(255, 205, 0);

    double _value;
    bool _dragging;
    bool _hover;

    public bool Dragging => _dragging;

    double _loopA = -1, _loopB = -1;

    /// <summary>구간반복 시작/끝 위치(0..1 비율). 미설정은 음수</summary>
    public void SetLoop(double a, double b)
    {
        _loopA = a; _loopB = b;
        Invalidate();
    }

    /// <summary>사용자가 클릭/드래그로 위치를 바꿀 때 (비율 0..1)</summary>
    public event Action<double>? Scrubbed;

    public SeekBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        BackColor = Color.FromArgb(30, 30, 30);
        Cursor = Cursors.Hand;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public double Value
    {
        get => _value;
        set
        {
            value = Math.Clamp(value, 0, 1);
            if (Math.Abs(value - _value) < 0.0001) return;
            _value = value;
            Invalidate();
        }
    }

    int Pad => LogicalToDeviceUnits(6);

    double RatioAt(int x) => Math.Clamp((x - Pad) / (double)Math.Max(1, Width - Pad * 2), 0, 1);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        Capture = true;
        Scrub(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) Scrub(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

    void Scrub(int x)
    {
        Value = RatioAt(x);
        Scrubbed?.Invoke(_value);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int pad = Pad;
        int h = LogicalToDeviceUnits(_hover || _dragging ? 4 : 3);
        int y = (Height - h) / 2;
        int w = Width - pad * 2;
        if (w <= 0) return;

        using (var b = new SolidBrush(Track)) g.FillRectangle(b, pad, y, w, h);
        int fw = (int)(w * _value);
        using (var b = new SolidBrush(Fill)) g.FillRectangle(b, pad, y, fw, h);

        // 구간반복 범위(A~B): 트랙 위아래로 살짝 튀어나온 파란 띠. 한쪽만 정해졌으면 그 위치에 눈금
        if (_loopA >= 0 || _loopB >= 0)
        {
            int ext = LogicalToDeviceUnits(3);
            using var lb = new SolidBrush(Color.FromArgb(200, 70, 150, 255));
            if (_loopA >= 0 && _loopB > _loopA)
                g.FillRectangle(lb, pad + (int)(w * _loopA), y - ext, Math.Max(2, (int)(w * (_loopB - _loopA))), h + ext * 2);
            else
            {
                double at = _loopA >= 0 ? _loopA : _loopB;
                g.FillRectangle(lb, pad + (int)(w * at) - 1, y - ext, 3, h + ext * 2);
            }
        }

        int d = LogicalToDeviceUnits(10);
        using (var b = new SolidBrush(Color.White))
            g.FillEllipse(b, pad + fw - d / 2, (Height - d) / 2, d, d);
    }
}
