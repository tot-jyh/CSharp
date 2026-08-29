namespace Hunbjter;

public sealed class ModelManagementForm : ThemedDialog
{
    private const int WatchColumnIndex = 0;

    private readonly SiteSettingsStore siteSettingsStore;
    private readonly FavoriteStore favoriteStore;
    private readonly HashSet<string> recordingFavoriteIds;
    private readonly ThemedGrid modelGrid = new();
    private readonly ThemedComboBox siteComboBox = new();
    private readonly ThemedTextBox nicknameTextBox = new();
    private readonly ThemedTextBox userIdTextBox = new();
    private readonly ThemedTextBox urlTextBox = new();
    private readonly ThemedTextBox memoTextBox = new();
    private readonly ThemedTextBox broadcastStartTextBox = new();
    private readonly ThemedTextBox broadcastEndTextBox = new();
    private readonly ThemedCheckBox enabledCheckBox = new();
    private readonly ThemedNumeric checkIntervalInput =
        ThemedDialog.CreateNumeric(0, ModelMonitor.MaximumIntervalSeconds, 10);
    private readonly ThemedButton addButton = new();
    private readonly ThemedButton updateButton = new();
    private readonly ThemedButton deleteButton = new();
    private readonly ThemedButton closeButton = new();
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
        Text = "모델관리";
        Size = new Size(1020, 744);
        MinimumSize = new Size(880, 620);

        var root = new BufferedTableLayoutPanel
        {
            BackColor = Theme.Background,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            RowCount = 4
        };
        // Without an explicit column style the single column falls back to AutoSize and the
        // grid collapses to its preferred size instead of filling the dialog.
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 216F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

        modelGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        modelGrid.Dock = DockStyle.Fill;
        modelGrid.RowTemplate.Height = 38;
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "감시", FillWeight = 46, Name = "Enabled", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "사이트", FillWeight = 74, Name = "Platform", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "닉네임", FillWeight = 100, Name = "DisplayName", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "아이디", FillWeight = 110, Name = "PlatformUserId", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "URL", FillWeight = 190, Name = "ProfileUrl", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "방송시간", FillWeight = 120, Name = "BroadcastTime", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "메모", FillWeight = 130, Name = "Memo", ReadOnly = true });
        modelGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "확인 주기", FillWeight = 70, Name = "CheckInterval", ReadOnly = true });
        modelGrid.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        modelGrid.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        modelGrid.Columns[4].DefaultCellStyle.ForeColor = Theme.TextMuted;
        modelGrid.Columns[4].DefaultCellStyle.Font = Theme.Small;
        modelGrid.Columns[6].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        modelGrid.SelectionChanged += (_, _) => LoadSelectedRow();
        modelGrid.CellMouseDown += modelGrid_CellMouseDown;
        modelGrid.CellClick += modelGrid_CellClick;
        modelGrid.CellPainting += modelGrid_CellPainting;
        modelGrid.ContextMenuStrip = modelContextMenu;

        toggleWatchMenuItem.Click += (_, _) => ToggleSelectedWatchFromMenu();
        modelContextMenu.Items.Add(toggleWatchMenuItem);
        modelContextMenu.Opening += modelContextMenu_Opening;
        ThemedMenuRenderer.Apply(modelContextMenu);

        var editor = new BufferedTableLayoutPanel
        {
            ColumnCount = 4,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 4),
            RowCount = 3
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        // A bit taller than the rows above it: the extra top margin below gives "확인 주기" clear
        // air between it and the 감시 checkbox directly above, so it reads as its own final
        // section rather than something stacked onto the watch toggle.
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));

        siteComboBox.SelectedIndexChanged += (_, _) => UpdateUrlFromUserIdIfAutomatic();
        userIdTextBox.TextChanged += (_, _) => UpdateUrlFromUserIdIfAutomatic();
        urlTextBox.TextChanged += (_, _) =>
        {
            if (!loadingEditor && !updatingUrlFromUserId)
            {
                urlEditedManually = true;
            }
        };

        enabledCheckBox.Checked = true;
        enabledCheckBox.Text = "감시 켜기";
        enabledCheckBox.Dock = DockStyle.Fill;
        // No separate caption above this one - the checkbox's own text already labels it, so a
        // stacked "감시" label (like every other field gets via CreateFieldPanel) would just be a
        // second, redundant line. The top margin instead lines the checkbox up roughly where the
        // other fields' inputs sit below their caption row.
        enabledCheckBox.Margin = new Padding(0, 22, 10, 0);
        enabledCheckBox.CheckedChanged += (_, _) =>
        {
            if (loadingEditor || string.IsNullOrWhiteSpace(selectedFavoriteId))
            {
                return;
            }

            var favorite = FindFavorite(selectedFavoriteId);
            if (favorite is null || favorite.Enabled == enabledCheckBox.Checked)
            {
                return;
            }

            if (SetFavoriteWatch(favorite, enabledCheckBox.Checked))
            {
                var idToReselect = favorite.Id;
                RefreshGrid();
                ReselectCurrent(idToReselect);
            }
            else
            {
                // Denied (e.g. currently recording) - put the box back without re-entering
                // this handler via the loadingEditor guard above.
                loadingEditor = true;
                enabledCheckBox.Checked = favorite.Enabled;
                loadingEditor = false;
            }
        };

        editor.Controls.Add(CreateFieldPanel("사이트", siteComboBox), 0, 0);
        editor.Controls.Add(CreateFieldPanel("닉네임", nicknameTextBox), 1, 0);
        editor.Controls.Add(CreateFieldPanel("아이디", userIdTextBox), 2, 0);
        editor.Controls.Add(CreateFieldPanel("URL", urlTextBox), 3, 0);
        editor.Controls.Add(enabledCheckBox, 0, 1);
        editor.Controls.Add(CreateFieldPanel("시작시간", broadcastStartTextBox), 1, 1);
        editor.Controls.Add(CreateFieldPanel("종료시간", broadcastEndTextBox), 2, 1);
        editor.Controls.Add(CreateFieldPanel("메모", memoTextBox), 3, 1);
        var checkIntervalPanel = CreateFieldPanel("확인 주기(초, 0=전역값)", checkIntervalInput);
        checkIntervalPanel.Margin = new Padding(0, 8, 10, 0);
        editor.Controls.Add(checkIntervalPanel, 0, 2);

        var checkIntervalHint = CreateLabel(
            "모델마다 방송 확인 간격을 다르게 줄 수 있습니다. 0이면 환경설정의 값을 그대로 씁니다.", Theme.Small, Theme.TextMuted);
        checkIntervalHint.Margin = new Padding(3, 8, 3, 0);
        editor.Controls.Add(checkIntervalHint, 1, 2);
        editor.SetColumnSpan(editor.Controls[editor.Controls.Count - 1], 3);

        var actionPanel = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Right,
            AutoSize = true,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.LeftToRight
        };

        ConfigureActionButton(addButton, "추가", ButtonVariant.Primary);
        ConfigureActionButton(updateButton, "수정", ButtonVariant.Secondary);
        ConfigureActionButton(deleteButton, "삭제", ButtonVariant.Danger);
        ConfigureActionButton(closeButton, "닫기", ButtonVariant.Ghost);
        addButton.Click += (_, _) => AddModel();
        updateButton.Click += (_, _) => UpdateModel();
        deleteButton.Click += (_, _) => DeleteModel();
        closeButton.Click += (_, _) => Close();

        actionPanel.Controls.Add(addButton);
        actionPanel.Controls.Add(updateButton);
        actionPanel.Controls.Add(deleteButton);
        actionPanel.Controls.Add(closeButton);

        statusLabel.AutoEllipsis = true;
        statusLabel.BackColor = Color.Transparent;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.ForeColor = Theme.TextSecondary;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        root.Controls.Add(modelGrid, 0, 0);
        root.Controls.Add(editor, 0, 1);
        root.Controls.Add(statusLabel, 0, 2);
        root.Controls.Add(actionPanel, 0, 3);
        Controls.Add(root);
    }

    private static void ConfigureActionButton(ThemedButton button, string text, ButtonVariant variant)
    {
        button.Margin = new Padding(8, 0, 0, 0);
        button.Size = new Size(88, 32);
        button.Text = text;
        button.Variant = variant;
    }

    private void LoadData()
    {
        sites = siteSettingsStore.Load();
        if (sites.Sites.Count == 0)
        {
            sites.Sites.Add(new SiteProfile
            {
                Name = "팬더",
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
                favorite.Enabled ? "ON" : "OFF",
                favorite.Platform,
                favorite.DisplayName,
                favorite.PlatformUserId,
                favorite.ProfileUrl,
                FormatBroadcastTime(favorite),
                favorite.Memo,
                FormatCheckInterval(favorite));
            modelGrid.Rows[rowIndex].Tag = favorite.Id;
        }

        modelGrid.CurrentCell = null;
        modelGrid.ClearSelection();
        refreshingGrid = false;
    }

    private void modelGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics is null || e.RowIndex < 0 || e.ColumnIndex != WatchColumnIndex)
        {
            return;
        }

        if (modelGrid.Rows[e.RowIndex].Tag is not string id)
        {
            return;
        }

        var favorite = FindFavorite(id);
        GridRenderers.PaintWatchBadge(e, favorite?.Enabled ?? false, hovered: false);
    }

    /// <summary>
    /// The watch column is a badge rather than a check box: the system check box renders
    /// light regardless of the palette, and clicking the cell reads the same.
    /// </summary>
    private void modelGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (refreshingGrid || e.RowIndex < 0 || e.ColumnIndex != WatchColumnIndex)
        {
            return;
        }

        if (modelGrid.Rows[e.RowIndex].Tag is not string id)
        {
            return;
        }

        var favorite = FindFavorite(id);
        if (favorite is null)
        {
            return;
        }

        if (SetFavoriteWatch(favorite, !favorite.Enabled))
        {
            modelGrid.Rows[e.RowIndex].Cells[WatchColumnIndex].Value = favorite.Enabled ? "ON" : "OFF";
            modelGrid.InvalidateCell(WatchColumnIndex, e.RowIndex);
        }
    }

    private FavoriteItem? FindFavorite(string id)
    {
        return favorites.Items.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadSelectedRow()
    {
        if (modelGrid.SelectedRows.Count == 0
            || modelGrid.SelectedRows[0].Tag is not string id)
        {
            selectedFavoriteId = "";
            return;
        }

        var favorite = FindFavorite(id);
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
        checkIntervalInput.Value = Math.Clamp(
            favorite.CheckIntervalSeconds ?? 0, 0, ModelMonitor.MaximumIntervalSeconds);
        enabledCheckBox.Checked = favorite.Enabled;
        urlEditedManually = !IsDefaultProfileUrl(favorite.ProfileUrl, siteComboBox.SelectedItem as SiteProfile, favorite.PlatformUserId);
        loadingEditor = false;
        UpdateButtonStates();
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

        var idToReselect = favorite.Id;
        SetFavoriteWatch(favorite, !favorite.Enabled);
        RefreshGrid();
        ReselectCurrent(idToReselect);
    }

    private bool TryGetSelectedModel(out FavoriteItem favorite)
    {
        favorite = null!;
        if (modelGrid.SelectedRows.Count == 0
            || modelGrid.SelectedRows[0].Tag is not string id)
        {
            return false;
        }

        var selected = FindFavorite(id);
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
            statusLabel.Text = "녹화중인 모델은 감시를 끌 수 없습니다.";
            return false;
        }

        favorite.Enabled = watch;
        // Stale status would otherwise linger (turning on: until the next check comes back;
        // turning off: forever, since a watch-off model never gets rechecked - it would
        // otherwise stay pinned in Form1's 방송중 grid) - clear it either way, matching
        // Form1.ToggleWatch's grid-badge path.
        foreach (var key in new[] { "liveStatus", "liveMessage", "streamUrl", "resolution" })
        {
            favorite.Metadata.Remove(key);
        }

        favorite.UpdatedAt = DateTimeOffset.Now;
        selectedFavoriteId = favorite.Id;
        favoriteStore.Save(favorites);
        enabledCheckBox.Checked = watch;
        statusLabel.Text = $"{favorite.DisplayName}: 감시 {(watch ? "켜짐" : "꺼짐")}";
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
        var existing = FindFavorite(id);
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
            CheckIntervalSeconds = ReadCheckIntervalSeconds(),
            Metadata = BuildBroadcastMetadata(),
            CreatedAt = now,
            UpdatedAt = now
        });

        SaveAndRefresh($"모델을 추가했습니다: {nickname}");
    }

    private void UpdateModel()
    {
        if (string.IsNullOrWhiteSpace(selectedFavoriteId))
        {
            statusLabel.Text = "수정할 모델을 선택하세요.";
            return;
        }

        var favorite = FindFavorite(selectedFavoriteId);
        if (favorite is null)
        {
            statusLabel.Text = "선택한 모델을 찾을 수 없습니다.";
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
            statusLabel.Text = "녹화중인 모델은 사이트와 아이디를 수정할 수 없습니다.";
            return;
        }

        if (!enabledCheckBox.Checked && recordingFavoriteIds.Contains(favorite.Id))
        {
            statusLabel.Text = "녹화중인 모델은 감시를 끌 수 없습니다.";
            return;
        }

        if (changesIdentity && favorites.Items.Any(item => item.Id.Equals(newId, StringComparison.OrdinalIgnoreCase)))
        {
            statusLabel.Text = "같은 사이트와 아이디의 모델이 이미 있습니다.";
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
        favorite.CheckIntervalSeconds = ReadCheckIntervalSeconds();
        favorite.UpdatedAt = DateTimeOffset.Now;
        selectedFavoriteId = favorite.Id;

        SaveAndRefresh($"모델을 수정했습니다: {nickname}");
    }

    private void DeleteModel()
    {
        if (string.IsNullOrWhiteSpace(selectedFavoriteId))
        {
            statusLabel.Text = "삭제할 모델을 선택하세요.";
            return;
        }

        if (recordingFavoriteIds.Contains(selectedFavoriteId))
        {
            statusLabel.Text = "녹화중인 모델은 삭제할 수 없습니다.";
            return;
        }

        var favorite = FindFavorite(selectedFavoriteId);
        if (favorite is null)
        {
            return;
        }

        if (ConfirmDialog.Ask(this, Text, $"{favorite.DisplayName} 모델을 삭제할까요?") != DialogResult.Yes)
        {
            return;
        }

        favorites.Items.Remove(favorite);
        selectedFavoriteId = "";
        SaveAndRefresh($"모델을 삭제했습니다: {favorite.DisplayName}");
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
            statusLabel.Text = "닉네임과 아이디를 모두 입력하세요.";
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
        var idToReselect = selectedFavoriteId;
        favoriteStore.Save(favorites);
        RefreshGrid();
        ReselectCurrent(idToReselect);
        statusLabel.Text = message;
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Reselects a row after <see cref="RefreshGrid"/>. <c>RefreshGrid</c> calls
    /// <c>ClearSelection()</c>, which fires <c>SelectionChanged</c> synchronously and runs
    /// <see cref="LoadSelectedRow"/> while nothing is selected - that wipes
    /// <see cref="selectedFavoriteId"/> to "" before this method would otherwise read it. So
    /// every caller must capture the id it wants reselected *before* calling RefreshGrid and
    /// pass it in here explicitly; falling back to the (by-then-clobbered) field is not safe.
    /// </summary>
    private void ReselectCurrent(string? id = null)
    {
        var targetId = id ?? selectedFavoriteId;
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        foreach (DataGridViewRow row in modelGrid.Rows)
        {
            if (row.Tag is string rowId && rowId.Equals(targetId, StringComparison.OrdinalIgnoreCase))
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
        checkIntervalInput.Value = 0;
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

    /// <summary>0 means "use the global 접속 확인 간격 from 환경설정" - stored as null, not 0,
    /// so PerModelIntervalRule's own &gt;0 check (Monitoring/IntervalRules.cs) treats it the
    /// same way a favorites.json written before this field existed already behaves.</summary>
    private int? ReadCheckIntervalSeconds()
    {
        var seconds = (int)checkIntervalInput.Value;
        return seconds > 0 ? seconds : null;
    }

    private static string FormatCheckInterval(FavoriteItem favorite)
    {
        return favorite.CheckIntervalSeconds is > 0 ? $"{favorite.CheckIntervalSeconds}초" : "-";
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

    private static Control CreateFieldPanel(string labelText, Control input, bool wrap = true)
    {
        var panel = new BufferedTableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 10, 0),
            RowCount = 2
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

        panel.Controls.Add(CreateLabel(labelText, Theme.Small, Theme.TextMuted), 0, 0);
        panel.Controls.Add(
            wrap
                ? new InputHost(input) { Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 1, 0, 0) }
                : input,
            0,
            1);
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
        return string.IsNullOrWhiteSpace(site.Name) ? "사이트" : site.Name.Trim();
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
