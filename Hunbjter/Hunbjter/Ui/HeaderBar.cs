using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Hunbjter;

/// <summary>
/// App header: brand mark on the left, live status text in the gutter, action buttons on
/// the right. The status line matters because <c>SetStatus</c> otherwise only reaches the
/// log, where it scrolls away within seconds.
/// </summary>
public sealed class HeaderBar : Control
{
    private const int LogoSize = 28;
    private const int LogoLeft = 18;
    private const int TextLeft = LogoLeft + LogoSize + 12;

    private readonly FlowLayoutPanel actionHost = new();
    private string statusText = "";

    public HeaderBar()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = Theme.Surface;
        Dock = DockStyle.Fill;
        Margin = new Padding(0);

        actionHost.AutoSize = true;
        actionHost.BackColor = Color.Transparent;
        actionHost.Dock = DockStyle.Right;
        actionHost.FlowDirection = FlowDirection.RightToLeft;
        actionHost.Margin = new Padding(0);
        actionHost.Padding = new Padding(0, 12, 16, 12);
        actionHost.WrapContents = false;
        Controls.Add(actionHost);
    }

    /// <summary>Add buttons here; they lay out right-to-left from the right edge.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FlowLayoutPanel ActionHost => actionHost;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string StatusText
    {
        get => statusText;
        set
        {
            if (statusText == value)
            {
                return;
            }

            statusText = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Surface);

        using (var pen = new Pen(Theme.Border))
        {
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }

        DrawLogo(e.Graphics);

        TextRenderer.DrawText(
            e.Graphics,
            "Hunbjter",
            Theme.Title,
            new Rectangle(TextLeft, 10, 160, 20),
            Theme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        TextRenderer.DrawText(
            e.Graphics,
            "",
            Theme.Tiny,
            new Rectangle(TextLeft + 1, 30, 160, 14),
            Theme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        DrawStatus(e.Graphics);
    }

    private void DrawLogo(Graphics graphics)
    {
        var tile = new RectangleF(LogoLeft, (Height - LogoSize) / 2f, LogoSize, LogoSize);

        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var path = Theme.RoundedPath(tile, 8))
        using (var brush = new LinearGradientBrush(tile, Theme.SurfaceAlt, Theme.Background, LinearGradientMode.ForwardDiagonal))
        {
            graphics.FillPath(brush, path);
        }

        using (var brush = new SolidBrush(Theme.Recording))
        {
            var diameter = LogoSize * 0.42f;
            graphics.FillEllipse(
                brush,
                tile.X + (LogoSize - diameter) / 2f,
                tile.Y + (LogoSize - diameter) / 2f,
                diameter,
                diameter);
        }

        graphics.SmoothingMode = previous;
    }

    private void DrawStatus(Graphics graphics)
    {
        if (string.IsNullOrWhiteSpace(statusText))
        {
            return;
        }

        var left = TextLeft + 180;
        var right = actionHost.Left - 16;
        if (right - left < 80)
        {
            return;
        }

        TextRenderer.DrawText(
            graphics,
            statusText,
            Theme.Small,
            new Rectangle(left, 0, right - left, Height - 1),
            Theme.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
