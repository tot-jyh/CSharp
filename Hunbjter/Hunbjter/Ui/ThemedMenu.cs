namespace Hunbjter;

/// <summary>Dark palette for context menus.</summary>
internal sealed class ThemedColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.Surface;

    public override Color ImageMarginGradientBegin => Theme.Surface;

    public override Color ImageMarginGradientMiddle => Theme.Surface;

    public override Color ImageMarginGradientEnd => Theme.Surface;

    public override Color MenuItemSelected => Theme.SurfaceHover;

    public override Color MenuItemSelectedGradientBegin => Theme.SurfaceHover;

    public override Color MenuItemSelectedGradientEnd => Theme.SurfaceHover;

    public override Color MenuItemBorder => Theme.Border;

    public override Color MenuBorder => Theme.Border;

    public override Color SeparatorDark => Theme.BorderSubtle;

    public override Color SeparatorLight => Theme.BorderSubtle;
}

internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
{
    public ThemedMenuRenderer()
        : base(new ThemedColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.TextPrimary : Theme.TextMuted;
        base.OnRenderItemText(e);
    }

    /// <summary>
    /// Applied per strip rather than through <see cref="ToolStripManager.Renderer"/>,
    /// which would also restyle chrome the app does not own.
    /// </summary>
    public static void Apply(ToolStrip strip)
    {
        strip.Renderer = new ThemedMenuRenderer();
        strip.BackColor = Theme.Surface;
        strip.ForeColor = Theme.TextPrimary;
        strip.Font = Theme.Base;

        if (strip is ToolStripDropDown dropDown)
        {
            dropDown.DropShadowEnabled = false;
        }
    }
}
