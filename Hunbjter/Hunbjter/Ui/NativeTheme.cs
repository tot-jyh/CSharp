using System.Runtime.InteropServices;

namespace Hunbjter;

/// <summary>
/// Pulls the OS-drawn parts of the window (title bar, borders, scroll bars) into the dark palette.
/// Every call is best-effort: the uxtheme entry points are undocumented ordinals, so a failure
/// must never be worse than a light scroll bar.
/// </summary>
internal static class NativeTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;

    // SetPreferredAppMode
    private const int PreferredAppModeForceDark = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
    private static extern int SetPreferredAppMode(int mode);

    [DllImport("uxtheme.dll", EntryPoint = "#136", SetLastError = true)]
    private static extern void FlushMenuThemes();

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    /// <summary>Call once during startup, before the first window is created.</summary>
    public static void EnableDarkMode()
    {
        try
        {
            SetPreferredAppMode(PreferredAppModeForceDark);
            FlushMenuThemes();
        }
        catch
        {
            // Undocumented ordinals; older or future builds may not expose them.
        }
    }

    /// <summary>Paints the form surface dark and darkens its native frame once the handle exists.</summary>
    public static void Apply(Form form)
    {
        form.BackColor = Theme.Background;
        form.ForeColor = Theme.TextPrimary;
        form.Font = Theme.Base;

        if (form.IsHandleCreated)
        {
            ApplyFrame(form.Handle);
        }

        // Re-apply on every handle recreation, otherwise the frame reverts to light.
        form.HandleCreated += (sender, _) =>
        {
            if (sender is Form created)
            {
                ApplyFrame(created.Handle);
            }
        };
    }

    /// <summary>
    /// Tints the DWM-drawn frame. Must run after the handle exists — before that it is a
    /// silent no-op — and again after every handle recreation (setting <c>ShowInTaskbar</c>,
    /// <c>FormBorderStyle</c> or <c>Opacity</c> recreates it and drops these attributes).
    /// </summary>
    public static void ApplyFrame(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var useDark = 1;
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));

            var caption = ToColorRef(Theme.Background);
            DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref caption, sizeof(int));

            var border = ToColorRef(Theme.Border);
            DwmSetWindowAttribute(handle, DwmwaBorderColor, ref border, sizeof(int));
        }
        catch
        {
            // Frame tinting is cosmetic; a light title bar is an acceptable degradation.
        }
    }

    /// <summary>
    /// Switches a control and every descendant to the dark common-control theme.
    /// This is what darkens the scroll bars owned by <see cref="DataGridView"/> and friends.
    /// </summary>
    public static void ApplyScrollBars(Control control)
    {
        if (control.IsHandleCreated)
        {
            ApplyDarkExplorerTheme(control);
        }
        else
        {
            control.HandleCreated += (sender, _) =>
            {
                if (sender is Control created)
                {
                    ApplyDarkExplorerTheme(created);
                }
            };
        }
    }

    private static void ApplyDarkExplorerTheme(Control control)
    {
        try
        {
            SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
        }
        catch
        {
            // Non-fatal: the control keeps the light system theme.
        }

        foreach (Control child in control.Controls)
        {
            ApplyScrollBars(child);
        }
    }

    /// <summary>Win32 COLORREF is 0x00BBGGRR, the reverse of the usual RGB packing.</summary>
    private static int ToColorRef(Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }
}
