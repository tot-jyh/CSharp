namespace Hunbjter
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            if (disposing)
            {
                // Cancel outstanding CDP awaits before the WebView they target is torn down.
                shutdownCts.Cancel();
                shutdownCts.Dispose();
                loginBrowserForm.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            rootLayout = new BufferedTableLayoutPanel();
            headerBar = new HeaderBar();
            modelManagementButton = new ThemedButton();
            environmentSettingsButton = new ThemedButton();
            siteManagementButton = new ThemedButton();
            clipButton = new ThemedButton();
            statsPanel = new BufferedTableLayoutPanel();
            watchingCard = new StatCard();
            liveCard = new StatCard();
            recordingCard = new StatCard();
            sizeCard = new StatCard();
            favoritePanel = new BufferedTableLayoutPanel();
            favoritesGridView = new ThemedGrid();
            dummyColumn = new DataGridViewTextBoxColumn();
            numberColumn = new DataGridViewTextBoxColumn();
            platformColumn = new DataGridViewTextBoxColumn();
            nameColumn = new DataGridViewTextBoxColumn();
            enabledColumn = new DataGridViewTextBoxColumn();
            recordingColumn = new DataGridViewTextBoxColumn();
            resolutionColumn = new DataGridViewTextBoxColumn();
            lastSeenColumn = new DataGridViewTextBoxColumn();
            lastCheckColumn = new DataGridViewTextBoxColumn();
            watchColumn = new DataGridViewTextBoxColumn();
            fileSizeColumn = new DataGridViewTextBoxColumn();
            instantCaptureColumn = new DataGridViewTextBoxColumn();
            favoriteContextMenu = new ContextMenuStrip(components);
            checkLiveMenuItem = new ToolStripMenuItem();
            startRecordingMenuItem = new ToolStripMenuItem();
            stopRecordingMenuItem = new ToolStripMenuItem();
            highlightCaptureMenuItem = new ToolStripMenuItem();
            splitRecordingMenuItem = new ToolStripMenuItem();
            toggleWatchMenuItem = new ToolStripMenuItem();
            deleteFavoriteMenuItem = new ToolStripMenuItem();
            logPanel = new BufferedTableLayoutPanel();
            logHeaderPanel = new BufferedTableLayoutPanel();
            logTitleLabel = new Label();
            toggleLogButton = new ThemedButton();
            clearLogButton = new ThemedButton();
            logView = new LogView();
            rootLayout.SuspendLayout();
            statsPanel.SuspendLayout();
            favoritePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)favoritesGridView).BeginInit();
            favoriteContextMenu.SuspendLayout();
            logPanel.SuspendLayout();
            logHeaderPanel.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.BackColor = Color.FromArgb(15, 17, 21);
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(headerBar, 0, 0);
            rootLayout.Controls.Add(statsPanel, 0, 1);
            rootLayout.Controls.Add(favoritePanel, 0, 2);
            rootLayout.Controls.Add(logPanel, 0, 3);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Location = new Point(0, 0);
            rootLayout.Margin = new Padding(0);
            rootLayout.Name = "rootLayout";
            rootLayout.RowCount = 4;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 98F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 172F));
            rootLayout.Size = new Size(1280, 860);
            rootLayout.TabIndex = 0;
            // 
            // headerBar
            // 
            headerBar.BackColor = Color.FromArgb(23, 26, 32);
            headerBar.Dock = DockStyle.Fill;
            headerBar.Location = new Point(0, 0);
            headerBar.Margin = new Padding(0);
            headerBar.Name = "headerBar";
            headerBar.Size = new Size(1280, 58);
            headerBar.TabIndex = 0;
            // 
            // modelManagementButton
            // 
            modelManagementButton.BackColor = Color.Transparent;
            modelManagementButton.FlatStyle = FlatStyle.Flat;
            modelManagementButton.Font = new Font("맑은 고딕", 9F);
            modelManagementButton.Location = new Point(218, 12);
            modelManagementButton.Margin = new Padding(6, 0, 0, 0);
            modelManagementButton.Name = "modelManagementButton";
            modelManagementButton.Size = new Size(100, 32);
            modelManagementButton.TabIndex = 2;
            modelManagementButton.Text = "모델관리";
            modelManagementButton.UseVisualStyleBackColor = false;
            modelManagementButton.Click += modelManagementButton_Click;
            // 
            // environmentSettingsButton
            // 
            environmentSettingsButton.BackColor = Color.Transparent;
            environmentSettingsButton.FlatStyle = FlatStyle.Flat;
            environmentSettingsButton.Font = new Font("맑은 고딕", 9F);
            environmentSettingsButton.Location = new Point(112, 12);
            environmentSettingsButton.Margin = new Padding(6, 0, 0, 0);
            environmentSettingsButton.Name = "environmentSettingsButton";
            environmentSettingsButton.Size = new Size(100, 32);
            environmentSettingsButton.TabIndex = 1;
            environmentSettingsButton.Text = "환경설정";
            environmentSettingsButton.UseVisualStyleBackColor = false;
            environmentSettingsButton.Click += environmentSettingsButton_Click;
            // 
            // siteManagementButton
            // 
            siteManagementButton.BackColor = Color.Transparent;
            siteManagementButton.FlatStyle = FlatStyle.Flat;
            siteManagementButton.Font = new Font("맑은 고딕", 9F);
            siteManagementButton.Location = new Point(6, 12);
            siteManagementButton.Margin = new Padding(6, 0, 0, 0);
            siteManagementButton.Name = "siteManagementButton";
            siteManagementButton.Size = new Size(100, 32);
            siteManagementButton.TabIndex = 0;
            siteManagementButton.Text = "사이트관리";
            siteManagementButton.UseVisualStyleBackColor = false;
            siteManagementButton.Click += siteManagementButton_Click;
            //
            // clipButton
            //
            clipButton.BackColor = Color.Transparent;
            clipButton.FlatStyle = FlatStyle.Flat;
            clipButton.Font = new Font("맑은 고딕", 9F);
            clipButton.Location = new Point(6, 12);
            clipButton.Margin = new Padding(6, 0, 0, 0);
            clipButton.Name = "clipButton";
            clipButton.Size = new Size(100, 32);
            clipButton.TabIndex = 3;
            clipButton.Text = "Clip";
            clipButton.UseVisualStyleBackColor = false;
            clipButton.Click += clipButton_Click;
            //
            // statsPanel
            // 
            statsPanel.BackColor = Color.Transparent;
            statsPanel.ColumnCount = 4;
            statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            statsPanel.Controls.Add(watchingCard, 0, 0);
            statsPanel.Controls.Add(liveCard, 1, 0);
            statsPanel.Controls.Add(recordingCard, 2, 0);
            statsPanel.Controls.Add(sizeCard, 3, 0);
            statsPanel.Dock = DockStyle.Fill;
            statsPanel.Location = new Point(16, 70);
            statsPanel.Margin = new Padding(16, 12, 16, 6);
            statsPanel.Name = "statsPanel";
            statsPanel.RowCount = 1;
            statsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            statsPanel.Size = new Size(1248, 80);
            statsPanel.TabIndex = 1;
            // 
            // watchingCard
            // 
            watchingCard.BackColor = Color.Transparent;
            watchingCard.Dock = DockStyle.Fill;
            watchingCard.Location = new Point(0, 0);
            watchingCard.Margin = new Padding(0, 0, 10, 0);
            watchingCard.Name = "watchingCard";
            watchingCard.Size = new Size(302, 80);
            watchingCard.TabIndex = 0;
            // 
            // liveCard
            // 
            liveCard.BackColor = Color.Transparent;
            liveCard.Dock = DockStyle.Fill;
            liveCard.Location = new Point(312, 0);
            liveCard.Margin = new Padding(0, 0, 10, 0);
            liveCard.Name = "liveCard";
            liveCard.Size = new Size(302, 80);
            liveCard.TabIndex = 1;
            // 
            // recordingCard
            // 
            recordingCard.BackColor = Color.Transparent;
            recordingCard.Dock = DockStyle.Fill;
            recordingCard.Location = new Point(624, 0);
            recordingCard.Margin = new Padding(0, 0, 10, 0);
            recordingCard.Name = "recordingCard";
            recordingCard.Size = new Size(302, 80);
            recordingCard.TabIndex = 2;
            // 
            // sizeCard
            // 
            sizeCard.BackColor = Color.Transparent;
            sizeCard.Dock = DockStyle.Fill;
            sizeCard.Location = new Point(936, 0);
            sizeCard.Margin = new Padding(0);
            sizeCard.Name = "sizeCard";
            sizeCard.Size = new Size(312, 80);
            sizeCard.TabIndex = 3;
            // 
            // favoritePanel
            // 
            favoritePanel.BackColor = Color.Transparent;
            favoritePanel.ColumnCount = 1;
            favoritePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            favoritePanel.Controls.Add(favoritesGridView, 0, 0);
            favoritePanel.Dock = DockStyle.Fill;
            favoritePanel.Location = new Point(16, 162);
            favoritePanel.Margin = new Padding(16, 6, 16, 6);
            favoritePanel.Name = "favoritePanel";
            favoritePanel.RowCount = 1;
            favoritePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            favoritePanel.Size = new Size(1248, 520);
            favoritePanel.TabIndex = 2;
            // 
            // favoritesGridView
            // 
            favoritesGridView.AllowUserToAddRows = false;
            favoritesGridView.AllowUserToDeleteRows = false;
            favoritesGridView.AllowUserToResizeColumns = false;
            favoritesGridView.AllowUserToResizeRows = false;
            favoritesGridView.BackgroundColor = Color.FromArgb(15, 17, 21);
            favoritesGridView.BorderStyle = BorderStyle.None;
            favoritesGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            favoritesGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(15, 17, 21);
            dataGridViewCellStyle1.Font = new Font("맑은 고딕", 8.25F, FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = Color.FromArgb(150, 160, 176);
            dataGridViewCellStyle1.SelectionBackColor = Color.FromArgb(15, 17, 21);
            dataGridViewCellStyle1.SelectionForeColor = Color.FromArgb(150, 160, 176);
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.False;
            favoritesGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            favoritesGridView.ColumnHeadersHeight = 34;
            favoritesGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            favoritesGridView.Columns.AddRange(new DataGridViewColumn[] { dummyColumn, numberColumn, platformColumn, nameColumn, watchColumn, enabledColumn, recordingColumn, resolutionColumn, lastSeenColumn, lastCheckColumn, fileSizeColumn, instantCaptureColumn });
            favoritesGridView.ContextMenuStrip = favoriteContextMenu;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(23, 26, 32);
            dataGridViewCellStyle2.Font = new Font("맑은 고딕", 9F);
            dataGridViewCellStyle2.ForeColor = Color.FromArgb(231, 234, 240);
            dataGridViewCellStyle2.Padding = new Padding(4, 0, 4, 0);
            dataGridViewCellStyle2.SelectionBackColor = Color.FromArgb(31, 49, 79);
            dataGridViewCellStyle2.SelectionForeColor = Color.FromArgb(231, 234, 240);
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            favoritesGridView.DefaultCellStyle = dataGridViewCellStyle2;
            favoritesGridView.Dock = DockStyle.Fill;
            favoritesGridView.EditMode = DataGridViewEditMode.EditProgrammatically;
            favoritesGridView.EnableHeadersVisualStyles = false;
            favoritesGridView.Font = new Font("맑은 고딕", 9F);
            favoritesGridView.GridColor = Color.FromArgb(31, 36, 46);
            favoritesGridView.Location = new Point(0, 0);
            favoritesGridView.Margin = new Padding(0);
            favoritesGridView.MultiSelect = false;
            favoritesGridView.Name = "favoritesGridView";
            favoritesGridView.ReadOnly = true;
            favoritesGridView.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            favoritesGridView.RowHeadersVisible = false;
            favoritesGridView.ScrollBars = ScrollBars.Vertical;
            favoritesGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            favoritesGridView.Size = new Size(1248, 520);
            favoritesGridView.TabIndex = 1;
            // 
            // dummyColumn
            // 
            dummyColumn.HeaderText = "";
            dummyColumn.Name = "dummyColumn";
            dummyColumn.ReadOnly = true;
            dummyColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            dummyColumn.Visible = false;
            dummyColumn.Width = 5;
            // 
            // numberColumn
            // 
            numberColumn.FillWeight = 46F;
            numberColumn.HeaderText = "#";
            numberColumn.MinimumWidth = 40;
            numberColumn.Name = "numberColumn";
            numberColumn.ReadOnly = true;
            numberColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // platformColumn
            // 
            platformColumn.FillWeight = 80F;
            platformColumn.HeaderText = "사이트";
            platformColumn.MinimumWidth = 60;
            platformColumn.Name = "platformColumn";
            platformColumn.ReadOnly = true;
            platformColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // nameColumn
            // 
            nameColumn.FillWeight = 220F;
            nameColumn.HeaderText = "모델";
            nameColumn.MinimumWidth = 180;
            nameColumn.Name = "nameColumn";
            nameColumn.ReadOnly = true;
            nameColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // enabledColumn
            // 
            enabledColumn.FillWeight = 132F;
            enabledColumn.HeaderText = "상태";
            enabledColumn.MinimumWidth = 90;
            enabledColumn.Name = "enabledColumn";
            enabledColumn.ReadOnly = true;
            enabledColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // recordingColumn
            // 
            recordingColumn.FillWeight = 84F;
            recordingColumn.HeaderText = "녹화";
            recordingColumn.MinimumWidth = 60;
            recordingColumn.Name = "recordingColumn";
            recordingColumn.ReadOnly = true;
            recordingColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // resolutionColumn
            // 
            resolutionColumn.FillWeight = 100F;
            resolutionColumn.HeaderText = "해상도";
            resolutionColumn.MinimumWidth = 70;
            resolutionColumn.Name = "resolutionColumn";
            resolutionColumn.ReadOnly = true;
            resolutionColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // lastSeenColumn
            // 
            lastSeenColumn.FillWeight = 128F;
            lastSeenColumn.HeaderText = "마지막 방송";
            lastSeenColumn.MinimumWidth = 100;
            lastSeenColumn.Name = "lastSeenColumn";
            lastSeenColumn.ReadOnly = true;
            lastSeenColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // lastCheckColumn
            // 
            lastCheckColumn.FillWeight = 128F;
            lastCheckColumn.HeaderText = "마지막 확인";
            lastCheckColumn.MinimumWidth = 100;
            lastCheckColumn.Name = "lastCheckColumn";
            lastCheckColumn.ReadOnly = true;
            lastCheckColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // watchColumn
            // 
            watchColumn.FillWeight = 76F;
            watchColumn.HeaderText = "감시중";
            watchColumn.MinimumWidth = 56;
            watchColumn.Name = "watchColumn";
            watchColumn.ReadOnly = true;
            watchColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // fileSizeColumn
            // 
            fileSizeColumn.FillWeight = 96F;
            fileSizeColumn.HeaderText = "파일 크기";
            fileSizeColumn.MinimumWidth = 70;
            fileSizeColumn.Name = "fileSizeColumn";
            fileSizeColumn.ReadOnly = true;
            fileSizeColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // instantCaptureColumn
            // 
            instantCaptureColumn.FillWeight = 90F;
            instantCaptureColumn.HeaderText = "순간기록";
            instantCaptureColumn.MinimumWidth = 70;
            instantCaptureColumn.Name = "instantCaptureColumn";
            instantCaptureColumn.ReadOnly = true;
            instantCaptureColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            // 
            // favoriteContextMenu
            // 
            favoriteContextMenu.Items.AddRange(new ToolStripItem[] { checkLiveMenuItem, startRecordingMenuItem, stopRecordingMenuItem, highlightCaptureMenuItem, splitRecordingMenuItem, toggleWatchMenuItem, deleteFavoriteMenuItem });
            favoriteContextMenu.Name = "favoriteContextMenu";
            favoriteContextMenu.Size = new Size(129, 136);
            favoriteContextMenu.Opening += favoriteContextMenu_Opening;
            // 
            // checkLiveMenuItem
            // 
            checkLiveMenuItem.Name = "checkLiveMenuItem";
            checkLiveMenuItem.Size = new Size(128, 22);
            checkLiveMenuItem.Text = "방송 확인";
            checkLiveMenuItem.Click += checkLiveMenuItem_Click;
            // 
            // startRecordingMenuItem
            // 
            startRecordingMenuItem.Name = "startRecordingMenuItem";
            startRecordingMenuItem.Size = new Size(128, 22);
            startRecordingMenuItem.Text = "녹화 시작";
            startRecordingMenuItem.Click += startRecordingMenuItem_Click;
            // 
            // stopRecordingMenuItem
            // 
            stopRecordingMenuItem.Name = "stopRecordingMenuItem";
            stopRecordingMenuItem.Size = new Size(128, 22);
            stopRecordingMenuItem.Text = "녹화 종료";
            stopRecordingMenuItem.Click += stopRecordingMenuItem_Click;
            // 
            // highlightCaptureMenuItem
            // 
            highlightCaptureMenuItem.Name = "highlightCaptureMenuItem";
            highlightCaptureMenuItem.Size = new Size(128, 22);
            highlightCaptureMenuItem.Text = "순간캡쳐";
            highlightCaptureMenuItem.Click += highlightCaptureMenuItem_Click;
            //
            // splitRecordingMenuItem
            //
            splitRecordingMenuItem.Name = "splitRecordingMenuItem";
            splitRecordingMenuItem.Size = new Size(128, 22);
            splitRecordingMenuItem.Text = "파일 분할";
            splitRecordingMenuItem.Click += splitRecordingMenuItem_Click;
            //
            // toggleWatchMenuItem
            // 
            toggleWatchMenuItem.Name = "toggleWatchMenuItem";
            toggleWatchMenuItem.Size = new Size(128, 22);
            toggleWatchMenuItem.Text = "Watch On";
            toggleWatchMenuItem.Click += toggleWatchMenuItem_Click;
            // 
            // deleteFavoriteMenuItem
            // 
            deleteFavoriteMenuItem.Name = "deleteFavoriteMenuItem";
            deleteFavoriteMenuItem.Size = new Size(128, 22);
            deleteFavoriteMenuItem.Text = "삭제";
            deleteFavoriteMenuItem.Click += deleteFavoriteMenuItem_Click;
            // 
            // logPanel
            // 
            logPanel.BackColor = Color.Transparent;
            logPanel.ColumnCount = 1;
            logPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            logPanel.Controls.Add(logHeaderPanel, 0, 0);
            logPanel.Controls.Add(logView, 0, 1);
            logPanel.Dock = DockStyle.Fill;
            logPanel.Location = new Point(16, 694);
            logPanel.Margin = new Padding(16, 6, 16, 14);
            logPanel.Name = "logPanel";
            logPanel.RowCount = 2;
            logPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            logPanel.Size = new Size(1248, 152);
            logPanel.TabIndex = 3;
            // 
            // logHeaderPanel
            // 
            logHeaderPanel.BackColor = Color.Transparent;
            logHeaderPanel.ColumnCount = 3;
            logHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            logHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84F));
            logHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
            logHeaderPanel.Controls.Add(logTitleLabel, 0, 0);
            logHeaderPanel.Controls.Add(toggleLogButton, 1, 0);
            logHeaderPanel.Controls.Add(clearLogButton, 2, 0);
            logHeaderPanel.Dock = DockStyle.Fill;
            logHeaderPanel.Location = new Point(0, 0);
            logHeaderPanel.Margin = new Padding(0);
            logHeaderPanel.Name = "logHeaderPanel";
            logHeaderPanel.RowCount = 1;
            logHeaderPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            logHeaderPanel.Size = new Size(1248, 30);
            logHeaderPanel.TabIndex = 0;
            // 
            // logTitleLabel
            // 
            logTitleLabel.BackColor = Color.Transparent;
            logTitleLabel.Dock = DockStyle.Fill;
            logTitleLabel.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            logTitleLabel.ForeColor = Color.FromArgb(150, 160, 176);
            logTitleLabel.Location = new Point(0, 0);
            logTitleLabel.Margin = new Padding(0);
            logTitleLabel.Name = "logTitleLabel";
            logTitleLabel.Size = new Size(1170, 30);
            logTitleLabel.TabIndex = 0;
            logTitleLabel.Text = "로그";
            logTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // toggleLogButton
            // 
            toggleLogButton.BackColor = Color.Transparent;
            toggleLogButton.FlatStyle = FlatStyle.Flat;
            toggleLogButton.Font = new Font("맑은 고딕", 9F);
            toggleLogButton.Margin = new Padding(6, 0, 0, 0);
            toggleLogButton.Name = "toggleLogButton";
            toggleLogButton.Size = new Size(78, 24);
            toggleLogButton.TabIndex = 3;
            toggleLogButton.Text = "보이기";
            toggleLogButton.UseVisualStyleBackColor = false;
            toggleLogButton.Click += toggleLogButton_Click;
            // 
            // clearLogButton
            // 
            clearLogButton.BackColor = Color.Transparent;
            clearLogButton.FlatStyle = FlatStyle.Flat;
            clearLogButton.Font = new Font("맑은 고딕", 9F);
            clearLogButton.Location = new Point(1176, 0);
            clearLogButton.Margin = new Padding(6, 0, 0, 0);
            clearLogButton.Name = "clearLogButton";
            clearLogButton.Size = new Size(72, 24);
            clearLogButton.TabIndex = 4;
            clearLogButton.Text = "지우기";
            clearLogButton.UseVisualStyleBackColor = false;
            clearLogButton.Click += clearLogButton_Click;
            // 
            // logView
            // 
            logView.BackColor = Color.FromArgb(23, 26, 32);
            logView.BorderStyle = BorderStyle.None;
            logView.DetectUrls = false;
            logView.Dock = DockStyle.Fill;
            logView.Font = new Font("Cascadia Mono", 8.5F);
            logView.ForeColor = Color.FromArgb(150, 160, 176);
            logView.Location = new Point(0, 30);
            logView.Margin = new Padding(0);
            logView.Name = "logView";
            logView.ReadOnly = true;
            logView.ScrollBars = RichTextBoxScrollBars.Vertical;
            logView.Size = new Size(1248, 122);
            logView.TabIndex = 5;
            logView.Text = "";
            logView.WordWrap = false;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(15, 17, 21);
            ClientSize = new Size(1280, 860);
            Controls.Add(rootLayout);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(1120, 740);
            Name = "Form1";
            Text = "Hunbjter Recorder";
            rootLayout.ResumeLayout(false);
            statsPanel.ResumeLayout(false);
            favoritePanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)favoritesGridView).EndInit();
            favoriteContextMenu.ResumeLayout(false);
            logPanel.ResumeLayout(false);
            logHeaderPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private BufferedTableLayoutPanel rootLayout;
        private HeaderBar headerBar;
        private ThemedButton siteManagementButton;
        private ThemedButton clipButton;
        private ThemedButton environmentSettingsButton;
        private ThemedButton modelManagementButton;
        private BufferedTableLayoutPanel statsPanel;
        private StatCard watchingCard;
        private StatCard liveCard;
        private StatCard recordingCard;
        private StatCard sizeCard;
        private BufferedTableLayoutPanel favoritePanel;
        private ThemedGrid favoritesGridView;
        private DataGridViewTextBoxColumn dummyColumn;
        private DataGridViewTextBoxColumn numberColumn;
        private DataGridViewTextBoxColumn platformColumn;
        private DataGridViewTextBoxColumn nameColumn;
        private DataGridViewTextBoxColumn enabledColumn;
        private DataGridViewTextBoxColumn recordingColumn;
        private DataGridViewTextBoxColumn resolutionColumn;
        private DataGridViewTextBoxColumn lastSeenColumn;
        private DataGridViewTextBoxColumn lastCheckColumn;
        private DataGridViewTextBoxColumn watchColumn;
        private DataGridViewTextBoxColumn fileSizeColumn;
        private DataGridViewTextBoxColumn instantCaptureColumn;
        private ContextMenuStrip favoriteContextMenu;
        private ToolStripMenuItem checkLiveMenuItem;
        private ToolStripMenuItem startRecordingMenuItem;
        private ToolStripMenuItem stopRecordingMenuItem;
        private ToolStripMenuItem highlightCaptureMenuItem;
        private ToolStripMenuItem splitRecordingMenuItem;
        private ToolStripMenuItem toggleWatchMenuItem;
        private ToolStripMenuItem deleteFavoriteMenuItem;
        private BufferedTableLayoutPanel logPanel;
        private BufferedTableLayoutPanel logHeaderPanel;
        private Label logTitleLabel;
        private ThemedButton toggleLogButton;
        private ThemedButton clearLogButton;
        private LogView logView;
    }
}
