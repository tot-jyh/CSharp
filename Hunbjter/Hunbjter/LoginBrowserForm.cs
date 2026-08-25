using Microsoft.Web.WebView2.WinForms;

namespace Hunbjter;

public sealed class LoginBrowserForm : ThemedForm
{
    private readonly WebView2 webView = new();
    private bool closingForDispose;

    public LoginBrowserForm()
    {
        Text = "Hunbjter Login";
        Icon = AppIcon.Shared;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 720);
        MinimumSize = new Size(720, 520);
        ShowInTaskbar = false;

        webView.AllowExternalDrop = true;
        webView.CreationProperties = WebViewProfile.CreateCreationProperties();
        webView.DefaultBackgroundColor = Color.White;
        webView.Dock = DockStyle.Fill;
        webView.ZoomFactor = 1D;

        Controls.Add(webView);
    }

    public WebView2 WebView => webView;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!closingForDispose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        closingForDispose = true;

        if (disposing)
        {
            webView.Dispose();
        }

        base.Dispose(disposing);
    }
}
