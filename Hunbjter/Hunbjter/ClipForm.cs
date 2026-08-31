namespace Hunbjter;

/// <summary>
/// "구간합침" (segment-combine) tool, ported from the standalone ClipM project
/// (D:\Task\Git\ohho0505\CSharp\ClipM\frmSegmentCombine.cs). Drag/drop or browse to a video,
/// mark [start,end] ranges, queue several of them (optionally across different source files),
/// then extract+concatenate them into one output file via ffmpeg.
///
/// Ported deliberately off Xabe.FFmpeg: reuses the same raw-Process ffmpeg pattern as
/// <see cref="RecordingService.CreateHighlightAsync"/> and the user's own configured
/// <c>settings.FfmpegPath</c>, and every message that ClipM showed via MessageBox is routed
/// through <see cref="log"/> instead, matching the rest of Hunbjter (which has no MessageBox
/// anywhere - everything goes to the log panel).
/// </summary>
public sealed class ClipForm : ThemedDialog
{
    private readonly Func<string> getFfmpegPath;
    private readonly Action<string> log;
    private readonly ClipService clipService = new();

    private string currentFilePath = "";
    private bool closingForDispose;
    private int dragSourceRowIndex = -1;
    private Point dragStartPoint;

    private Label dropLabel = null!;
    private ThemedGrid grid = null!;
    private ThemedMaskedTextBox startTextBox = null!;
    private ThemedMaskedTextBox endTextBox = null!;
    private ThemedButton addButton = null!;
    private ThemedButton combineButton = null!;

    private sealed record SegmentRow(string FilePath, TimeSpan Start, TimeSpan End);

    /// <summary>
    /// <paramref name="getFfmpegPath"/> is a delegate rather than a captured <see cref="LoginSettings"/>
    /// instance because Form1 periodically *reassigns* its own settings field wholesale
    /// (<c>settings = settingsStore.Load()</c> after 환경설정/사이트관리 close) rather than mutating
    /// it in place - capturing the object itself at construction time would go stale the first
    /// time that happens, since this form is a long-lived singleton created once.
    /// </summary>
    public ClipForm(Func<string> getFfmpegPath, Action<string> log)
    {
        this.getFfmpegPath = getFfmpegPath;
        this.log = log;

        InitializeComponent();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Same singleton-hide pattern as LoginBrowserForm: the header button reopens the same
        // window (with whatever segments are still queued) rather than rebuilding it each time.
        if (!closingForDispose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        closingForDispose = true;
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        Text = "Clip";
        ClientSize = new Size(820, 620);
        MinimumSize = new Size(600, 420);
        Padding = new Padding(16);

        var rootLayout = new BufferedTableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));

        rootLayout.Controls.Add(BuildDropRow(), 0, 0);
        rootLayout.Controls.Add(BuildGrid(), 0, 1);
        rootLayout.Controls.Add(BuildBottomRow(), 0, 2);

        Controls.Add(rootLayout);
    }

    /// <summary>
    /// Every section is its own "card" (Theme.Surface fill, distinct from the form's darker
    /// Theme.Background) with a bottom margin, so the gap of bare Background between cards reads
    /// as a clear boundary line between the drop zone / grid / controls sections.
    /// </summary>
    private Control BuildDropRow()
    {
        var card = new Panel
        {
            BackColor = Theme.Surface,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(8)
        };

        var dropLayout = new BufferedTableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            RowCount = 1
        };
        dropLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        dropLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        dropLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        dropLabel = new Label
        {
            AllowDrop = true,
            BackColor = Theme.SurfaceAlt,
            Dock = DockStyle.Fill,
            Font = Theme.Base,
            ForeColor = Theme.TextSecondary,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(12, 0, 12, 0),
            Text = "여기에 동영상 파일을 드래그 앤 드롭하거나, 찾기 버튼을 눌러 선택하세요.",
            TextAlign = ContentAlignment.MiddleLeft
        };
        dropLabel.DragEnter += DropLabel_DragEnter;
        dropLabel.DragDrop += DropLabel_DragDrop;

        var browseButton = new ThemedButton
        {
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0),
            Size = new Size(88, 32),
            Text = "찾기",
            Variant = ButtonVariant.Secondary
        };
        browseButton.Click += (_, _) => BrowseSourceFile();

        dropLayout.Controls.Add(dropLabel, 0, 0);
        dropLayout.Controls.Add(browseButton, 1, 0);
        card.Controls.Add(dropLayout);
        return card;
    }

    private Control BuildGrid()
    {
        var card = new Panel
        {
            BackColor = Theme.Surface,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(1)
        };

        grid = new ThemedGrid
        {
            AllowDrop = true,
            Dock = DockStyle.Fill,
            MultiSelect = true,
            RestBackColor = Theme.Surface
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "파일명", FillWeight = 220, Name = "FileName" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "시작", FillWeight = 80, Name = "Start" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "종료", FillWeight = 80, Name = "End" });
        grid.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

        grid.MouseDown += Grid_MouseDown;
        grid.MouseMove += Grid_MouseMove;
        grid.DragEnter += Grid_DragEnter;
        grid.DragOver += Grid_DragOver;
        grid.DragDrop += Grid_DragDrop;
        grid.KeyDown += Grid_KeyDown;

        card.Controls.Add(grid);
        return card;
    }

    private Control BuildBottomRow()
    {
        var card = new Panel
        {
            BackColor = Theme.Surface,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0),
            Padding = new Padding(8)
        };

        var bottomFlow = new FlowLayoutPanel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        startTextBox = new ThemedMaskedTextBox();
        endTextBox = new ThemedMaskedTextBox();
        ResetTimeInputs();
        // Convenience carried over from ClipM: leaving the start field copies it into end, since
        // marking the start of the next segment usually means retyping only the end time.
        startTextBox.Leave += (_, _) => endTextBox.Text = startTextBox.Text;

        var startHost = new InputHost(startTextBox) { Margin = new Padding(0, 4, 16, 4), Width = 110 };
        var endHost = new InputHost(endTextBox) { Margin = new Padding(0, 4, 24, 4), Width = 110 };

        addButton = new ThemedButton
        {
            Margin = new Padding(0, 4, 8, 4),
            Size = new Size(88, 32),
            Text = "추가",
            Variant = ButtonVariant.Secondary
        };
        addButton.Click += AddButton_Click;

        combineButton = new ThemedButton
        {
            Margin = new Padding(0, 4, 0, 4),
            Size = new Size(88, 32),
            Text = "합치기",
            Variant = ButtonVariant.Primary
        };
        combineButton.Click += CombineButton_Click;

        bottomFlow.Controls.Add(FlowLabel("시작"));
        bottomFlow.Controls.Add(startHost);
        bottomFlow.Controls.Add(FlowLabel("종료"));
        bottomFlow.Controls.Add(endHost);
        bottomFlow.Controls.Add(addButton);
        bottomFlow.Controls.Add(combineButton);

        card.Controls.Add(bottomFlow);
        return card;
    }

    private static Label FlowLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Font = Theme.Base,
            ForeColor = Theme.TextSecondary,
            Margin = new Padding(0, 15, 6, 0),
            Text = text
        };
    }

    // ------------------------------------------------------------ source file selection

    private void DropLabel_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void DropLabel_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var validFile = files?.FirstOrDefault(File.Exists);
        if (validFile is null)
        {
            log("유효한 파일이 없습니다.");
            return;
        }

        SetCurrentFile(validFile);
    }

    private void BrowseSourceFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "구간을 추출할 동영상 파일을 선택하세요.",
            Filter = "동영상 파일|*.ts;*.mp4;*.mkv;*.avi;*.mov|모든 파일|*.*",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetCurrentFile(dialog.FileName);
        }
    }

    private void SetCurrentFile(string path)
    {
        currentFilePath = path;
        dropLabel.Text = $"선택 파일: {Path.GetFileName(path)}";

        // Reset whenever the target file changes - a time range left over from a previous
        // (differently-sized) file is easy to reuse by mistake and land past the new file's end.
        ResetTimeInputs();

        log($"Clip 대상 파일 선택: {path}");
    }

    /// <summary>
    /// "Reset" means 00:00:00, not blank - a fully valid, already-complete time the user
    /// overtypes from, matching ClipM's original default rather than an empty "__:__:__" mask.
    /// </summary>
    private void ResetTimeInputs()
    {
        startTextBox.Text = "000000";
        endTextBox.Text = "000000";
    }

    // ------------------------------------------------------------ segment list

    private void AddButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(currentFilePath) || !File.Exists(currentFilePath))
        {
            log("상단에서 파일을 먼저 선택하세요.");
            return;
        }

        if (!TryGetStartEnd(out var start, out var end))
        {
            return;
        }

        var rowIndex = grid.Rows.Add(
            Path.GetFileName(currentFilePath),
            start.ToString(@"hh\:mm\:ss"),
            end.ToString(@"hh\:mm\:ss"));
        grid.Rows[rowIndex].Tag = new SegmentRow(currentFilePath, start, end);
    }

    private bool TryGetStartEnd(out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;

        if (!startTextBox.MaskCompleted || !endTextBox.MaskCompleted)
        {
            log("시작/종료 시간을 모두 입력하세요. (HH:mm:ss)");
            return false;
        }

        if (!TimeSpan.TryParseExact(startTextBox.Text, @"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture, out start)
            || !TimeSpan.TryParseExact(endTextBox.Text, @"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture, out end))
        {
            log("시간 형식이 잘못되었습니다. (HH:mm:ss)");
            return false;
        }

        if (end <= start)
        {
            log("종료시간은 시작시간보다 커야 합니다.");
            return false;
        }

        return true;
    }

    private void Grid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete)
        {
            foreach (var rowIndex in grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index).OrderByDescending(i => i))
            {
                grid.Rows.RemoveAt(rowIndex);
            }
        }
        else if (e.Control && e.KeyCode == Keys.A)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                row.Selected = true;
            }
        }
    }

    // ------------------------------------------------------------ row reordering (drag/drop)

    private void Grid_MouseDown(object? sender, MouseEventArgs e)
    {
        dragSourceRowIndex = grid.HitTest(e.X, e.Y).RowIndex;
        dragStartPoint = e.Location;
    }

    private void Grid_MouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || dragSourceRowIndex < 0)
        {
            return;
        }

        var dragSize = SystemInformation.DragSize;
        if (Math.Abs(e.X - dragStartPoint.X) < dragSize.Width && Math.Abs(e.Y - dragStartPoint.Y) < dragSize.Height)
        {
            return;
        }

        grid.DoDragDrop(dragSourceRowIndex, DragDropEffects.Move);
        dragSourceRowIndex = -1;
    }

    private static void Grid_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data != null && e.Data.GetDataPresent(typeof(int)) ? DragDropEffects.Move : DragDropEffects.None;
    }

    private static void Grid_DragOver(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data != null && e.Data.GetDataPresent(typeof(int)) ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void Grid_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data == null || !e.Data.GetDataPresent(typeof(int)))
        {
            return;
        }

        var sourceIndex = (int)e.Data.GetData(typeof(int))!;
        var clientPoint = grid.PointToClient(new Point(e.X, e.Y));
        var hitIndex = grid.HitTest(clientPoint.X, clientPoint.Y).RowIndex;
        var targetIndex = hitIndex >= 0 ? hitIndex : grid.Rows.Count - 1;

        if (sourceIndex < 0 || sourceIndex >= grid.Rows.Count || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        var sourceRow = grid.Rows[sourceIndex];
        var values = new object?[sourceRow.Cells.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = sourceRow.Cells[i].Value;
        }

        var tag = sourceRow.Tag;

        grid.Rows.RemoveAt(sourceIndex);
        if (targetIndex > sourceIndex)
        {
            targetIndex--;
        }

        grid.Rows.Insert(targetIndex, values);
        grid.Rows[targetIndex].Tag = tag;
        grid.Rows[targetIndex].Selected = true;
    }

    /// <summary>
    /// Output lands next to the first segment's source file, named after it so it is obvious
    /// later which recording an edit came from: "{원본파일명}_edit_{yyyyMMdd}.mp4". A numeric
    /// suffix is appended if that name is already taken (e.g. a second edit made the same day).
    /// </summary>
    private static string BuildOutputPath(string firstSegmentPath)
    {
        var outDir = Path.GetDirectoryName(firstSegmentPath) ?? Environment.CurrentDirectory;
        var baseName = $"{Path.GetFileNameWithoutExtension(firstSegmentPath)}_edit_{DateTime.Now:yyyyMMdd}";

        var candidate = Path.Combine(outDir, $"{baseName}.mp4");
        var suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(outDir, $"{baseName}_{suffix}.mp4");
            suffix++;
        }

        return candidate;
    }

    // ------------------------------------------------------------ combine

    private async void CombineButton_Click(object? sender, EventArgs e)
    {
        if (grid.Rows.Count == 0)
        {
            log("합칠 항목이 없습니다.");
            return;
        }

        var rows = grid.Rows
            .Cast<DataGridViewRow>()
            .Select(r => r.Tag as SegmentRow)
            .Where(r => r is not null)
            .Cast<SegmentRow>()
            .ToList();

        if (rows.Count == 0)
        {
            log("유효한 구간 데이터가 없습니다.");
            return;
        }

        var ffmpegPath = getFfmpegPath();
        if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
        {
            log("ffmpeg 경로가 설정되지 않았습니다. 환경설정에서 지정하세요.");
            return;
        }

        addButton.Enabled = false;
        combineButton.Enabled = false;
        var oldText = combineButton.Text;
        combineButton.Text = "합치는 중...";

        var tempFiles = new List<string>();
        try
        {
            log($"구간 {rows.Count}개 합치기 시작");

            foreach (var row in rows)
            {
                var tempClip = Path.Combine(Path.GetTempPath(), $"clipm_seg_{Guid.NewGuid():N}.mp4");
                tempFiles.Add(tempClip);
                await clipService.ExtractSegmentAsync(ffmpegPath, row.FilePath, row.Start, row.End - row.Start, tempClip);
            }

            var outFile = BuildOutputPath(rows[0].FilePath);

            await clipService.ConcatAsync(ffmpegPath, tempFiles, outFile);

            log($"완료: {outFile}");
        }
        catch (Exception ex)
        {
            log($"합치기 중 오류: {ex.Message}");
        }
        finally
        {
            foreach (var temp in tempFiles)
            {
                try
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
                catch
                {
                    // Best-effort cleanup - a leftover temp segment is harmless.
                }
            }

            combineButton.Text = oldText;
            addButton.Enabled = true;
            combineButton.Enabled = true;
        }
    }
}
