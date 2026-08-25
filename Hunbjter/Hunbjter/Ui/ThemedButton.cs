using System.ComponentModel;

namespace Hunbjter;

public enum ButtonVariant
{
    Primary,
    Secondary,
    Ghost,
    Danger
}

/// <summary>Flat, fully owner-drawn button with hover and press feedback.</summary>
public sealed class ThemedButton : Button
{
    private bool hovered;
    private bool pressed;

    public ThemedButton()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        UseVisualStyleBackColor = false;
        Font = Theme.Base;
        Cursor = Cursors.Hand;
        Size = new Size(104, 32);
        Margin = new Padding(6, 0, 0, 0);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonVariant Variant { get; set; } = ButtonVariant.Secondary;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = Theme.RadiusControl;

    /// <summary>The dotted focus rectangle has no place in a flat dark theme.</summary>
    protected override bool ShowFocusCues => false;

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            pressed = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.ResolveBackdrop(this));

        var bounds = new RectangleF(0, 0, Width, Height);
        var (fill, border, foreground) = ResolveColors();

        if (fill.A > 0)
        {
            Theme.FillRounded(e.Graphics, bounds, CornerRadius, fill);
        }

        if (border.A > 0)
        {
            Theme.DrawRoundedBorder(e.Graphics, bounds, CornerRadius, border);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(0, 0, Width, Height),
            foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private (Color Fill, Color Border, Color Foreground) ResolveColors()
    {
        if (!Enabled)
        {
            return (Theme.Surface, Theme.BorderSubtle, Theme.TextMuted);
        }

        return Variant switch
        {
            ButtonVariant.Primary => (
                pressed ? Theme.AccentPressed : hovered ? Theme.AccentHover : Theme.Accent,
                Color.Transparent,
                Theme.TextOnAccent),

            ButtonVariant.Danger => (
                pressed ? Theme.Blend(Color.Black, Theme.Danger, 0.18)
                    : hovered ? Theme.Blend(Color.White, Theme.Danger, 0.10) : Theme.Danger,
                Color.Transparent,
                Theme.TextOnAccent),

            ButtonVariant.Ghost => (
                pressed ? Theme.SurfacePressed : hovered ? Theme.SurfaceHover : Color.Transparent,
                Color.Transparent,
                hovered ? Theme.TextPrimary : Theme.TextSecondary),

            _ => (
                pressed ? Theme.SurfacePressed : hovered ? Theme.SurfaceHover : Theme.SurfaceAlt,
                hovered ? Theme.Blend(Theme.Accent, Theme.Border, 0.35) : Theme.Border,
                Theme.TextPrimary)
        };
    }
}
