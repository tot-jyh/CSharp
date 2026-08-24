namespace Hunbjter
{
    public partial class Form1 : Form
    {
        private readonly LoginSettingsStore settingsStore = new();
        private readonly SiteSettingsStore siteSettingsStore = new();
        private readonly FavoriteStore favoriteStore = new();
        private readonly PandaLiveService pandaLiveService = new();
        private readonly RecordingService recordingService = new();
        private readonly LoginBrowserForm loginBrowserForm = new();
        private readonly Dictionary<string, RecordingSession> recordingSessions = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> pendingNewFavoriteIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly System.Windows.Forms.Timer modelCheckTimer = new();
        private readonly System.Windows.Forms.Timer recordingFileSizeTimer = new();
        private readonly NotifyIcon trayIcon = new();
        private readonly ContextMenuStrip trayContextMenu = new();
        private readonly DataGridView liveFavoritesGridView = new();
        private readonly Label liveFavoritesLabel = new();
        private readonly Label standbyFavoritesLabel = new();
        private DataGridView? activeFavoritesGridView;
        private readonly ToolStripMenuItem showTrayMenuItem = new();
        private readonly ToolStripMenuItem exitTrayMenuItem = new();
        private static readonly TimeSpan RecentLastSeenWindow = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan RecentLastSeenCheckInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PaidRoomRetryInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RecordingFileSizeInterval = TimeSpan.FromSeconds(10);
        private LoginSettings settings = new();
        private FavoritesDocument favorites = new();
        private int favoriteSortColumn = 3;
        private SortOrder favoriteSortOrder = SortOrder.Ascending;
        private Task? pendingNewFavoriteCheckTask;
        private bool modelCheckInProgress;
        private bool newFavoriteCheckInProgress;
        private bool startupCheckStarted;
        private bool closingConfirmed;
        private bool isShuttingDown;

        public Form1()
        {
            InitializeComponent();
            ConfigureSplitFavoriteGrids();
            ConfigureTrayIcon();
            WireFavoriteGridEvents(favoritesGridView);
            WireFavoriteGridEvents(liveFavoritesGridView);
            modelCheckTimer.Tick += modelCheckTimer_Tick;
            recordingFileSizeTimer.Interval = (int)RecordingFileSizeInterval.TotalMilliseconds;
            recordingFileSizeTimer.Tick += recordingFileSizeTimer_Tick;
            recordingFileSizeTimer.Start();
            Shown += Form1_Shown;
            LoadSettings();
            LoadFavorites();
            ConfigureModelCheckTimer();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!closingConfirmed && e.CloseReason == CloseReason.UserClosing)
            {
                var result = ShowTimedExitConfirm();

                if (result != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }

                closingConfirmed = true;
            }

            isShuttingDown = true;
            modelCheckTimer.Stop();
            recordingFileSizeTimer.Stop();
            StopAllRecordingsForExit();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayContextMenu.Dispose();
            base.OnFormClosed(e);
        }

        private void ConfigureSplitFavoriteGrids()
        {
            CloneFavoriteGrid(favoritesGridView, liveFavoritesGridView);
            liveFavoritesGridView.Name = "liveFavoritesGridView";

            liveFavoritesLabel.Text = "방송중";
            liveFavoritesLabel.Dock = DockStyle.Fill;
            liveFavoritesLabel.TextAlign = ContentAlignment.MiddleLeft;
            liveFavoritesLabel.Font = new Font(Font, FontStyle.Bold);
            liveFavoritesLabel.Padding = new Padding(2, 0, 0, 0);

            standbyFavoritesLabel.Text = "목록";
            standbyFavoritesLabel.Dock = DockStyle.Fill;
            standbyFavoritesLabel.TextAlign = ContentAlignment.MiddleLeft;
            standbyFavoritesLabel.Font = new Font(Font, FontStyle.Bold);
            standbyFavoritesLabel.Padding = new Padding(2, 0, 0, 0);

            favoritePanel.SuspendLayout();
            favoritePanel.Controls.Clear();
            favoritePanel.RowStyles.Clear();
            favoritePanel.RowCount = 4;
            favoritePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            favoritePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
            favoritePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            favoritePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
            favoritePanel.Controls.Add(liveFavoritesLabel, 0, 0);
            favoritePanel.Controls.Add(liveFavoritesGridView, 0, 1);
            favoritePanel.Controls.Add(standbyFavoritesLabel, 0, 2);
            favoritePanel.Controls.Add(favoritesGridView, 0, 3);
            favoritePanel.ResumeLayout(false);
        }

        private static void CloneFavoriteGrid(DataGridView source, DataGridView target)
        {
            target.AllowUserToAddRows = source.AllowUserToAddRows;
            target.AllowUserToDeleteRows = source.AllowUserToDeleteRows;
            target.AllowUserToResizeRows = source.AllowUserToResizeRows;
            target.BackgroundColor = source.BackgroundColor;
            target.BorderStyle = source.BorderStyle;
            target.ColumnHeadersDefaultCellStyle.Alignment = source.ColumnHeadersDefaultCellStyle.Alignment;
            target.DefaultCellStyle.Alignment = source.DefaultCellStyle.Alignment;
            target.ColumnHeadersHeight = source.ColumnHeadersHeight;
            target.ColumnHeadersHeightSizeMode = source.ColumnHeadersHeightSizeMode;
            target.ContextMenuStrip = source.ContextMenuStrip;
            target.Dock = DockStyle.Fill;
            target.EditMode = source.EditMode;
            target.Margin = source.Margin;
            target.MultiSelect = source.MultiSelect;
            target.ReadOnly = source.ReadOnly;
            target.RowHeadersVisible = source.RowHeadersVisible;
            target.RowTemplate.Height = source.RowTemplate.Height;
            target.SelectionMode = source.SelectionMode;
            target.TabIndex = source.TabIndex;

            target.Columns.Clear();
            foreach (DataGridViewColumn column in source.Columns)
            {
                target.Columns.Add((DataGridViewColumn)column.Clone());
            }
        }

        private void WireFavoriteGridEvents(DataGridView grid)
        {
            grid.SelectionChanged += favoritesGridView_SelectionChanged;
            grid.CellMouseDown += favoritesGridView_CellMouseDown;
            grid.CellContentClick += favoritesGridView_CellContentClick;
            grid.CellPainting += favoritesGridView_CellPainting;
            grid.CellMouseMove += favoritesGridView_CellMouseMove;
            grid.CellMouseLeave += favoritesGridView_CellMouseLeave;
            grid.ColumnHeaderMouseClick += favoritesGridView_ColumnHeaderMouseClick;
        }
        private void ConfigureTrayIcon()
        {
            ShowInTaskbar = false;

            showTrayMenuItem.Text = "\uC5F4\uAE30";
            showTrayMenuItem.Click += (_, _) => RestoreFromTray();

            exitTrayMenuItem.Text = "\uC885\uB8CC";
            exitTrayMenuItem.Click += (_, _) =>
            {
                RestoreFromTray();
                Close();
            };

            trayContextMenu.Items.AddRange(new ToolStripItem[]
            {
                showTrayMenuItem,
                exitTrayMenuItem
            });

            trayIcon.ContextMenuStrip = trayContextMenu;
            trayIcon.Icon = Icon ?? SystemIcons.Application;
            trayIcon.Text = Text;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        }

        private void RestoreFromTray()
        {
            if (IsDisposed)
            {
                return;
            }

            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private DialogResult ShowTimedExitConfirm()
        {
            using var confirmForm = new Form
            {
                Text = Text,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(380, 178),
                Padding = new Padding(18)
            };

            var root = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

            var titleLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font(confirmForm.Font, FontStyle.Bold),
                Text = "\uC885\uB8CC\uD560\uAE4C\uC694?",
                TextAlign = ContentAlignment.MiddleLeft
            };

            var messageLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "\uB179\uD654\uC911\uC778 \uD56D\uBAA9\uC774 \uC788\uC73C\uBA74 \uC548\uC804\uD558\uAC8C \uC885\uB8CC\uD55C \uB4A4 \uC571\uC744 \uB2EB\uC2B5\uB2C8\uB2E4.",
                TextAlign = ContentAlignment.MiddleLeft
            };

            var countdownLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = SystemColors.GrayText,
                Text = "3\uCD08 \uD6C4 \uC790\uB3D9\uC73C\uB85C \uC885\uB8CC\uD569\uB2C8\uB2E4.",
                TextAlign = ContentAlignment.MiddleLeft
            };

            var buttonPanel = new TableLayoutPanel
            {
                ColumnCount = 3,
                Dock = DockStyle.Fill,
                RowCount = 1
            };
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var yesButton = new Button
            {
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                DialogResult = DialogResult.Yes,
                Size = new Size(84, 30),
                Text = "\uC608"
            };
            var noButton = new Button
            {
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                DialogResult = DialogResult.No,
                Size = new Size(84, 30),
                Text = "\uC544\uB2C8\uC694"
            };

            buttonPanel.Controls.Add(yesButton, 1, 0);
            buttonPanel.Controls.Add(noButton, 2, 0);
            root.Controls.Add(titleLabel, 0, 0);
            root.Controls.Add(messageLabel, 0, 1);
            root.Controls.Add(countdownLabel, 0, 2);
            root.Controls.Add(buttonPanel, 0, 3);
            confirmForm.Controls.Add(root);
            confirmForm.AcceptButton = yesButton;
            confirmForm.CancelButton = noButton;

            var remainingSeconds = 3;
            using var timer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };
            timer.Tick += (_, _) =>
            {
                remainingSeconds--;
                if (remainingSeconds <= 0)
                {
                    timer.Stop();
                    confirmForm.DialogResult = DialogResult.Yes;
                    confirmForm.Close();
                    return;
                }

                countdownLabel.Text = $"{remainingSeconds}\uCD08 \uD6C4 \uC790\uB3D9\uC73C\uB85C \uC885\uB8CC\uD569\uB2C8\uB2E4.";
            };
            confirmForm.Shown += (_, _) => timer.Start();

            return confirmForm.ShowDialog(this);
        }

        private void LoadSettings()
        {
            settings = settingsStore.Load();

            SetStatus("사이트관리에서 로그인 정보를 관리하세요.");
            ConfigureModelCheckTimer();
        }

        private async void Form1_Shown(object? sender, EventArgs e)
        {
            if (startupCheckStarted)
            {
                return;
            }

            startupCheckStarted = true;
            await CheckStartupFavoritesAsync();
        }

        private async Task CheckStartupFavoritesAsync()
        {
            if (modelCheckInProgress)
            {
                return;
            }

            var targets = favorites.Items.Where(item => item.Enabled).ToList();
            if (targets.Count == 0)
            {
                return;
            }

            modelCheckInProgress = true;
            modelCheckTimer.Stop();
            try
            {
                await CheckFavoritesAndAutoRecordAsync(targets, "앱 시작 방송 확인");
            }
            finally
            {
                modelCheckInProgress = false;
                ConfigureModelCheckTimer();
            }
        }

        private async void siteManagementButton_Click(object sender, EventArgs e)
        {
            using var siteManagementForm = new SiteManagementForm(siteSettingsStore, settingsStore, settings);
            siteManagementForm.FavoritesChanged += (_, _) =>
            {
                ReloadFavoritesAndQueueNew();
                pendingNewFavoriteCheckTask = CheckPendingNewFavoritesAsync();
            };

            if (siteManagementForm.ShowDialog(this) == DialogResult.OK)
            {
                if (pendingNewFavoriteCheckTask is not null)
                {
                    await pendingNewFavoriteCheckTask;
                }

                LoadSettings();
                LoadFavorites(resetRuntimeState: false);
                pendingNewFavoriteCheckTask = CheckPendingNewFavoritesAsync();
                await pendingNewFavoriteCheckTask;
                SetStatus("사이트 설정을 반영했습니다.");
                await PreparePandaSessionForMainAsync("사이트 설정 후 세션 준비");
                await CheckFavoritesAfterSiteSettingsAsync();
            }
        }

        private async Task CheckFavoritesAfterSiteSettingsAsync()
        {
            var targets = favorites.Items
                .Where(item => item.Enabled && !recordingSessions.ContainsKey(item.Id))
                .ToList();
            if (targets.Count == 0)
            {
                return;
            }

            await CheckFavoritesAndAutoRecordAsync(targets, "사이트 설정 후 모델 접속확인");
            ConfigureModelCheckTimer();
        }

        private void environmentSettingsButton_Click(object sender, EventArgs e)
        {
            ShowEnvironmentSettings();
        }

        private async void modelManagementButton_Click(object sender, EventArgs e)
        {
            using var modelManagementForm = new ModelManagementForm(
                siteSettingsStore,
                favoriteStore,
                recordingSessions.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase));
            modelManagementForm.FavoritesChanged += (_, _) =>
            {
                ReloadFavoritesAndQueueNew();
                pendingNewFavoriteCheckTask = CheckPendingNewFavoritesAsync();
            };

            modelManagementForm.ShowDialog(this);
            if (pendingNewFavoriteCheckTask is not null)
            {
                await pendingNewFavoriteCheckTask;
            }

            LoadFavorites(resetRuntimeState: false);
            await CheckFavoritesAfterModelManagementAsync();
        }

        private async Task CheckFavoritesAfterModelManagementAsync()
        {
            var targets = favorites.Items
                .Where(item => item.Enabled && !recordingSessions.ContainsKey(item.Id))
                .ToList();
            if (targets.Count == 0)
            {
                return;
            }

            await CheckFavoritesAndAutoRecordAsync(targets, "모델관리 후 모델 접속확인");
            ConfigureModelCheckTimer();
        }

        private bool ShowEnvironmentSettings()
        {
            using var form = new EnvironmentSettingsForm(settings);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return false;
            }

            settingsStore.Save(settings);
            SetStatus("환경설정을 저장했습니다.");
            AddLog($"환경설정 저장: ffmpeg={settings.FfmpegPath}, 저장위치={settings.RecordingDirectory}");
            ConfigureModelCheckTimer();
            return true;
        }

        private void ConfigureModelCheckTimer()
        {
            var intervalSeconds = GetConfiguredModelCheckIntervalSeconds();
            if (HasRecentlySeenFavorite())
            {
                intervalSeconds = Math.Min(intervalSeconds, (int)RecentLastSeenCheckInterval.TotalSeconds);
            }
            if (HasPaidRoomRetryFavorite())
            {
                intervalSeconds = Math.Min(intervalSeconds, (int)PaidRoomRetryInterval.TotalSeconds);
            }

            modelCheckTimer.Stop();
            modelCheckTimer.Interval = Math.Clamp(intervalSeconds, 10, 86400) * 1000;
            modelCheckTimer.Start();
        }

        private int GetConfiguredModelCheckIntervalSeconds()
        {
            return Math.Clamp(settings.ModelCheckIntervalSeconds > 0
                ? settings.ModelCheckIntervalSeconds
                : 300, 10, 86400);
        }

        private bool HasRecentlySeenFavorite()
        {
            var now = DateTimeOffset.Now;
            return favorites.Items.Any(item =>
                item.Enabled
                && !recordingSessions.ContainsKey(item.Id)
                && IsRecentlySeenButNotLive(item, now));
        }

        private bool HasPaidRoomRetryFavorite()
        {
            return favorites.Items.Any(item =>
                item.Enabled
                && !recordingSessions.ContainsKey(item.Id)
                && HasPaidRoomTicketMessage(item));
        }

        private bool IsFavoriteDueForAutoCheck(FavoriteItem favorite, DateTimeOffset now)
        {
            var interval = HasPaidRoomTicketMessage(favorite)
                ? PaidRoomRetryInterval
                : IsRecentlySeenButNotLive(favorite, now)
                ? RecentLastSeenCheckInterval
                : TimeSpan.FromSeconds(GetConfiguredModelCheckIntervalSeconds());

            var lastCheck = GetLastCheckedAt(favorite);
            return !lastCheck.HasValue || now - lastCheck.Value >= interval;
        }

        private static bool IsRecentlySeen(FavoriteItem favorite, DateTimeOffset now)
        {
            return favorite.LastSeenAt.HasValue
                && favorite.LastSeenAt.Value >= now - RecentLastSeenWindow;
        }

        private static bool IsRecentlySeenButNotLive(FavoriteItem favorite, DateTimeOffset now)
        {
            return IsRecentlySeen(favorite, now)
                && (!favorite.Metadata.TryGetValue("liveStatus", out var status) || status != "live");
        }

        private void ShowLoginBrowser()
        {
            if (loginBrowserForm.Visible)
            {
                loginBrowserForm.Activate();
                return;
            }

            loginBrowserForm.Show(this);
        }

        private void LoadFavorites(bool resetRuntimeState = true)
        {
            favorites = favoriteStore.Load();
            if (resetRuntimeState)
            {
                ResetRuntimeFavoriteState();
            }
            else
            {
                ClearStaleRecordingMetadata();
            }

            RefreshFavoriteList();
            AddLog($"목록 {favorites.Items.Count}개를 불러왔습니다.");
        }

        private void ClearStaleRecordingMetadata()
        {
            var changed = false;
            foreach (var favorite in favorites.Items.Where(item => !recordingSessions.ContainsKey(item.Id)))
            {
                changed |= favorite.Metadata.Remove("recording");
                changed |= favorite.Metadata.Remove("recordingPath");
                changed |= favorite.Metadata.Remove("recordingPaused");
            }

            if (changed)
            {
                favoriteStore.Save(favorites);
            }
        }

        private void ReloadFavoritesAndQueueNew()
        {
            var knownIds = favorites.Items
                .Select(item => item.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            LoadFavorites(resetRuntimeState: false);

            var newTargets = favorites.Items
                .Where(item => item.Enabled && !knownIds.Contains(item.Id))
                .ToList();

            foreach (var favorite in newTargets)
            {
                pendingNewFavoriteIds.Add(favorite.Id);
            }
        }

        private async Task CheckPendingNewFavoritesAsync()
        {
            if (newFavoriteCheckInProgress)
            {
                return;
            }

            newFavoriteCheckInProgress = true;
            try
            {
                while (pendingNewFavoriteIds.Count > 0)
                {
                    var targetIds = pendingNewFavoriteIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    pendingNewFavoriteIds.Clear();

                    var targets = favorites.Items
                        .Where(item => item.Enabled && targetIds.Contains(item.Id))
                        .ToList();

                    if (targets.Count == 0)
                    {
                        continue;
                    }

                    await CheckFavoritesAndAutoRecordAsync(targets, "신규 모델 방송 확인");
                    ConfigureModelCheckTimer();
                }
            }
            finally
            {
                newFavoriteCheckInProgress = false;
            }
        }

        private void ResetRuntimeFavoriteState()
        {
            var changed = false;
            foreach (var favorite in favorites.Items)
            {
                foreach (var key in new[] { "liveStatus", "liveMessage", "streamUrl", "resolution", "recording", "recordingPath", "recordingPaused", "offlineCheckCount" })
                {
                    changed |= favorite.Metadata.Remove(key);
                }
            }

            if (changed)
            {
                favoriteStore.Save(favorites);
            }
        }

        private void RefreshFavoriteList()
        {
            liveFavoritesGridView.Rows.Clear();
            favoritesGridView.Rows.Clear();

            var sorted = SortFavoritesForDisplay();
            AddFavoriteRows(liveFavoritesGridView, sorted.Where(IsLiveListFavorite));
            AddFavoriteRows(favoritesGridView, sorted.Where(item => !IsLiveListFavorite(item)));

            liveFavoritesGridView.ClearSelection();
            favoritesGridView.ClearSelection();
        }

        private void AddFavoriteRows(DataGridView grid, IEnumerable<FavoriteItem> items)
        {
            var sequence = 1;
            foreach (var favorite in items)
            {
                var rowIndex = grid.Rows.Add(
                    "",
                    sequence.ToString(),
                    favorite.Platform,
                    FormatDisplayNameWithUserId(favorite),
                    GetFavoriteStatusText(favorite),
                    GetRecordingText(favorite),
                    GetResolutionText(favorite),
                    FormatLastSeen(favorite.LastSeenAt),
                    FormatLastCheck(favorite),
                    GetWatchText(favorite),
                    GetRecordingFileSizeText(favorite),
                    GetInstantCaptureText(favorite));
                var row = grid.Rows[rowIndex];
                row.Tag = favorite;
                UpdateInstantCaptureCell(row, favorite);
                sequence++;
            }
        }

        private bool IsLiveListFavorite(FavoriteItem favorite)
        {
            return recordingSessions.ContainsKey(favorite.Id)
                || (favorite.Metadata.TryGetValue("liveStatus", out var status) && status == "live");
        }
        private List<FavoriteItem> SortFavoritesForDisplay()
        {
            var sorted = favorites.Items.ToList();
            sorted.Sort(CompareFavoritesForDisplay);
            return sorted;
        }

        private int CompareFavoritesForDisplay(FavoriteItem left, FavoriteItem right)
        {
            var result = favoriteSortColumn switch
            {
                1 => CompareNullableDates(left.CreatedAt, right.CreatedAt),
                2 => CompareText(left.Platform, right.Platform),
                3 => CompareText(left.DisplayName, right.DisplayName),
                4 => CompareText(GetFavoriteStatusText(left), GetFavoriteStatusText(right)),
                5 => CompareText(GetRecordingText(left), GetRecordingText(right)),
                6 => CompareText(GetResolutionText(left), GetResolutionText(right)),
                7 => CompareNullableDates(left.LastSeenAt, right.LastSeenAt),
                8 => CompareNullableDates(GetLastCheckedAt(left), GetLastCheckedAt(right)),
                9 => CompareText(GetWatchText(left), GetWatchText(right)),
                10 => CompareLong(GetRecordingFileSizeBytes(left), GetRecordingFileSizeBytes(right)),
                11 => CompareText(GetInstantCaptureText(left), GetInstantCaptureText(right)),
                _ => CompareText(left.DisplayName, right.DisplayName)
            };

            if (favoriteSortOrder == SortOrder.Descending)
            {
                result = -result;
            }

            return result != 0
                ? result
                : CompareText(left.DisplayName, right.DisplayName);
        }
        private async void checkLiveButton_Click(object sender, EventArgs e)
        {
            await CheckSelectedOrAllFavoritesAsync();
        }

        private async void checkLiveMenuItem_Click(object? sender, EventArgs e)
        {
            await CheckSelectedOrAllFavoritesAsync();
        }

        private async void modelCheckTimer_Tick(object? sender, EventArgs e)
        {
            if (modelCheckInProgress)
            {
                return;
            }

            modelCheckInProgress = true;
            modelCheckTimer.Stop();
            try
            {
                await CheckAllFavoritesAndAutoRecordAsync();
            }
            finally
            {
                modelCheckInProgress = false;
                ConfigureModelCheckTimer();
            }
        }

        private void recordingFileSizeTimer_Tick(object? sender, EventArgs e)
        {
            UpdateRecordingFileSizes();
        }

        private void UpdateRecordingFileSizes()
        {
            UpdateRecordingFileSizes(liveFavoritesGridView);
            UpdateRecordingFileSizes(favoritesGridView);
        }

        private void UpdateRecordingFileSizes(DataGridView grid)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Tag is not FavoriteItem favorite || row.IsNewRow)
                {
                    continue;
                }

                row.Cells[10].Value = GetRecordingFileSizeText(favorite);
                UpdateInstantCaptureCell(row, favorite);
            }
        }

        private void UpdateInstantCaptureCell(DataGridViewRow row, FavoriteItem favorite)
        {
            var isRecording = recordingSessions.ContainsKey(favorite.Id);
            var needsButton = isRecording && row.Cells[11] is not DataGridViewButtonCell;
            var needsText = !isRecording && row.Cells[11] is not DataGridViewTextBoxCell;

            if (needsButton)
            {
                row.Cells[11] = new DataGridViewButtonCell();
            }
            else if (needsText)
            {
                row.Cells[11] = new DataGridViewTextBoxCell();
            }

            row.Cells[11].Value = GetInstantCaptureText(favorite);
            row.Cells[11].ReadOnly = true;
        }
        private async void startRecordingMenuItem_Click(object? sender, EventArgs e)
        {
            if (TryGetSelectedFavorite(out var favorite))
            {
                await StartRecordingAsync(favorite, verifyLiveBeforeStart: true);
            }
        }

        private void stopRecordingMenuItem_Click(object? sender, EventArgs e)
        {
            if (TryGetSelectedFavorite(out var favorite)
                && recordingSessions.Remove(favorite.Id, out var session))
            {
                StopRecording(favorite, session);
            }
        }

        private async void highlightCaptureMenuItem_Click(object? sender, EventArgs e)
        {
            if (TryGetSelectedFavorite(out var favorite))
            {
                await CreateHighlightCaptureAsync(favorite);
            }
        }

        private async void toggleWatchMenuItem_Click(object? sender, EventArgs e)
        {
            if (!TryGetSelectedFavorite(out var favorite))
            {
                return;
            }

            if (favorite.Enabled && recordingSessions.ContainsKey(favorite.Id))
            {
                SetStatus("녹화중인 모델은 Watch를 Off로 변경할 수 없습니다.");
                AddLog($"{favorite.DisplayName}: 녹화중이라 Watch Off를 건너뜁니다.");
                return;
            }

            favorite.Enabled = !favorite.Enabled;
            if (favorite.Enabled)
            {
                favorite.Metadata.Remove("liveStatus");
                favorite.Metadata.Remove("liveMessage");
                ClearLivePlaybackMetadata(favorite);
            }

            favorite.UpdatedAt = DateTimeOffset.Now;
            favoriteStore.Save(favorites);
            RefreshFavoriteList();
            ConfigureModelCheckTimer();
            SetStatus($"{favorite.DisplayName}: Watch {(favorite.Enabled ? "On" : "Off")}");
            AddLog($"{favorite.DisplayName}: Watch {(favorite.Enabled ? "On" : "Off")}");

            if (favorite.Enabled && !recordingSessions.ContainsKey(favorite.Id))
            {
                await CheckFavoritesAndAutoRecordAsync(new[] { favorite }, "Watch On 방송 확인");
                ConfigureModelCheckTimer();
            }
        }

        private void favoriteContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!TryGetSelectedFavorite(out var favorite))
            {
                e.Cancel = true;
                return;
            }

            var isRecording = recordingSessions.ContainsKey(favorite.Id);
            var isLive = favorite.Metadata.TryGetValue("liveStatus", out var status) && status == "live";
            checkLiveMenuItem.Enabled = favorite.Enabled;
            startRecordingMenuItem.Enabled = isLive && !isRecording && favorite.Enabled;
            stopRecordingMenuItem.Enabled = isRecording;
            highlightCaptureMenuItem.Enabled = isRecording;
            toggleWatchMenuItem.Text = favorite.Enabled ? "Watch Off" : "Watch On";
            toggleWatchMenuItem.Enabled = !isRecording || !favorite.Enabled;
            deleteFavoriteMenuItem.Enabled = !isRecording;
        }
        private void deleteFavoriteMenuItem_Click(object? sender, EventArgs e)
        {
            DeleteSelectedFavorite();
        }

        private void favoritesGridView_SelectionChanged(object? sender, EventArgs e)
        {
            if (sender is DataGridView grid && grid.SelectedRows.Count > 0)
            {
                activeFavoritesGridView = grid;
            }
        }

        private void favoritesGridView_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (sender is not DataGridView grid || e.Button != MouseButtons.Right || e.RowIndex < 0)
            {
                return;
            }

            activeFavoritesGridView = grid;
            liveFavoritesGridView.ClearSelection();
            favoritesGridView.ClearSelection();
            grid.Rows[e.RowIndex].Selected = true;
            grid.CurrentCell = grid.Rows[e.RowIndex].Cells[Math.Max(e.ColumnIndex, 1)];
        }

        private bool TryGetSelectedFavorite(out FavoriteItem favorite)
        {
            favorite = null!;
            var row = GetSelectedFavoriteRow(activeFavoritesGridView)
                ?? GetSelectedFavoriteRow(liveFavoritesGridView)
                ?? GetSelectedFavoriteRow(favoritesGridView);

            if (row?.Tag is not FavoriteItem selected)
            {
                return false;
            }

            favorite = selected;
            return true;
        }

        private static DataGridViewRow? GetSelectedFavoriteRow(DataGridView? grid)
        {
            if (grid is null)
            {
                return null;
            }

            return grid.SelectedRows.Count > 0
                ? grid.SelectedRows[0]
                : grid.CurrentRow;
        }

        private IEnumerable<DataGridViewRow> GetSelectedFavoriteRows()
        {
            var grid = activeFavoritesGridView ?? favoritesGridView;
            return grid.SelectedRows.Count > 0
                ? grid.SelectedRows.Cast<DataGridViewRow>()
                : Enumerable.Empty<DataGridViewRow>();
        }

        private async void favoritesGridView_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex != 11)
            {
                return;
            }

            activeFavoritesGridView = grid;
            if (grid.Rows[e.RowIndex].Tag is not FavoriteItem favorite
                || !recordingSessions.ContainsKey(favorite.Id))
            {
                return;
            }

            await CreateHighlightCaptureAsync(favorite);
        }

        private void favoritesGridView_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex != 11)
            {
                return;
            }

            if (grid.Rows[e.RowIndex].Tag is not FavoriteItem favorite
                || !recordingSessions.ContainsKey(favorite.Id))
            {
                return;
            }

            if (e.Graphics is null)
            {
                return;
            }

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

            var buttonBounds = Rectangle.Inflate(e.CellBounds, -14, -11);
            if (buttonBounds.Width < 28)
            {
                buttonBounds = Rectangle.Inflate(e.CellBounds, -6, -11);
            }

            ButtonRenderer.DrawButton(e.Graphics, buttonBounds, "R", grid.Font, false, System.Windows.Forms.VisualStyles.PushButtonState.Normal);
            e.Handled = true;
        }

        private void favoritesGridView_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (sender is not DataGridView grid)
            {
                return;
            }

            var overCaptureButton = e.RowIndex >= 0
                && e.ColumnIndex == 11
                && grid.Rows[e.RowIndex].Tag is FavoriteItem favorite
                && recordingSessions.ContainsKey(favorite.Id);
            grid.Cursor = overCaptureButton ? Cursors.Hand : Cursors.Default;
        }

        private void favoritesGridView_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
        {
            if (sender is DataGridView grid)
            {
                grid.Cursor = Cursors.Default;
            }
        }

        private void favoritesGridView_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (sender is DataGridView grid)
            {
                activeFavoritesGridView = grid;
            }

            if (e.ColumnIndex == 0)
            {
                return;
            }

            if (favoriteSortColumn == e.ColumnIndex)
            {
                favoriteSortOrder = favoriteSortOrder == SortOrder.Ascending
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
            }
            else
            {
                favoriteSortColumn = e.ColumnIndex;
                favoriteSortOrder = SortOrder.Ascending;
            }

            RefreshFavoriteList();
        }
        private async Task StartRecordingAsync(FavoriteItem favorite, bool verifyLiveBeforeStart)
        {
            if (isShuttingDown || recordingSessions.ContainsKey(favorite.Id))
            {
                return;
            }

            if (!favorite.Enabled)
            {
                AddLog($"{favorite.DisplayName}: Watch Off 상태라 녹화를 건너뜁니다.");
                return;
            }

            favorite.Metadata.TryGetValue("liveStatus", out var liveStatus);
            favorite.Metadata.TryGetValue("streamUrl", out var streamUrl);

            if (!verifyLiveBeforeStart && (liveStatus != "live" || string.IsNullOrWhiteSpace(streamUrl)))
            {
                SetStatus("방송 확인 후 방송중인 항목을 선택하세요.");
                return;
            }

            try
            {
                if (!EnsureRecordingEnvironment())
                {
                    return;
                }

                if (verifyLiveBeforeStart)
                {
                    AddLog($"{favorite.DisplayName}: 녹화 전 방송 URL 재확인");
                    await PreparePandaSessionForMainAsync("녹화 전 세션 준비");
                    await CheckFavoriteLiveAsync(favorite);
                    favoriteStore.Save(favorites);

                    if (!favorite.Metadata.TryGetValue("liveStatus", out liveStatus) || liveStatus != "live"
                        || !favorite.Metadata.TryGetValue("streamUrl", out streamUrl) || string.IsNullOrWhiteSpace(streamUrl))
                    {
                        RefreshFavoriteList();
                        SetStatus("녹화 전 방송 URL 확인에 실패했습니다.");
                        return;
                    }
                }

                if (string.IsNullOrWhiteSpace(streamUrl))
                {
                    SetStatus("녹화 URL이 없습니다.");
                    return;
                }

                var httpContext = await pandaLiveService.GetRecordingHttpContextAsync(loginBrowserForm.WebView);
                AddLog($"{favorite.DisplayName}: 녹화 헤더 준비 - 쿠키 {httpContext.CookieCount}개, User-Agent {(string.IsNullOrWhiteSpace(httpContext.UserAgent) ? "없음" : "사용")}, host {GetHostForLog(streamUrl)}");
                var session = await recordingService.StartAsync(favorite, streamUrl, settings.RecordingDirectory, settings.FfmpegPath, httpContext);
                recordingSessions[favorite.Id] = session;
                session.Process.Exited += (_, _) => QueueRecordingExited(favorite, session);
                if (session.Process.HasExited)
                {
                    QueueRecordingExited(favorite, session);
                }
                favorite.Metadata["recording"] = "true";
                favorite.Metadata["recordingPath"] = session.OutputPath;
                favorite.UpdatedAt = DateTimeOffset.Now;
                favoriteStore.Save(favorites);
                RefreshFavoriteList();
                SetStatus("녹화를 시작했습니다.");
                AddLog($"{favorite.DisplayName}: 최고 화질 녹화 시작 - {session.OutputPath}");
            }
            catch (Exception ex)
            {
                SetStatus($"녹화 시작 실패: {ex.Message}");
            }
            finally
            {
            }
        }

        private void StopRecording(FavoriteItem favorite, RecordingSession session)
        {
            session.Stop();
            session.Dispose();
            favorite.Metadata["recording"] = "false";
            favorite.UpdatedAt = DateTimeOffset.Now;
            favoriteStore.Save(favorites);
            RefreshFavoriteList();
            SetStatus("녹화를 중지했습니다.");
            AddLog($"{favorite.DisplayName}: 녹화 중지");
        }

        private async Task CreateHighlightCaptureAsync(FavoriteItem favorite)
        {
            if (!recordingSessions.TryGetValue(favorite.Id, out var session))
            {
                SetStatus("녹화중인 모델만 하이라이트 캡쳐할 수 있습니다.");
                AddLog($"{favorite.DisplayName}: 녹화중이 아니어서 하이라이트 캡쳐를 건너뜁니다.");
                return;
            }

            settings = settingsStore.Load();
            if (!IsRecordingEnvironmentValid())
            {
                SetStatus("하이라이트 캡쳐 전 환경설정이 필요합니다.");
                if (!ShowEnvironmentSettings() || !IsRecordingEnvironmentValid())
                {
                    return;
                }
            }

            var seconds = Math.Clamp(settings.HighlightCaptureSeconds > 0 ? settings.HighlightCaptureSeconds : 60, 5, 3600);
            try
            {
                SetStatus($"하이라이트 캡쳐 중: {seconds}초");
                var outputPath = await recordingService.CreateHighlightAsync(
                    favorite,
                    session.OutputPath,
                    settings.RecordingDirectory,
                    settings.FfmpegPath,
                    seconds);
                SetStatus("하이라이트 캡쳐를 만들었습니다.");
                AddLog($"{favorite.DisplayName}: 하이라이트 캡쳐 완료 ({seconds}초) - {outputPath}");
            }
            catch (Exception ex)
            {
                SetStatus($"하이라이트 캡쳐 실패: {ex.Message}");
                AddLog($"{favorite.DisplayName}: 하이라이트 캡쳐 실패 - {ex.Message}");
            }
        }

        private void StopAllRecordingsForExit()
        {
            if (recordingSessions.Count == 0)
            {
                return;
            }

            var sessions = recordingSessions.ToList();
            recordingSessions.Clear();

            foreach (var pair in sessions)
            {
                var favorite = favorites.Items.FirstOrDefault(item => item.Id.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
                try
                {
                    pair.Value.Stop();
                    pair.Value.Dispose();
                }
                catch
                {
                    // The app is closing; best-effort cleanup is enough here.
                }

                if (favorite is not null)
                {
                    favorite.Metadata["recording"] = "false";
                    favorite.UpdatedAt = DateTimeOffset.Now;
                }
            }

            favoriteStore.Save(favorites);
        }

        private bool EnsureRecordingEnvironment()
        {
            settings = settingsStore.Load();

            if (IsRecordingEnvironmentValid())
            {
                return true;
            }

            SetStatus("녹화 전 환경설정이 필요합니다.");

            if (!ShowEnvironmentSettings())
            {
                return false;
            }

            if (IsRecordingEnvironmentValid())
            {
                return true;
            }

            SetStatus("ffmpeg 경로 또는 녹화 저장위치를 확인하세요.");
            return false;
        }

        private bool IsRecordingEnvironmentValid()
        {
            if (!File.Exists(settings.FfmpegPath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.RecordingDirectory))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(settings.RecordingDirectory);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void QueueRecordingExited(FavoriteItem favorite, RecordingSession session)
        {
            if (isShuttingDown || IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(() => RecordingExited(favorite, session));
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
        private async void RecordingExited(FavoriteItem favorite, RecordingSession session)
        {
            if (isShuttingDown || !recordingSessions.TryGetValue(favorite.Id, out var active) || !ReferenceEquals(active, session))
            {
                return;
            }

            recordingSessions.Remove(favorite.Id);
            var exitCode = session.ExitCode;
            var errorSummary = session.ErrorSummary;
            session.Dispose();
            favorite.Metadata["recording"] = "false";
            favorite.UpdatedAt = DateTimeOffset.Now;
            favoriteStore.Save(favorites);
            RefreshFavoriteList();
            SetStatus("녹화가 종료되었습니다.");
            AddLog($"{favorite.DisplayName}: 녹화 종료 (코드 {exitCode}) - {session.OutputPath}");
            if (!string.IsNullOrWhiteSpace(errorSummary))
            {
                AddLog($"ffmpeg: {errorSummary}");
                if (errorSummary.Contains("403", StringComparison.OrdinalIgnoreCase)
                    || errorSummary.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
                {
                    AddLog("ffmpeg 권한 거부: 브라우저 재생 요청 헤더 추가 캡처가 필요합니다.");
                }
            }

            if (favorite.Enabled && !isShuttingDown)
            {
                await CheckFavoriteAfterRecordingExitAsync(favorite);
            }
        }

        private async Task CheckFavoriteAfterRecordingExitAsync(FavoriteItem favorite)
        {
            if (isShuttingDown)
            {
                return;
            }

            try
            {
                AddLog($"{favorite.DisplayName}: 녹화 프로세스 종료 후 방송 상태 재확인");
                await PreparePandaSessionForMainAsync("녹화 종료 후 세션 준비");
                await CheckFavoriteLiveAsync(favorite);

                if (favorite.Metadata.TryGetValue("liveStatus", out var status) && status == "live")
                {
                    AddLog($"{favorite.DisplayName}: 방송중으로 확인되어 녹화 재시작");
                    await StartRecordingAsync(favorite, verifyLiveBeforeStart: false);
                }

                favoriteStore.Save(favorites);
                RefreshFavoriteList();
            }
            catch (Exception ex)
            {
                AddLog($"{favorite.DisplayName}: 녹화 종료 후 재확인 실패 - {ex.Message}");
            }
        }

        private async Task CheckSelectedOrAllFavoritesAsync()
        {
            var selectedRows = GetSelectedFavoriteRows().ToList();
            var targets = selectedRows.Count > 0
                ? selectedRows
                    .Select(row => row.Tag)
                    .OfType<FavoriteItem>()
                    .ToList()
                : favorites.Items.Where(item => item.Enabled).ToList();

            if (targets.Count == 0)
            {
                SetStatus("확인할 모델이 없습니다.");
                return;
            }

            SetStatus($"방송 확인 중: {targets.Count}개");

            try
            {
                await PreparePandaSessionForMainAsync("방송 확인 전 세션 준비");
                foreach (var favorite in targets)
                {
                    await CheckFavoriteLiveAsync(favorite);
                    HandleRecordingOfflineCheck(favorite);
                }

                favoriteStore.Save(favorites);
                RefreshFavoriteList();
                SetStatus($"방송 확인 완료: {targets.Count}개");
            }
            catch (Exception ex)
            {
                SetStatus($"방송 확인 실패: {ex.Message}");
            }
        }
        private async Task CheckAllFavoritesAndAutoRecordAsync()
        {
            var now = DateTimeOffset.Now;
            var targets = favorites.Items
                .Where(item => IsAutoCheckTarget(item, now))
                .ToList();
            if (targets.Count == 0)
            {
                return;
            }

            await CheckFavoritesAndAutoRecordAsync(targets, "자동 방송 확인");
        }

        private bool IsAutoCheckTarget(FavoriteItem favorite, DateTimeOffset now)
        {
            return favorite.Enabled
                && !recordingSessions.ContainsKey(favorite.Id)
                && IsFavoriteDueForAutoCheck(favorite, now);
        }

        private async Task CheckFavoritesAndAutoRecordAsync(IReadOnlyCollection<FavoriteItem> targets, string reason)
        {
            if (targets.Count == 0)
            {
                return;
            }

            SetStatus($"{reason} 중: {targets.Count}개");

            try
            {
                await PreparePandaSessionForMainAsync($"{reason} 전 세션 준비");
                foreach (var favorite in targets)
                {
                    await CheckFavoriteLiveAsync(favorite);
                    HandleRecordingOfflineCheck(favorite);
                    if (favorite.Metadata.TryGetValue("liveStatus", out var status)
                        && status == "live"
                        && !recordingSessions.ContainsKey(favorite.Id))
                    {
                        await StartRecordingAsync(favorite, verifyLiveBeforeStart: false);
                    }
                }

                favoriteStore.Save(favorites);
                RefreshFavoriteList();
                SetStatus($"{reason} 완료: {targets.Count}개");
            }
            catch (Exception ex)
            {
                SetStatus($"{reason} 실패: {ex.Message}");
            }
        }

        private async Task CheckFavoriteLiveAsync(FavoriteItem favorite)
        {
            if (!IsPandaFavorite(favorite))
            {
                ClearLivePlaybackMetadata(favorite);
                favorite.Metadata["liveStatus"] = "unsupported";
                favorite.UpdatedAt = DateTimeOffset.Now;
                AddLog($"지원하지 않는 사이트: {favorite.DisplayName} / {favorite.Platform}");
                return;
            }

            var status = await pandaLiveService.GetLiveStatusAsync(loginBrowserForm.WebView, favorite.PlatformUserId);
            if (!status.Success && IsSessionRelatedFailure(status.Message))
            {
                AddLog($"{favorite.DisplayName}: 세션 상태 재확인 중");
                await PreparePandaSessionForMainAsync("세션 재준비");
                status = await pandaLiveService.GetLiveStatusAsync(loginBrowserForm.WebView, favorite.PlatformUserId);
            }

            var now = DateTimeOffset.Now;
            favorite.UpdatedAt = now;
            favorite.Metadata["lastCheckedAt"] = now.ToString("O");

            if (!status.Success)
            {
                ClearLivePlaybackMetadata(favorite);
                favorite.Metadata["liveStatus"] = "error";
                favorite.Metadata["liveMessage"] = status.Message;
                AddLog($"{favorite.DisplayName}: 확인 실패 - {status.Message}");
                return;
            }

            var resolutionForLog = status.Width > 0 && status.Height > 0 ? $"{status.Width}x{status.Height}" : "";
            favorite.Metadata["liveStatus"] = status.IsLive ? "live" : "offline";
            favorite.Metadata["liveMessage"] = status.Message;
            if (status.IsLive)
            {
                favorite.Metadata["streamUrl"] = status.StreamUrl;
                favorite.Metadata["resolution"] = resolutionForLog;
            }
            else
            {
                ClearLivePlaybackMetadata(favorite);
            }

            if (!string.IsNullOrWhiteSpace(status.Title))
            {
                favorite.LastBroadcastTitle = status.Title;
            }

            if (status.IsLive)
            {
                favorite.Metadata["offlineCheckCount"] = "0";
                favorite.LastSeenAt = now;
                favorite.LastLiveAt = now;
            }

            AddLog($"{favorite.DisplayName}: {(status.IsLive ? "방송중" : "오프라인")} {resolutionForLog}");
        }

        private static void ClearLivePlaybackMetadata(FavoriteItem favorite)
        {
            favorite.Metadata.Remove("streamUrl");
            favorite.Metadata.Remove("resolution");
        }

        private void HandleRecordingOfflineCheck(FavoriteItem favorite)
        {
            if (!recordingSessions.TryGetValue(favorite.Id, out var session))
            {
                return;
            }

            var status = favorite.Metadata.TryGetValue("liveStatus", out var liveStatus)
                ? liveStatus
                : "";

            if (status == "live")
            {
                favorite.Metadata["offlineCheckCount"] = "0";
                return;
            }

            if (status != "offline" && status != "error")
            {
                return;
            }

            var currentCount = favorite.Metadata.TryGetValue("offlineCheckCount", out var raw)
                && int.TryParse(raw, out var parsed)
                    ? parsed
                    : 0;
            currentCount++;
            favorite.Metadata["offlineCheckCount"] = currentCount.ToString();

            var threshold = Math.Clamp(settings.RecordingStopAfterOfflineChecks > 0 ? settings.RecordingStopAfterOfflineChecks : 2, 1, 10);
            AddLog($"{favorite.DisplayName}: 방송 종료 판단 {currentCount}/{threshold}");
            if (currentCount >= threshold)
            {
                recordingSessions.Remove(favorite.Id);
                StopRecording(favorite, session);
                AddLog($"{favorite.DisplayName}: 방송 종료 판단으로 녹화 종료");
            }
        }

        private void removeFavoriteButton_Click(object sender, EventArgs e)
        {
            DeleteSelectedFavorite();
        }

        private void DeleteSelectedFavorite()
        {
            if (!TryGetSelectedFavorite(out var selected))
            {
                SetStatus("삭제할 찜 항목을 선택하세요.");
                return;
            }

            favorites.Items.RemoveAll(item => item.Id.Equals(selected.Id, StringComparison.OrdinalIgnoreCase));
            favoriteStore.Save(favorites);
            RefreshFavoriteList();
            SetStatus($"찜 항목을 삭제했습니다. {selected.DisplayName}");
        }
        private static string FormatLastSeen(DateTimeOffset? lastSeenAt)
        {
            return lastSeenAt.HasValue
                ? lastSeenAt.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                : "-";
        }

        private static string FormatLastCheck(FavoriteItem favorite)
        {
            var checkedAt = GetLastCheckedAt(favorite);
            return checkedAt.HasValue
                ? checkedAt.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                : "-";
        }

        private static DateTimeOffset? GetLastCheckedAt(FavoriteItem favorite)
        {
            return favorite.Metadata.TryGetValue("lastCheckedAt", out var raw)
                && DateTimeOffset.TryParse(raw, out var checkedAt)
                    ? checkedAt
                    : null;
        }

        private static bool IsPandaFavorite(FavoriteItem favorite)
        {
            return favorite.Platform.Contains("팬더", StringComparison.OrdinalIgnoreCase)
                || favorite.Platform.Contains("panda", StringComparison.OrdinalIgnoreCase)
                || favorite.ProfileUrl.Contains("pandalive.co.kr", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAdultSessionDelay(string message)
        {
            return message.Contains("성인", StringComparison.OrdinalIgnoreCase)
                || message.Contains("adult", StringComparison.OrdinalIgnoreCase);
        }

        private async Task PreparePandaSessionForMainAsync(string reason)
        {
            try
            {
                var session = await pandaLiveService.PrepareSessionAsync(loginBrowserForm.WebView);
                AddLog($"{reason}: 쿠키 {session.CookieCount}개, 사용자 정보 {(session.HasViewerUserIndex ? "확인" : "미확인")}");
            }
            catch (Exception ex)
            {
                AddLog($"{reason} 실패: {ex.Message}");
            }
        }

        private static bool IsSessionRelatedFailure(string message)
        {
            return IsAdultSessionDelay(message)
                || message.Contains("로그인", StringComparison.OrdinalIgnoreCase)
                || message.Contains("login", StringComparison.OrdinalIgnoreCase)
                || message.Contains("권한", StringComparison.OrdinalIgnoreCase)
                || message.Contains("인증", StringComparison.OrdinalIgnoreCase)
                || message.Contains("403", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetHostForLog(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "unknown";
        }


        private static string FormatDisplayNameWithUserId(FavoriteItem favorite)
        {
            return string.IsNullOrWhiteSpace(favorite.PlatformUserId)
                ? favorite.DisplayName
                : $"{favorite.DisplayName}({favorite.PlatformUserId})";
        }

        private string GetInstantCaptureText(FavoriteItem favorite)
        {
            return recordingSessions.ContainsKey(favorite.Id)
                ? "R"
                : "-";
        }
        private static string GetFavoriteStatusText(FavoriteItem favorite)
        {
            if (!favorite.Enabled)
            {
                return "watch-off";
            }

            if (!favorite.Metadata.TryGetValue("liveStatus", out var status))
            {
                return "-";
            }

            var message = favorite.Metadata.TryGetValue("liveMessage", out var liveMessage)
                ? liveMessage
                : "";

            if (status == "live")
            {
                return "방송중";
            }

            if (status == "offline" || IsOfflineBroadcastMessage(message))
            {
                return "OFF LINE";
            }

            if (IsSessionRelatedFailure(message))
            {
                return "로그인 체크";
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            return status switch
            {
                "unsupported" => "미지원",
                "error" => "확인실패",
                _ => status
            };
        }

        private static bool IsOfflineBroadcastMessage(string message)
        {
            return message.Contains("종료된 방송", StringComparison.OrdinalIgnoreCase)
                || message.Contains("종료되거나", StringComparison.OrdinalIgnoreCase)
                || message.Contains("offline", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasPaidRoomTicketMessage(FavoriteItem favorite)
        {
            return favorite.Metadata.TryGetValue("liveMessage", out var message)
                && (message.Contains("풀방 입장권", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("풀방입장권", StringComparison.OrdinalIgnoreCase));
        }

        private string GetRecordingText(FavoriteItem favorite)
        {
            return recordingSessions.ContainsKey(favorite.Id)
                ? "R(Ing)"
                : "-";
        }

        private static string GetWatchText(FavoriteItem favorite)
        {
            return favorite.Enabled ? "On" : "-";
        }

        private string GetRecordingFileSizeText(FavoriteItem favorite)
        {
            var bytes = GetRecordingFileSizeBytes(favorite);
            return bytes > 0
                ? FormatFileSize(bytes)
                : "-";
        }

        private long GetRecordingFileSizeBytes(FavoriteItem favorite)
        {
            var path = recordingSessions.TryGetValue(favorite.Id, out var session)
                ? session.OutputPath
                : favorite.Metadata.TryGetValue("recordingPath", out var metadataPath)
                    ? metadataPath
                    : "";

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return 0;
            }

            try
            {
                return new FileInfo(path).Length;
            }
            catch
            {
                return 0;
            }
        }

        private static string FormatFileSize(long bytes)
        {
            return $"{bytes / 1024d / 1024d:0.0} MB";
        }


        private static int CompareText(string? left, string? right)
        {
            return string.Compare(left ?? "", right ?? "", StringComparison.CurrentCultureIgnoreCase);
        }

        private static int CompareNullableDates(DateTimeOffset? left, DateTimeOffset? right)
        {
            if (left.HasValue && right.HasValue)
            {
                return DateTimeOffset.Compare(left.Value, right.Value);
            }

            if (left.HasValue)
            {
                return 1;
            }

            if (right.HasValue)
            {
                return -1;
            }

            return 0;
        }

        private static int CompareLong(long left, long right)
        {
            return left.CompareTo(right);
        }

        private static string GetResolutionText(FavoriteItem favorite)
        {
            return favorite.Metadata.TryGetValue("resolution", out var resolution)
                && !string.IsNullOrWhiteSpace(resolution)
                    ? resolution
                    : "-";
        }

        private void AddLog(string message)
        {
            if (logTextBox.IsDisposed)
            {
                return;
            }

            logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        private void SetStatus(string message)
        {
            AddLog(message);
        }

    }
}








