namespace Hunbjter;

public sealed class ModelManagementForm : Form
{
    private readonly SiteSettingsStore siteSettingsStore;
    private readonly FavoriteStore favoriteStore;
    private readonly HashSet<string> recordingFavoriteIds;
    private readonly DataGridView modelGrid = new();
    private readonly ComboBox siteComboBox = new();
    private readonly TextBox nicknameTextBox = new();
    private readonly TextBox userIdTextBox = new();
    private readonly TextBox urlTextBox = new();
    private readonly TextBox memoTextBox = new();
    private readonly TextBox broadcastStartTextBox = new();
    private readonly TextBox broadcastEndTextBox = new();
    private readonly CheckBox enabledCheckBox = new();
    private readonly Button addButton = new();
    private readonly Button updateButton = new();
    private readonly Button deleteButton = new();
    private readonly Button closeButton = new();
    private readonly Label statusLabel = new();
    private readonly ContextMenuStrip modelContextMenu = new();
    private readonly ToolStripMenuItem toggleWatchMenuItem = new();

    private SiteSettingsDocument sites = new();
    private FavoritesDocument favorites = new();
    private string selectedFavoriteId = "";
    private bool refreshingGrid;
    private bool loadingEditor;
    private bool updatingUrlFromUserId;
    private bool urlEditedManually;

    public ModelManagementForm(
        SiteSettingsStore siteSettingsStore,
        FavoriteStore favoriteStore,
        HashSet<string> recordingFavoriteIds)
    {
        this.siteSettingsStore = siteSettingsStore;
        this.favoriteStore = favoriteStore;
        this.recordingFavoriteIds = recordingFavoriteIds;

        InitializeComponent();
        LoadData();
    }

    public event EventHandler? FavoritesChanged;

    private void InitializeComponent()
    {
        Text = "\uBAA8\uB378\uAD00\uB9AC";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 640);
        MinimumSize = new Size(860, 560);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 4
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 204F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

        modelGrid.AllowUserToAddRows = false;
        modelGrid.AllowUserToDeleteRows = false;
        modelGrid.AllowUserToResizeRows = false;
        modelGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        modelGrid.BackgroundColor = Color.White;
        modelGrid.Dock = DockStyle.Fill;
        modelGrid.MultiSelect = false;
        modelGrid.ReadOnly = false;
        modelGrid.RowHeadersVisible = false;
        modelGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        modelGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Watch", FillWeight = 45, Name = "Enabled", ReadOnly = false });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "\uC0AC\uC774\uD2B8", FillWeight = 80, Name = "Platform", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "\uB2C9\uB124\uC784", FillWeight = 100, Name = "DisplayName", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "\uC544\uC774\uB514", FillWeight = 110, Name = "PlatformUserId", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "URL", FillWeight = 190, Name = "ProfileUrl", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "\uBC29\uC1A1\uC2DC\uAC04", FillWeight = 120, Name = "BroadcastTime", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "\uBA54\uBAA8", FillWeight = 130, Name = "Memo", ReadOnly = true });
        modelGrid.SelectionChanged += (_, _) => LoadSelectedRow();
        modelGrid.CellMouseDown += modelGrid_CellMouseDown;
        modelGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (modelGrid.IsCurrentCellDirty)
            {
                modelGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        modelGrid.CellValueChanged += modelGrid_CellValueChanged;
        modelGrid.ContextMenuStrip = modelContextMenu;

        toggleWatchMenuItem.Click += (_, _) => ToggleSelectedWatchFromMenu();
        modelContextMenu.Items.Add(toggleWatchMenuItem);
        modelContextMenu.Opening += modelContextMenu_Opening;

        var editor = new TableLayoutPanel
        {
            ColumnCount = 4,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 4),
            RowCount = 3
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));

        siteComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        siteComboBox.Dock = DockStyle.Fill;
        siteComboBox.SelectedIndexChanged += (_, _) => UpdateUrlFromUserIdIfAutomatic();
        nicknameTextBox.Dock = DockStyle.Fill;
        userIdTextBox.Dock = DockStyle.Fill;
        userIdTextBox.TextChanged += (_, _) => UpdateUrlFromUserIdIfAutomatic();
        urlTextBox.Dock = DockStyle.Fill;
        urlTextBox.TextChanged += (_, _) =>
        {
            if (!loadingEditor && !updatingUrlFromUserId)
            {
                urlEditedManually = true;
            }
        };
        memoTextBox.Dock = DockStyle.Fill;
        broadcastStartTextBox.Dock = DockStyle.Fill;
        broadcastEndTextBox.Dock = DockStyle.Fill;
        enabledCheckBox.Checked = true;
        enabledCheckBox.Text = "Watch";
        enabledCheckBox.AutoSize = true;
        enabledCheckBox.Anchor = AnchorStyles.Left;

        var memoPanel = CreateFieldPanel("\uBA54\uBAA8", memoTextBox);
        editor.Controls.Add(CreateFieldPanel("\uC0AC\uC774\uD2B8", siteComboBox), 0, 0);
        editor.Controls.Add(CreateFieldPanel("\uB2C9\uB124\uC784", nicknameTextBox), 1, 0);
        editor.Controls.Add(CreateFieldPanel("\uC544\uC774\uB514", userIdTextBox), 2, 0);
        editor.Controls.Add(CreateFieldPanel("URL", urlTextBox), 3, 0);
        editor.Controls.Add(CreateFieldPanel("Watch", enabledCheckBox), 0, 1);
        editor.Controls.Add(CreateFieldPanel("\uC2DC\uC791\uC2DC\uAC04", broadcastStartTextBox), 1, 1);
        editor.Controls.Add(CreateFieldPanel("\uC885\uB8CC\uC2DC\uAC04", broadcastEndTextBox), 2, 1);
        editor.Controls.Add(memoPanel, 3, 1);

        var actionPanel = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };

        addButton.Text = "\uCD94\uAC00";
        addButton.Size = new Size(82, 28);
        addButton.Click += (_, _) => AddModel();
        updateButton.Text = "\uC218\uC815";
        updateButton.Size = new Size(82, 28);
        updateButton.Click += (_, _) => UpdateModel();
        deleteButton.Text = "\uC0AD\uC81C";
        deleteButton.Size = new Size(82, 28);
        deleteButton.Click += (_, _) => DeleteModel();
        closeButton.Text = "\uB2EB\uAE30";
        closeButton.Size = new Size(82, 28);
        closeButton.Click += (_, _) => Close();

        actionPanel.Controls.Add(addButton);
        actionPanel.Controls.Add(updateButton);
        actionPanel.Controls.Add(deleteButton);
        actionPanel.Controls.Add(closeButton);

        statusLabel.AutoEllipsis = true;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        root.Controls.Add(modelGrid, 0, 0);
        root.Controls.Add(editor, 0, 1);
        root.Controls.Add(statusLabel, 0, 2);
        root.Controls.Add(actionPanel, 0, 3);
        Controls.Add(root);
    }

    private void LoadData()
    {
        sites = siteSettingsStore.Load();
        if (sites.Sites.Count == 0)
        {
            sites.Sites.Add(new SiteProfile
            {
                Name = "\uD32C\uB354",
                LoginUrl = "https://www.pandalive.co.kr/"
            });
        }

        siteComboBox.DataSource = sites.Sites.ToList();
        siteComboBox.DisplayMember = nameof(SiteProfile.Name);

        favorites = favoriteStore.Load();
        RefreshGrid();
        ClearEditor();
    }

    private void RefreshGrid()
    {
        refreshingGrid = true;
        modelGrid.Rows.Clear();
        foreach (var favorite in favorites.Items.OrderBy(item => item.DisplayName))
        {
            var rowIndex = modelGrid.Rows.Add(
                favorite.Enabled,
                favorite.Platform,
                favorite.DisplayName,
                favorite.PlatformUserId,
                favorite.ProfileUrl,
                FormatBroadcastTime(favorite),
                favorite.Memo);
            modelGrid.Rows[rowIndex].Tag = favorite.Id;
        }

        refreshingGrid = false;
    }

    private void LoadSelectedRow()
    {
        if (modelGrid.SelectedRows.Count == 0
            || modelGrid.SelectedRows[0].Tag is not string id)
        {
            selectedFavoriteId = "";
            return;
        }

        var favorite = favorites.Items.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (favorite is null)
        {
            selectedFavoriteId = "";
            return;
        }

        selectedFavoriteId = favorite.Id;
        loadingEditor = true;
        SelectSite(favorite.Platform);
        nicknameTextBox.Text = favorite.DisplayName;
        userIdTextBox.Text = favorite.PlatformUserId;
        urlTextBox.Text = favorite.ProfileUrl;
        memoTextBox.Text = favorite.Memo;
        broadcastStartTextBox.Text = GetMetadata(favorite, "broadcastStartTime");
        broadcastEndTextBox.Text = GetMetadata(favorite, "broadcastEndTime");
        enabledCheckBox.Checked = favorite.Enabled;
        urlEditedManually = !IsDefaultProfileUrl(favorite.ProfileUrl, siteComboBox.SelectedItem as SiteProfile, favorite.PlatformUserId);
        loadingEditor = false;
        UpdateButtonStates();
    }

    private void modelGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (refreshingGrid || e.RowIndex < 0 || modelGrid.Columns[e.ColumnIndex].Name != "Enabled")
        {
            return;
        }

        if (modelGrid.Rows[e.RowIndex].Tag is not string id)
        {
            return;
        }

        var favorite = favorites.Items.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (favorite is null)
        {
            return;
        }

        var watch = Convert.ToBoolean(modelGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
        if (!watch && recordingFavoriteIds.Contains(favorite.Id))
        {
            statusLabel.Text = "\uB179\uD654\uC911\uC778 \uBAA8\uB378\uC740 Watch\uB97C Off\uB85C \uBCC0\uACBD\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            modelGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = true;
            return;
        }

        if (!SetFavoriteWatch(favorite, watch))
        {
            modelGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = favorite.Enabled;
        }
    }

    private void modelGrid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0)
        {
            return;
        }

        modelGrid.ClearSelection();
        modelGrid.Rows[e.RowIndex].Selected = true;
        modelGrid.CurrentCell = modelGrid.Rows[e.RowIndex].Cells[Math.Max(e.ColumnIndex, 0)];
    }

    private void modelContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!TryGetSelectedModel(out var favorite))
        {
            e.Cancel = true;
            return;
        }

        toggleWatchMenuItem.Text = favorite.Enabled ? "Watch Off" : "Watch On";
        toggleWatchMenuItem.Enabled = favorite.Enabled || !recordingFavoriteIds.Contains(favorite.Id);
    }

    private void ToggleSelectedWatchFromMenu()
    {
        if (!TryGetSelectedModel(out var favorite))
        {
            return;
        }

        SetFavoriteWatch(favorite, !favorite.Enabled);
        RefreshGrid();
        ReselectCurrent();
    }

    private bool TryGetSelectedModel(out FavoriteItem favorite)
    {
        favorite = null!;
        if (modelGrid.SelectedRows.Count == 0
            || modelGrid.SelectedRows[0].Tag is not string id)
        {
            return false;
        }

        var selected = favorites.Items.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            return false;
        }

        favorite = selected;
        return true;
    }

    private bool SetFavoriteWatch(FavoriteItem favorite, bool watch)
    {
        if (!watch && recordingFavoriteIds.Contains(favorite.Id))
        {
            statusLabel.Text = "\uB179\uD654\uC911\uC778 \uBAA8\uB378\uC740 Watch\uB97C Off\uB85C \uBCC0\uACBD\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            return false;
        }

        favorite.Enabled = watch;
        favorite.UpdatedAt = DateTimeOffset.Now;
        selectedFavoriteId = favorite.Id;
        favoriteStore.Save(favorites);
        enabledCheckBox.Checked = watch;
        statusLabel.Text = $"{favorite.DisplayName}: Watch {(watch ? "On" : "Off")}";
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void AddModel()
    {
        if (!TryReadEditor(out var site, out var nickname, out var userId, out var profileUrl))
        {
            return;
        }

        var id = BuildFavoriteId(site, userId);
        var now = DateTimeOffset.Now;
        var existing = favorites.Items.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            selectedFavoriteId = existing.Id;
            UpdateModel();
            return;
        }

        favorites.Items.Add(new FavoriteItem
        {
            Id = id,
            Platform = DisplayName(site),
            PlatformUserId = userId,
            DisplayName = nickname,
            ProfileUrl = profileUrl,
            LastKnownUrl = profileUrl,
            Memo = memoTextBox.Text.Trim(),
            Enabled = enabledCheckBox.Checked,
            Metadata = BuildBroadcastMetadata(),
            CreatedAt = now,
            UpdatedAt = now
        });

        SaveAndRefresh($"ëª¨ë¸??ì¶”ê??ˆìŠµ?ˆë‹¤: {nickname}");
    }

    private void UpdateModel()
    {
        if (string.IsNullOrWhiteSpace(selectedFavoriteId))
        {
            statusLabel.Text = "?˜ì •??ëª¨ë¸??? íƒ?˜ì„¸??";
            return;
        }

        var favorite = favorites.Items.FirstOrDefault(item => item.Id.Equals(selectedFavoriteId, StringComparison.OrdinalIgnoreCase));
        if (favorite is null)
        {
            statusLabel.Text = "? íƒ??ëª¨ë¸??ì°¾ì„ ???†ìŠµ?ˆë‹¤.";
            return;
        }

        if (!TryReadEditor(out var site, out var nickname, out var userId, out var profileUrl))
        {
            return;
        }

        var newId = BuildFavoriteId(site, userId);
        var changesIdentity = !newId.Equals(favorite.Id, StringComparison.OrdinalIgnoreCase);
        if (changesIdentity && recordingFavoriteIds.Contains(favorite.Id))
        {
            statusLabel.Text = "?¹í™”ì¤‘ì¸ ëª¨ë¸?€ ?¬ì´???„ì´?”ë? ?˜ì •?????†ìŠµ?ˆë‹¤.";
            return;
        }

        if (!enabledCheckBox.Checked && recordingFavoriteIds.Contains(favorite.Id))
        {
            statusLabel.Text = "\uB179\uD654\uC911\uC778 \uBAA8\uB378\uC740 Watch\uB97C Off\uB85C \uBCC0\uACBD\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            return;
        }

        if (changesIdentity && favorites.Items.Any(item => item.Id.Equals(newId, StringComparison.OrdinalIgnoreCase)))
        {
            statusLabel.Text = "ê°™ì? ?¬ì´?¸ì? ?„ì´?”ì˜ ëª¨ë¸???´ë? ?ˆìŠµ?ˆë‹¤.";
            return;
        }

        favorite.Id = newId;
        favorite.Platform = DisplayName(site);
        favorite.PlatformUserId = userId;
        favorite.DisplayName = nickname;
        favorite.ProfileUrl = profileUrl;
        favorite.LastKnownUrl = profileUrl;
        favorite.Memo = memoTextBox.Text.Trim();
        SetMetadata(favorite, "broadcastStartTime", broadcastStartTextBox.Text.Trim());
        SetMetadata(favorite, "broadcastEndTime", broadcastEndTextBox.Text.Trim());
        favorite.Enabled = enabledCheckBox.Checked;
        favorite.UpdatedAt = DateTimeOffset.Now;
        selectedFavoriteId = favorite.Id;

        SaveAndRefresh($"ëª¨ë¸???˜ì •?ˆìŠµ?ˆë‹¤: {nickname}");
    }

    private void DeleteModel()
    {
        if (string.IsNullOrWhiteSpace(selectedFavoriteId))
        {
            statusLabel.Text = "?? œ??ëª¨ë¸??? íƒ?˜ì„¸??";
            return;
        }

        if (recordingFavoriteIds.Contains(selectedFavoriteId))
        {
            statusLabel.Text = "?¹í™”ì¤‘ì¸ ëª¨ë¸?€ ?? œ?????†ìŠµ?ˆë‹¤.";
            return;
        }

        var favorite = favorites.Items.FirstOrDefault(item => item.Id.Equals(selectedFavoriteId, StringComparison.OrdinalIgnoreCase));
        if (favorite is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"{favorite.DisplayName} \uBAA8\uB378\uC744 \uC0AD\uC81C\uD560\uAE4C\uC694?",
            Text,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes)
        {
            return;
        }

        favorites.Items.Remove(favorite);
        selectedFavoriteId = "";
        SaveAndRefresh($"\uBAA8\uB378\uC744 \uC0AD\uC81C\uD588\uC2B5\uB2C8\uB2E4: {favorite.DisplayName}");
        ClearEditor();
    }

    private bool TryReadEditor(out SiteProfile site, out string nickname, out string userId, out string profileUrl)
    {
        site = siteComboBox.SelectedItem as SiteProfile ?? sites.Sites.First();
        nickname = nicknameTextBox.Text.Trim();
        userId = userIdTextBox.Text.Trim();
        profileUrl = urlTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(userId))
        {
            statusLabel.Text = "?‰ë„¤?„ê³¼ ?„ì´?”ë? ëª¨ë‘ ?…ë ¥?˜ì„¸??";
            return false;
        }

        if (string.IsNullOrWhiteSpace(profileUrl))
        {
            profileUrl = BuildProfileUrl(site.LoginUrl, userId);
        }

        return true;
    }

    private void SaveAndRefresh(string message)
    {
        favoriteStore.Save(favorites);
        RefreshGrid();
        ReselectCurrent();
        statusLabel.Text = message;
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ReselectCurrent()
    {
        if (string.IsNullOrWhiteSpace(selectedFavoriteId))
        {
            return;
        }

        foreach (DataGridViewRow row in modelGrid.Rows)
        {
            if (row.Tag is string id && id.Equals(selectedFavoriteId, StringComparison.OrdinalIgnoreCase))
            {
                row.Selected = true;
                modelGrid.CurrentCell = row.Cells[0];
                break;
            }
        }
    }

    private void ClearEditor()
    {
        selectedFavoriteId = "";
        if (siteComboBox.Items.Count > 0)
        {
            siteComboBox.SelectedIndex = 0;
        }

        nicknameTextBox.Clear();
        userIdTextBox.Clear();
        urlTextBox.Clear();
        memoTextBox.Clear();
        broadcastStartTextBox.Clear();
        broadcastEndTextBox.Clear();
        enabledCheckBox.Checked = true;
        urlEditedManually = false;
        UpdateButtonStates();
    }

    private void UpdateUrlFromUserIdIfAutomatic()
    {
        if (loadingEditor || urlEditedManually)
        {
            return;
        }

        if (siteComboBox.SelectedItem is not SiteProfile site)
        {
            return;
        }

        var userId = userIdTextBox.Text.Trim();
        updatingUrlFromUserId = true;
        urlTextBox.Text = string.IsNullOrWhiteSpace(userId)
            ? ""
            : BuildProfileUrl(site.LoginUrl, userId);
        updatingUrlFromUserId = false;
    }

    private static bool IsDefaultProfileUrl(string profileUrl, SiteProfile? site, string platformUserId)
    {
        if (site is null || string.IsNullOrWhiteSpace(platformUserId))
        {
            return string.IsNullOrWhiteSpace(profileUrl);
        }

        return profileUrl.Trim().Equals(BuildProfileUrl(site.LoginUrl, platformUserId), StringComparison.OrdinalIgnoreCase);
    }


    private Dictionary<string, string> BuildBroadcastMetadata()
    {
        var metadata = new Dictionary<string, string>();
        SetMetadata(metadata, "broadcastStartTime", broadcastStartTextBox.Text.Trim());
        SetMetadata(metadata, "broadcastEndTime", broadcastEndTextBox.Text.Trim());
        return metadata;
    }

    private static string FormatBroadcastTime(FavoriteItem favorite)
    {
        var startTime = GetMetadata(favorite, "broadcastStartTime");
        var endTime = GetMetadata(favorite, "broadcastEndTime");
        if (string.IsNullOrWhiteSpace(startTime) && string.IsNullOrWhiteSpace(endTime))
        {
            return "-";
        }

        return $"{startTime} ~ {endTime}";
    }

    private static string GetMetadata(FavoriteItem favorite, string key)
    {
        return favorite.Metadata.TryGetValue(key, out var value) ? value : "";
    }

    private static void SetMetadata(FavoriteItem favorite, string key, string value)
    {
        SetMetadata(favorite.Metadata, key, value);
    }

    private static void SetMetadata(Dictionary<string, string> metadata, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            metadata.Remove(key);
            return;
        }

        metadata[key] = value;
    }
    private void UpdateButtonStates()
    {
        updateButton.Enabled = !string.IsNullOrWhiteSpace(selectedFavoriteId);
        deleteButton.Enabled = !string.IsNullOrWhiteSpace(selectedFavoriteId)
            && !recordingFavoriteIds.Contains(selectedFavoriteId);
    }

    private void SelectSite(string platform)
    {
        for (var index = 0; index < siteComboBox.Items.Count; index++)
        {
            if (siteComboBox.Items[index] is SiteProfile site
                && DisplayName(site).Equals(platform, StringComparison.OrdinalIgnoreCase))
            {
                siteComboBox.SelectedIndex = index;
                return;
            }
        }

        if (siteComboBox.Items.Count > 0)
        {
            siteComboBox.SelectedIndex = 0;
        }
    }

    private static Control CreateFieldPanel(string labelText, Control input)
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
            RowCount = 3
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft
        };

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(input, 0, 1);
        return panel;
    }

    private static string BuildFavoriteId(SiteProfile site, string platformUserId)
    {
        return $"{BuildPlatformKey(site)}:{platformUserId.Trim()}";
    }

    private static string BuildPlatformKey(SiteProfile site)
    {
        var displayName = DisplayName(site);
        return string.IsNullOrWhiteSpace(displayName)
            ? "site"
            : displayName.Trim().ToLowerInvariant();
    }

    private static string DisplayName(SiteProfile site)
    {
        return string.IsNullOrWhiteSpace(site.Name) ? "\uC0AC\uC774\uD2B8" : site.Name.Trim();
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
}
