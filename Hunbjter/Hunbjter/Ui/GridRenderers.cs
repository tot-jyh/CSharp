using System.Drawing.Drawing2D;

namespace Hunbjter;

/// <summary>
/// Per-column cell painters for the favorites grid. Every painter repaints the background
/// *and the border* first — dropping the border part would erase the horizontal hairline
/// for that cell and leave a dashed rule across the row.
/// </summary>
internal static class GridRenderers
{
    /// <summary>
    /// <see cref="DataGridViewPaintParts.Background"/> only covers the unselected fill, so
    /// <see cref="DataGridViewPaintParts.SelectionBackground"/> has to be requested as well or
    /// custom-painted columns stay dark inside a highlighted row. <see cref="DataGridViewPaintParts.Border"/>
    /// carries the horizontal hairline; dropping it dashes the row separator.
    /// </summary>
    private const DataGridViewPaintParts BaseParts =
        DataGridViewPaintParts.Background
        | DataGridViewPaintParts.SelectionBackground
        | DataGridViewPaintParts.Border;

    private const TextFormatFlags LeftLine =
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter
        | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;

    /// <summary>Nickname on the first line, platform id beneath it.</summary>
    public static void PaintNameTwoLine(DataGridViewCellPaintingEventArgs e, FavoriteItem favorite)
    {
        if (e.Graphics is null)
        {
            return;
        }

        e.Paint(e.CellBounds, BaseParts);

        var left = e.CellBounds.X + 12;
        var width = e.CellBounds.Width - 20;

        TextRenderer.DrawText(
            e.Graphics,
            favorite.DisplayName,
            Theme.BaseBold,
            new Rectangle(left, e.CellBounds.Y + 6, width, 18),
            Theme.TextPrimary,
            LeftLine);

        TextRenderer.DrawText(
            e.Graphics,
            favorite.PlatformUserId,
            Theme.Small,
            new Rectangle(left, e.CellBounds.Y + 24, width, 16),
            Theme.TextMuted,
            LeftLine);

        e.Handled = true;
    }

    public static void PaintStatusBadge(DataGridViewCellPaintingEventArgs e, string statusText)
    {
        if (e.Graphics is null)
        {
            return;
        }

        e.Paint(e.CellBounds, BaseParts);

        var (foreground, background) = StatusVisual.ForStatus(statusText);

        if (background.A == 0)
        {
            TextRenderer.DrawText(
                e.Graphics,
                statusText,
                Theme.Base,
                e.CellBounds,
                foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        else
        {
            Theme.DrawBadge(
                e.Graphics,
                e.CellBounds,
                statusText,
                foreground,
                background,
                Theme.SmallBold,
                withDot: statusText == "방송중");
        }

        e.Handled = true;
    }

    public static void PaintRecIndicator(DataGridViewCellPaintingEventArgs e, bool isRecording, bool isPaused = false)
    {
        if (e.Graphics is null)
        {
            return;
        }

        e.Paint(e.CellBounds, BaseParts);

        if (isRecording)
        {
            Theme.DrawBadge(
                e.Graphics,
                e.CellBounds,
                "REC",
                Theme.Recording,
                Theme.Blend(Theme.Recording, Theme.Surface, 0.18),
                Theme.Tiny,
                withDot: true);
        }
        else if (isPaused)
        {
            // Distinct from REC (red) and from the plain dash: an operator paused recording
            // manually while watch stayed on, so it won't auto-restart until they resume it.
            Theme.DrawBadge(
                e.Graphics,
                e.CellBounds,
                "일시중지",
                Theme.Warning,
                Theme.Blend(Theme.Warning, Theme.Surface, 0.18),
                Theme.Tiny);
        }
        else
        {
            DrawDash(e);
        }

        e.Handled = true;
    }

    /// <summary>
    /// Clickable, like the capture button - hover just brightens the pill's fill. <paramref name="locked"/>
    /// mirrors the context menu's "Watch Off" item being disabled while the model is recording
    /// (see Form1.ToggleWatch / ModelManagementForm.SetFavoriteWatch): the pill still reads "ON"
    /// but is drawn dim and ignores hover, so it does not invite a click that will just no-op.
    /// </summary>
    public static void PaintWatchBadge(DataGridViewCellPaintingEventArgs e, bool enabled, bool hovered, bool locked = false)
    {
        if (e.Graphics is null)
        {
            return;
        }

        e.Paint(e.CellBounds, BaseParts);

        if (enabled && locked)
        {
            Theme.DrawBadge(e.Graphics, e.CellBounds, "ON", Theme.TextMuted, Theme.SurfaceAlt, Theme.Tiny);
        }
        else if (enabled)
        {
            // Solid fill (not the translucent tint every other badge uses) - watch on/off is the
            // one toggle in this grid the user scans for at a glance, so it gets a loud, distinct
            // treatment instead of blending into the row. Yellow (Theme.Warning) rather than the
            // blue accent so it doesn't get lost among the other blue "primary" chrome elsewhere
            // in the UI - dark text reads better than white against this brightness.
            var background = hovered ? Theme.Blend(Color.White, Theme.Warning, 0.15) : Theme.Warning;
            Theme.DrawBadge(e.Graphics, e.CellBounds, "ON", Theme.Background, background, Theme.SmallBold);
        }
        else
        {
            var foreground = hovered ? Theme.TextSecondary : Theme.TextMuted;
            var background = hovered ? Theme.SurfaceHover : Theme.SurfaceAlt;
            Theme.DrawBadge(e.Graphics, e.CellBounds, "OFF", foreground, background, Theme.Tiny);
        }

        e.Handled = true;
    }

    /// <summary>The instant-capture affordance: a real-looking chip only while recording.</summary>
    public static void PaintCaptureButton(DataGridViewCellPaintingEventArgs e, bool isRecording, bool hovered)
    {
        if (e.Graphics is null)
        {
            return;
        }

        e.Paint(e.CellBounds, BaseParts);

        if (!isRecording)
        {
            DrawDash(e);
            e.Handled = true;
            return;
        }

        var width = Math.Min(48, e.CellBounds.Width - 12);
        var height = Math.Min(26, e.CellBounds.Height - 10);
        var chip = new Rectangle(
            e.CellBounds.X + (e.CellBounds.Width - width) / 2,
            e.CellBounds.Y + (e.CellBounds.Height - height) / 2,
            width,
            height);

        var fill = hovered ? Theme.AccentHover : Theme.Accent;
        Theme.FillRounded(e.Graphics, chip, Theme.RadiusControl, fill);

        TextRenderer.DrawText(
            e.Graphics,
            "캡쳐",
            Theme.SmallBold,
            chip,
            Theme.TextOnAccent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        e.Handled = true;
    }

    private static void DrawDash(DataGridViewCellPaintingEventArgs e)
    {
        var previous = e.Graphics!.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var pen = new Pen(Theme.TextMuted, 1.4f))
        {
            var y = e.CellBounds.Y + (e.CellBounds.Height / 2);
            var centerX = e.CellBounds.X + (e.CellBounds.Width / 2);
            e.Graphics.DrawLine(pen, centerX - 5, y, centerX + 5, y);
        }

        e.Graphics.SmoothingMode = previous;
    }
}
