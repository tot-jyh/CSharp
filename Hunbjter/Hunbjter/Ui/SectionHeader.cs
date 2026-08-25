using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Hunbjter;

/// <summary>Row label above a grid: status dot, title, and a count chip.</summary>
public sealed class SectionHeader : Control
{
    private string title = "";
    private int count;
    private Color dotColor = Theme.TextMuted;

    public SectionHeader()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Dock = DockStyle.Fill;
        Margin = new Padding(0);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title
    {
        get => title;
        set => SetField(ref title, value);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Count
    {
        get => count;
        set => SetField(ref count, value);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color DotColor
    {
        get => dotColor;
        set => SetField(ref dotColor, value);
    }

    /// <summary>When set, the dot is drawn as a hollow ring instead of a filled disc.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HollowDot { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.ResolveBackdrop(this));

        var centerY = Height / 2;

        var previous = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var dot = new Rectangle(2, centerY - 4, 8, 8);
        if (HollowDot)
        {
            using var pen = new Pen(dotColor, 1.6f);
            e.Graphics.DrawEllipse(pen, dot);
        }
        else
        {
            using var brush = new SolidBrush(dotColor);
            e.Graphics.FillEllipse(brush, dot);
        }

        e.Graphics.SmoothingMode = previous;

        var titleSize = TextRenderer.MeasureText(
            e.Graphics, title, Theme.BaseBold, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);

        TextRenderer.DrawText(
            e.Graphics,
            title,
            Theme.BaseBold,
            new Rectangle(16, 0, titleSize.Width + 4, Height),
            Theme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var chipText = count.ToString();
        var chipTextSize = TextRenderer.MeasureText(
            e.Graphics, chipText, Theme.SmallBold, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        var chipWidth = Math.Max(chipTextSize.Width + 14, 24);
        var chip = new Rectangle(16 + titleSize.Width + 8, centerY - 9, chipWidth, 18);

        Theme.FillRounded(e.Graphics, chip, 9, Theme.SurfaceAlt);
        TextRenderer.DrawText(
            e.Graphics,
            chipText,
            Theme.SmallBold,
            chip,
            Theme.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
