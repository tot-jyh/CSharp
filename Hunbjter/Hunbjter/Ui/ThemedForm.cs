namespace Hunbjter;

/// <summary>
/// Base for every window in the app. Paints the client area dark and keeps the native
/// frame dark across handle recreations.
/// </summary>
public class ThemedForm : Form
{
    public ThemedForm()
    {
        BackColor = Theme.Background;
        ForeColor = Theme.TextPrimary;
        Font = Theme.Base;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Fires again after a handle recreation, which is exactly what makes this survive
        // Form1 setting ShowInTaskbar = false during tray setup.
        NativeTheme.ApplyFrame(Handle);
    }
}

/// <summary>Modal dialog flavor of <see cref="ThemedForm"/>.</summary>
public class ThemedDialog : ThemedForm
{
    public ThemedDialog()
    {
        Icon = AppIcon.Shared;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
    }

    public static Label CreateLabel(string text, Font? font = null, Color? color = null)
    {
        return new Label
        {
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Font = font ?? Theme.Base,
            ForeColor = color ?? Theme.TextSecondary,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    public static ThemedTextBox CreateTextBox(string placeholder = "")
    {
        return new ThemedTextBox
        {
            PlaceholderText = placeholder
        };
    }

    public static ThemedNumeric CreateNumeric(int minimum, int maximum, int increment)
    {
        var numeric = new ThemedNumeric
        {
            Increment = increment
        };

        // Widen the range before assigning either bound, otherwise the assignment clamps.
        numeric.Maximum = Math.Max(maximum, minimum);
        numeric.Minimum = minimum;
        return numeric;
    }
}
