namespace Hunbjter;

public sealed class EnvironmentSettingsForm : Form
{
    private readonly LoginSettings settings;
    private readonly TextBox ffmpegPathTextBox = new();
    private readonly TextBox recordingDirectoryTextBox = new();
    private readonly NumericUpDown modelCheckIntervalSecondsInput = new();
    private readonly NumericUpDown recordingStopAfterOfflineChecksInput = new();
    private readonly NumericUpDown highlightCaptureSecondsInput = new();

    public EnvironmentSettingsForm(LoginSettings settings)
    {
        this.settings = settings;

        InitializeComponent();
        ffmpegPathTextBox.Text = settings.FfmpegPath;
        recordingDirectoryTextBox.Text = settings.RecordingDirectory;
        modelCheckIntervalSecondsInput.Value = Math.Clamp(settings.ModelCheckIntervalSeconds > 0 ? settings.ModelCheckIntervalSeconds : 300, 10, 86400);
        recordingStopAfterOfflineChecksInput.Value = Math.Clamp(settings.RecordingStopAfterOfflineChecks > 0 ? settings.RecordingStopAfterOfflineChecks : 2, 1, 10);
        highlightCaptureSecondsInput.Value = Math.Clamp(settings.HighlightCaptureSeconds > 0 ? settings.HighlightCaptureSeconds : 60, 5, 3600);
    }

    public string FfmpegPath => ffmpegPathTextBox.Text.Trim();

    public string RecordingDirectory => recordingDirectoryTextBox.Text.Trim();

    public int ModelCheckIntervalSeconds => (int)modelCheckIntervalSecondsInput.Value;

    public int RecordingStopAfterOfflineChecks => (int)recordingStopAfterOfflineChecksInput.Value;

    public int HighlightCaptureSeconds => (int)highlightCaptureSecondsInput.Value;

    private void InitializeComponent()
    {
        Text = "환경설정";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(660, 314);
        Padding = new Padding(16);

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 7
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

        var ffmpegBrowseButton = CreateBrowseButton("찾기");
        var recordingDirectoryBrowseButton = CreateBrowseButton("찾기");
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var saveButton = new Button
        {
            Text = "저장",
            Size = new Size(82, 28),
            DialogResult = DialogResult.OK
        };
        var cancelButton = new Button
        {
            Text = "취소",
            Size = new Size(82, 28),
            DialogResult = DialogResult.Cancel
        };

        ffmpegPathTextBox.Dock = DockStyle.Fill;
        recordingDirectoryTextBox.Dock = DockStyle.Fill;
        ConfigureNumberInput(modelCheckIntervalSecondsInput, 10, 86400, 10);
        ConfigureNumberInput(recordingStopAfterOfflineChecksInput, 1, 10, 1);
        ConfigureNumberInput(highlightCaptureSecondsInput, 5, 3600, 5);

        ffmpegBrowseButton.Click += (_, _) => BrowseFfmpegPath();
        recordingDirectoryBrowseButton.Click += (_, _) => BrowseRecordingDirectory();
        saveButton.Click += (_, _) => SaveSettings();

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(saveButton);

        rootLayout.Controls.Add(CreateLabel("ffmpeg 경로"), 0, 0);
        rootLayout.Controls.Add(ffmpegPathTextBox, 1, 0);
        rootLayout.Controls.Add(ffmpegBrowseButton, 2, 0);
        rootLayout.Controls.Add(CreateLabel("녹화 저장위치"), 0, 1);
        rootLayout.Controls.Add(recordingDirectoryTextBox, 1, 1);
        rootLayout.Controls.Add(recordingDirectoryBrowseButton, 2, 1);
        rootLayout.Controls.Add(CreateLabel("접속 확인 간격"), 0, 2);
        rootLayout.Controls.Add(modelCheckIntervalSecondsInput, 1, 2);
        rootLayout.Controls.Add(CreateLabel("초"), 2, 2);
        rootLayout.Controls.Add(CreateLabel("방송 종료 판단 횟수"), 0, 3);
        rootLayout.Controls.Add(recordingStopAfterOfflineChecksInput, 1, 3);
        rootLayout.Controls.Add(CreateLabel("회"), 2, 3);
        rootLayout.Controls.Add(CreateLabel("하이라이트 캡쳐"), 0, 4);
        rootLayout.Controls.Add(highlightCaptureSecondsInput, 1, 4);
        rootLayout.Controls.Add(CreateLabel("초"), 2, 4);
        rootLayout.Controls.Add(buttonPanel, 0, 6);
        rootLayout.SetColumnSpan(buttonPanel, 3);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.Add(rootLayout);
    }

    private static void ConfigureNumberInput(NumericUpDown input, int minimum, int maximum, int increment)
    {
        input.Dock = DockStyle.Left;
        input.Minimum = minimum;
        input.Maximum = maximum;
        input.Increment = increment;
        input.Width = 120;
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Button CreateBrowseButton(string text)
    {
        return new Button
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 28,
            Margin = new Padding(8, 1, 0, 0)
        };
    }

    private void BrowseFfmpegPath()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "ffmpeg.exe 파일을 선택하세요.",
            Filter = "ffmpeg.exe|ffmpeg.exe|실행 파일 (*.exe)|*.exe",
            FileName = "ffmpeg.exe",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ffmpegPathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseRecordingDirectory()
    {
        var defaultDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "Hunbjter");

        using var dialog = new FolderBrowserDialog
        {
            Description = "녹화 파일을 저장할 폴더를 선택하세요.",
            SelectedPath = Directory.Exists(recordingDirectoryTextBox.Text)
                ? recordingDirectoryTextBox.Text
                : defaultDirectory,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            recordingDirectoryTextBox.Text = dialog.SelectedPath;
        }
    }

    private void SaveSettings()
    {
        settings.FfmpegPath = FfmpegPath;
        settings.RecordingDirectory = RecordingDirectory;
        settings.ModelCheckIntervalSeconds = ModelCheckIntervalSeconds;
        settings.RecordingStopAfterOfflineChecks = RecordingStopAfterOfflineChecks;
        settings.HighlightCaptureSeconds = HighlightCaptureSeconds;
    }
}
