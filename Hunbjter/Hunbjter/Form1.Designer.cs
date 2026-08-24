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
                loginBrowserForm.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            rootLayout = new TableLayoutPanel();
            ribbonPanel = new FlowLayoutPanel();
            siteManagementButton = new Button();
            environmentSettingsButton = new Button();
            modelManagementButton = new Button();
            favoritePanel = new TableLayoutPanel();
            favoritesGridView = new DataGridView();
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
            instantCaptureColumn = new DataGridViewButtonColumn();
            favoriteContextMenu = new ContextMenuStrip(components);
            checkLiveMenuItem = new ToolStripMenuItem();
            startRecordingMenuItem = new ToolStripMenuItem();
            stopRecordingMenuItem = new ToolStripMenuItem();
            highlightCaptureMenuItem = new ToolStripMenuItem();
            toggleWatchMenuItem = new ToolStripMenuItem();
            deleteFavoriteMenuItem = new ToolStripMenuItem();
            logTextBox = new TextBox();
            rootLayout.SuspendLayout();
            ribbonPanel.SuspendLayout();
            favoritePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)favoritesGridView).BeginInit();
            favoriteContextMenu.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 1;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(ribbonPanel, 0, 0);
            rootLayout.Controls.Add(favoritePanel, 0, 1);
            rootLayout.Controls.Add(logTextBox, 0, 2);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Location = new Point(0, 0);
            rootLayout.Name = "rootLayout";
            rootLayout.RowCount = 3;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
            rootLayout.Size = new Size(1220, 780);
            rootLayout.TabIndex = 0;
            // 
            // ribbonPanel
            // 
            ribbonPanel.BackColor = SystemColors.ControlLight;
            ribbonPanel.Controls.Add(siteManagementButton);
            ribbonPanel.Controls.Add(environmentSettingsButton);
            ribbonPanel.Controls.Add(modelManagementButton);
            ribbonPanel.Dock = DockStyle.Fill;
            ribbonPanel.Location = new Point(0, 0);
            ribbonPanel.Margin = new Padding(0);
            ribbonPanel.Name = "ribbonPanel";
            ribbonPanel.Padding = new Padding(12, 10, 12, 8);
            ribbonPanel.Size = new Size(1220, 54);
            ribbonPanel.TabIndex = 0;
            // 
            // siteManagementButton
            // 
            siteManagementButton.Location = new Point(15, 13);
            siteManagementButton.Name = "siteManagementButton";
            siteManagementButton.Size = new Size(110, 30);
            siteManagementButton.TabIndex = 0;
            siteManagementButton.Text = "\uC0AC\uC774\uD2B8\uAD00\uB9AC";
            siteManagementButton.UseVisualStyleBackColor = true;
            siteManagementButton.Click += siteManagementButton_Click;
            // 
            // environmentSettingsButton
            // 
            environmentSettingsButton.Location = new Point(131, 13);
            environmentSettingsButton.Name = "environmentSettingsButton";
            environmentSettingsButton.Size = new Size(110, 30);
            environmentSettingsButton.TabIndex = 1;
            environmentSettingsButton.Text = "\uD658\uACBD\uC124\uC815";
            environmentSettingsButton.UseVisualStyleBackColor = true;
            environmentSettingsButton.Click += environmentSettingsButton_Click;
            // 
            // modelManagementButton
            // 
            modelManagementButton.Location = new Point(247, 13);
            modelManagementButton.Name = "modelManagementButton";
            modelManagementButton.Size = new Size(110, 30);
            modelManagementButton.TabIndex = 2;
            modelManagementButton.Text = "\uBAA8\uB378\uAD00\uB9AC";
            modelManagementButton.UseVisualStyleBackColor = true;
            modelManagementButton.Click += modelManagementButton_Click;
            // 
            // favoritePanel
            // 
            favoritePanel.ColumnCount = 1;
            favoritePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            favoritePanel.Controls.Add(favoritesGridView, 0, 0);
            favoritePanel.Dock = DockStyle.Fill;
            favoritePanel.Location = new Point(12, 57);
            favoritePanel.Margin = new Padding(12, 3, 12, 6);
            favoritePanel.Name = "favoritePanel";
            favoritePanel.RowCount = 1;
            favoritePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            favoritePanel.Size = new Size(1196, 599);
            favoritePanel.TabIndex = 2;
            // 
            // favoritesGridView
            // 
            favoritesGridView.AllowUserToAddRows = false;
            favoritesGridView.AllowUserToDeleteRows = false;
            favoritesGridView.AllowUserToResizeRows = false;
            favoritesGridView.BackgroundColor = SystemColors.Window;
            favoritesGridView.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            favoritesGridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            favoritesGridView.BorderStyle = BorderStyle.Fixed3D;
            favoritesGridView.ColumnHeadersHeight = 28;
            favoritesGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            favoritesGridView.Columns.AddRange(new DataGridViewColumn[] { dummyColumn, numberColumn, platformColumn, nameColumn, enabledColumn, recordingColumn, resolutionColumn, lastSeenColumn, lastCheckColumn, watchColumn, fileSizeColumn, instantCaptureColumn });
            favoritesGridView.ContextMenuStrip = favoriteContextMenu;
            favoritesGridView.Dock = DockStyle.Fill;
            favoritesGridView.EditMode = DataGridViewEditMode.EditProgrammatically;
            favoritesGridView.Location = new Point(0, 0);
            favoritesGridView.Margin = new Padding(0);
            favoritesGridView.MultiSelect = false;
            favoritesGridView.Name = "favoritesGridView";
            favoritesGridView.ReadOnly = true;
            favoritesGridView.RowHeadersVisible = false;
            favoritesGridView.RowTemplate.Height = 48;
            favoritesGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            favoritesGridView.Size = new Size(1196, 599);
            favoritesGridView.TabIndex = 1;
            // 
            // dummyColumn
            // 
            dummyColumn.HeaderText = "";
            dummyColumn.Name = "dummyColumn";
            dummyColumn.ReadOnly = true;
            dummyColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            dummyColumn.Visible = false;
            dummyColumn.Width = 0;
            // 
            // numberColumn
            // 
            numberColumn.HeaderText = "\uC21C\uBC88";
            numberColumn.Name = "numberColumn";
            numberColumn.ReadOnly = true;
            numberColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            numberColumn.Width = 54;
            // 
            // platformColumn
            // 
            platformColumn.HeaderText = "\uC0AC\uC774\uD2B8";
            platformColumn.Name = "platformColumn";
            platformColumn.ReadOnly = true;
            platformColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            platformColumn.Width = 96;
            // 
            // nameColumn
            // 
            nameColumn.HeaderText = "\uB2C9\uB124\uC784(\uC544\uC774\uB514)";
            nameColumn.Name = "nameColumn";
            nameColumn.ReadOnly = true;
            nameColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            nameColumn.Width = 162;
            // 
            // enabledColumn
            // 
            enabledColumn.HeaderText = "\uC0C1\uD0DC";
            enabledColumn.Name = "enabledColumn";
            enabledColumn.ReadOnly = true;
            enabledColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            enabledColumn.Width = 126;
            // 
            // recordingColumn
            // 
            recordingColumn.HeaderText = "Record";
            recordingColumn.Name = "recordingColumn";
            recordingColumn.ReadOnly = true;
            recordingColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            recordingColumn.Width = 90;
            // 
            // resolutionColumn
            // 
            resolutionColumn.HeaderText = "\uD574\uC0C1\uB3C4";
            resolutionColumn.Name = "resolutionColumn";
            resolutionColumn.ReadOnly = true;
            resolutionColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            resolutionColumn.Width = 100;
            // 
            // lastSeenColumn
            // 
            lastSeenColumn.HeaderText = "Last seen";
            lastSeenColumn.Name = "lastSeenColumn";
            lastSeenColumn.ReadOnly = true;
            lastSeenColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            lastSeenColumn.Width = 150;
            // 
            // lastCheckColumn
            // 
            lastCheckColumn.HeaderText = "Last check";
            lastCheckColumn.Name = "lastCheckColumn";
            lastCheckColumn.ReadOnly = true;
            lastCheckColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            lastCheckColumn.Width = 150;
            // 
            // watchColumn
            // 
            watchColumn.HeaderText = "Watch";
            watchColumn.Name = "watchColumn";
            watchColumn.ReadOnly = true;
            watchColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            watchColumn.Width = 80;
            // 
            // fileSizeColumn
            // 
            fileSizeColumn.HeaderText = "\uD30C\uC77C \uD06C\uAE30";
            fileSizeColumn.Name = "fileSizeColumn";
            fileSizeColumn.ReadOnly = true;
            fileSizeColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            fileSizeColumn.Width = 100;
            // 
            // instantCaptureColumn
            // 
            instantCaptureColumn.HeaderText = "\uC21C\uAC04\uAE30\uB85D";
            instantCaptureColumn.Name = "instantCaptureColumn";
            instantCaptureColumn.ReadOnly = true;
            instantCaptureColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
            instantCaptureColumn.Text = "R";
            instantCaptureColumn.UseColumnTextForButtonValue = false;
            instantCaptureColumn.Width = 80;            // favoriteContextMenu
            // 
            favoriteContextMenu.Items.AddRange(new ToolStripItem[] { checkLiveMenuItem, startRecordingMenuItem, stopRecordingMenuItem, highlightCaptureMenuItem, toggleWatchMenuItem, deleteFavoriteMenuItem });
            favoriteContextMenu.Name = "favoriteContextMenu";
            favoriteContextMenu.Size = new Size(163, 154);
            favoriteContextMenu.Opening += favoriteContextMenu_Opening;
            // 
            // checkLiveMenuItem
            // 
            checkLiveMenuItem.Name = "checkLiveMenuItem";
            checkLiveMenuItem.Size = new Size(162, 22);
            checkLiveMenuItem.Text = "\uBC29\uC1A1 \uD655\uC778";
            checkLiveMenuItem.Click += checkLiveMenuItem_Click;
            // 
            // startRecordingMenuItem
            // 
            startRecordingMenuItem.Name = "startRecordingMenuItem";
            startRecordingMenuItem.Size = new Size(162, 22);
            startRecordingMenuItem.Text = "\uB179\uD654 \uC2DC\uC791";
            startRecordingMenuItem.Click += startRecordingMenuItem_Click;
            // 
            // stopRecordingMenuItem
            // 
            stopRecordingMenuItem.Name = "stopRecordingMenuItem";
            stopRecordingMenuItem.Size = new Size(162, 22);
            stopRecordingMenuItem.Text = "\uB179\uD654 \uC885\uB8CC";
            stopRecordingMenuItem.Click += stopRecordingMenuItem_Click;
            // 
            // highlightCaptureMenuItem
            // 
            highlightCaptureMenuItem.Name = "highlightCaptureMenuItem";
            highlightCaptureMenuItem.Size = new Size(162, 22);
            highlightCaptureMenuItem.Text = "\uC21C\uAC04\uCEA1\uCCD0";
            highlightCaptureMenuItem.Click += highlightCaptureMenuItem_Click;
            // 
            // toggleWatchMenuItem
            // 
            toggleWatchMenuItem.Name = "toggleWatchMenuItem";
            toggleWatchMenuItem.Size = new Size(162, 22);
            toggleWatchMenuItem.Text = "Watch On";
            toggleWatchMenuItem.Click += toggleWatchMenuItem_Click;
            // 
            // deleteFavoriteMenuItem
            // 
            deleteFavoriteMenuItem.Name = "deleteFavoriteMenuItem";
            deleteFavoriteMenuItem.Size = new Size(162, 22);
            deleteFavoriteMenuItem.Text = "\uC0AD\uC81C";
            deleteFavoriteMenuItem.Click += deleteFavoriteMenuItem_Click;
            // 
            // logTextBox
            // 
            logTextBox.BackColor = Color.White;
            logTextBox.Dock = DockStyle.Fill;
            logTextBox.Location = new Point(12, 665);
            logTextBox.Margin = new Padding(12, 3, 12, 10);
            logTextBox.Multiline = true;
            logTextBox.Name = "logTextBox";
            logTextBox.ReadOnly = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.Size = new Size(1196, 105);
            logTextBox.TabIndex = 3;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1220, 780);
            Controls.Add(rootLayout);
            MinimumSize = new Size(1024, 680);
            Name = "Form1";
            Text = "Hunbjter Recorder";
            rootLayout.ResumeLayout(false);
            rootLayout.PerformLayout();
            ribbonPanel.ResumeLayout(false);
            favoritePanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)favoritesGridView).EndInit();
            favoriteContextMenu.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel rootLayout;
        private FlowLayoutPanel ribbonPanel;
        private Button siteManagementButton;
        private Button environmentSettingsButton;
        private Button modelManagementButton;
        private TableLayoutPanel favoritePanel;
        private DataGridView favoritesGridView;
        private DataGridViewTextBoxColumn dummyColumn;
        private DataGridViewTextBoxColumn numberColumn;
        private DataGridViewTextBoxColumn enabledColumn;
        private DataGridViewTextBoxColumn recordingColumn;
        private DataGridViewTextBoxColumn watchColumn;
        private DataGridViewTextBoxColumn fileSizeColumn;
        private DataGridViewButtonColumn instantCaptureColumn;
        private DataGridViewTextBoxColumn resolutionColumn;
        private DataGridViewTextBoxColumn nameColumn;
        private DataGridViewTextBoxColumn platformColumn;
        private DataGridViewTextBoxColumn lastSeenColumn;
        private DataGridViewTextBoxColumn lastCheckColumn;        private TextBox logTextBox;
        private ContextMenuStrip favoriteContextMenu;
        private ToolStripMenuItem checkLiveMenuItem;
        private ToolStripMenuItem startRecordingMenuItem;
        private ToolStripMenuItem stopRecordingMenuItem;
        private ToolStripMenuItem highlightCaptureMenuItem;
        private ToolStripMenuItem toggleWatchMenuItem;
        private ToolStripMenuItem deleteFavoriteMenuItem;
    }
}


