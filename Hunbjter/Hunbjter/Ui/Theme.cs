using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Hunbjter;

/// <summary>
/// Single source of truth for colors, fonts and drawing primitives.
/// Change a value here and the whole app follows.
/// </summary>
internal static class Theme
{
    // Shell
    public static readonly Color Background = FromHex("#0F1115");
    public static readonly Color Surface = FromHex("#171A20");
    public static readonly Color SurfaceAlt = FromHex("#1C202A");
    public static readonly Color SurfaceHover = FromHex("#232936");
    public static readonly Color SurfacePressed = FromHex("#2B323F");

    // Lines
    public static readonly Color Border = FromHex("#2A303C");
    public static readonly Color BorderSubtle = FromHex("#1F242E");

    // Text
    public static readonly Color TextPrimary = FromHex("#E7EAF0");
    public static readonly Color TextSecondary = FromHex("#96A0B0");
    public static readonly Color TextMuted = FromHex("#626C7C");
    public static readonly Color TextOnAccent = FromHex("#FFFFFF");

    // Semantic
    public static readonly Color Accent = FromHex("#3B82F6");
    public static readonly Color AccentHover = FromHex("#4C8DF7");
    public static readonly Color AccentPressed = FromHex("#2F6FD8");
    public static readonly Color Live = FromHex("#22C55E");
    public static readonly Color Recording = FromHex("#F43F5E");
    public static readonly Color Warning = FromHex("#F59E0B");
    public static readonly Color Danger = FromHex("#EF4444");
    public static readonly Color Offline = FromHex("#6B7280");

    // Metrics
    public const int RadiusCard = 10;
    public const int RadiusControl = 6;
    public const int RadiusBadge = 999;
    public const int RowHeight = 46;
    public const int HeaderHeight = 34;

    private static readonly FontFamily UiFamily = ResolveFamily("맑은 고딕", "Malgun Gothic", "Segoe UI");
    private static readonly FontFamily MonoFamily = ResolveFamily("Cascadia Mono", "Consolas", "Courier New");

    public static readonly Font Base = new(UiFamily, 9F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font BaseBold = new(UiFamily, 9F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Small = new(UiFamily, 8.25F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font SmallBold = new(UiFamily, 8.25F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Tiny = new(UiFamily, 7.5F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Title = new(UiFamily, 12F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Metric = new(UiFamily, 19F, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font Mono = new(MonoFamily, 8.5F, FontStyle.Regular, GraphicsUnit.Point);

    public static Color FromHex(string hex)
    {
        return ColorTranslator.FromHtml(hex);
    }

    /// <summary>Blends <paramref name="front"/> over <paramref name="back"/> at the given opacity.</summary>
    public static Color Blend(Color front, Color back, double opacity)
    {
        opacity = Math.Clamp(opacity, 0d, 1d);
        return Color.FromArgb(
            (int)Math.Round(front.R * opacity + back.R * (1 - opacity)),
            (int)Math.Round(front.G * opacity + back.G * (1 - opacity)),
            (int)Math.Round(front.B * opacity + back.B * (1 - opacity)));
    }

    /// <summary>
    /// Finds the nearest opaque ancestor background. Owner-drawn controls must fill their
    /// client rect with this before painting a rounded shape: WinForms' transparent
    /// background only asks the immediate parent to render, so a chain of
    /// <see cref="Color.Transparent"/> containers smears whatever was drawn there last.
    /// It also gives the anti-aliased edge a real color to blend against.
    /// </summary>
    public static Color ResolveBackdrop(Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent.BackColor.A == 255)
            {
                return parent.BackColor;
            }
        }

        return Background;
    }

    public static GraphicsPath RoundedPath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f);

        if (radius <= 0.5f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = radius * 2f;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics graphics, RectangleF bounds, float radius, Color fill)
    {
        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var path = RoundedPath(bounds, radius))
        using (var brush = new SolidBrush(fill))
        {
            graphics.FillPath(brush, path);
        }

        graphics.SmoothingMode = previous;
    }

    public static void DrawRoundedBorder(Graphics graphics, RectangleF bounds, float radius, Color color, float width = 1f)
    {
        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var inset = RectangleF.Inflate(bounds, -width / 2f, -width / 2f);
        using (var path = RoundedPath(inset, radius))
        using (var pen = new Pen(color, width))
        {
            graphics.DrawPath(pen, path);
        }

        graphics.SmoothingMode = previous;
    }

    /// <summary>Draws a pill badge and returns the area it occupied.</summary>
    public static Rectangle DrawBadge(
        Graphics graphics,
        Rectangle cell,
        string text,
        Color foreground,
        Color background,
        Font? font = null,
        bool withDot = false)
    {
        font ??= SmallBold;

        var textSize = TextRenderer.MeasureText(graphics, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        var dotSpace = withDot ? 14 : 0;
        var width = Math.Min(textSize.Width + 20 + dotSpace, cell.Width - 8);
        var height = Math.Min(22, cell.Height - 8);
        var bounds = new Rectangle(
            cell.X + (cell.Width - width) / 2,
            cell.Y + (cell.Height - height) / 2,
            width,
            height);

        FillRounded(graphics, bounds, height / 2f, background);

        var textBounds = bounds;
        if (withDot)
        {
            var previous = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(foreground))
            {
                graphics.FillEllipse(brush, bounds.X + 9, bounds.Y + height / 2 - 3, 6, 6);
            }

            graphics.SmoothingMode = previous;
            textBounds = new Rectangle(bounds.X + dotSpace, bounds.Y, bounds.Width - dotSpace, bounds.Height);
        }

        TextRenderer.DrawText(
            graphics,
            text,
            font,
            textBounds,
            foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        return bounds;
    }

    private static FontFamily ResolveFamily(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            try
            {
                return new FontFamily(candidate);
            }
            catch (ArgumentException)
            {
                // Font is not installed on this machine; try the next candidate.
            }
        }

        return FontFamily.GenericSansSerif;
    }
}
