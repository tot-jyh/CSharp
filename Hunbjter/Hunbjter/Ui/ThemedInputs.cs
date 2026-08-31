namespace Hunbjter;

/// <summary>Rounded, bordered shell that gives flat inputs a visible field boundary.</summary>
public sealed class InputHost : Panel
{
    private readonly Control inner;
    private bool hasFocus;

    public InputHost(Control inner, int height = 32)
    {
        this.inner = inner;

        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        BackColor = Color.Transparent;
        Padding = new Padding(10, 6, 10, 6);
        Height = height;
        Margin = new Padding(0);

        inner.Dock = DockStyle.Fill;
        inner.GotFocus += OnInnerFocusChanged;
        inner.LostFocus += OnInnerFocusChanged;
        Controls.Add(inner);
    }

    public Control Inner => inner;

    private void OnInnerFocusChanged(object? sender, EventArgs e)
    {
        hasFocus = inner.Focused;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.ResolveBackdrop(this));

        var bounds = new RectangleF(0, 0, Width, Height);
        Theme.FillRounded(e.Graphics, bounds, Theme.RadiusControl, Theme.SurfaceAlt);
        Theme.DrawRoundedBorder(
            e.Graphics,
            bounds,
            Theme.RadiusControl,
            hasFocus ? Theme.Accent : Theme.Border);
    }
}

public sealed class ThemedTextBox : TextBox
{
    public ThemedTextBox()
    {
        BackColor = Theme.SurfaceAlt;
        ForeColor = Theme.TextPrimary;
        BorderStyle = BorderStyle.None;
        Font = Theme.Base;
    }
}

/// <summary>
/// Dark-styled <see cref="MaskedTextBox"/>, mask defaulted to "00:00:00" (HH:mm:ss) for the
/// Clip tool's segment start/end fields - enforces digit-only input per position as you type,
/// rather than free text validated after the fact.
/// </summary>
public sealed class ThemedMaskedTextBox : MaskedTextBox
{
    public ThemedMaskedTextBox()
    {
        BackColor = Theme.SurfaceAlt;
        ForeColor = Theme.TextPrimary;
        BorderStyle = BorderStyle.None;
        Font = Theme.Base;
        Mask = "00:00:00";
        InsertKeyMode = InsertKeyMode.Overwrite;
    }
}

/// <summary>
/// <see cref="NumericUpDown"/> with the palette pushed onto its internal text box and
/// spin buttons, which do not inherit the parent colors on their own.
/// </summary>
public sealed class ThemedNumeric : NumericUpDown
{
    public ThemedNumeric()
    {
        BorderStyle = BorderStyle.None;
        BackColor = Theme.SurfaceAlt;
        ForeColor = Theme.TextPrimary;
        Font = Theme.Base;
        TextAlign = HorizontalAlignment.Left;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        foreach (Control child in Controls)
        {
            child.BackColor = Theme.SurfaceAlt;
            child.ForeColor = Theme.TextPrimary;
        }

        NativeTheme.ApplyScrollBars(this);
    }
}

/// <summary>
/// Owner-drawn check box. The system one paints a white box no matter what colors are set.
/// </summary>
public sealed class ThemedCheckBox : CheckBox
{
    private const int BoxSize = 18;

    public ThemedCheckBox()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);

        AutoSize = false;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = Theme.Base;
        ForeColor = Theme.TextPrimary;
        Size = new Size(150, 30);
    }

    protected override bool ShowFocusCues => false;

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.ResolveBackdrop(this));

        var box = new Rectangle(0, (Height - BoxSize) / 2, BoxSize, BoxSize);
        Theme.FillRounded(e.Graphics, box, 4, Checked ? Theme.Accent : Theme.SurfaceAlt);
        Theme.DrawRoundedBorder(e.Graphics, box, 4, Checked ? Theme.Accent : Theme.Border);

        if (Checked)
        {
            var previous = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (var pen = new Pen(Theme.TextOnAccent, 2f))
            {
                e.Graphics.DrawLines(pen,
                [
                    new Point(box.X + 4, box.Y + 9),
                    new Point(box.X + 7, box.Y + 12),
                    new Point(box.X + 13, box.Y + 5)
                ]);
            }

            e.Graphics.SmoothingMode = previous;
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(BoxSize + 8, 0, Width - BoxSize - 8, Height),
            Enabled ? ForeColor : Theme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>
/// Owner-drawn so the drop-down list is dark too; a plain <see cref="ComboBox"/> keeps
/// painting its items with system colors no matter what BackColor is set.
/// </summary>
public sealed class ThemedComboBox : ComboBox
{
    public ThemedComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        BackColor = Theme.SurfaceAlt;
        ForeColor = Theme.TextPrimary;
        Font = Theme.Base;
        ItemHeight = 22;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            base.OnDrawItem(e);
            return;
        }

        var selected = (e.State & DrawItemState.Selected) != 0;
        using (var brush = new SolidBrush(selected ? Theme.SurfaceHover : Theme.SurfaceAlt))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        TextRenderer.DrawText(
            e.Graphics,
            GetItemText(Items[e.Index]),
            Font,
            e.Bounds,
            Theme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}

/// <summary><see cref="TableLayoutPanel"/> flickers badly on resize against a dark background.</summary>
public sealed class BufferedTableLayoutPanel : TableLayoutPanel
{
    public BufferedTableLayoutPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Margin = new Padding(0);
    }
}
