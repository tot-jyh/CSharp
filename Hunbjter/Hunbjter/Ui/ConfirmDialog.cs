namespace Hunbjter;

/// <summary>
/// Yes/No prompt in the app palette. <see cref="MessageBox"/> is a native dialog and cannot
/// be themed, so it would flash a light window in the middle of a dark app.
/// </summary>
internal static class ConfirmDialog
{
    public static DialogResult Ask(IWin32Window owner, string title, string message, string? detail = null)
    {
        using var dialog = new ThemedDialog
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(400, detail is null ? 160 : 196),
            Padding = new Padding(20)
        };

        var root = new BufferedTableLayoutPanel
        {
            BackColor = Theme.Background,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

        var messageLabel = new Label
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Font = Theme.BaseBold,
            ForeColor = Theme.TextPrimary,
            Text = message,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var detailLabel = new Label
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextSecondary,
            Text = detail ?? "",
            TextAlign = ContentAlignment.TopLeft
        };

        var yesButton = new ThemedButton
        {
            DialogResult = DialogResult.Yes,
            Margin = new Padding(8, 0, 0, 0),
            Size = new Size(88, 32),
            Text = "예",
            Variant = ButtonVariant.Primary
        };
        var noButton = new ThemedButton
        {
            DialogResult = DialogResult.No,
            Margin = new Padding(8, 0, 0, 0),
            Size = new Size(88, 32),
            Text = "아니요",
            Variant = ButtonVariant.Ghost
        };

        var buttonPanel = new FlowLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0),
            WrapContents = false
        };
        buttonPanel.Controls.Add(yesButton);
        buttonPanel.Controls.Add(noButton);

        root.Controls.Add(messageLabel, 0, 0);
        root.Controls.Add(detailLabel, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);
        dialog.Controls.Add(root);

        dialog.AcceptButton = yesButton;
        dialog.CancelButton = noButton;

        return dialog.ShowDialog(owner);
    }
}
