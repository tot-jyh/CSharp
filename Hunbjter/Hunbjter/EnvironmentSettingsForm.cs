namespace Hunbjter;

public sealed class EnvironmentSettingsForm : ThemedDialog
{
    private const int FieldRowHeight = 54;

    private readonly LoginSettings settings;
    private readonly ThemedTextBox ffmpegPathTextBox = new();
    private readonly ThemedTextBox recordingDirectoryTextBox = new();
    private readonly ThemedNumeric modelCheckIntervalSecondsInput = CreateNumeric(10, 86400, 10);
    private readonly ThemedNumeric recordingStopAfterOfflineChecksInput = CreateNumeric(1, 10, 1);
    private readonly ThemedNumeric highlightCaptureSecondsInput = CreateNumeric(5, 3600, 5);

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
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(680, 380);
        Padding = new Padding(22, 18, 22, 18);

        var rootLayout = new BufferedTableLayoutPanel
        {
            BackColor = Theme.Background,
            ColumnCount = 3,
            Dock = DockStyle.Fill,
            RowCount = 7
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        for (var i = 0; i < 5; i++)
        {
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, FieldRowHeight));
        }

        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

        var ffmpegBrowseButton = CreateBrowseButton();
        var recordingDirectoryBrowseButton = CreateBrowseButton();
        ffmpegBrowseButton.Click += (_, _) => BrowseFfmpegPath();
        recordingDirectoryBrowseButton.Click += (_, _) => BrowseRecordingDirectory();

        var saveButton = new ThemedButton
        {
            DialogResult = DialogResult.OK,
            Size = new Size(88, 32),
            Text = "저장",
            Variant = ButtonVariant.Primary
        };
        var cancelButton = new ThemedButton
        {
            DialogResult = DialogResult.Cancel,
            Size = new Size(88, 32),
            Text = "취소",
            Variant = ButtonVariant.Ghost
        };
        saveButton.Click += (_, _) => SaveSettings();

        var buttonPanel = new FlowLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 6, 0, 0),
            WrapContents = false
        };
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);

        rootLayout.Controls.Add(CreateLabel("ffmpeg 경로"), 0, 0);
        rootLayout.Controls.Add(Field(ffmpegPathTextBox), 1, 0);
        rootLayout.Controls.Add(ffmpegBrowseButton, 2, 0);

        rootLayout.Controls.Add(CreateLabel("녹화 저장위치"), 0, 1);
        rootLayout.Controls.Add(Field(recordingDirectoryTextBox), 1, 1);
        rootLayout.Controls.Add(recordingDirectoryBrowseButton, 2, 1);

        rootLayout.Controls.Add(CreateLabel("접속 확인 간격"), 0, 2);
        rootLayout.Controls.Add(Field(modelCheckIntervalSecondsInput, 160), 1, 2);
        rootLayout.Controls.Add(CreateLabel("초", color: Theme.TextMuted), 2, 2);

        rootLayout.Controls.Add(CreateLabel("방송 종료 판단 횟수"), 0, 3);
        rootLayout.Controls.Add(Field(recordingStopAfterOfflineChecksInput, 160), 1, 3);
        rootLayout.Controls.Add(CreateLabel("회", color: Theme.TextMuted), 2, 3);

        rootLayout.Controls.Add(CreateLabel("하이라이트 캡쳐"), 0, 4);
        rootLayout.Controls.Add(Field(highlightCaptureSecondsInput, 160), 1, 4);
        rootLayout.Controls.Add(CreateLabel("초", color: Theme.TextMuted), 2, 4);

        rootLayout.Controls.Add(buttonPanel, 0, 6);
        rootLayout.SetColumnSpan(buttonPanel, 3);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.Add(rootLayout);
    }

    /// <summary>Wraps an input in its bordered shell and vertically centers it in the row.</summary>
    private static InputHost Field(Control inner, int fixedWidth = 0)
    {
        var host = new InputHost(inner)
        {
            Anchor = fixedWidth > 0 ? AnchorStyles.Left : AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 11, 0, 11)
        };

        if (fixedWidth > 0)
        {
            host.Width = fixedWidth;
        }

        return host;
    }

    private static ThemedButton CreateBrowseButton()
    {
        return new ThemedButton
        {
            Anchor = AnchorStyles.Left,
            Margin = new Padding(10, 11, 0, 11),
            Size = new Size(88, 32),
            Text = "찾기",
            Variant = ButtonVariant.Secondary
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
