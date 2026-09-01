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
            UserDataFolder = UserDataFolder,

            // loginBrowserForm - the host of this shared WebView2 - stays Hide()'d whenever the
            // user isn't actively using 사이트관리, which is most of the app's runtime (every
            // background live check runs through it too). Chromium throttles JS timers hard in an
            // occluded/hidden window, and pandalive appears to lean on a JS-driven session/token
            // refresh (GetViewerUserIndexAsync scans localStorage/window state for it) - so while
            // hidden, that refresh barely runs and the session decays (cookie count observed
            // shrinking check to check), while a normal always-visible browser tab never sees
            // this because it's never occluded. These three flags are the standard trio
            // Electron/CEF apps use to make a background window's JS behave as if foregrounded.
            AdditionalBrowserArguments =
                "--disable-background-timer-throttling --disable-backgrounding-occluded-windows --disable-renderer-backgrounding"
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
