namespace Hunbjter
{
    public partial class Form1 : ThemedForm, IRecordingCoordinator
    {
        private readonly LoginSettingsStore settingsStore = new();
        private readonly SiteSettingsStore siteSettingsStore = new();
        private readonly FavoriteStore favoriteStore = new();
        private readonly FavoritesPersistence favoritesPersistence;
        private readonly PandaLiveService pandaLiveService = new();
        private readonly RecordingService recordingService = new();
        private readonly LoginBrowserForm loginBrowserForm = new();
        private readonly WebViewGate webViewGate;
        private readonly CancellationTokenSource shutdownCts = new();
        private readonly Dictionary<string, RecordingSession> recordingSessions = new(StringComparer.OrdinalIgnoreCase);
        private readonly MonitorRoster monitorRoster;
        private readonly WebViewLiveStatusProbe liveStatusProbe;
        private readonly System.Windows.Forms.Timer recordingFileSizeTimer = new();
        private readonly NotifyIcon trayIcon = new();
        private readonly ContextMenuStrip trayContextMenu = new();
        private readonly ThemedGrid liveFavoritesGridView = new();
        private readonly SectionHeader liveHeader = new();
        private readonly SectionHeader standbyHeader = new();
        private readonly Dictionary<string, long> recordingSizeCache = new(StringComparer.OrdinalIgnoreCase);
        private DataGridView? activeFavoritesGridView;
        private (int Row, int Column) hoverInteractiveCell = (-1, -1);
        private readonly ToolStripMenuItem showTrayMenuItem = new();
        private readonly ToolStripMenuItem exitTrayMenuItem = new();
        private static readonly TimeSpan RecordingFileSizeInterval = TimeSpan.FromSeconds(10);
        private LoginSettings settings = new();
        private FavoritesDocument favorites = new();
        private int favoriteSortColumn = 3;
        private SortOrder favoriteSortOrder = SortOrder.Ascending;
        private bool closingConfirmed;
        private bool isShuttingDown;
        private bool logCollapsed = true;

        public Form1()
        {
            webViewGate = new WebViewGate(() => loginBrowserForm.WebView);
            favoritesPersistence = new FavoritesPersistence(favoriteStore);
            favoritesPersistence.SaveFailed += (_, e) => AddLog(e.Message);
            liveStatusProbe = new WebViewLiveStatusProbe(pandaLiveService);
            liveStatusProbe.LogRequested += (_, e) => AddLog(e.Message);

            monitorRoster = new MonitorRoster(new MonitorContext(
                liveStatusProbe,
                webViewGate,
                this,
                [
                    new PerModelIntervalRule(),
                    new PaidRoomRetryRule(),
                    new SessionFailureRetryRule(),
                    new RecentlySeenRule(),
                    new FailureBackoffRule()
                ],
                () => settings,
                TimeProvider.System));
            monitorRoster.LogRequested += (_, e) => AddLog(e.Message);
            monitorRoster.StatusChanged += monitorRoster_StatusChanged;

            InitializeComponent();
            ApplyTheme();
            ApplyLogVisibility();
            ConfigureSplitFavoriteGrids();
            ConfigureTrayIcon();
            WireFavoriteGridEvents(favoritesGridView);
            WireFavoriteGridEvents(liveFavoritesGridView);
            recordingFileSizeTimer.Interval = (int)RecordingFileSizeInterval.TotalMilliseconds;
            recordingFileSizeTimer.Tick += recordingFileSizeTimer_Tick;
            recordingFileSizeTimer.Start();
            Shown += Form1_Shown;
            LoadSettings();
            LoadFavorites();

#if DEBUG
            designPreviewActive = TryApplyDesignPreview();
#endif
        }

#if DEBUG
        private bool designPreviewActive;

        /// <summary>
        /// Fills the grids with synthetic rows so every badge state can be reviewed without a
        /// live session. Gated behind an environment flag, so a normal launch is unaffected:
        /// no network, no recording, and the real favorites store is never read or written.
        /// </summary>
        private bool TryApplyDesignPreview()
        {
            if (!DesignPreview.IsEnabled)
            {
                return false;
            }

            favorites = DesignPreview.CreateFavorites();

            foreach (var id in DesignPreview.RecordingIds)
            {
                recordingSessions[id] = DesignPreview.CreateIdleSession();
            }

            RefreshFavoriteList();

            foreach (var line in DesignPreview.SampleLog)
            {
                logView.Append(line);
            }

            SetStatus("디자인 미리보기 모드 (실제 데이터 아님)");
            return true;
        }
#endif

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        }

        /// <summary>
        /// Answers <see cref="SingleInstanceGuard.WakeMessage"/> from a second launch attempt by
        /// surfacing this window instead. The message is a broadcast, so it arrives here even
        /// while the form is hidden in the tray.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == SingleInstanceGuard.WakeMessage)
            {
                RestoreFromTray();
                return;
            }

            base.WndProc(ref m);
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

            // Cancel and abandon: awaiting the monitor loops here would deadlock, because their
            // continuations need the message pump this thread is about to stop running.
            shutdownCts.Cancel();
            monitorRoster.Retire();
            favoritesPersistence.Flush();
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

        /// <summary>Applies the dark palette to the pieces the designer cannot express.</summary>
        private void ApplyTheme()
        {
            ThemedMenuRenderer.Apply(favoriteContextMenu);
            ApplyColumnStyles(favoritesGridView);

            // Deliberately in code, not in Form1.Designer.cs: HeaderBar.ActionHost is a property
            // returning a nested container, not a Designer-tracked field, so the WinForms
            // Designer has no idea it needs to preserve Controls.Add calls into it - the first
            // time this project's owner opens the form in Visual Studio and it resaves
            // InitializeComponent, that wiring (and these buttons along with it) silently
            // vanishes from the rendered form. Same story for StatCard.Caption/AccentColor below:
            // both are [DesignerSerializationVisibility(Hidden)] (to silence a WFO1000 warning),
            // which is the Designer's own signal to never persist them - so any assignment placed
            // in the Designer file is guaranteed to be dropped on the next resave. Keeping all of
            // it here instead makes it immune to that.
            ConfigureHeaderBar();
            ConfigureStatCards();
        }

        private void ConfigureHeaderBar()
        {
            siteManagementButton.Variant = ButtonVariant.Secondary;
            environmentSettingsButton.Variant = ButtonVariant.Secondary;
            modelManagementButton.Variant = ButtonVariant.Primary;
            toggleLogButton.Variant = ButtonVariant.Ghost;
            clearLogButton.Variant = ButtonVariant.Ghost;

            // FlowDirection is RightToLeft, so the first control added ends up furthest right.
            headerBar.ActionHost.Controls.Add(modelManagementButton);
            headerBar.ActionHost.Controls.Add(environmentSettingsButton);
            headerBar.ActionHost.Controls.Add(siteManagementButton);

            // The logo/wordmark doubles as a shortcut to 사이트관리 - same action as the button.
            headerBar.BrandClicked += siteManagementButton_Click;
        }

        private void ConfigureStatCards()
        {
            watchingCard.Caption = "감시중";
            watchingCard.AccentColor = Theme.Accent;

            liveCard.Caption = "방송중";
            liveCard.AccentColor = Theme.Live;

            recordingCard.Caption = "녹화중";
            recordingCard.AccentColor = Theme.Recording;

            sizeCard.Caption = "녹화 용량";
            sizeCard.AccentColor = Theme.Warning;
        }

        private void ConfigureSplitFavoriteGrids()
        {
            CloneFavoriteGrid(favoritesGridView, liveFavoritesGridView);
            liveFavoritesGridView.Name = "liveFavoritesGridView";

            // A faint cast of the same green already used for the 방송중 dot/REC badges is the
            // one signal that reliably tells the two grids apart from across the room, not just
            // from the section header above them. RestBackColor (not DefaultCellStyle.BackColor
            // directly) keeps the tint intact after a row is hovered and the mouse moves away.
            var liveTint = Theme.Blend(Theme.Live, Theme.Background, 0.08);
            liveFavoritesGridView.BackgroundColor = liveTint;
            liveFavoritesGridView.RestBackColor = liveTint;
            var liveHeaderTint = Theme.Blend(Theme.Live, Theme.Background, 0.14);
            liveFavoritesGridView.ColumnHeadersDefaultCellStyle.BackColor = liveHeaderTint;
            // Matches the header fill so a header click does not flash the untinted default.
            liveFavoritesGridView.ColumnHeadersDefaultCellStyle.SelectionBackColor = liveHeaderTint;

            // Each grid hides what is not useful for its own rows. The column indices below stay
            // fixed (0..11) either way - hiding a column only affects rendering/hit-testing, not
            // its Index, so every Cells[10]/Cells[11]/switch(ColumnIndex)/sort-by-index elsewhere
            // in this file keeps working unmodified for both grids.
            liveFavoritesGridView.Columns[8].Visible = false; // 마지막 확인 - 방송중인 항목은 계속 갱신되므로 불필요

            // Standby rows have never had a live check succeed, so these are always blank there.
            favoritesGridView.Columns[5].Visible = false;  // 녹화
            favoritesGridView.Columns[6].Visible = false;  // 해상도
            favoritesGridView.Columns[10].Visible = false; // 파일 크기
            favoritesGridView.Columns[11].Visible = false; // 순간기록

            liveHeader.Title = "방송중";
            liveHeader.DotColor = Theme.Live;

            standbyHeader.Title = "대기 목록";
            standbyHeader.DotColor = Theme.TextMuted;
            standbyHeader.HollowDot = true;

            favoritePanel.SuspendLayout();
            favoritePanel.Controls.Clear();
            favoritePanel.RowStyles.Clear();
            favoritePanel.RowCount = 4;
            favoritePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            favoritePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
            favoritePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            favoritePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
            favoritePanel.Controls.Add(liveHeader, 0, 0);
            favoritePanel.Controls.Add(liveFavoritesGridView, 0, 1);
            favoritePanel.Controls.Add(standbyHeader, 0, 2);
            favoritePanel.Controls.Add(favoritesGridView, 0, 3);
            favoritePanel.ResumeLayout(false);
        }

        /// <summary>
        /// Only the column set is cloned. Everything visual now comes from the
        /// <see cref="ThemedGrid"/> constructor, so both grids stay in sync by construction.
        /// </summary>
        private static void CloneFavoriteGrid(ThemedGrid source, ThemedGrid target)
        {
            target.ContextMenuStrip = source.ContextMenuStrip;
            target.Dock = DockStyle.Fill;
            target.Margin = source.Margin;
            target.TabIndex = source.TabIndex;

            target.Columns.Clear();
            foreach (DataGridViewColumn column in source.Columns)
            {
                var clone = (DataGridViewColumn)column.Clone();

                // Clone() does not reliably carry AutoSizeMode, and the grid depends on the
                // name column filling the leftover width to avoid a horizontal scroll bar.
                clone.AutoSizeMode = column.AutoSizeMode;
                clone.MinimumWidth = column.MinimumWidth;
                target.Columns.Add(clone);
            }

            ApplyColumnStyles(target);
        }

        /// <summary>Secondary columns are dimmed so the model name and status carry the row.</summary>
        private static void ApplyColumnStyles(ThemedGrid grid)
        {
            grid.Columns[1].DefaultCellStyle.ForeColor = Theme.TextMuted;
            grid.Columns[1].DefaultCellStyle.Font = Theme.Small;
            grid.Columns[2].DefaultCellStyle.ForeColor = Theme.TextSecondary;
            grid.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            foreach (var index in new[] { 6, 7, 8, 10 })
            {
                grid.Columns[index].DefaultCellStyle.ForeColor = Theme.TextSecondary;
                grid.Columns[index].DefaultCellStyle.Font = Theme.Mono;
            }
        }

        private void WireFavoriteGridEvents(DataGridView grid)
        {
            grid.SelectionChanged += favoritesGridView_SelectionChanged;
            grid.CellMouseDown += favoritesGridView_CellMouseDown;
            grid.CellClick += favoritesGridView_CellClick;
            grid.CellPainting += favoritesGridView_CellPainting;
            grid.CellMouseMove += favoritesGridView_CellMouseMove;
            grid.CellMouseLeave += favoritesGridView_CellMouseLeave;
            grid.ColumnHeaderMouseClick += favoritesGridView_ColumnHeaderMouseClick;
        }
        private void ConfigureTrayIcon()
        {
            // Foreground: taskbar + tray both show. Minimized: OnResize above hides the window
            // entirely, which drops it from the taskbar and leaves only the tray icon.
            ShowInTaskbar = true;

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

            ThemedMenuRenderer.Apply(trayContextMenu);
            trayIcon.ContextMenuStrip = trayContextMenu;
            trayIcon.Icon = AppIcon.Shared;
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
            using var confirmForm = new ThemedDialog
            {
                Text = Text,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
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
                Font = Theme.Title,
                ForeColor = Theme.TextPrimary,
                Text = "\uC885\uB8CC\uD560\uAE4C\uC694?",
                TextAlign = ContentAlignment.MiddleLeft
            };

            var messageLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Theme.TextSecondary,
                Text = "\uB179\uD654\uC911\uC778 \uD56D\uBAA9\uC774 \uC788\uC73C\uBA74 \uC548\uC804\uD558\uAC8C \uC885\uB8CC\uD55C \uB4A4 \uC571\uC744 \uB2EB\uC2B5\uB2C8\uB2E4.",
                TextAlign = ContentAlignment.MiddleLeft
            };

            var countdownLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Theme.TextMuted,
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

            var yesButton = new ThemedButton
            {
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                DialogResult = DialogResult.Yes,
                Size = new Size(84, 30),
                Text = "\uC608",
                Variant = ButtonVariant.Primary
            };
            var noButton = new ThemedButton
            {
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                DialogResult = DialogResult.No,
                Size = new Size(84, 30),
                Text = "\uC544\uB2C8\uC694",
                Variant = ButtonVariant.Ghost
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
        }

        private void Form1_Shown(object? sender, EventArgs e)
        {
#if DEBUG
            if (designPreviewActive)
            {
                return;
            }
#endif

            // Started here rather than in the constructor so each monitor's loop captures the
            // UI thread's SynchronizationContext — that is what makes WebView2 access legal.
            monitorRoster.Sync(favorites);
            monitorRoster.Start();
        }

        private void monitorRoster_StatusChanged(object? sender, ModelStatusChangedEventArgs e)
        {
            if (isShuttingDown || IsDisposed)
            {
                return;
            }

            // Debounced: with each model checking on its own schedule, saving synchronously here
            // would rewrite the whole document once per model per interval instead of once per
            // batch. Structural changes (add/delete/watch toggle/recording lifecycle) still save
            // immediately elsewhere and are unaffected.
            favoritesPersistence.MarkDirty(favorites);
            RefreshFavoriteList();
        }

        private async void siteManagementButton_Click(object sender, EventArgs e)
        {
            using var siteManagementForm = new SiteManagementForm(siteSettingsStore, settingsStore, settings);

            // The dialog loads, edits and saves the same files the monitors write, so hold the
            // monitors while it is open rather than racing it.
            monitorRoster.Suspend();
            favoritesPersistence.Flush();
            try
            {
                var result = siteManagementForm.ShowDialog(this);

                LoadSettings();
                LoadFavorites(resetRuntimeState: false);
                monitorRoster.Sync(favorites);

                // A (re)login just happened here, or was at least attempted - any model stuck
                // on a session-related failure is worth an immediate recheck.
                RequestImmediateRecheckForStaleFavorites("사이트 설정 변경 후 재확인");

                if (result == DialogResult.OK)
                {
                    SetStatus("사이트 설정을 반영했습니다.");
                }
            }
            finally
            {
                monitorRoster.Resume();
            }

            await Task.CompletedTask;
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

            monitorRoster.Suspend();
            favoritesPersistence.Flush();
            try
            {
                modelManagementForm.ShowDialog(this);

                LoadFavorites(resetRuntimeState: false);
                monitorRoster.Sync(favorites);

                // A model just flipped to watch-on here goes through ModelManagementForm's own
                // SetFavoriteWatch, not Form1.ToggleWatch, so it never got a RequestImmediate
                // nudge (ModelManagementForm has no reference to monitorRoster to call it with).
                RequestImmediateRecheckForStaleFavorites("모델관리 변경 후 재확인");
            }
            finally
            {
                monitorRoster.Resume();
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Forces an immediate recheck for any watched model that either has no live status yet
        /// (freshly enabled, e.g. via a watch toggle) or is stuck on a session-related failure
        /// ("로그인 체크") - both would otherwise sit until FailureBackoffRule's backoff elapses
        /// (up to 30 minutes) or the model's normal interval comes due, even though whatever
        /// caused the wait (a fresh watch-on, a fresh login) just happened.
        /// </summary>
        private void RequestImmediateRecheckForStaleFavorites(string reason)
        {
            foreach (var favorite in favorites.Items)
            {
                if (!favorite.Enabled)
                {
                    continue;
                }

                var hasStatus = favorite.Metadata.ContainsKey("liveStatus");
                var message = favorite.Metadata.TryGetValue("liveMessage", out var liveMessage) ? liveMessage : "";
                var needsRecheck = !hasStatus || PandaMessages.IsSessionRelatedFailure(message);

                if (needsRecheck && monitorRoster.Find(favorite.Id) is { } monitor)
                {
                    monitor.RequestImmediate(reason);
                }
            }
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
            monitorRoster.Resume();
            return true;
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

            // A corrupted store used to come back as an empty roster, which is indistinguishable
            // from "the user has no models". Surface it instead.
            if (favoriteStore.LastLoadFailure is { } loadFailure)
            {
                AddLog(loadFailure);
            }

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
            // Every automatic per-model check ends here too (monitorRoster_StatusChanged), and
            // with four-plus independent per-model loops this can fire every few seconds. Without
            // preserving the selection across it, a refresh landing between "right-click a row"
            // and "click 방송 확인 in the menu that just opened" silently drops the selection -
            // CheckSelectedOrAllFavoritesAsync then sees nothing selected and falls back to
            // checking every watched model instead of just the one the user picked.
            var selectedIds = CaptureSelectedFavoriteIds();

            liveFavoritesGridView.Rows.Clear();
            favoritesGridView.Rows.Clear();

            var sorted = SortFavoritesForDisplay();
            AddFavoriteRows(liveFavoritesGridView, sorted.Where(IsLiveListFavorite));
            AddFavoriteRows(favoritesGridView, sorted.Where(item => !IsLiveListFavorite(item)));

            if (selectedIds.Count == 0 || !RestoreSelection(selectedIds))
            {
                // ClearSelection alone leaves the current cell highlighted, which reads as a
                // phantom selection on the first row after every refresh.
                liveFavoritesGridView.CurrentCell = null;
                favoritesGridView.CurrentCell = null;
                liveFavoritesGridView.ClearSelection();
                favoritesGridView.ClearSelection();
            }

            liveHeader.Count = liveFavoritesGridView.Rows.Count;
            standbyHeader.Count = favoritesGridView.Rows.Count;
            ApplySortGlyphs();
            UpdateStatCards();
        }

        private HashSet<string> CaptureSelectedFavoriteIds()
        {
            return liveFavoritesGridView.SelectedRows.Cast<DataGridViewRow>()
                .Concat(favoritesGridView.SelectedRows.Cast<DataGridViewRow>())
                .Select(row => row.Tag)
                .OfType<FavoriteItem>()
                .Select(item => item.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Re-selects whichever grid the model landed in - it may have moved between 방송중 and
        /// 대기 목록 across the refresh - and points activeFavoritesGridView at it, since that is
        /// what GetSelectedFavoriteRows()/TryGetSelectedFavorite() actually read.
        /// </summary>
        private bool RestoreSelection(HashSet<string> selectedIds)
        {
            return RestoreSelectionIn(liveFavoritesGridView, selectedIds)
                || RestoreSelectionIn(favoritesGridView, selectedIds);
        }

        private bool RestoreSelectionIn(ThemedGrid grid, HashSet<string> selectedIds)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Tag is not FavoriteItem favorite || !selectedIds.Contains(favorite.Id))
                {
                    continue;
                }

                row.Selected = true;
                grid.CurrentCell = row.Cells[Math.Max(grid.CurrentCell?.ColumnIndex ?? 1, 1)];
                activeFavoritesGridView = grid;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Every column is <see cref="DataGridViewColumnSortMode.Programmatic"/>, so the sort
        /// indicator has to be driven explicitly or it never appears.
        /// </summary>
        private void ApplySortGlyphs()
        {
            liveFavoritesGridView.ShowSortGlyph(favoriteSortColumn, favoriteSortOrder);
            favoritesGridView.ShowSortGlyph(favoriteSortColumn, favoriteSortOrder);
        }

        private void UpdateStatCards()
        {
            RefreshRecordingSizeCache();

            watchingCard.Value = favorites.Items.Count(item => item.Enabled).ToString();
            liveCard.Value = favorites.Items
                .Count(item => item.Enabled
                    && item.Metadata.TryGetValue("liveStatus", out var status) && status == "live")
                .ToString();
            recordingCard.Value = recordingSessions.Count.ToString();
            sizeCard.Value = FormatFileSizeCompact(recordingSizeCache.Values.Sum());
        }

        /// <summary>
        /// Only files being written right now are measured, so the total reflects the current
        /// session rather than every recording ever left on disk.
        /// </summary>
        private void RefreshRecordingSizeCache()
        {
            recordingSizeCache.Clear();
            foreach (var favorite in favorites.Items.Where(item => recordingSessions.ContainsKey(item.Id)))
            {
                recordingSizeCache[favorite.Id] = GetRecordingFileSizeBytes(favorite);
            }
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
                grid.Rows[rowIndex].Tag = favorite;
                sequence++;
            }
        }

        private bool IsLiveListFavorite(FavoriteItem favorite)
        {
            // A watch-off model never gets rechecked, so a stale "liveStatus: live" from before
            // it was turned off would otherwise pin it in the 방송중 grid forever.
            if (!favorite.Enabled)
            {
                return false;
            }

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

        private void recordingFileSizeTimer_Tick(object? sender, EventArgs e)
        {
            UpdateRecordingFileSizes();
        }

        private void UpdateRecordingFileSizes()
        {
            UpdateRecordingFileSizes(liveFavoritesGridView);
            UpdateRecordingFileSizes(favoritesGridView);
            UpdateStatCards();
        }

        private void UpdateRecordingFileSizes(ThemedGrid grid)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Tag is not FavoriteItem favorite || row.IsNewRow)
                {
                    continue;
                }

                row.Cells[10].Value = GetRecordingFileSizeText(favorite);
                row.Cells[11].Value = GetInstantCaptureText(favorite);

                // The REC and capture cells are custom painted, so the value change alone
                // does not schedule a repaint.
                grid.InvalidateRow(row.Index);
            }
        }
        private async void startRecordingMenuItem_Click(object? sender, EventArgs e)
        {
            if (TryGetSelectedFavorite(out var favorite))
            {
                using var lease = await webViewGate.AcquireAsync(GatePriority.Manual, shutdownCts.Token);
                await StartRecordingAsync(favorite, verifyLiveBeforeStart: true, lease);
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

        private async void splitRecordingMenuItem_Click(object? sender, EventArgs e)
        {
            if (TryGetSelectedFavorite(out var favorite))
            {
                using var lease = await webViewGate.AcquireAsync(GatePriority.Manual, shutdownCts.Token);
                await SplitRecordingAsync(favorite, lease);
            }
        }

        private void toggleWatchMenuItem_Click(object? sender, EventArgs e)
        {
            if (TryGetSelectedFavorite(out var favorite))
            {
                ToggleWatch(favorite);
            }
        }

        /// <summary>
        /// Shared by the context menu item and a direct click on the 감시 badge in either grid,
        /// so both paths get the same recording guard and the same monitor reschedule/nudge.
        /// </summary>
        private void ToggleWatch(FavoriteItem favorite)
        {
            if (favorite.Enabled && recordingSessions.ContainsKey(favorite.Id))
            {
                SetStatus("녹화중인 모델은 Watch를 Off로 변경할 수 없습니다.");
                AddLog($"{favorite.DisplayName}: 녹화중이라 Watch Off를 건너뜁니다.");
                return;
            }

            favorite.Enabled = !favorite.Enabled;
            // Stale status would otherwise linger (turning on: until the next check comes back;
            // turning off: forever, since a watch-off model never gets rechecked again) - clear
            // it either way. IsLiveListFavorite/UpdateStatCards also gate on Enabled directly, so
            // this is belt-and-suspenders against any other code path that reads liveStatus.
            foreach (var key in new[] { "liveStatus", "liveMessage", "streamUrl", "resolution" })
            {
                favorite.Metadata.Remove(key);
            }

            favorite.UpdatedAt = DateTimeOffset.Now;
            favoriteStore.Save(favorites);
            RefreshFavoriteList();
            SetStatus($"{favorite.DisplayName}: Watch {(favorite.Enabled ? "On" : "Off")}");
            AddLog($"{favorite.DisplayName}: Watch {(favorite.Enabled ? "On" : "Off")}");

            if (monitorRoster.Find(favorite.Id) is { } monitor)
            {
                if (favorite.Enabled)
                {
                    monitor.RequestImmediate("Watch On 방송 확인");
                }
                else
                {
                    monitor.Reschedule();
                }
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
            splitRecordingMenuItem.Enabled = isRecording;
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
            if (sender is not DataGridView grid || grid.SelectedRows.Count == 0)
            {
                return;
            }

            activeFavoritesGridView = grid;

            // Each grid keeps its own selection independently, so selecting a row here does not
            // clear whatever was left selected in the other grid. Left alone, that leftover
            // selection wins back activeFavoritesGridView the next time RefreshFavoriteList runs
            // (RestoreSelection tries liveFavoritesGridView first) - "방송 확인"/"녹화 시작" would
            // then silently act on a stale, unrelated row instead of the one just clicked.
            var other = ReferenceEquals(grid, liveFavoritesGridView) ? favoritesGridView : liveFavoritesGridView;
            if (other.SelectedRows.Count > 0)
            {
                other.ClearSelection();
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

        private async void favoritesGridView_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (sender is not DataGridView grid || e.RowIndex < 0)
            {
                return;
            }

            if (grid.Rows[e.RowIndex].Tag is not FavoriteItem favorite)
            {
                return;
            }

            activeFavoritesGridView = grid;

            switch (e.ColumnIndex)
            {
                case 9:
                    ToggleWatch(favorite);
                    break;
                case 11:
                    if (recordingSessions.ContainsKey(favorite.Id))
                    {
                        await CreateHighlightCaptureAsync(favorite);
                    }
                    break;
            }
        }

        /// <summary>
        /// Dispatches the semantic columns to <see cref="GridRenderers"/>. Anything not claimed
        /// here falls through to <see cref="ThemedGrid"/>, which paints it without the focus rectangle.
        /// </summary>
        private void favoritesGridView_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (sender is not ThemedGrid grid || e.Graphics is null || e.RowIndex < 0)
            {
                return;
            }

            if (grid.Rows[e.RowIndex].Tag is not FavoriteItem favorite)
            {
                return;
            }

            var isRecording = recordingSessions.ContainsKey(favorite.Id);

            switch (e.ColumnIndex)
            {
                case 3:
                    GridRenderers.PaintNameTwoLine(e, favorite);
                    break;
                case 4:
                    GridRenderers.PaintStatusBadge(e, GetFavoriteStatusText(favorite));
                    break;
                case 5:
                    GridRenderers.PaintRecIndicator(e, isRecording);
                    break;
                case 9:
                    GridRenderers.PaintWatchBadge(
                        e,
                        favorite.Enabled,
                        hoverInteractiveCell == (e.RowIndex, e.ColumnIndex));
                    break;
                case 11:
                    GridRenderers.PaintCaptureButton(
                        e,
                        isRecording,
                        hoverInteractiveCell == (e.RowIndex, e.ColumnIndex));
                    break;
            }
        }

        private void favoritesGridView_CellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (sender is not ThemedGrid grid)
            {
                return;
            }

            var isFavoriteRow = e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is FavoriteItem;
            var overWatchBadge = isFavoriteRow && e.ColumnIndex == 9;
            var overCaptureButton = isFavoriteRow
                && e.ColumnIndex == 11
                && grid.Rows[e.RowIndex].Tag is FavoriteItem favorite
                && recordingSessions.ContainsKey(favorite.Id);
            var interactive = overWatchBadge || overCaptureButton;

            grid.Cursor = interactive ? Cursors.Hand : Cursors.Default;

            SetHoverInteractiveCell(grid, interactive ? (e.RowIndex, e.ColumnIndex) : (-1, -1));
        }

        private void favoritesGridView_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
        {
            if (sender is ThemedGrid grid)
            {
                grid.Cursor = Cursors.Default;
                SetHoverInteractiveCell(grid, (-1, -1));
            }
        }

        private void SetHoverInteractiveCell(ThemedGrid grid, (int Row, int Column) next)
        {
            if (hoverInteractiveCell == next)
            {
                return;
            }

            var previous = hoverInteractiveCell;
            hoverInteractiveCell = next;

            InvalidateInteractiveCell(grid, previous);
            InvalidateInteractiveCell(grid, next);
        }

        private static void InvalidateInteractiveCell(ThemedGrid grid, (int Row, int Column) cell)
        {
            if (cell.Row >= 0 && cell.Row < grid.Rows.Count && cell.Column >= 0 && cell.Column < grid.Columns.Count)
            {
                grid.InvalidateCell(cell.Column, cell.Row);
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
        private async Task StartRecordingAsync(FavoriteItem favorite, bool verifyLiveBeforeStart, WebViewLease lease)
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
                    await liveStatusProbe.PrepareSessionAsync(lease, "녹화 전 세션 준비", shutdownCts.Token);

                    if (monitorRoster.Find(favorite.Id) is { } monitor)
                    {
                        await monitor.RunCheckAsync(CheckTrigger.BeforeRecording, lease, shutdownCts.Token);
                    }

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

                var httpContext = await pandaLiveService.GetRecordingHttpContextAsync(lease.WebView, shutdownCts.Token);
                AddLog($"{favorite.DisplayName}: 녹화 헤더 준비 - 쿠키 {httpContext.CookieCount}개, User-Agent {(string.IsNullOrWhiteSpace(httpContext.UserAgent) ? "없음" : "사용")}, host {PandaMessages.HostForLog(streamUrl)}");
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
                monitorRoster.Find(favorite.Id)?.Reschedule();
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
            monitorRoster.Find(favorite.Id)?.Reschedule();
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

        /// <summary>
        /// Starts a new ffmpeg process on a freshly-checked stream URL and swaps it in for the
        /// current recording session, so the file rolls over with no recording gap and no
        /// separate stop-then-start click. The cached streamUrl from the original check cannot
        /// be reused here: pandalive's playback tokens (AWS IVS) carry an "aws:single-use-uuid"
        /// claim and a ~10 minute expiry, so a second ffmpeg against the same URL a moment later
        /// gets an immediate 403 regardless of timing - a fresh check is required either way,
        /// same as "녹화 시작" does.
        /// </summary>
        private async Task SplitRecordingAsync(FavoriteItem favorite, WebViewLease lease)
        {
            if (!recordingSessions.TryGetValue(favorite.Id, out var oldSession))
            {
                SetStatus("녹화중인 모델만 파일을 분할할 수 있습니다.");
                AddLog($"{favorite.DisplayName}: 녹화중이 아니어서 파일 분할을 건너뜁니다.");
                return;
            }

            settings = settingsStore.Load();
            if (!IsRecordingEnvironmentValid())
            {
                SetStatus("파일 분할 전 환경설정이 필요합니다.");
                if (!ShowEnvironmentSettings() || !IsRecordingEnvironmentValid())
                {
                    return;
                }
            }

            if (monitorRoster.Find(favorite.Id) is not { } monitor)
            {
                SetStatus("파일 분할 실패: 모델을 찾을 수 없습니다.");
                return;
            }

            try
            {
                AddLog($"{favorite.DisplayName}: 파일 분할 전 방송 URL 재확인");
                await liveStatusProbe.PrepareSessionAsync(lease, "파일 분할 전 세션 준비", shutdownCts.Token);
                await monitor.RunCheckAsync(CheckTrigger.BeforeRecording, lease, shutdownCts.Token);
                favoriteStore.Save(favorites);

                if (!favorite.Metadata.TryGetValue("liveStatus", out var liveStatus) || liveStatus != "live"
                    || !favorite.Metadata.TryGetValue("streamUrl", out var streamUrl) || string.IsNullOrWhiteSpace(streamUrl))
                {
                    RefreshFavoriteList();
                    SetStatus("파일 분할 실패: 방송 URL 확인에 실패했습니다.");
                    return;
                }

                var httpContext = await pandaLiveService.GetRecordingHttpContextAsync(lease.WebView, shutdownCts.Token);
                var newSession = await recordingService.StartAsync(
                    favorite, streamUrl, settings.RecordingDirectory, settings.FfmpegPath, httpContext);

                await Task.Delay(1000, shutdownCts.Token);
                if (newSession.Process.HasExited)
                {
                    // Read everything off the session before disposing it - Dispose() releases
                    // the underlying Process, and reading ExitCode/anything else afterward
                    // throws "No process is associated with this object." instead of reporting
                    // the actual failure.
                    var exitCode = newSession.ExitCode;
                    var errorSummary = newSession.ErrorSummary;
                    newSession.Dispose();
                    var detail = string.IsNullOrWhiteSpace(errorSummary) ? "" : $" - {errorSummary}";
                    SetStatus("파일 분할 실패: 새 녹화가 바로 종료되었습니다.");
                    AddLog($"{favorite.DisplayName}: 파일 분할 실패 - 새 ffmpeg가 즉시 종료됨 (코드 {exitCode}){detail}");
                    return;
                }

                // Swap the dictionary to the new session before stopping the old one. The
                // ReferenceEquals guard in RecordingExited then sees the old process is no
                // longer "active" once it exits below, so it skips the normal exit handling
                // (no spurious "녹화 종료" log, no offline recheck) for what is really just a
                // planned handoff, not the model actually going offline.
                recordingSessions[favorite.Id] = newSession;
                newSession.Process.Exited += (_, _) => QueueRecordingExited(favorite, newSession);
                if (newSession.Process.HasExited)
                {
                    QueueRecordingExited(favorite, newSession);
                }

                favorite.Metadata["recordingPath"] = newSession.OutputPath;
                favorite.UpdatedAt = DateTimeOffset.Now;
                favoriteStore.Save(favorites);
                RefreshFavoriteList();

                oldSession.Stop();
                oldSession.Dispose();

                SetStatus("녹화 파일을 분할했습니다.");
                AddLog($"{favorite.DisplayName}: 파일 분할 - {oldSession.OutputPath} → {newSession.OutputPath}");
            }
            catch (Exception ex)
            {
                SetStatus($"파일 분할 실패: {ex.Message}");
                AddLog($"{favorite.DisplayName}: 파일 분할 실패 - {ex.Message}");
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

                if (monitorRoster.Find(favorite.Id) is not { } monitor)
                {
                    return;
                }

                using var lease = await webViewGate.AcquireAsync(GatePriority.Urgent, shutdownCts.Token);
                await liveStatusProbe.PrepareSessionAsync(lease, "녹화 종료 후 세션 준비", shutdownCts.Token);

                var outcome = await monitor.RunCheckAsync(CheckTrigger.AfterRecordingExit, lease, shutdownCts.Token);
                if (outcome.ShouldStartRecording)
                {
                    AddLog($"{favorite.DisplayName}: 방송중으로 확인되어 녹화 재시작");
                    await StartRecordingAsync(favorite, verifyLiveBeforeStart: false, lease);
                }

                monitor.Reschedule();
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
#if DEBUG
            if (designPreviewActive)
            {
                return;
            }
#endif

            var selectedRows = GetSelectedFavoriteRows().ToList();
            var selectedIds = selectedRows
                .Select(row => row.Tag)
                .OfType<FavoriteItem>()
                .Select(item => item.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var targets = selectedIds.Count > 0
                ? monitorRoster.Monitors.Where(monitor => selectedIds.Contains(monitor.Id)).ToList()
                : monitorRoster.Monitors.Where(monitor => monitor.Favorite.Enabled).ToList();

            if (targets.Count == 0)
            {
                SetStatus("확인할 모델이 없습니다.");
                return;
            }

            SetStatus($"방송 확인 중: {targets.Count}개");

            try
            {
                await monitorRoster.RunManualAsync(targets, "방송 확인", shutdownCts.Token);

                favoriteStore.Save(favorites);
                RefreshFavoriteList();
                SetStatus($"방송 확인 완료: {targets.Count}개");
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
            catch (Exception ex)
            {
                SetStatus($"방송 확인 실패: {ex.Message}");
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
            monitorRoster.Sync(favorites);
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

            if (status == "offline" || PandaMessages.IsOfflineBroadcast(message))
            {
                return "OFF LINE";
            }

            if (PandaMessages.IsSessionRelatedFailure(message))
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

        /// <summary>
        /// Tiered formatting for the summary card. Kept separate from <see cref="FormatFileSize"/>,
        /// which feeds the grid cell and is expected to stay in megabytes.
        /// </summary>
        private static string FormatFileSizeCompact(long bytes)
        {
            if (bytes <= 0)
            {
                return "-";
            }

            var gigabytes = bytes / 1024d / 1024d / 1024d;
            return gigabytes >= 1d
                ? $"{gigabytes:0.00} GB"
                : $"{bytes / 1024d / 1024d:0.0} MB";
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
            if (logView.IsDisposed || isShuttingDown)
            {
                return;
            }

            // RichTextBox is stricter about cross-thread access than the TextBox it replaced,
            // and ffmpeg's Process.Exited callbacks arrive on a pool thread.
            if (InvokeRequired)
            {
                BeginInvoke(() => AddLog(message));
                return;
            }

            logView.Append(message);
        }

        private void SetStatus(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => SetStatus(message));
                return;
            }

            headerBar.StatusText = message;
            AddLog(message);
        }

        private void clearLogButton_Click(object sender, EventArgs e)
        {
            logView.ClearLog();
        }

        private void toggleLogButton_Click(object sender, EventArgs e)
        {
            logCollapsed = !logCollapsed;
            ApplyLogVisibility();
        }

        /// <summary>
        /// Collapsing hides the log body and shrinks rootLayout's log row down to just the
        /// header's own height (30px) plus logPanel's own top/bottom margin (6+14) - 50px total.
        /// favoritePanel sits on the only Percent row left in rootLayout, so it reclaims the
        /// freed space automatically; nothing else needs to be touched.
        /// </summary>
        private void ApplyLogVisibility()
        {
            const float expandedHeight = 172F;
            const float collapsedHeight = 50F;

            logView.Visible = !logCollapsed;
            toggleLogButton.Text = logCollapsed ? "보이기" : "숨기기";

            rootLayout.RowStyles[3] = new RowStyle(SizeType.Absolute, logCollapsed ? collapsedHeight : expandedHeight);
            rootLayout.PerformLayout();
        }

        // ---------------------------------------------------------- IRecordingCoordinator
        //
        // The monitors drive recording through this narrow surface. Note that nothing here may
        // open a modal dialog: EnsureRecordingEnvironment can, so it stays on the Form1 side of
        // StartRecordingAsync rather than being reachable from a monitor loop.

        bool IRecordingCoordinator.IsRecording(string modelId)
        {
            return recordingSessions.ContainsKey(modelId);
        }

        async Task IRecordingCoordinator.StartAsync(FavoriteItem favorite, WebViewLease lease, CancellationToken cancellationToken)
        {
            await StartRecordingAsync(favorite, verifyLiveBeforeStart: false, lease);
        }

        void IRecordingCoordinator.StopForOfflineBroadcast(FavoriteItem favorite)
        {
            if (recordingSessions.Remove(favorite.Id, out var session))
            {
                StopRecording(favorite, session);
            }
        }

    }
}








