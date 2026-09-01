using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Hunbjter;

public sealed class SiteManagementForm : ThemedDialog
{
    private readonly SiteSettingsStore siteSettingsStore;
    private readonly LoginSettingsStore legacySettingsStore;
    private readonly BufferedTableLayoutPanel rootLayout = new();
    private readonly BufferedTableLayoutPanel contentLayout = new();
    private readonly BufferedTableLayoutPanel siteColumnLayout = new();
    private readonly BufferedTableLayoutPanel siteTabHeaderLayout = new();
    private readonly FlowLayoutPanel siteTabStrip = new();
    private readonly FlowLayoutPanel siteActionPanel = new();
    private readonly ThemedButton addSiteButton = new();
    private readonly ThemedButton deleteSiteButton = new();
    private readonly Panel siteEditorHost = new();
    private readonly Panel browserFrame = new();
    private readonly WebView2 webView = new();
    private readonly FlowLayoutPanel bottomButtonPanel = new();
    private readonly ThemedButton saveButton = new();
    private readonly ThemedButton closeButton = new();
    private readonly FavoriteStore favoriteStore = new();
    private readonly HashSet<string> capturedNetworkUrls = new(StringComparer.OrdinalIgnoreCase);

    private SiteSettingsDocument document;
    private SiteProfile? selectedSite;

    /// <summary>
    /// Rebuilt fresh by <see cref="CreateSiteEditor"/> every time a site tab is (re)shown - the
    /// top "연결" button (built once, in a different part of the layout) needs a stable reference
    /// to whichever instance is currently on screen so its login-automation result message
    /// actually reaches the user. It used to write into a throwaway, never-added Label instead,
    /// so a login automation failure (e.g. "아이디 입력칸을 찾지 못했습니다") was silently
    /// discarded and the button looked like it had done something even when it had not.
    /// </summary>
    private Label siteStatusLabel = new();
    private CoreWebView2DevToolsProtocolEventReceiver? requestWillBeSentReceiver;
    private CoreWebView2DevToolsProtocolEventReceiver? responseReceivedReceiver;
    private string networkCapturePath = "";
    private string apiCapturePath = "";
    private string savedDocumentSnapshot = "";
    private bool networkCaptureEnabled;

    public event EventHandler? FavoritesChanged;

    public SiteManagementForm(
        SiteSettingsStore siteSettingsStore,
        LoginSettingsStore legacySettingsStore,
        LoginSettings legacySettings)
    {
        this.siteSettingsStore = siteSettingsStore;
        this.legacySettingsStore = legacySettingsStore;
        document = siteSettingsStore.Load();

        if (document.Sites.Count == 0)
        {
            document.Sites.Add(new SiteProfile
            {
                Name = Texts.Panda,
                LoginUrl = legacySettings.LoginUrl,
                UserId = legacySettings.UserId,
                EncryptedPassword = legacySettings.EncryptedPassword
            });
        }

        InitializeComponent();
        ReloadTabs();
        savedDocumentSnapshot = CreateDocumentSnapshot();
    }

    public SiteSettingsDocument Document => document;

    private void InitializeComponent()
    {
        Text = Texts.SiteManagement;
        Size = new Size(1040, 700);
        MinimumSize = new Size(880, 560);

        rootLayout.BackColor = Theme.Background;
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowCount = 1;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Padding = new Padding(22);

        contentLayout.ColumnCount = 2;
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        contentLayout.Dock = DockStyle.Fill;
        contentLayout.Margin = new Padding(0);

        siteColumnLayout.ColumnCount = 1;
        siteColumnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        siteColumnLayout.Dock = DockStyle.Fill;
        siteColumnLayout.Margin = new Padding(0, 0, 18, 0);
        siteColumnLayout.RowCount = 2;
        siteColumnLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        siteColumnLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        siteTabHeaderLayout.ColumnCount = 1;
        siteTabHeaderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        siteTabHeaderLayout.RowCount = 4;
        siteTabHeaderLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        siteTabHeaderLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12F));
        siteTabHeaderLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        siteTabHeaderLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        siteTabHeaderLayout.AutoSize = true;
        siteTabHeaderLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        siteTabHeaderLayout.Dock = DockStyle.Top;
        siteTabHeaderLayout.Margin = new Padding(0);

        siteTabStrip.AutoSize = true;
        siteTabStrip.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        siteTabStrip.BackColor = Color.Transparent;
        siteTabStrip.Dock = DockStyle.Top;
        siteTabStrip.Margin = new Padding(0, 0, 0, 2);
        siteTabStrip.Padding = new Padding(0);
        siteTabStrip.AutoScroll = false;
        siteTabStrip.WrapContents = true;

        siteActionPanel.BackColor = Color.Transparent;
        siteActionPanel.Dock = DockStyle.Fill;
        siteActionPanel.FlowDirection = FlowDirection.LeftToRight;
        siteActionPanel.Margin = new Padding(0, 4, 0, 0);
        siteActionPanel.Padding = new Padding(0);
        siteActionPanel.WrapContents = false;

        addSiteButton.Margin = new Padding(0, 0, 8, 0);
        addSiteButton.Size = new Size(88, 32);
        addSiteButton.Text = "+ 추가";
        addSiteButton.Variant = ButtonVariant.Secondary;
        addSiteButton.Click += (_, _) => AddSiteTab();

        deleteSiteButton.Margin = new Padding(0);
        deleteSiteButton.Size = new Size(88, 32);
        deleteSiteButton.Text = "삭제";
        deleteSiteButton.Variant = ButtonVariant.Danger;
        deleteSiteButton.Click += (_, _) =>
        {
            if (selectedSite is not null)
            {
                DeleteSite(selectedSite);
            }
        };

        siteEditorHost.BackColor = Theme.Surface;
        siteEditorHost.Dock = DockStyle.Fill;
        siteEditorHost.Margin = new Padding(0);
        siteEditorHost.Padding = new Padding(1);

        siteActionPanel.Controls.Add(addSiteButton);
        siteActionPanel.Controls.Add(deleteSiteButton);
        siteTabHeaderLayout.Controls.Add(siteTabStrip, 0, 0);

        var siteSeparator = new Panel
        {
            BackColor = Theme.Border,
            Dock = DockStyle.Top,
            Height = 1,
            Margin = new Padding(0, 6, 0, 5)
        };
        siteTabHeaderLayout.Controls.Add(siteSeparator, 0, 1);
        siteTabHeaderLayout.Controls.Add(CreateTopConnectButtonHost(), 0, 2);
        siteTabHeaderLayout.Controls.Add(siteActionPanel, 0, 3);

        siteColumnLayout.Controls.Add(siteTabHeaderLayout, 0, 0);
        siteColumnLayout.Controls.Add(siteEditorHost, 0, 1);

        var browserColumnLayout = new BufferedTableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0),
            RowCount = 2
        };
        browserColumnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        browserColumnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        browserColumnLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        browserFrame.BackColor = Theme.Border;
        browserFrame.Dock = DockStyle.Fill;
        browserFrame.Margin = new Padding(0);
        browserFrame.Padding = new Padding(1);

        webView.AllowExternalDrop = true;
        webView.CreationProperties = WebViewProfile.CreateCreationProperties();
        webView.DefaultBackgroundColor = Color.White;
        webView.Dock = DockStyle.Fill;
        webView.ZoomFactor = 1D;
        browserFrame.Controls.Add(webView);

        contentLayout.Controls.Add(siteColumnLayout, 0, 0);
        contentLayout.Controls.Add(browserColumnLayout, 1, 0);

        bottomButtonPanel.Anchor = AnchorStyles.Right;
        bottomButtonPanel.AutoSize = true;
        bottomButtonPanel.BackColor = Color.Transparent;
        bottomButtonPanel.FlowDirection = FlowDirection.RightToLeft;
        bottomButtonPanel.Controls.Add(saveButton);
        bottomButtonPanel.Controls.Add(closeButton);

        saveButton.Margin = new Padding(8, 0, 0, 0);
        saveButton.Size = new Size(88, 32);
        saveButton.Text = Texts.Confirm;
        saveButton.Variant = ButtonVariant.Primary;
        saveButton.Click += saveButton_Click;

        closeButton.Margin = new Padding(8, 0, 0, 0);
        closeButton.Size = new Size(88, 32);
        closeButton.Text = Texts.Cancel;
        closeButton.Variant = ButtonVariant.Ghost;
        closeButton.Click += (_, _) => Close();

        browserColumnLayout.Controls.Add(bottomButtonPanel, 0, 0);
        browserColumnLayout.Controls.Add(browserFrame, 0, 1);

        rootLayout.Controls.Add(contentLayout, 0, 0);
        Controls.Add(rootLayout);
    }

    private Control CreateTopConnectButtonHost()
    {
        var host = new BufferedTableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0, 4, 0, 0),
            RowCount = 1
        };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var connectButton = new ThemedButton
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Size = new Size(112, 32),
            Text = Texts.Connect,
            Variant = ButtonVariant.Primary
        };
        connectButton.Click += async (_, _) => await ConnectSelectedSiteAsync(connectButton);
        host.Controls.Add(connectButton, 1, 0);
        return host;
    }

    private async Task ConnectSelectedSiteAsync(ThemedButton connectButton)
    {
        if (selectedSite is null)
        {
            return;
        }

        var password = LoginSettingsStore.UnprotectPassword(selectedSite.EncryptedPassword);
        await ConnectAsync(selectedSite, password, connectButton, siteStatusLabel);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK && HasUnsavedChanges())
        {
            var result = ConfirmDialog.Ask(this, Text, "저장하지 않고 닫을까요?", "변경한 사이트 정보가 사라집니다.");

            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnFormClosing(e);
    }

    private void ReloadTabs()
    {
        siteTabStrip.Controls.Clear();

        if (selectedSite is null || !document.Sites.Contains(selectedSite))
        {
            selectedSite = document.Sites.FirstOrDefault();
        }

        Button? selectedButton = null;
        foreach (var site in document.Sites)
        {
            var button = CreateSiteButton(site);
            siteTabStrip.Controls.Add(button);

            if (site == selectedSite)
            {
                selectedButton = button;
            }
        }

        deleteSiteButton.Enabled = selectedSite is not null;
        if (selectedButton is not null)
        {
            siteTabStrip.ScrollControlIntoView(selectedButton);
        }

        ShowSelectedSite();
    }

    private ThemedButton CreateSiteButton(SiteProfile site)
    {
        var button = new ThemedButton
        {
            Margin = new Padding(0, 0, 6, 6),
            Size = new Size(104, 32),
            Tag = site,
            Text = DisplayName(site),
            Variant = site == selectedSite ? ButtonVariant.Primary : ButtonVariant.Secondary
        };

        button.Click += (_, _) =>
        {
            selectedSite = site;
            ReloadTabs();
        };

        return button;
    }

    private void ShowSelectedSite()
    {
        siteEditorHost.Controls.Clear();

        if (selectedSite is null)
        {
            return;
        }

        siteEditorHost.Controls.Add(CreateSiteEditor(selectedSite));
    }

    private Control CreateSiteEditor(SiteProfile site)
    {
        var editorLayout = new BufferedTableLayoutPanel
        {
            BackColor = Theme.Surface,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 18, 16, 14),
            RowCount = 2
        };

        editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 320F));
        editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var panel = new BufferedTableLayoutPanel
        {
            BackColor = Theme.Surface,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            RowCount = 6
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

        var nameTextBox = CreateTextBox(Texts.SiteName);
        nameTextBox.Text = site.Name;
        nameTextBox.TextChanged += (_, _) => site.Name = nameTextBox.Text.Trim();

        var urlTextBox = CreateTextBox("url");
        urlTextBox.Text = site.LoginUrl;
        urlTextBox.TextChanged += (_, _) => site.LoginUrl = urlTextBox.Text.Trim();

        var idTextBox = CreateTextBox("id");
        idTextBox.Text = site.UserId;
        idTextBox.TextChanged += (_, _) => site.UserId = idTextBox.Text.Trim();

        var passwordTextBox = CreateTextBox("pw");
        passwordTextBox.PasswordChar = '*';
        passwordTextBox.Text = LoginSettingsStore.UnprotectPassword(site.EncryptedPassword);
        passwordTextBox.TextChanged += (_, _) => site.EncryptedPassword = LoginSettingsStore.ProtectPassword(passwordTextBox.Text);

        siteStatusLabel = new Label
        {
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            ForeColor = Theme.TextSecondary,
            Text = Texts.EnterSiteInfo,
            TextAlign = ContentAlignment.MiddleLeft
        };

        panel.Controls.Add(CreateFieldPanel(Texts.SiteNameLabel, nameTextBox), 0, 0);
        panel.Controls.Add(CreateFieldPanel(Texts.UrlLabel, urlTextBox), 0, 1);
        panel.Controls.Add(CreateFieldPanel(Texts.IdLabel, idTextBox), 0, 2);
        panel.Controls.Add(CreateFieldPanel(Texts.PasswordLabel, passwordTextBox), 0, 3);
        panel.Controls.Add(siteStatusLabel, 0, 5);

        editorLayout.Controls.Add(panel, 0, 0);
        return editorLayout;
    }

    private void AddModelToFavorites(SiteProfile site, string nickname, string platformUserId, Label statusLabel)
    {
        nickname = nickname.Trim();
        platformUserId = platformUserId.Trim();

        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(platformUserId))
        {
            statusLabel.Text = "닉네임과 아이디를 모두 입력하세요.";
            return;
        }

        var favorites = favoriteStore.Load();
        var platform = BuildPlatformKey(site);
        var id = $"{platform}:{platformUserId}";
        var now = DateTimeOffset.Now;
        var existing = favorites.Items.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            favorites.Items.Add(new FavoriteItem
            {
                Id = id,
                Platform = DisplayName(site),
                PlatformUserId = platformUserId,
                DisplayName = nickname,
                ProfileUrl = BuildProfileUrl(site.LoginUrl, platformUserId),
                LastKnownUrl = BuildProfileUrl(site.LoginUrl, platformUserId),
                CreatedAt = now,
                UpdatedAt = now
            });

            statusLabel.Text = $"목록에 추가했습니다: {nickname}";
        }
        else
        {
            existing.Platform = DisplayName(site);
            existing.PlatformUserId = platformUserId;
            existing.DisplayName = nickname;
            existing.ProfileUrl = BuildProfileUrl(site.LoginUrl, platformUserId);
            existing.LastKnownUrl = existing.ProfileUrl;
            existing.UpdatedAt = now;

            statusLabel.Text = $"기존 목록을 갱신했습니다: {nickname}";
        }

        favoriteStore.Save(favorites);
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string BuildPlatformKey(SiteProfile site)
    {
        var displayName = DisplayName(site);
        return string.IsNullOrWhiteSpace(displayName)
            ? "site"
            : displayName.Trim().ToLowerInvariant();
    }

    private static string BuildProfileUrl(string loginUrl, string platformUserId)
    {
        if (!Uri.TryCreate(loginUrl, UriKind.Absolute, out var uri))
        {
            return platformUserId;
        }

        var builder = new UriBuilder(uri)
        {
            Path = platformUserId.Trim('/'),
            Query = ""
        };

        return builder.Uri.ToString();
    }

    private static BufferedTableLayoutPanel CreateFieldPanel(string labelText, TextBox textBox)
    {
        var panel = new BufferedTableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));

        panel.Controls.Add(CreateLabel(labelText, Theme.Small, Theme.TextMuted), 0, 0);
        panel.Controls.Add(
            new InputHost(textBox) { Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0) },
            0,
            1);
        return panel;
    }

    private async Task ConnectAsync(SiteProfile site, string password, ThemedButton connectButton, Label statusLabel)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            statusLabel.Text = Texts.EnterPassword;
            return;
        }

        connectButton.Enabled = false;
        saveButton.Enabled = false;
        statusLabel.Text = Texts.Connecting;
        SaveDocument();

        try
        {
            var automation = new WebViewLoginAutomation(webView);
            var result = await automation.LoginAsync(ToLoginSettings(site), password, CancellationToken.None);
            statusLabel.Text = result.Message;
        }
        catch (Exception ex)
        {
            statusLabel.Text = string.Format(Texts.ConnectFailed, ex.Message);
        }
        finally
        {
            connectButton.Enabled = true;
            saveButton.Enabled = true;
        }
    }

    private async Task ToggleNetworkCaptureAsync(ThemedButton captureButton, Label statusLabel)
    {
        if (networkCaptureEnabled)
        {
            await StopNetworkCaptureAsync(captureButton, statusLabel);
            return;
        }

        await StartNetworkCaptureAsync(captureButton, statusLabel);
    }

    private async Task StartNetworkCaptureAsync(ThemedButton captureButton, Label statusLabel)
    {
        await WebViewProfile.EnsureCoreAsync(webView);

        capturedNetworkUrls.Clear();
        var captureTimestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        networkCapturePath = CreateCapturePath("network-capture", captureTimestamp, "log");
        apiCapturePath = CreateCapturePath("pandalive-api-capture", captureTimestamp, "jsonl");

        requestWillBeSentReceiver = webView.CoreWebView2.GetDevToolsProtocolEventReceiver("Network.requestWillBeSent");
        responseReceivedReceiver = webView.CoreWebView2.GetDevToolsProtocolEventReceiver("Network.responseReceived");
        requestWillBeSentReceiver.DevToolsProtocolEventReceived += NetworkRequestWillBeSent;
        responseReceivedReceiver.DevToolsProtocolEventReceived += NetworkResponseReceived;

        await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
        networkCaptureEnabled = true;
        captureButton.Text = "캡쳐중지";
        statusLabel.Text = $"요청 캡쳐 시작: {networkCapturePath}";
    }

    private async Task StopNetworkCaptureAsync(ThemedButton captureButton, Label statusLabel)
    {
        if (requestWillBeSentReceiver is not null)
        {
            requestWillBeSentReceiver.DevToolsProtocolEventReceived -= NetworkRequestWillBeSent;
        }

        if (responseReceivedReceiver is not null)
        {
            responseReceivedReceiver.DevToolsProtocolEventReceived -= NetworkResponseReceived;
        }

        requestWillBeSentReceiver = null;
        responseReceivedReceiver = null;
        networkCaptureEnabled = false;
        captureButton.Text = "요청캡쳐";

        try
        {
            if (webView.CoreWebView2 is not null)
            {
                await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.disable", "{}");
            }
        }
        catch
        {
            // Ignore DevTools shutdown errors while closing or navigating.
        }

        statusLabel.Text = $"요청 캡쳐 중지: {networkCapturePath}";
    }

    private void NetworkRequestWillBeSent(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        var url = ExtractNetworkUrl(e.ParameterObjectAsJson);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (ShouldCaptureNetworkUrl(url) && capturedNetworkUrls.Add(url))
        {
            try
            {
                File.AppendAllText(networkCapturePath, $"[{DateTime.Now:HH:mm:ss}] {url}{Environment.NewLine}");
            }
            catch
            {
                // Capture is diagnostic only; do not break the UI on logging failures.
            }
        }

        if (!ShouldCaptureApiDetails(url))
        {
            return;
        }

        try
        {
            AppendApiCapture(new
            {
                timestamp = DateTimeOffset.Now,
                eventName = "Network.requestWillBeSent",
                url,
                request = ExtractRequestDetails(e.ParameterObjectAsJson)
            });
        }
        catch
        {
            // Detail capture is diagnostic only.
        }
    }

    private async void NetworkResponseReceived(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        var url = ExtractNetworkUrl(e.ParameterObjectAsJson);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (ShouldCaptureNetworkUrl(url) && capturedNetworkUrls.Add(url))
        {
            try
            {
                File.AppendAllText(networkCapturePath, $"[{DateTime.Now:HH:mm:ss}] {url}{Environment.NewLine}");
            }
            catch
            {
                // Capture is diagnostic only; do not break the UI on logging failures.
            }
        }

        if (!ShouldCaptureApiDetails(url))
        {
            return;
        }

        try
        {
            var requestId = ExtractRequestId(e.ParameterObjectAsJson);
            var body = string.IsNullOrWhiteSpace(requestId)
                ? ""
                : await GetResponseBodyAsync(requestId);

            AppendApiCapture(new
            {
                timestamp = DateTimeOffset.Now,
                eventName = "Network.responseReceived",
                url,
                response = ExtractResponseDetails(e.ParameterObjectAsJson),
                body
            });
        }
        catch
        {
            // Detail capture is diagnostic only.
        }
    }

    private static string ExtractNetworkUrl(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("request", out var request)
                && request.TryGetProperty("url", out var requestUrl))
            {
                return requestUrl.GetString() ?? "";
            }

            if (root.TryGetProperty("response", out var response)
                && response.TryGetProperty("url", out var responseUrl))
            {
                return responseUrl.GetString() ?? "";
            }
        }
        catch
        {
            return "";
        }

        return "";
    }

    private static bool ShouldCaptureNetworkUrl(string url)
    {
        return url.Contains("pandalive", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)
            || url.Contains(".ts", StringComparison.OrdinalIgnoreCase)
            || url.Contains("stream", StringComparison.OrdinalIgnoreCase)
            || url.Contains("hls", StringComparison.OrdinalIgnoreCase)
            || url.Contains("live", StringComparison.OrdinalIgnoreCase)
            || url.Contains("play", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldCaptureApiDetails(string url)
    {
        return url.Contains("api.pandalive.co.kr/v1/live/play", StringComparison.OrdinalIgnoreCase)
            || url.Contains("api.pandalive.co.kr/v1/live/index", StringComparison.OrdinalIgnoreCase);
    }

    private void AppendApiCapture(object entry)
    {
        var json = JsonSerializer.Serialize(entry);
        File.AppendAllText(apiCapturePath, json + Environment.NewLine);
    }

    private async Task<string> GetResponseBodyAsync(string requestId)
    {
        if (webView.CoreWebView2 is null)
        {
            return "";
        }

        var argument = JsonSerializer.Serialize(new { requestId });
        return await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.getResponseBody", argument);
    }

    private static string ExtractRequestId(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("requestId", out var requestId)
                ? requestId.GetString() ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    private static object ExtractRequestDetails(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var request = root.GetProperty("request");

            return new
            {
                method = request.TryGetProperty("method", out var method) ? method.GetString() : "",
                postData = request.TryGetProperty("postData", out var postData) ? postData.GetString() : "",
                headers = request.TryGetProperty("headers", out var headers) ? JsonSerializer.Deserialize<Dictionary<string, object>>(headers.GetRawText()) : null
            };
        }
        catch
        {
            return new { error = "request parse failed" };
        }
    }

    private static object ExtractResponseDetails(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var response = root.GetProperty("response");

            return new
            {
                status = response.TryGetProperty("status", out var status) ? status.GetInt32() : 0,
                mimeType = response.TryGetProperty("mimeType", out var mimeType) ? mimeType.GetString() : "",
                headers = response.TryGetProperty("headers", out var headers) ? JsonSerializer.Deserialize<Dictionary<string, object>>(headers.GetRawText()) : null
            };
        }
        catch
        {
            return new { error = "response parse failed" };
        }
    }

    private static string CreateCapturePath(string prefix, string timestamp, string extension)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hunbjter");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"{prefix}-{timestamp}.{extension}");
    }

    private void AddSiteTab()
    {
        var site = new SiteProfile
        {
            Name = string.Format(Texts.NewSiteName, document.Sites.Count + 1)
        };

        document.Sites.Add(site);
        selectedSite = site;
        ReloadTabs();
    }

    private void DeleteSite(SiteProfile site)
    {
        var result = ConfirmDialog.Ask(
            this,
            Texts.DeleteSite,
            string.Format(Texts.DeleteConfirm, DisplayName(site)));

        if (result != DialogResult.Yes)
        {
            return;
        }

        document.Sites.Remove(site);
        selectedSite = document.Sites.FirstOrDefault();

        if (document.Sites.Count == 0)
        {
            AddSiteTab();
            return;
        }

        ReloadTabs();
    }

    private void saveButton_Click(object? sender, EventArgs e)
    {
        SaveDocument();
        DialogResult = DialogResult.OK;
    }

    private void SaveDocument()
    {
        siteSettingsStore.Save(document);
        savedDocumentSnapshot = CreateDocumentSnapshot();

        var first = document.Sites.FirstOrDefault();
        if (first is not null)
        {
            var legacySettings = legacySettingsStore.Load();
            legacySettings.LoginUrl = first.LoginUrl;
            legacySettings.UserId = first.UserId;
            legacySettings.EncryptedPassword = first.EncryptedPassword;
            legacySettingsStore.Save(legacySettings);
        }
    }

    private bool HasUnsavedChanges()
    {
        return !CreateDocumentSnapshot().Equals(savedDocumentSnapshot, StringComparison.Ordinal);
    }

    private string CreateDocumentSnapshot()
    {
        return JsonSerializer.Serialize(document);
    }

    private static LoginSettings ToLoginSettings(SiteProfile site)
    {
        return new LoginSettings
        {
            LoginUrl = site.LoginUrl,
            UserId = site.UserId,
            EncryptedPassword = site.EncryptedPassword
        };
    }

    private static string DisplayName(SiteProfile site)
    {
        return string.IsNullOrWhiteSpace(site.Name) ? Texts.NewSite : site.Name.Trim();
    }

    private static class Texts
    {
        public const string SiteManagement = "사이트관리";
        public const string Panda = "팬더";
        public const string Confirm = "확인";
        public const string Cancel = "취소";
        public const string Connect = "연결";
        public const string SiteName = "사이트명";
        public const string SiteNameLabel = "사이트명";
        public const string UrlLabel = "URL";
        public const string IdLabel = "아이디";
        public const string PasswordLabel = "비밀번호";
        public const string EnterSiteInfo = "사이트 정보를 입력한 뒤 연결을 누르세요.";
        public const string EnterPassword = "비밀번호를 입력하세요.";
        public const string Connecting = "연결 중...";
        public const string ConnectFailed = "연결 실패: {0}";
        public const string NewSiteName = "사이트 {0}";
        public const string NewSite = "새 사이트";
        public const string DeleteSite = "사이트 삭제";
        public const string DeleteConfirm = "'{0}' 사이트를 삭제할까요?";
    }
}
