using System.ComponentModel;

namespace Hunbjter;

/// <summary>
/// A <see cref="DataGridView"/> preconfigured for the dark palette: no vertical rules,
/// hairline row separators, flat headers and an accent-tinted selection.
/// </summary>
public class ThemedGrid : DataGridView
{
    private int hoverRowIndex = -1;
    private Color restBackColor = Theme.Surface;

    public ThemedGrid()
    {
        // Control.DoubleBuffered is protected, so subclassing is the only clean way to set it.
        // Never add ControlStyles.UserPaint here: it disables DataGridView's own painting.
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        // Order matters. While header visual styles are on, the header colors below are
        // ignored and ColumnHeadersBorderStyle = None throws.
        EnableHeadersVisualStyles = false;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        AllowUserToResizeColumns = false;
        AllowUserToOrderColumns = false;

        // Proportional by default: each column's FillWeight decides its share, so a section with
        // fewer visible columns (a hidden column is simply excluded from the distribution) does
        // not dump all the freed width onto whichever single column used to have AutoSizeMode.Fill.
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        BackgroundColor = Theme.Background;
        BorderStyle = BorderStyle.None;
        GridColor = Theme.BorderSubtle;

        ColumnHeadersHeight = Theme.HeaderHeight;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        RowHeadersVisible = false;

        MultiSelect = false;
        ReadOnly = true;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        EditMode = DataGridViewEditMode.EditProgrammatically;
        ScrollBars = ScrollBars.Vertical;
        RowTemplate.Height = Theme.RowHeight;
        Font = Theme.Base;
        Margin = new Padding(0);

        DefaultCellStyle.BackColor = restBackColor;
        DefaultCellStyle.ForeColor = Theme.TextPrimary;
        DefaultCellStyle.SelectionBackColor = Theme.Blend(Theme.Accent, Theme.Surface, 0.22);
        DefaultCellStyle.SelectionForeColor = Theme.TextPrimary;
        DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        DefaultCellStyle.Font = Theme.Base;
        DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);

        ColumnHeadersDefaultCellStyle.BackColor = Theme.Background;
        ColumnHeadersDefaultCellStyle.ForeColor = Theme.TextSecondary;
        // Without matching selection colors the header flashes system blue on click.
        ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Background;
        ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.TextSecondary;
        ColumnHeadersDefaultCellStyle.Font = Theme.SmallBold;
        ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
    }

    /// <summary>
    /// The resting (non-hovered) row fill. Routed through here rather than read back from
    /// DefaultCellStyle.BackColor because SetHoverRow/ResetRowBackground need a color to restore
    /// to on mouse-leave - hardcoding Theme.Surface there would snap a tinted grid (see Form1's
    /// 방송중 section) back to the untinted default the moment a row is hovered and un-hovered.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color RestBackColor
    {
        get => restBackColor;
        set
        {
            restBackColor = value;
            DefaultCellStyle.BackColor = value;

            if (hoverRowIndex < 0 || hoverRowIndex >= Rows.Count)
            {
                return;
            }

            // The currently-hovered row keeps its hover color; everything else should already
            // show the new rest color via DefaultCellStyle, but any row-level override survives
            // a style change, so clear it explicitly to be safe.
            for (var i = 0; i < Rows.Count; i++)
            {
                if (i != hoverRowIndex)
                {
                    Rows[i].DefaultCellStyle.BackColor = value;
                }
            }
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeTheme.ApplyScrollBars(this);
    }

    /// <summary>
    /// Subscribers get first refusal so per-column renderers can claim their cells; whatever
    /// is left falls back to default painting with the focus rectangle masked out.
    /// <c>PaintParts</c> itself is read-only here, so the mask has to go through
    /// <see cref="DataGridViewCellPaintingEventArgs.Paint"/>.
    /// </summary>
    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        base.OnCellPainting(e);

        if (e.Handled)
        {
            return;
        }

        e.Paint(e.CellBounds, e.PaintParts & ~DataGridViewPaintParts.Focus);
        e.Handled = true;
    }

    protected override void OnCellMouseEnter(DataGridViewCellEventArgs e)
    {
        SetHoverRow(e.RowIndex);
        base.OnCellMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        SetHoverRow(-1);
        base.OnMouseLeave(e);
    }

    protected override void OnRowsRemoved(DataGridViewRowsRemovedEventArgs e)
    {
        hoverRowIndex = -1;
        base.OnRowsRemoved(e);
    }

    /// <summary>
    /// Row hover is applied through the row's cell style rather than by painting over the
    /// row, so custom-painted cells that call <c>e.Paint(..., Background)</c> pick it up too.
    /// </summary>
    private void SetHoverRow(int rowIndex)
    {
        if (hoverRowIndex == rowIndex)
        {
            return;
        }

        ResetRowBackground(hoverRowIndex);
        hoverRowIndex = rowIndex;

        if (hoverRowIndex >= 0 && hoverRowIndex < Rows.Count)
        {
            Rows[hoverRowIndex].DefaultCellStyle.BackColor = Theme.SurfaceHover;
        }
    }

    private void ResetRowBackground(int rowIndex)
    {
        if (rowIndex >= 0 && rowIndex < Rows.Count)
        {
            Rows[rowIndex].DefaultCellStyle.BackColor = restBackColor;
        }
    }

    /// <summary>
    /// Every column uses <see cref="DataGridViewColumnSortMode.Programmatic"/>, so WinForms
    /// draws no sort glyph on its own. Driving the header cells keeps that indicator visible.
    /// </summary>
    public void ShowSortGlyph(int columnIndex, SortOrder direction)
    {
        foreach (DataGridViewColumn column in Columns)
        {
            column.HeaderCell.SortGlyphDirection = column.Index == columnIndex
                ? direction
                : SortOrder.None;
        }
    }
}
