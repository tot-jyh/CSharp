using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace HunbjPlayer;

/// <summary>
/// 구간(시작~종료)을 목록에 모아 ffmpeg로 잘라 하나의 파일로 합치는 패널.
/// Hunbjter의 Clip 창과 같은 방식(구간 추출 -c copy 후 concat)을 플레이어 안에 넣은 것.
/// </summary>
public sealed class ClipPanel : Panel
{
    static readonly Color Back = Color.FromArgb(24, 26, 32);
    static readonly Color RowBack = Color.FromArgb(28, 31, 38);
    static readonly Color Border = Color.FromArgb(48, 52, 62);
    static readonly Color Blue = Color.FromArgb(59, 130, 246);
    const string TimeFormat = @"hh\:mm\:ss";

    sealed record Segment(string FilePath, TimeSpan Start, TimeSpan End);

    const int ExcludeColumn = 3;
    int[]? _dragRows; // 끌기 시작 시점에 확정한 이동 대상 행들
    Point _dragStart;

    readonly DataGridView _grid = new();
    readonly MaskedTextBox _start = TimeBox();
    readonly MaskedTextBox _end = TimeBox();
    readonly Button _add = Btn("추가", primary: false);
    readonly Button _combine = Btn("합치기", primary: true);
    readonly Button _clear = Btn("클리어", primary: false);
    // 시작/종료 칸 옆의 -1초 / +1초 버튼
    readonly Button _startMinus = Btn("-1", primary: false);
    readonly Button _startPlus = Btn("+1", primary: false);
    readonly Button _endMinus = Btn("-1", primary: false);
    readonly Button _endPlus = Btn("+1", primary: false);
    readonly Label _lblStart = Lbl("시작");
    readonly Label _lblEnd = Lbl("종료");
    readonly Panel _bottom = new() { Dock = DockStyle.Bottom, BackColor = Back };

    /// <summary>현재 재생 중인 파일 경로</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string?>? CurrentFile { get; set; }
    /// <summary>ffmpeg.exe 경로</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<string?>? FfmpegPath { get; set; }
    /// <summary>ffmpeg를 찾지 못했을 때 호출: 내려받아 설치하고 경로를 돌려줌(실패 시 null)</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<Task<string?>>? EnsureFfmpeg { get; set; }
    /// <summary>화면 안내(영상 OSD). (문구, 표시 ms)</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action<string, int>? Notify { get; set; }

    public ClipPanel()
    {
        BackColor = Back;
        Visible = false;
        Dock = DockStyle.Right;

        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = RowBack;
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = Border;
        _grid.EnableHeadersVisualStyles = false;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = false; // 제외 체크박스 열만 편집 가능 (나머지 열은 ReadOnly)
        _grid.AllowDrop = true;
        _grid.MultiSelect = true; // Ctrl/Shift 클릭, Ctrl+A로 여러 구간 선택
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Back, ForeColor = Color.Gainsboro, Alignment = DataGridViewContentAlignment.MiddleCenter,
            SelectionBackColor = Back, SelectionForeColor = Color.Gainsboro
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = RowBack, ForeColor = Color.Gainsboro,
            SelectionBackColor = Color.FromArgb(32, 50, 84), SelectionForeColor = Color.White
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "파일명", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 100, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "시작", Width = 80, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "종료", Width = 80, ReadOnly = true, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "제외", Width = 50, Resizable = DataGridViewTriState.False });
        // 체크박스는 클릭 즉시 값이 반영되도록 하고, 제외된 행은 흐리게 표시
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == ExcludeColumn) PaintExcluded(_grid.Rows[e.RowIndex]);
        };
        _grid.MouseDown += (_, e) =>
        {
            _dragStart = e.Location;
            if (_grid.HitTest(e.X, e.Y) is not { Type: DataGridViewHitTestType.Cell, ColumnIndex: not ExcludeColumn } h) { _dragRows = null; return; }
            // 이미 선택된 행 중 하나를 (Ctrl/Shift 없이) 끌면 선택된 행 전체를, 아니면 그 행 하나만 이동
            bool extend = (Control.ModifierKeys & (Keys.Control | Keys.Shift)) != 0;
            var selected = _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index).ToArray();
            _dragRows = !extend && selected.Contains(h.RowIndex) ? selected : new[] { h.RowIndex };
        };
        _grid.MouseMove += GridMouseMove;
        // 행을 클릭하면 그 구간의 시작/종료 시간이 아래 입력칸에 설정됨 (체크박스 열 제외)
        _grid.CellClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex == ExcludeColumn) return;
            if (_grid.Rows[e.RowIndex].Tag is not Segment seg) return;
            SetStart(seg.Start.TotalSeconds, seg.FilePath);
            SetEnd(seg.End.TotalSeconds, seg.FilePath);
        };
        // 더블클릭하면 그 구간을 해당 시작 시간부터 재생
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex == ExcludeColumn) return;
            if (_grid.Rows[e.RowIndex].Tag is Segment seg) SegmentActivated?.Invoke(seg.FilePath, seg.Start.TotalSeconds, seg.End.TotalSeconds);
        };
        _grid.DragOver += GridDragOver;
        _grid.DragEnter += GridDragOver;
        _grid.DragDrop += GridDragDrop;
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Delete) return;
            // 선택된 모든 구간 삭제 (선택이 없으면 현재 행)
            var rows = _grid.SelectedRows.Cast<DataGridViewRow>().ToList();
            if (rows.Count == 0 && _grid.CurrentRow != null) rows.Add(_grid.CurrentRow);
            foreach (var r in rows.OrderByDescending(r => r.Index)) _grid.Rows.Remove(r);
            e.Handled = true;
        };

        // 사용자가 입력칸을 직접 고치면 그 값은 "지금 재생 중인 파일" 기준으로 취급 (이전에 기억한 파일 기준은 버림)
        _start.TextChanged += (_, _) => { if (!_programmatic) _startFile = null; };
        _end.TextChanged += (_, _) => { if (!_programmatic) _endFile = null; };
        _bottom.Controls.AddRange(new Control[] { _lblStart, _start, _startMinus, _startPlus, _lblEnd, _end, _endMinus, _endPlus, _add, _combine, _clear });
        _startMinus.Click += (_, _) => Nudge(_start, -1);
        _startPlus.Click += (_, _) => Nudge(_start, +1);
        _endMinus.Click += (_, _) => Nudge(_end, -1);
        _endPlus.Click += (_, _) => Nudge(_end, +1);
        _bottom.Resize += (_, _) => LayoutBottom();
        _add.Click += (_, _) => Add();
        _combine.Click += async (_, _) => await CombineAsync();
        _clear.Click += (_, _) => Clear();

        Controls.Add(_grid);
        Controls.Add(_bottom);
        ApplyDpi();
    }

    static Label Lbl(string text) => new() { Text = text, AutoSize = true, ForeColor = Color.Gray, BackColor = Back };

    static MaskedTextBox TimeBox() => new()
    {
        Mask = "00:00:00", Text = "000000", TextAlign = HorizontalAlignment.Center,
        BackColor = RowBack, ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.FixedSingle,
        InsertKeyMode = InsertKeyMode.Overwrite, ResetOnSpace = false
    };

    static Button Btn(string text, bool primary)
    {
        var b = new Button
        {
            Text = text, FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
            BackColor = primary ? Color.FromArgb(59, 130, 246) : Color.FromArgb(32, 36, 44), TabStop = false,
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderColor = primary ? Color.FromArgb(59, 130, 246) : Border;
        return b;
    }

    int S(int v) => LogicalToDeviceUnits(v);

    public void ApplyDpi()
    {
        Width = S(420);
        _bottom.Height = S(86);
        _grid.RowTemplate.Height = S(30);
        _grid.ColumnHeadersHeight = S(32);
        _grid.Columns[1].Width = S(80);
        _grid.Columns[2].Width = S(80);
        _grid.Columns[ExcludeColumn].Width = S(50);
        foreach (DataGridViewRow r in _grid.Rows) r.Height = S(30);
        _start.Width = _end.Width = S(72);
        foreach (var b in new[] { _startMinus, _startPlus, _endMinus, _endPlus })
        {
            b.Size = new Size(S(32), _start.Height);
            b.Font = new Font(Font.FontFamily, 8f);
            b.Padding = Padding.Empty;
        }
        _add.Size = new Size(S(68), S(32));
        _combine.Size = new Size(S(84), S(32));
        _clear.Size = new Size(S(68), S(32));
        LayoutBottom();
    }

    // 폭이 좁으면 버튼이 아랫줄로 내려가도록 단순 배치
    void LayoutBottom()
    {
        int pad = S(12), gap = S(8), y1 = S(14), y2 = S(48);
        int x = pad;
        _lblStart.Location = new Point(x, y1 + S(4)); x += _lblStart.Width + S(4);
        _start.Location = new Point(x, y1); x += _start.Width + S(2);
        _startMinus.Location = new Point(x, y1); x += _startMinus.Width + S(1);
        _startPlus.Location = new Point(x, y1); x += _startPlus.Width + gap + S(4);
        _lblEnd.Location = new Point(x, y1 + S(4)); x += _lblEnd.Width + S(4);
        _end.Location = new Point(x, y1); x += _end.Width + S(2);
        _endMinus.Location = new Point(x, y1); x += _endMinus.Width + S(1);
        _endPlus.Location = new Point(x, y1);
        _add.Location = new Point(pad, y2);
        _combine.Location = new Point(pad + _add.Width + gap, y2);
        _clear.Location = new Point(_combine.Right + gap, y2);
    }

    // ------------------------------------------------------------ 외부(단축키)에서 호출

    // 시작/종료를 [ ] 키 등으로 정할 때 "어느 파일 기준인지"도 함께 기억함.
    // 추가(\) 시점에 재생 중인 파일이 바뀌어 있어도(예: 재생이 끝나 다음 파일로 넘어감) 시간을 잡은 파일로 구간이 만들어짐.
    string? _startFile, _endFile;
    bool _programmatic; // 코드로 입력칸을 바꾸는 중에는 사용자가 직접 고친 것으로 보지 않음

    /// <summary>작업 진행 중 합치기 버튼 글자로 상태를 보여줌 (영상이 멈춰 있어 화면 안내가 안 보일 때도 확인 가능)</summary>
    public void SetBusyText(string text) => _combine.Text = text;

    public void SetStart(double seconds, string? file = null)
    {
        _programmatic = true;
        _start.Text = Format(seconds);
        _programmatic = false;
        _startFile = file;
    }

    public void SetEnd(double seconds, string? file = null)
    {
        _programmatic = true;
        _end.Text = Format(seconds);
        _programmatic = false;
        _endFile = file;
    }

    // 입력칸 시간을 1초 앞/뒤로 (0 미만은 0). 어느 파일 기준인지(_startFile/_endFile)는 그대로 유지
    void Nudge(MaskedTextBox box, int delta)
    {
        if (!TimeSpan.TryParseExact(box.Text, TimeFormat, CultureInfo.InvariantCulture, out var t)) t = TimeSpan.Zero;
        _programmatic = true;
        box.Text = Format(Math.Max(0, t.TotalSeconds + delta));
        _programmatic = false;
    }

    static string Format(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, Math.Floor(seconds)));
        return $"{(int)t.TotalHours:00}{t.Minutes:00}{t.Seconds:00}"; // 마스크에 맞춰 숫자만
    }

    public void Add()
    {
        // 시작/종료를 정한 파일이 있으면 그 파일, 직접 입력했다면 지금 재생 중인 파일
        if (_startFile != null && _endFile != null && !string.Equals(_startFile, _endFile, StringComparison.OrdinalIgnoreCase))
        {
            Notify?.Invoke("시작과 종료가 서로 다른 파일에서 잡혔습니다. 다시 지정하세요.", 3500);
            return;
        }
        var file = _startFile ?? _endFile ?? CurrentFile?.Invoke();
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) { Notify?.Invoke("재생 중인 파일이 없습니다.", 2500); return; }
        if (!TryGetRange(out var start, out var end)) return;
        _startFile = _endFile = null; // 한 번 쓴 파일 기준은 비워서, 다음 구간은 새로 정하게 함

        int i = _grid.Rows.Add(Path.GetFileName(file), start.ToString(TimeFormat), end.ToString(TimeFormat));
        _grid.Rows[i].Height = S(30);
        _grid.Rows[i].Tag = new Segment(file, start, end);
        _grid.ClearSelection();
        _grid.Rows[i].Selected = true;
        // 패널이 닫혀 있으면(숨김 상태) 스크롤 위치를 지정할 수 없어 예외가 나므로 보일 때만 맨 아래로 스크롤
        if (_grid.Visible && _grid.IsHandleCreated)
        {
            try { _grid.FirstDisplayedScrollingRowIndex = i; } catch (InvalidOperationException) { /* 레이아웃 전이면 무시 */ }
        }
        Notify?.Invoke($"구간 추가  {start.ToString(TimeFormat)} ~ {end.ToString(TimeFormat)}", 1500);
    }

    // ------------------------------------------------------------ 상태 저장/복원

    /// <summary>현재 구간 목록(제외 체크 포함)을 저장용 데이터로 돌려줌</summary>
    public List<ClipRowData> GetRows() => _grid.Rows.Cast<DataGridViewRow>()
        .Where(r => r.Tag is Segment)
        .Select(r =>
        {
            var s = (Segment)r.Tag!;
            return new ClipRowData { FilePath = s.FilePath, Start = s.Start.TotalSeconds, End = s.End.TotalSeconds, Excluded = IsExcluded(r) };
        }).ToList();

    /// <summary>저장해 둔 구간 목록을 복원 (원본 파일이 사라진 행은 건너뜀)</summary>
    public void SetRows(IEnumerable<ClipRowData> rows)
    {
        _grid.Rows.Clear();
        foreach (var d in rows)
        {
            if (!File.Exists(d.FilePath) || d.End <= d.Start) continue;
            var start = TimeSpan.FromSeconds(d.Start);
            var end = TimeSpan.FromSeconds(d.End);
            int i = _grid.Rows.Add(Path.GetFileName(d.FilePath), start.ToString(TimeFormat), end.ToString(TimeFormat), d.Excluded);
            _grid.Rows[i].Height = S(30);
            _grid.Rows[i].Tag = new Segment(d.FilePath, start, end);
            PaintExcluded(_grid.Rows[i]);
        }
        _grid.ClearSelection();
    }

    bool TryGetRange(out TimeSpan start, out TimeSpan end)
    {
        start = end = default;
        if (!_start.MaskCompleted || !_end.MaskCompleted
            || !TimeSpan.TryParseExact(_start.Text, TimeFormat, CultureInfo.InvariantCulture, out start)
            || !TimeSpan.TryParseExact(_end.Text, TimeFormat, CultureInfo.InvariantCulture, out end))
        {
            Notify?.Invoke("시작/종료 시간을 확인하세요. (HH:mm:ss)", 2500);
            return false;
        }
        if (end <= start) { Notify?.Invoke("종료시간은 시작시간보다 커야 합니다.", 2500); return false; }
        return true;
    }

    // ------------------------------------------------------------ 제외 / 클리어 / 순서 변경

    static bool IsExcluded(DataGridViewRow r) => r.Cells[ExcludeColumn].Value is true;

    void PaintExcluded(DataGridViewRow row)
    {
        row.DefaultCellStyle.ForeColor = IsExcluded(row) ? Color.FromArgb(110, 114, 124) : Color.Empty;
    }

    void Clear()
    {
        if (_grid.Rows.Count == 0) { Notify?.Invoke("지울 항목이 없습니다.", 1500); return; }
        int n = _grid.Rows.Count;
        _grid.Rows.Clear();
        Notify?.Invoke($"구간 {n}개 삭제", 1500);
    }

    void GridMouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _dragRows == null) return;
        var d = SystemInformation.DragSize;
        if (Math.Abs(e.X - _dragStart.X) < d.Width && Math.Abs(e.Y - _dragStart.Y) < d.Height) return;
        var rows = _dragRows;
        _dragRows = null;
        _grid.DoDragDrop(rows, DragDropEffects.Move);
    }

    /// <summary>파일이 구간 목록 위에 드롭됨 (재생목록에 추가하도록 폼에 전달)</summary>
    [System.ComponentModel.Browsable(false)]
    public event Action<string[]>? FilesDropped;
    /// <summary>구간 행을 더블클릭: (파일, 시작 초, 종료 초)</summary>
    public event Action<string, double, double>? SegmentActivated;

    // 행 순서 변경(int)과 탐색기 파일 드롭을 모두 받음
    void GridDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(typeof(int[])) == true) e.Effect = DragDropEffects.Move;
        else if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
        else e.Effect = DragDropEffects.None;
    }

    void GridDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            FilesDropped?.Invoke(files);
            return;
        }
        if (e.Data?.GetData(typeof(int[])) is not int[] src) return;
        var p = _grid.PointToClient(new Point(e.X, e.Y));
        int hit = _grid.HitTest(p.X, p.Y).RowIndex;
        MoveRows(src, hit >= 0 ? hit : _grid.Rows.Count - 1);
    }

    // 선택한 여러 행을 target 위치로 함께 이동 (서로의 상대 순서 유지). 아래로 옮기면 target 행 뒤에, 위로 옮기면 앞에 놓임
    void MoveRows(int[] indices, int target)
    {
        var set = indices.Where(i => i >= 0 && i < _grid.Rows.Count).Distinct().OrderBy(i => i).ToArray();
        if (set.Length == 0 || target < 0 || target >= _grid.Rows.Count || set.Contains(target)) return;

        var moving = set.Select(i => _grid.Rows[i]).ToList();
        int removedBefore = set.Count(i => i < target);
        bool down = target > set[0];
        foreach (var i in set.Reverse()) _grid.Rows.RemoveAt(i);
        int insertAt = Math.Clamp(target - removedBefore + (down ? 1 : 0), 0, _grid.Rows.Count);
        foreach (var row in moving) _grid.Rows.Insert(insertAt++, row);

        _grid.ClearSelection();
        foreach (var row in moving) row.Selected = true;
    }

    // ------------------------------------------------------------ 합치기 (ffmpeg)

    async Task CombineAsync()
    {
        // 제외 체크된 행은 건너뜀 (위에서 아래 순서 그대로)
        var segments = _grid.Rows.Cast<DataGridViewRow>()
            .Where(r => !IsExcluded(r))
            .Select(r => r.Tag as Segment).OfType<Segment>().ToList();
        if (segments.Count == 0)
        {
            Notify?.Invoke(_grid.Rows.Count == 0 ? "합칠 항목이 없습니다." : "제외되지 않은 항목이 없습니다.", 2500);
            return;
        }

        var ffmpeg = FfmpegPath?.Invoke();
        if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg))
        {
            // 찾지 못하면 내려받기를 시도 (진행 상황/실패 안내는 EnsureFfmpeg가 화면에 표시)
            if (EnsureFfmpeg == null) { Notify?.Invoke("ffmpeg.exe를 찾을 수 없습니다.", 3500); return; }
            _add.Enabled = _combine.Enabled = _clear.Enabled = false;
            var label = _combine.Text;
            _combine.Text = "ffmpeg 준비 중...";
            try { ffmpeg = await EnsureFfmpeg(); }
            finally { _combine.Text = label; _add.Enabled = _combine.Enabled = _clear.Enabled = true; }
            if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg)) return;
            Notify?.Invoke("ffmpeg 준비 완료. 합치기를 시작합니다.", 2000);
        }

        _add.Enabled = _combine.Enabled = _clear.Enabled = false;
        var oldText = _combine.Text;
        _combine.Text = "합치는 중...";
        var temps = new List<string>();
        try
        {
            Notify?.Invoke($"구간 {segments.Count}개 합치기 시작", 2000);
            foreach (var seg in segments)
            {
                var temp = Path.Combine(Path.GetTempPath(), $"hunbjplayer_seg_{Guid.NewGuid():N}.mp4");
                temps.Add(temp);
                await RunFfmpegAsync(ffmpeg, new[]
                {
                    "-hide_banner", "-y", "-ss", seg.Start.ToString(TimeFormat), "-i", seg.FilePath,
                    "-t", (seg.End - seg.Start).ToString(TimeFormat), "-c", "copy", temp
                }, "구간 추출 실패");

                // 시작 시간이 원본 길이를 넘으면 ffmpeg는 오류 없이 몇 KB짜리 빈 파일을 만들므로 직접 걸러냄
                if (!File.Exists(temp) || new FileInfo(temp).Length < 4096)
                    throw new InvalidOperationException(
                        $"구간 {segments.IndexOf(seg) + 1}이(가) 비어 있습니다. '{Path.GetFileName(seg.FilePath)}'에 {seg.Start.ToString(TimeFormat)} 이후 내용이 없습니다. " +
                        "구간의 원본 파일이 맞는지 확인하세요.");
            }

            var outFile = BuildOutputPath(segments[0].FilePath);
            var list = Path.Combine(Path.GetTempPath(), $"hunbjplayer_concat_{Guid.NewGuid():N}.txt");
            temps.Add(list);
            await File.WriteAllTextAsync(list,
                string.Join(Environment.NewLine, temps.Where(t => t.EndsWith(".mp4")).Select(p => $"file '{p.Replace("'", "'\\''")}'")),
                new UTF8Encoding(false));
            await RunFfmpegAsync(ffmpeg, new[]
            {
                "-hide_banner", "-y", "-f", "concat", "-safe", "0", "-i", list, "-c", "copy", outFile
            }, "구간 합치기 실패");

            Notify?.Invoke($"완료: {Path.GetFileName(outFile)}", 6000);
        }
        catch (Exception ex)
        {
            Notify?.Invoke($"합치기 오류: {ex.Message}", 6000);
        }
        finally
        {
            foreach (var t in temps)
            {
                try { if (File.Exists(t)) File.Delete(t); } catch { /* 임시 파일 정리 실패는 무시 */ }
            }
            _combine.Text = oldText;
            _add.Enabled = _combine.Enabled = _clear.Enabled = true;
        }
    }

    static async Task RunFfmpegAsync(string ffmpeg, IEnumerable<string> args, string failMessage)
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpeg, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, RedirectStandardInput = true
            }
        };
        foreach (var a in args) p.StartInfo.ArgumentList.Add(a);
        if (!p.Start()) throw new InvalidOperationException("ffmpeg를 시작하지 못했습니다.");
        var err = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            err = err.Replace("\r", " ").Replace("\n", " ").Trim();
            throw new InvalidOperationException($"{failMessage} (코드 {p.ExitCode}): {(err.Length <= 300 ? err : err[^300..])}");
        }
    }

    /// <summary>첫 구간 원본 옆에 "{원본}_edit_{yyyyMMdd}.mp4" (겹치면 _2, _3...)</summary>
    static string BuildOutputPath(string firstSegmentPath)
    {
        var dir = Path.GetDirectoryName(firstSegmentPath) ?? Environment.CurrentDirectory;
        var baseName = $"{Path.GetFileNameWithoutExtension(firstSegmentPath)}_edit_{DateTime.Now:yyyyMMdd}";
        var candidate = Path.Combine(dir, $"{baseName}.mp4");
        for (int n = 2; File.Exists(candidate); n++) candidate = Path.Combine(dir, $"{baseName}_{n}.mp4");
        return candidate;
    }
}
