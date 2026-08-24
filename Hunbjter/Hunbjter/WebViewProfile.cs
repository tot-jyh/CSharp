using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Hunbjter;

internal static class WebViewProfile
{
    private static readonly string UserDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Hunbjter",
        "WebView2Profile");

    public static CoreWebView2CreationProperties CreateCreationProperties()
    {
        Directory.CreateDirectory(UserDataFolder);

        return new CoreWebView2CreationProperties
        {
            UserDataFolder = UserDataFolder
        };
    }

    public static Task EnsureCoreAsync(WebView2 webView)
    {
        if (webView.CreationProperties is null && webView.CoreWebView2 is null)
        {
            webView.CreationProperties = CreateCreationProperties();
        }

        return webView.EnsureCoreWebView2Async();
    }
}
