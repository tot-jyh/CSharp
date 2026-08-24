using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Hunbjter;

public sealed class SiteManagementForm : Form
{
    private const string PlusTabText = "+";

    private readonly SiteSettingsStore siteSettingsStore;
    private readonly LoginSettingsStore legacySettingsStore;
    private readonly TableLayoutPanel rootLayout = new();
    private readonly TableLayoutPanel contentLayout = new();
    private readonly TableLayoutPanel siteColumnLayout = new();
    private readonly TableLayoutPanel siteTabHeaderLayout = new();
    private readonly FlowLayoutPanel siteTabStrip = new();
    private readonly FlowLayoutPanel siteActionPanel = new();
    private readonly Button addSiteButton = new();
    private readonly Button deleteSiteButton = new();
    private readonly Panel siteEditorHost = new();
    private readonly Panel browserFrame = new();
    private readonly WebView2 webView = new();
    private readonly FlowLayoutPanel bottomButtonPanel = new();
    private readonly Button saveButton = new();
    private readonly Button closeButton = new();
    private readonly FavoriteStore favoriteStore = new();
    private readonly HashSet<string> capturedNetworkUrls = new(StringComparer.OrdinalIgnoreCase);

    private SiteSettingsDocument document;
    private SiteProfile? selectedSite;
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
                Name = string.IsNullOrWhiteSpace(legacySettings.LoginUrl) ? Texts.Panda : Texts.Panda,
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
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 640);
        MinimumSize = new Size(820, 520);

        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowCount = 1;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Padding = new Padding(24);

        contentLayout.ColumnCount = 2;
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        contentLayout.Dock = DockStyle.Fill;
        contentLayout.Margin = new Padding(0);

        siteColumnLayout.ColumnCount = 1;
        siteColumnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        siteColumnLayout.Dock = DockStyle.Fill;
        siteColumnLayout.Margin = new Padding(0, 0, 20, 0);
        siteColumnLayout.RowCount = 2;
        siteColumnLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        siteColumnLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        siteTabHeaderLayout.ColumnCount = 1;
        siteTabHeaderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        siteTabHeaderLayout.RowCount = 4;
        siteTabHeaderLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        siteTabHeaderLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12F));
        siteTabHeaderLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        siteTabHeaderLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        siteTabHeaderLayout.AutoSize = true;
        siteTabHeaderLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        siteTabHeaderLayout.Dock = DockStyle.Top;
        siteTabHeaderLayout.Margin = new Padding(0);

        siteTabStrip.AutoSize = true;
        siteTabStrip.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        siteTabStrip.Dock = DockStyle.Top;
        siteTabStrip.Margin = new Padding(0, 0, 0, 2);
        siteTabStrip.Padding = new Padding(0);
        siteTabStrip.AutoScroll = false;
        siteTabStrip.WrapContents = true;

        siteActionPanel.Dock = DockStyle.Fill;
        siteActionPanel.FlowDirection = FlowDirection.LeftToRight;
        siteActionPanel.Margin = new Padding(0, 4, 0, 0);
        siteActionPanel.Padding = new Padding(0);
        siteActionPanel.WrapContents = false;

        addSiteButton.Size = new Size(82, 28);
        addSiteButton.Margin = new Padding(0, 0, 6, 0);
        addSiteButton.Text = $"{PlusTabText} \uCD94\uAC00";
        addSiteButton.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
        addSiteButton.UseVisualStyleBackColor = true;
        addSiteButton.Click += (_, _) => AddSiteTab();

        deleteSiteButton.Size = new Size(82, 28);
        deleteSiteButton.Margin = new Padding(0);
        deleteSiteButton.Text = "\uC0AD\uC81C";
        deleteSiteButton.UseVisualStyleBackColor = true;
        deleteSiteButton.Click += (_, _) =>
        {
            if (selectedSite is not null)
            {
                DeleteSite(selectedSite);
            }
        };

        siteEditorHost.BorderStyle = BorderStyle.FixedSingle;
        siteEditorHost.Dock = DockStyle.Fill;
        siteEditorHost.Margin = new Padding(0);

        siteActionPanel.Controls.Add(addSiteButton);
        siteActionPanel.Controls.Add(deleteSiteButton);
        siteTabHeaderLayout.Controls.Add(siteTabStrip, 0, 0);
        var siteSeparator = new Panel
        {
            BackColor = Color.FromArgb(210, 210, 210),
            Dock = DockStyle.Top,
            Height = 1,
            Margin = new Padding(0, 6, 0, 5)
        };
        siteTabHeaderLayout.Controls.Add(siteSeparator, 0, 1);
        siteTabHeaderLayout.Controls.Add(CreateTopConnectButtonHost(), 0, 2);
        siteTabHeaderLayout.Controls.Add(siteActionPanel, 0, 3);

        siteColumnLayout.Controls.Add(siteTabHeaderLayout, 0, 0);
        siteColumnLayout.Controls.Add(siteEditorHost, 0, 1);

        var browserColumnLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0),
            RowCount = 2
        };
        browserColumnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        browserColumnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        browserColumnLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        browserFrame.BorderStyle = BorderStyle.FixedSingle;
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
        bottomButtonPanel.Controls.Add(saveButton);
        bottomButtonPanel.Controls.Add(closeButton);

        saveButton.Text = Texts.Confirm;
        saveButton.Size = new Size(82, 28);
        saveButton.UseVisualStyleBackColor = true;
        saveButton.Click += saveButton_Click;

        closeButton.Text = Texts.Cancel;
        closeButton.Size = new Size(82, 28);
        closeButton.UseVisualStyleBackColor = true;
        closeButton.Click += (_, _) => Close();

        browserColumnLayout.Controls.Add(bottomButtonPanel, 0, 0);
        browserColumnLayout.Controls.Add(browserFrame, 0, 1);

        rootLayout.Controls.Add(contentLayout, 0, 0);
        Controls.Add(rootLayout);
    }

    private Control CreateTopConnectButtonHost()
    {
        var host = new TableLayoutPanel
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

        var connectButton = new Button
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Size = new Size(112, 28),
            Text = Texts.Connect,
            UseVisualStyleBackColor = true
        };
        connectButton.Click += async (_, _) => await ConnectSelectedSiteAsync(connectButton);
        host.Controls.Add(connectButton, 1, 0);
        return host;
    }

    private async Task ConnectSelectedSiteAsync(Button connectButton)
    {
        if (selectedSite is null)
        {
            return;
        }

        var password = LoginSettingsStore.UnprotectPassword(selectedSite.EncryptedPassword);
        using var statusHost = new Label();
        await ConnectAsync(selectedSite, password, connectButton, statusHost);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK && HasUnsavedChanges())
        {
            var result = MessageBox.Show(
                this,
                "?€?¥í•˜ì§€ ?Šê³  ?«ì„ê¹Œìš”?",
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

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

    private Button CreateSiteButton(SiteProfile site)
    {
        var isSelected = site == selectedSite;
        var button = new Button
        {
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 3, 3),
            Size = new Size(96, 28),
            Tag = site,
            Text = DisplayName(site),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false
        };

        if (isSelected)
        {
            button.BackColor = Color.FromArgb(37, 99, 235);
            button.ForeColor = Color.White;
            button.Font = new Font(Font, FontStyle.Bold);
            button.FlatAppearance.BorderColor = Color.FromArgb(30, 64, 175);
            button.FlatAppearance.BorderSize = 2;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 99, 235);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(29, 78, 216);
        }
        else
        {
            button.BackColor = SystemColors.Control;
            button.ForeColor = SystemColors.ControlText;
            button.Font = new Font(Font, FontStyle.Regular);
            button.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(229, 241, 251);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(204, 228, 247);
        }

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
        var editorLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 16, 12, 12)
        };

        editorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 318F));
        editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var panel = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

        var nameTextBox = CreateTextBox(Texts.SiteName);
        nameTextBox.Text = site.Name;
        nameTextBox.TextChanged += (_, _) =>
        {
            site.Name = nameTextBox.Text.Trim();
        };

        var urlTextBox = CreateTextBox("url");
        urlTextBox.Text = site.LoginUrl;
        urlTextBox.TextChanged += (_, _) => site.LoginUrl = urlTextBox.Text.Trim();

        var idTextBox = CreateTextBox("id");
        idTextBox.Text = site.UserId;
        idTextBox.TextChanged += (_, _) => site.UserId = idTextBox.Text.Trim();

        var passwordTextBox = CreateTextBox("pw");
        passwordTextBox.PasswordChar = '*';
        passwordTextBox.Text = LoginSettingsStore.UnprotectPassword(site.EncryptedPassword);
        passwordTextBox.TextChanged += (_, _) => site.EncryptedPassword = LoginSettingsStore.ProtectPassword(passwordTextBox.Text);var captureButton = new Button
        {
            Size = new Size(112, 34),
            Text = "\uC694\uCCAD\uCEA1\uCC98",
            Visible = false,
            UseVisualStyleBackColor = true
        };

        var statusLabel = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            Text = Texts.EnterSiteInfo,
            TextAlign = ContentAlignment.MiddleLeft
        };
        captureButton.Click += async (_, _) => await ToggleNetworkCaptureAsync(captureButton, statusLabel);

        panel.Controls.Add(CreateFieldPanel(Texts.SiteNameLabel, nameTextBox), 0, 0);
        panel.Controls.Add(CreateFieldPanel(Texts.UrlLabel, urlTextBox), 0, 1);
        panel.Controls.Add(CreateFieldPanel(Texts.IdLabel, idTextBox), 0, 2);
        panel.Controls.Add(CreateFieldPanel(Texts.PasswordLabel, passwordTextBox), 0, 3);
        panel.Controls.Add(statusLabel, 0, 6);

        editorLayout.Controls.Add(panel, 0, 0);

        return editorLayout;
    }

    private Control CreateModelManagementPanel(SiteProfile site, Label statusLabel)
    {
        var groupBox = new GroupBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0),
            Text = "\uBAA8\uB378\uAD00\uB9AC"
        };

        var layout = new TableLayoutPanel
        {
            ColumnCount = 3,
            Dock = DockStyle.Top,
            Padding = new Padding(10, 16, 10, 10),
            RowCount = 2
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

        var nicknameTextBox = CreateTextBox("\uB2C9\uB124\uC784");
        var idTextBox = CreateTextBox("\uC544\uC774\uB514");
        var addButton = new Button
        {
            Dock = DockStyle.Bottom,
            Text = "\uCD94\uAC00",
            UseVisualStyleBackColor = true
        };

        var hintLabel = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            Text = "\uBAA8\uB378 \uC815\uBCF4\uB294 \uB514\uC790\uC778 \uD655\uC778 \uD6C4 \uC800\uC7A5\uC18C\uC5D0 \uC5F0\uACB0\uD569\uB2C8\uB2E4.",
            TextAlign = ContentAlignment.MiddleLeft
        };

        addButton.Click += (_, _) =>
        {
            AddModelToFavorites(site, nicknameTextBox.Text, idTextBox.Text, statusLabel);
            nicknameTextBox.Clear();
            idTextBox.Clear();
            nicknameTextBox.Focus();
        };

        layout.Controls.Add(CreateFieldPanel("\uB2C9\uB124\uC784", nicknameTextBox), 0, 0);
        layout.Controls.Add(CreateFieldPanel("\uC544\uC774\uB514", idTextBox), 1, 0);
        layout.Controls.Add(addButton, 2, 0);
        layout.Controls.Add(hintLabel, 0, 1);
        layout.SetColumnSpan(hintLabel, 3);

        groupBox.Controls.Add(layout);
        return groupBox;
    }

    private void AddModelToFavorites(SiteProfile site, string nickname, string platformUserId, Label statusLabel)
    {
        nickname = nickname.Trim();
        platformUserId = platformUserId.Trim();

        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(platformUserId))
        {
            statusLabel.Text = "\uB2C9\uB124\uC784\uACFC \uC544\uC774\uB514\uB97C \uBAA8\uB450 \uC785\uB825\uD558\uC138\uC694.";
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

            statusLabel.Text = string.Format("\uBAA9\uB85D\uC5D0 \uCD94\uAC00\uD588\uC2B5\uB2C8\uB2E4: {0}", nickname);
        }
        else
        {
            existing.Platform = DisplayName(site);
            existing.PlatformUserId = platformUserId;
            existing.DisplayName = nickname;
            existing.ProfileUrl = BuildProfileUrl(site.LoginUrl, platformUserId);
            existing.LastKnownUrl = existing.ProfileUrl;
            existing.UpdatedAt = now;

            statusLabel.Text = string.Format("\uAE30\uC874 \uBAA9\uB85D\uC744 \uAC31\uC2E0\uD588\uC2B5\uB2C8\uB2E4: {0}", nickname);
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

    private static TextBox CreateTextBox(string placeholder)
    {
        return new TextBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = placeholder,
            TextAlign = HorizontalAlignment.Left
        };
    }

    private static TableLayoutPanel CreateFieldPanel(string labelText, TextBox textBox)
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = labelText,
            TextAlign = ContentAlignment.BottomLeft
        };

        textBox.Dock = DockStyle.Fill;

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(textBox, 0, 1);
        return panel;
    }

    private async Task ConnectAsync(SiteProfile site, string password, Button connectButton, Label statusLabel)
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

    private async Task ToggleNetworkCaptureAsync(Button captureButton, Label statusLabel)
    {
        if (networkCaptureEnabled)
        {
            await StopNetworkCaptureAsync(captureButton, statusLabel);
            return;
        }

        await StartNetworkCaptureAsync(captureButton, statusLabel);
    }

    private async Task StartNetworkCaptureAsync(Button captureButton, Label statusLabel)
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
        captureButton.Text = "\uCEA1\uCC98\uC911\uC9C0";
        statusLabel.Text = $"\uC694\uCCAD \uCEA1\uCC98 \uC2DC\uC791: {networkCapturePath}";
    }

    private async Task StopNetworkCaptureAsync(Button captureButton, Label statusLabel)
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
        captureButton.Text = "\uC694\uCCAD\uCEA1\uCC98";

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

        statusLabel.Text = $"\uC694\uCCAD \uCEA1\uCC98 \uC2DC\uC791: {networkCapturePath}";
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
        var result = ShowConfirmDialog(
            string.Format(Texts.DeleteConfirm, DisplayName(site)),
            Texts.DeleteSite);

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

    private DialogResult ShowConfirmDialog(string message, string title)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(320, 130)
        };

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(18)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

        var messageLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = message,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };

        var yesButton = new Button
        {
            DialogResult = DialogResult.Yes,
            Size = new Size(82, 28),
            Text = "\uC608"
        };

        var noButton = new Button
        {
            DialogResult = DialogResult.No,
            Size = new Size(82, 28),
            Text = "\uC544\uB2C8\uC694"
        };

        buttonPanel.Controls.Add(yesButton);
        buttonPanel.Controls.Add(noButton);
        layout.Controls.Add(messageLabel, 0, 0);
        layout.Controls.Add(buttonPanel, 0, 1);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = yesButton;
        dialog.CancelButton = noButton;

        return dialog.ShowDialog(this);
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

    private static string BuildTabText(SiteProfile site)
    {
        return $"{DisplayName(site)}  -";
    }

    private static string DisplayName(SiteProfile site)
    {
        return string.IsNullOrWhiteSpace(site.Name) ? Texts.NewSite : site.Name.Trim();
    }

    private static class Texts
    {
        public const string SiteManagement = "\uC0AC\uC774\uD2B8\uAD00\uB9AC";
        public const string Panda = "\uD32C\uB354";
        public const string Confirm = "\uD655\uC778";
        public const string Cancel = "\uCDE8\uC18C";
        public const string Connect = "\uC5F0\uACB0";
        public const string SiteName = "\uC0AC\uC774\uD2B8\uBA85";
        public const string SiteNameLabel = "\uC0AC\uC774\uD2B8\uBA85";
        public const string UrlLabel = "URL";
        public const string IdLabel = "\uC544\uC774\uB514";
        public const string PasswordLabel = "\uBE44\uBC00\uBC88\uD638";
        public const string EnterSiteInfo = "\uC0AC\uC774\uD2B8 \uC815\uBCF4\uB97C \uC785\uB825\uD55C \uB4A4 \uC5F0\uACB0\uC744 \uB204\uB974\uC138\uC694.";
        public const string EnterPassword = "\uBE44\uBC00\uBC88\uD638\uB97C \uC785\uB825\uD558\uC138\uC694.";
        public const string Connecting = "\uC5F0\uACB0 \uC911...";
        public const string ConnectFailed = "\uC5F0\uACB0 \uC2E4\uD328: {0}";
        public const string NewSiteName = "\uC0AC\uC774\uD2B8 {0}";
        public const string NewSite = "\uC0C8 \uC0AC\uC774\uD2B8";
        public const string DeleteSite = "\uC0AC\uC774\uD2B8 \uC0AD\uC81C";
        public const string DeleteConfirm = "'{0}' \uC0AC\uC774\uD2B8\uB97C \uC0AD\uC81C\uD560\uAE4C\uC694?";
    }
}
