using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Hunbjter;

/// <summary>Summary tile: a colored dot, a caption and one large number.</summary>
public sealed class StatCard : Control
{
    private string caption = "";
    private string value = "-";
    private Color accentColor = Theme.Accent;

    public StatCard()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 10, 0);
        Dock = DockStyle.Fill;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption
    {
        get => caption;
        set => SetField(ref caption, value);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Value
    {
        get => value;
        set => SetField(ref this.value, value);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor
    {
        get => accentColor;
        set => SetField(ref accentColor, value);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.ResolveBackdrop(this));

        var bounds = new RectangleF(0, 0, Width, Height);
        Theme.FillRounded(e.Graphics, bounds, Theme.RadiusCard, Theme.Surface);
        Theme.DrawRoundedBorder(e.Graphics, bounds, Theme.RadiusCard, Theme.Border);

        const int padX = 16;
        const int padY = 13;

        var previous = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(accentColor))
        {
            e.Graphics.FillEllipse(brush, padX, padY + 3, 7, 7);
        }

        e.Graphics.SmoothingMode = previous;

        TextRenderer.DrawText(
            e.Graphics,
            caption,
            Theme.Small,
            new Rectangle(padX + 13, padY - 1, Width - padX - 20, 16),
            Theme.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);

        TextRenderer.DrawText(
            e.Graphics,
            value,
            Theme.Metric,
            new Rectangle(padX - 2, padY + 18, Width - padX - 8, Height - padY - 22),
            Theme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoClipping);
    }

    private void SetField<T>(ref T field, T next)
    {
        if (EqualityComparer<T>.Default.Equals(field, next))
        {
            return;
        }

        field = next;
        Invalidate();
    }
}
