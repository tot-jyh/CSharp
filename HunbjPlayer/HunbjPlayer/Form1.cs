namespace HunbjPlayer;

public class Form1 : Form
{
    static readonly Color BarBack = Color.FromArgb(30, 30, 30);
    static readonly Color PanelBack = Color.FromArgb(56, 56, 56);
    static readonly Color Accent = Color.FromArgb(255, 205, 0);
    const string IconFont = "Segoe MDL2 Assets";
    const string GlyphPin = ""; // 최상위(핀)
    const string GlyphSettings = ""; // 환경설정(톱니바퀴)
    const string GlyphPlay = "", GlyphPause = "", GlyphStop = "",
        GlyphOpen = "", GlyphMenu = "", GlyphClip = "";

    // 영상 창(mpv)에 포커스가 있을 때도 같은 단축키가 동작하도록 mpv 키 → WinForms 키 매핑
    static readonly (string Mpv, Keys Key)[] MpvKeys =
    {
        ("SPACE", Keys.Space),
        ("LEFT", Keys.Left), ("RIGHT", Keys.Right),
        ("Ctrl+LEFT", Keys.Control | Keys.Left), ("Ctrl+RIGHT", Keys.Control | Keys.Right),
        ("Shift+LEFT", Keys.Shift | Keys.Left), ("Shift+RIGHT", Keys.Shift | Keys.Right),
        ("UP", Keys.Up), ("DOWN", Keys.Down), ("PGDWN", Keys.PageDown), ("PGUP", Keys.PageUp), ("Shift+PGDWN", Keys.Shift | Keys.PageDown), ("Shift+PGUP", Keys.Shift | Keys.PageUp),
        ("r", Keys.R), ("o", Keys.O), ("p", Keys.P), ("m", Keys.M), ("f", Keys.F), ("d", Keys.D), ("x", Keys.X), ("c", Keys.C), ("z", Keys.Z), ("ENTER", Keys.Enter), ("F11", Keys.F11), ("ESC", Keys.Escape),
        ("Ctrl+o", Keys.Control | Keys.O),
        ("[", Keys.OemOpenBrackets), ("]", Keys.OemCloseBrackets), ("\\", Keys.OemPipe),
        // 구간반복: Ctrl+[ 시작, Ctrl+] 종료, Ctrl+\ 해제, Ctrl+Shift+\ 편집(Clip) 시작/종료로 설정
        ("Ctrl+[", Keys.Control | Keys.OemOpenBrackets), ("Ctrl+]", Keys.Control | Keys.OemCloseBrackets),
        ("Ctrl+\\", Keys.Control | Keys.OemPipe),
        ("Ctrl+Shift+\\", Keys.Control | Keys.Shift | Keys.OemPipe), ("Ctrl+|", Keys.Control | Keys.Shift | Keys.OemPipe),
    };

    readonly PlayerSettings _settings = PlayerSettings.Load();
    MpvPlayer? _mpv;
    readonly Panel _video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    // 영상 + 하단 바를 담는 재생창 패널. 재생목록/편집목록은 이 패널 오른쪽에 붙음
    readonly Panel _player = new() { Dock = DockStyle.Fill, BackColor = Color.Black };

    // 하단 바
    readonly Panel _bar = new() { Dock = DockStyle.Bottom, BackColor = BarBack };
    readonly SeekBar _seek = new() { Dock = DockStyle.Top };
    readonly Panel _row = new() { Dock = DockStyle.Fill, BackColor = BarBack };
    readonly Button _btnPlay = IconBtn(GlyphPlay);
    readonly Button _btnStop = IconBtn(GlyphStop);
    readonly Button _btnOpen = IconBtn(GlyphOpen);
    readonly Button _btnList = IconBtn(GlyphMenu);
    readonly Button _btnClip = IconBtn(GlyphClip);
    readonly Button _btnPin = IconBtn(GlyphPin);
    readonly Button _btnSettings = IconBtn(GlyphSettings);
    readonly ClipPanel _clip = new();
    readonly Label _time = new() { ForeColor = Color.Gainsboro, AutoSize = true, BackColor = BarBack };

    // 재생속도 콤보박스 (현재 속도 표시 + 목록에서 선택). 마지막 항목 "최대"는 MaxSpeed
    static readonly (string Text, double Speed)[] SpeedItems =
    {
        ("0.5x", 0.5), ("1.0x", 1.0), ("1.5x", 1.5), ("2.0x", 2.0), ("3.0x", 3.0), ("5.0x", 5.0), ("10.0x", 10.0), ("최대", 12.0),
    };
    readonly ComboBox _speedBox = new()
    {
        // DropDownList + 직접 그리기: 어디를 눌러도 목록이 열리고, 목록에 없는 값(예: 1.3x)도 현재 속도로 표시 가능
        DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, FlatStyle = FlatStyle.Flat,
        TabStop = false, BackColor = Color.FromArgb(56, 56, 56), ForeColor = Color.White
    };
    string _speedLabel = "1.0x";

    // 재생목록
    readonly ListBox _listBox = new()
    {
        Dock = DockStyle.Fill, BackColor = PanelBack, ForeColor = Color.Gainsboro,
        BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, IntegralHeight = false,
        SelectionMode = SelectionMode.MultiExtended // Ctrl/Shift로 여러 파일 선택
    };
    readonly Panel _listPanel = new() { Dock = DockStyle.Right, BackColor = PanelBack, Visible = false };
    readonly List<string> _items = new();
    // "정렬없음"으로 돌아갈 기준 순서: 추가한 순서(직접 끌어 옮기면 그 순서가 새 기준). 정렬 버튼은 이 기준을 바꾸지 않음
    List<string> _baseOrder = new();
    int _index = -1;

    // 재생목록 아래 정렬 버튼 패널
    readonly Panel _listBottom = new() { Dock = DockStyle.Bottom, BackColor = Color.FromArgb(40, 40, 40) };
    readonly Button _btnSortNameAsc = SortBtn("이름순 ↑");
    readonly Button _btnSortNameDesc = SortBtn("이름순 ↓");
    readonly Button _btnSortDateAsc = SortBtn("최근순 ↑");
    readonly Button _btnSortDateDesc = SortBtn("최근순 ↓");
    readonly Button _btnSortNone = SortBtn("정렬없음");
    readonly ToolTip _sortTip = new() { InitialDelay = 300, ShowAlways = true };

    static Button SortBtn(string text)
    {
        var b = new Button
        {
            Text = text, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(56, 56, 56),
            TabStop = false, UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 75, 75);
        return b;
    }

    // 재생 상태 갱신 (mpv는 프레임마다 시간이 바뀌므로 이벤트 대신 주기적으로 조회)
    readonly System.Windows.Forms.Timer _poll = new() { Interval = 200 };
    double _seekTarget = -1;
    string? _seekOsd;
    long _seekAt;
    bool _muted;

    bool _fullscreen;
    bool _listWasVisible;
    bool _clipWasVisible;
    FormBorderStyle _prevBorder;
    FormWindowState _prevState;

    static Button IconBtn(string glyph)
    {
        var b = new Button
        {
            Text = glyph, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = BarBack,
            Font = new Font(IconFont, 12f), TabStop = false, UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(80, 80, 80);
        return b;
    }

    public Form1(string[] args)
    {
        Text = "HunbjPlayer";
        ClientSize = new Size(960, 600);
        MinimumSize = new Size(560, 300);
        BackColor = Color.Black;
        KeyPreview = true;
        AllowDrop = true;

        using (var s = typeof(Form1).Assembly.GetManifestResourceStream("app.ico"))
            if (s != null) Icon = new Icon(s);
        ApplySavedBounds();

        _row.Controls.AddRange(new Control[] { _btnPlay, _btnStop, _btnOpen, _speedBox, _time, _btnSettings, _btnPin, _btnClip, _btnList });
        _bar.Controls.Add(_row);
        _bar.Controls.Add(_seek);
        _listPanel.Controls.Add(_listBox);
        _listBottom.Controls.AddRange(new Control[] { _btnSortNameAsc, _btnSortNameDesc, _btnSortDateAsc, _btnSortDateDesc, _btnSortNone });
        _listPanel.Controls.Add(_listBottom); // 나중에 추가된 쪽이 먼저 도킹되므로 목록 아래 고정
        _listBottom.Resize += (_, _) => LayoutSortButtons();
        _btnSortNameAsc.Click += (_, _) => SortList(SortMode.NameAsc);
        _btnSortNameDesc.Click += (_, _) => SortList(SortMode.NameDesc);
        _btnSortDateAsc.Click += (_, _) => SortList(SortMode.DateAsc);
        _btnSortDateDesc.Click += (_, _) => SortList(SortMode.DateDesc);
        _btnSortNone.Click += (_, _) => SortList(SortMode.None);
        _sortTip.SetToolTip(_btnSortNameAsc, "파일 이름 오름차순 (가 → 하, A → Z, 번호 작은 것부터)");
        _sortTip.SetToolTip(_btnSortNameDesc, "파일 이름 내림차순 (하 → 가, Z → A, 번호 큰 것부터)");
        _sortTip.SetToolTip(_btnSortDateAsc, "수정 날짜 오름차순 (오래된 파일이 위)");
        _sortTip.SetToolTip(_btnSortDateDesc, "수정 날짜 내림차순 (최근 파일이 위)");
        _sortTip.SetToolTip(_btnSortNone, "정렬 없음 (추가한 순서로 되돌림)");

        // 레이아웃: [ 재생창(영상 + 하단 바) ][ 재생목록 ][ 편집(Clip) 목록 ] 순서. 재생목록/편집목록은 재생창 오른쪽에 붙고,
        // 진행바 등 하단 바는 재생창 폭까지만 차지함. (나중에 추가된 컨트롤이 먼저 도킹됨: Clip → 목록 → 재생창)
        _player.Controls.Add(_video);
        _player.Controls.Add(_bar);
        Controls.Add(_player);
        Controls.Add(_listPanel);
        Controls.Add(_clip);

        _listBox.DrawItem += DrawListItem;
        _listBox.DoubleClick += (_, _) =>
        {
            // 여러 개가 선택돼 있어도 더블클릭한 그 항목을 재생
            int i = _listBox.IndexFromPoint(_listBox.PointToClient(Cursor.Position));
            if (i >= 0) PlayAt(i);
        };

        // 재생목록 항목을 끌어서 순서 변경 (여러 개 선택 상태에서 선택된 항목을 끌면 함께 이동).
        // 탐색기 파일 드롭은 목록에 추가만
        _listBox.AllowDrop = true;

        // 파일명이 길어 "..."로 잘려 보이는 항목 위에 마우스를 올리면 전체 파일명을 풍선 도움말로 표시
        _listBox.MouseMove += (_, e) => UpdateListTip(e.Location);
        _listBox.MouseLeave += (_, _) => { _tipIndex = -1; _listTip.Hide(_listBox); };
        _listBox.MouseDown += (_, _) => { _tipIndex = -1; _listTip.Hide(_listBox); };

        _listBox.MouseDown += (_, e) =>
        {
            // 기본 처리(선택 변경)보다 먼저 호출되므로 여기서 끌 대상을 확정해 둠
            int hit = _listBox.IndexFromPoint(e.Location);
            _lbDragStart = e.Location;
            if (hit < 0) { _lbDragSet = null; return; }
            bool extend = (ModifierKeys & (Keys.Control | Keys.Shift)) != 0;
            var selected = _listBox.SelectedIndices.Cast<int>().ToArray();
            _lbDragSet = !extend && selected.Contains(hit) ? selected : new[] { hit };
        };
        _listBox.MouseMove += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || _lbDragSet == null) return;
            var d = SystemInformation.DragSize;
            if (Math.Abs(e.X - _lbDragStart.X) < d.Width && Math.Abs(e.Y - _lbDragStart.Y) < d.Height) return;
            var set = _lbDragSet;
            _lbDragSet = null;
            _listBox.DoDragDrop(set, DragDropEffects.Move);
        };
        DragEventHandler listOver = (_, e) =>
        {
            if (e.Data?.GetDataPresent(typeof(int[])) == true) e.Effect = DragDropEffects.Move;
            else if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) OnFileDragOver(null, e);
            else e.Effect = DragDropEffects.None;
        };
        _listBox.DragEnter += listOver;
        _listBox.DragOver += listOver;
        _listBox.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                ActivateAfterDrop();
                AddToList(files);
            }
            else if (e.Data?.GetData(typeof(int[])) is int[] src)
            {
                var p = _listBox.PointToClient(new Point(e.X, e.Y));
                int hit = _listBox.IndexFromPoint(p);
                MoveItems(src, hit >= 0 ? hit : _items.Count - 1);
            }
        };

        _btnPlay.Click += (_, _) => TogglePlay();
        _btnStop.Click += (_, _) => _mpv?.Stop();
        _btnOpen.Click += (_, _) => OpenDialog();
        _btnList.Click += (_, _) => ToggleList();
        foreach (var item in SpeedItems) _speedBox.Items.Add(item.Text);
        _speedBox.ItemHeight = 20;
        _speedBox.DrawItem += DrawSpeedItem;
        _speedBox.SelectionChangeCommitted += (_, _) =>
        {
            int i = _speedBox.SelectedIndex;
            if (i >= 0) SetSpeed(SpeedItems[i].Speed);
            // 표시는 항상 현재 속도 문구를 쓰므로 선택 상태는 비우고, 포커스는 폼으로 돌려 단축키가 계속 동작하게 함
            BeginInvoke(() => { _speedBox.SelectedIndex = -1; ActiveControl = null; Focus(); });
        };
        _speedBox.DropDownClosed += (_, _) => BeginInvoke(() => { ActiveControl = null; Focus(); });
        _btnClip.Click += (_, _) => ToggleClip();
        _btnPin.Click += (_, _) => SetTopMost(!TopMost);
        _btnSettings.Click += (_, _) => ShowSettings();
        _sortTip.SetToolTip(_btnSettings, "환경설정");
        _sortTip.SetToolTip(_btnPin, "항상 위");
        _sortTip.SetToolTip(_btnClip, "편집(Clip) 목록");
        _sortTip.SetToolTip(_btnList, "재생목록");
        SetTopMost(_settings.TopMost);
        _clip.CurrentFile = PlayingPath;
        _clip.FfmpegPath = ResolveFfmpeg;
        _clip.EnsureFfmpeg = EnsureFfmpegAsync; // 없으면 합치기 직전에 내려받음
        _clip.Notify = (text, ms) => _mpv?.ShowText(text, ms);
        _video.DoubleClick += (_, _) => TogglePlay();
        _video.MouseDown += (_, _) => FocusVideo();

        _seek.Scrubbed += OnScrub;
        _fsBar.Seek.Scrubbed += OnScrub; // 전체화면 진행바에서도 같은 방식으로 이동
        _fsTimer.Tick += (_, _) => FsTick();
        _frameOsd.Tick += (_, _) => ShowFrameOsd();

        _poll.Tick += (_, _) => UpdateState();

        // 파일 드롭: 영상 영역 → 바로 재생(목록에도 추가), 재생목록/Clip 패널/영상 상단 → 목록에 추가만
        DragEnter += OnFileDragOver;
        DragOver += OnFileDragOver;
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
            var addOnly = IsAddOnlyZone(new Point(e.X, e.Y));
            LogDrop(e, files, addOnly);
            // 탐색기에서 끌어다 놓으면 탐색기가 활성 창으로 남아 단축키가 먹지 않음 → 이 창을 활성화
            ActivateAfterDrop();
            if (addOnly) AddToList(files);
            else if (files.Length == 1 && File.Exists(files[0]) && _settings.SimilarPrefix > 0) ReplaceWithSimilar(files[0]);
            else ReplaceAndPlay(files);
        };
        _clip.FilesDropped += files => { ActivateAfterDrop(); AddToList(files); };
        _clip.SegmentActivated += PlaySegment;

        _row.Resize += (_, _) => LayoutRow();
        ApplyDpi();
        ResetUi();

        RestoreLists();
        Load += (_, _) => { InitPlayer(); RestorePanels(); };
        FormClosing += (_, _) =>
        {
            SaveSettings();
            _poll.Stop();
            _fsTimer.Stop();
            _fsBar.Close();
            _fsTitle.Close();
            _toast.Close();
            _mpv?.Dispose();
            _mpv = null;
        };

        var startPaths = ResolveStartPaths(args);
        if (startPaths.Length > 0) Shown += (_, _) => ReplaceAndPlay(startPaths);    }

    // 저장된 창 위치/크기 복원. 모니터 구성이 바뀌어 화면 밖이면 기본 위치 사용
    void ApplySavedBounds()
    {
        if (!_settings.HasBounds) return;
        var r = new Rectangle(_settings.Left, _settings.Top, _settings.Width, _settings.Height);
        if (r.Width < MinimumSize.Width || r.Height < MinimumSize.Height) return;
        if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(r))) return;
        StartPosition = FormStartPosition.Manual;
        Bounds = r;
        if (_settings.Maximized) WindowState = FormWindowState.Maximized;
    }

    void SaveSettings()
    {
        // 최대화/전체화면 중에는 일반 상태의 위치·크기(RestoreBounds)를 저장
        bool normal = WindowState == FormWindowState.Normal && !_fullscreen;
        var b = normal ? Bounds : RestoreBounds;
        // 패널은 닫힌 상태로 시작하므로, 열려 있던 패널 폭은 빼고 저장 (다음 실행 때 영상 영역 크기가 그대로)
        bool listOpen = _fullscreen ? _listWasVisible : _listPanel.Visible;
        bool clipOpen = _fullscreen ? _clipWasVisible : _clip.Visible;
        int extra = (listOpen ? _listPanel.Width : 0) + (clipOpen ? _clip.Width : 0);
        _settings.Left = b.Left; _settings.Top = b.Top; _settings.Width = Math.Max(MinimumSize.Width, b.Width - extra); _settings.Height = b.Height;
        _settings.Maximized = _fullscreen ? _prevState == FormWindowState.Maximized : WindowState == FormWindowState.Maximized;
        _settings.HasBounds = true;
        _settings.TopMost = TopMost;
        _settings.ListOpen = listOpen;
        _settings.ClipOpen = clipOpen;
        _settings.Playlist = _items.ToList();
        _settings.PlaylistIndex = _index;
        _settings.PlaylistSort = (int)_sortMode;
        _settings.ClipRows = _clip.GetRows();
        // 목록에 없는 파일의 재생시간 캐시는 버려 설정 파일이 계속 커지지 않게 함
        foreach (var key in _settings.Durations.Keys.Where(k => !_items.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList())
            _settings.Durations.Remove(key);
        if (_mpv != null) _settings.Volume = _mpv.Volume;
        _settings.Save();
    }

    void InitPlayer()
    {
        try
        {
            _mpv = new MpvPlayer(_video.Handle);
        }
        catch (Exception ex)
        {
            ShowError($"재생 엔진(libmpv-2.dll)을 불러오지 못했습니다.\n{ex.Message}", 15000);
            return;
        }

        _mpv.Volume = _settings.Volume;

        foreach (var (mpvKey, key) in MpvKeys)
            _mpv.Command("keybind", mpvKey, $"script-message hkey {(int)key}");
        _mpv.Command("keybind", "MBTN_LEFT_DBL", "script-message hdbl");
        _mpv.Command("keybind", "MBTN_LEFT", "script-message hclick");

        _mpv.EndReached += () => BeginInvoke(OnEnded);
        _mpv.LoadFailed += reason => BeginInvoke(() => OnLoadFailed(reason));
        _mpv.PlaybackRestart += () => BeginInvoke(OnSeekDone);
        _mpv.ClientMessage += args =>
        {
            if (args.Length >= 2 && args[0] == "hkey" && int.TryParse(args[1], out int k))
                BeginInvoke(() => { FocusVideo(clicked: false); HandleKey((Keys)k, fromVideo: true); });
            else if (args.Length >= 1 && args[0] == "hclick")
                BeginInvoke(FocusVideo);
            else if (args.Length >= 1 && args[0] == "hdbl")
                BeginInvoke(TogglePlay);
        };
        _poll.Start();
    }

    int S(int v) => LogicalToDeviceUnits(v);

    // DPI 배율에 맞춰 바 높이/버튼 크기를 다시 계산 (잘림 방지)
    void ApplyDpi()
    {
        _seek.Height = S(20);
        _row.Height = S(40);
        _bar.Height = _seek.Height + S(40);
        _listPanel.Width = S(300);
        _listBottom.Height = S(88);
        LayoutSortButtons();
        _clip.ApplyDpi();
        _listBox.ItemHeight = S(22);
        foreach (var b in new[] { _btnPlay, _btnStop, _btnOpen, _btnSettings, _btnPin, _btnClip, _btnList }) b.Size = new Size(S(44), S(36));
        _speedBox.Width = S(84);
        _speedBox.ItemHeight = S(20);
        LayoutRow();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyDpi();
    }

    void LayoutRow()
    {
        int h = _row.ClientSize.Height;
        int y = (h - _btnPlay.Height) / 2;
        int x = S(6);
        foreach (var b in new[] { _btnPlay, _btnStop, _btnOpen })
        {
            b.Location = new Point(x, y);
            x += b.Width;
        }
        _speedBox.Location = new Point(x + S(8), (h - _speedBox.Height) / 2);
        _time.Location = new Point(_speedBox.Right + S(12), (h - _time.Height) / 2);
        _btnList.Location = new Point(_row.ClientSize.Width - _btnList.Width - S(6), y);
        _btnClip.Location = new Point(_btnList.Left - _btnClip.Width, y);
        _btnPin.Location = new Point(_btnClip.Left - _btnPin.Width, y);
        _btnSettings.Location = new Point(_btnPin.Left - _btnSettings.Width, y);
    }

    void DrawListItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool current = e.Index == _index;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(selected ? Color.FromArgb(80, 80, 80) : PanelBack))
            e.Graphics.FillRectangle(bg, e.Bounds);
        var color = current ? Accent : Color.Gainsboro;
        var text = $"{e.Index + 1}. {Path.GetFileName(_items[e.Index])}";

        // 오른쪽 끝에 재생시간(아직 모르면 비워 둠), 파일명은 그 앞까지만
        var durText = _settings.Durations.TryGetValue(_items[e.Index], out var dur) && dur > 0 ? Fmt(dur) : "";
        int durWidth = durText.Length == 0 ? 0 : TextRenderer.MeasureText(durText, _listBox.Font).Width + S(10);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, text, _listBox.Font,
            new Rectangle(e.Bounds.X + S(6), e.Bounds.Y, Math.Max(0, e.Bounds.Width - S(6) - durWidth), e.Bounds.Height),
            color, flags | TextFormatFlags.EndEllipsis);
        if (durWidth > 0)
            TextRenderer.DrawText(e.Graphics, durText, _listBox.Font,
                new Rectangle(e.Bounds.Right - durWidth, e.Bounds.Y, durWidth - S(6), e.Bounds.Height),
                current ? Accent : Color.Silver, flags | TextFormatFlags.Right);
    }

    readonly ToolTip _listTip = new() { InitialDelay = 0, ReshowDelay = 0, AutoPopDelay = 10000, ShowAlways = true };
    int _tipIndex = -1;

    void UpdateListTip(Point p)
    {
        int i = _listBox.IndexFromPoint(p);
        // 항목이 없는 빈 영역이거나 마우스가 항목 영역 밖이면 숨김
        if (i < 0 || i >= _items.Count || !_listBox.GetItemRectangle(i).Contains(p))
        {
            if (_tipIndex != -1) { _tipIndex = -1; _listTip.Hide(_listBox); }
            return;
        }
        if (i == _tipIndex) return; // 같은 항목 위에서는 다시 띄우지 않음 (깜박임 방지)
        _tipIndex = i;

        // 목록에 그려지는 것과 같은 계산으로, 실제로 잘려 보일 때만 표시
        var name = Path.GetFileName(_items[i]);
        var text = $"{i + 1}. {name}";
        var durText = _settings.Durations.TryGetValue(_items[i], out var dur) && dur > 0 ? Fmt(dur) : "";
        int durWidth = durText.Length == 0 ? 0 : TextRenderer.MeasureText(durText, _listBox.Font).Width + S(10);
        int avail = _listBox.ClientSize.Width - S(6) - durWidth;
        if (TextRenderer.MeasureText(text, _listBox.Font).Width <= avail)
        {
            _listTip.Hide(_listBox);
            return;
        }
        var rect = _listBox.GetItemRectangle(i);
        _listTip.Show(name, _listBox, rect.Left + S(8), rect.Bottom + S(2), 10000);
    }

    // ------------------------------------------------------------ 재생목록 재생시간 (ffprobe)

    bool _probing;

    // 시간을 모르는 항목들을 하나씩 ffprobe로 읽어 목록에 채움. ffprobe가 없으면 재생할 때 mpv가 알려주는 값으로만 채워짐
    async void ProbeDurationsAsync()
    {
        if (_probing) return;
        var ffmpeg = ResolveFfmpeg();
        var ffprobe = ffmpeg == null ? null : Path.Combine(Path.GetDirectoryName(ffmpeg) ?? "", "ffprobe.exe");
        if (ffprobe == null || !File.Exists(ffprobe)) return;

        _probing = true;
        try
        {
            while (!IsDisposed)
            {
                var path = _items.FirstOrDefault(p => !_settings.Durations.ContainsKey(p));
                if (path == null) break;
                double sec = await Task.Run(() => ReadDuration(ffprobe, path));
                _settings.Durations[path] = sec; // 읽지 못한 파일은 0으로 기록해 다시 시도하지 않음
                _listBox.Invalidate();
            }
        }
        finally { _probing = false; }
    }

    static double ReadDuration(string ffprobe, string path)
    {
        try
        {
            using var p = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ffprobe, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                    ArgumentList = { "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path }
                }
            };
            p.Start();
            var output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } return 0; }
            return double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
        }
        catch { return 0; }
    }

    void ToggleList() => SetListVisible(!_listPanel.Visible);

    void SetListVisible(bool visible)
    {
        SetPanelVisible(_listPanel, visible);
        _btnList.ForeColor = visible ? Accent : Color.White;
    }

    // 종료 전의 재생목록(존재하는 파일만)·Clip 구간 목록을 복원. 재생은 시작하지 않고 마지막 재생 항목만 표시
    void RestoreLists()
    {
        foreach (var path in _settings.Playlist.Where(File.Exists))
        {
            if (_items.Contains(path, StringComparer.OrdinalIgnoreCase)) continue;
            _items.Add(path);
            _listBox.Items.Add(path);
            _baseOrder.Add(path);
        }
        // 파일이 사라져 번호가 어긋날 수 있으므로 마지막 재생 파일의 경로로 위치를 다시 찾음
        if (_settings.PlaylistIndex >= 0 && _settings.PlaylistIndex < _settings.Playlist.Count)
        {
            int i = _items.FindIndex(x => string.Equals(x, _settings.Playlist[_settings.PlaylistIndex], StringComparison.OrdinalIgnoreCase));
            if (i >= 0) { _index = i; _listBox.SelectedIndex = i; }
        }
        _sortMode = Enum.IsDefined(typeof(SortMode), _settings.PlaylistSort) ? (SortMode)_settings.PlaylistSort : SortMode.None;
        MarkSortButton();
        _clip.SetRows(_settings.ClipRows);
        ProbeDurationsAsync();
    }

    // 패널 열림 상태 복원: 창 폭은 패널 폭만큼 함께 늘어나 영상 영역 크기는 그대로
    void RestorePanels()
    {
        if (_settings.ListOpen) SetListVisible(true);
        if (_settings.ClipOpen) SetClipVisible(true);
    }

    // 사이드 패널(재생목록/Clip)을 열고 닫을 때 영상 영역 크기가 변하지 않도록 창 폭을 패널 폭만큼 함께 늘리고 줄임.
    // 최대화 상태에서는 창을 늘릴 수 없으므로 기존처럼 영상 영역이 줄어듦.
    void SetPanelVisible(Control panel, bool show)
    {
        if (panel.Visible == show) return;
        bool resizeWindow = WindowState == FormWindowState.Normal && !_fullscreen;
        int delta = panel.Width;
        SuspendLayout();
        panel.Visible = show;
        if (resizeWindow)
        {
            Width += show ? delta : -delta;
            // 화면 오른쪽을 넘으면 왼쪽으로 밀어서 패널이 화면 안에 들어오게 함
            var wa = Screen.FromControl(this).WorkingArea;
            if (Right > wa.Right) Left = Math.Max(wa.Left, wa.Right - Width);
        }
        ResumeLayout(true);
    }

    // Activate()만으로는 전경 창 제한에 막힐 수 있어 드롭 처리가 끝난 뒤 한 번 더 시도
    void ActivateAfterDrop()
    {
        Activate();
        BeginInvoke(() =>
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            ActiveControl = null;
        });
    }

    // 환경설정 창: 확인을 누르면 바로 적용하고 저장 (프로그램을 끝내기 전에 저장이 날아가지 않도록)
    void ShowSettings()
    {
        using var dlg = new SettingsForm(_settings.SkipShort, _settings.SkipMedium, _settings.SkipLong, _settings.SimilarPrefix);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _settings.SkipShort = dlg.SkipShort;
        _settings.SkipMedium = dlg.SkipMedium;
        _settings.SkipLong = dlg.SkipLong;
        _settings.SimilarPrefix = dlg.SimilarPrefix;
        _settings.Save();
        ShowOsd($"건너뛰기: ←/→ {_settings.SkipShort}초, Ctrl {_settings.SkipMedium}초, Shift {_settings.SkipLong}초", 2500);
    }

    // 항상 위: 켜지면 버튼이 강조색
    void SetTopMost(bool on)
    {
        TopMost = on;
        _btnPin.ForeColor = on ? Accent : Color.White;
    }

    void ToggleClip() => SetClipVisible(!_clip.Visible);

    void SetClipVisible(bool visible)
    {
        SetPanelVisible(_clip, visible);
        _btnClip.ForeColor = visible ? Accent : Color.White;
    }

    // ffmpeg 위치: 플레이어 설정 → Hunbjter 환경설정 → 실행 파일 옆 → PATH 순으로 탐색
    string? ResolveFfmpeg()
    {
        var candidates = new List<string?>
        {
            _settings.FfmpegPath, ReadHunbjterFfmpeg(),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),          // 실행 파일 옆 (자동 설치 위치)
            Path.Combine(FfmpegInstaller.AppDataDir, "ffmpeg.exe"),        // 실행 파일 옆에 쓸 수 없을 때의 자동 설치 위치
        };
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try { candidates.Add(Path.Combine(dir.Trim(), "ffmpeg.exe")); } catch { /* 잘못된 PATH 항목 무시 */ }
        }
        return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
    }

    bool _ffmpegDownloading;

    /// <summary>
    /// ffmpeg.exe 경로를 돌려줌. 어디에서도 찾을 수 없으면 실행 파일 옆에 내려받아 설치(진행 상황은 화면 왼쪽 위에 표시).
    /// 내려받기에 실패하면 null
    /// </summary>
    async Task<string?> EnsureFfmpegAsync()
    {
        var found = ResolveFfmpeg();
        if (found != null) return found;
        if (_ffmpegDownloading) { ShowOsd("ffmpeg를 이미 내려받는 중입니다...", 2500); return null; }

        _ffmpegDownloading = true;
        try
        {
            ShowOsd("ffmpeg.exe를 찾을 수 없어 내려받습니다...", 4000);
            double lastPct = -1; long lastShown = 0;
            // 진행률을 너무 자주 그리지 않도록 1% 이상 변했거나 1초 이상 지났을 때만 갱신 (Progress<T>는 이 UI 스레드로 전달됨)
            var progress = new Progress<(long Done, long Total)>(p =>
            {
                double pct = p.Total > 0 ? p.Done * 100.0 / p.Total : -1;
                long now = Environment.TickCount64;
                if (Math.Abs(pct - lastPct) < 1 && now - lastShown < 1000) return;
                lastPct = pct; lastShown = now;
                _clip.SetBusyText(p.Total > 0 ? $"내려받는 중 {pct:0}%" : $"내려받는 중 {p.Done / 1048576}MB");
                ShowOsd(p.Total > 0
                    ? $"ffmpeg 내려받는 중 {pct:0}%  ({p.Done / 1048576}/{p.Total / 1048576}MB)"
                    : $"ffmpeg 내려받는 중 {p.Done / 1048576}MB", 2000);
            });

            var path = await FfmpegInstaller.DownloadAsync(progress);
            ShowOsd($"ffmpeg 설치 완료: {Path.GetDirectoryName(path)}", 5000);
            ProbeDurationsAsync(); // ffprobe가 생겼으니 재생목록 재생시간도 채움
            return path;
        }
        catch (Exception ex)
        {
            ShowError($"ffmpeg를 내려받지 못했습니다.\n{ex.Message}\n인터넷 연결을 확인하거나, ffmpeg.exe를 HunbjPlayer.exe와 같은 폴더에 직접 넣어 주세요.", 12000);
            return null;
        }
        finally { _ffmpegDownloading = false; }
    }

    static string? ReadHunbjterFfmpeg()
    {
        try
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hunbjter", "login-settings.json");
            if (!File.Exists(file)) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.TryGetProperty("FfmpegPath", out var p) ? p.GetString() : null;
        }
        catch { return null; }
    }

    // ------------------------------------------------------------ 구간반복 (A-B loop)

    double _loopA = -1, _loopB = -1; // 초. 미설정은 음수

    static string Inv(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    // Ctrl+[ 시작 / Ctrl+] 종료 / Ctrl+\ 해제 / Ctrl+Shift+\ 반복 구간을 Clip 시작·종료 입력칸으로 보냄
    void LoopHotkey(char key)
    {
        if (_mpv == null || _mpv.Idle) return;
        double cur = _mpv.TimePos;
        if (key != '|') _segA = _segB = -1; // 구간반복을 직접 지정/해제하면 편집 구간 재생 상태는 끝
        switch (key)
        {
            case '[':
                _loopA = cur;
                if (_loopB >= 0 && _loopB <= _loopA) _loopB = -1; // 끝이 시작보다 앞서면 끝은 다시 지정
                ShowOsd($"구간반복 시작  {Fmt(_loopA)}");
                break;
            case ']':
                if (_loopA < 0) _loopA = 0;
                if (cur <= _loopA) { ShowOsd("종료는 시작보다 뒤여야 합니다"); return; }
                _loopB = cur;
                ShowOsd($"구간반복  {Fmt(_loopA)} ~ {Fmt(_loopB)}");
                break;
            case '\\':
                _loopA = _loopB = -1;
                ShowOsd("구간반복 해제");
                break;
            case '|':
                if (_loopA < 0 || _loopB <= _loopA) { ShowOsd("설정된 구간반복이 없습니다"); return; }
                _clip.SetStart(_loopA, PlayingPath());
                _clip.SetEnd(_loopB, PlayingPath());
                ShowOsd($"편집 구간 설정  {Fmt(_loopA)} ~ {Fmt(_loopB)}");
                return;
        }
        ApplyLoop();
        // mpv는 설정 시점에 이미 B 이후(또는 B 지점)에 있으면 반복하지 않으므로, 구간이 완성되면 A부터 다시 재생
        if (_loopA >= 0 && _loopB > _loopA) _mpv.SeekExact(_loopA);
    }

    // mpv의 ab-loop-a/b 속성과 시크바 표시를 현재 값에 맞춤 (둘 다 있어야 실제로 반복)
    void ApplyLoop()
    {
        if (_mpv == null) return;
        _mpv.SetProperty("ab-loop-a", _loopA >= 0 ? Inv(_loopA) : "no");
        _mpv.SetProperty("ab-loop-b", _loopB >= 0 && _loopA >= 0 ? Inv(_loopB) : "no");
        double len = _mpv.Duration;
        _seek.SetLoop(len > 0 && _loopA >= 0 ? _loopA / len : -1, len > 0 && _loopB >= 0 ? _loopB / len : -1);
        _fsBar.Seek.SetLoop(len > 0 && _loopA >= 0 ? _loopA / len : -1, len > 0 && _loopB >= 0 ? _loopB / len : -1);
    }

    // [ ] \ : 현재 재생 시간 기준으로 클립 구간 시작/종료 설정, 추가
    void ClipHotkey(char key)
    {
        if (_mpv == null || _mpv.Idle) return;
        double cur = Math.Max(0, _mpv.TimePos);
        switch (key)
        {
            case '[':
                _clip.SetStart(cur, PlayingPath());
                ShowOsd($"시작 {Fmt(cur)}");
                break;
            case ']':
                _clip.SetEnd(cur, PlayingPath());
                ShowOsd($"종료 {Fmt(cur)}");
                break;
            case '\\':
                _clip.Add();
                break;
        }
    }

    void OpenDialog()
    {
        using var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "동영상/오디오|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.ts;*.webm;*.m4v;*.mp3;*.m4a;*.flac;*.wav|모든 파일|*.*"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) AddAndPlay(dlg.FileNames);
    }

    /// <summary>
    /// 재생창에 드롭: 현재 재생목록을 모두 비우고 드롭한 파일(폴더는 안의 동영상)만 목록에 넣어 첫 파일을 재생.
    /// 드롭한 것에서 재생할 파일을 하나도 찾지 못하면 기존 목록은 그대로 둠.
    /// </summary>
    void ReplaceAndPlay(string[] files)
    {
        if (!ExpandPaths(files).Any()) return;

        _items.Clear();
        _listBox.Items.Clear();
        _baseOrder.Clear();
        _index = -1;
        AddAndPlay(files);
        // 재생목록 패널이 닫혀 있어도 몇 개가 들어갔는지 알 수 있게 안내
        ShowOsd($"재생목록 {_items.Count}개 (첫 파일 재생)");
    }

    /// <summary>
    /// 재생창에 파일 1개를 드롭: 같은 폴더에서 파일명 앞부분(환경설정의 글자 수)이 같은 동영상을 모두 찾아
    /// 이름순으로 재생목록을 새로 채우고, 드롭한 파일부터 재생
    /// </summary>
    void ReplaceWithSimilar(string file)
    {
        var name = Path.GetFileName(file);
        var prefix = name[..Math.Min(_settings.SimilarPrefix, Path.GetFileNameWithoutExtension(name).Length)];
        string[] similar;
        try
        {
            similar = Directory.GetFiles(Path.GetDirectoryName(file)!)
                .Where(f => VideoExtensions.Contains(Path.GetExtension(f)) && Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        catch (Exception) { similar = Array.Empty<string>(); } // 폴더를 읽지 못하면 드롭한 파일만
        if (!similar.Contains(file, StringComparer.OrdinalIgnoreCase)) similar = similar.Append(file).ToArray();
        Array.Sort(similar, StrCmpLogicalW);

        _items.Clear();
        _listBox.Items.Clear();
        _baseOrder.Clear();
        _index = -1;
        AddFiles(similar, out _);
        int at = _items.FindIndex(x => string.Equals(x, file, StringComparison.OrdinalIgnoreCase));
        if (at >= 0) PlayAt(at);
        ShowOsd(similar.Length > 1 ? $"비슷한 이름 {similar.Length}개 ('{prefix}…') 재생목록에 추가" : "재생목록 1개 (비슷한 이름의 파일 없음)");
    }

    /// <summary>목록에 추가하고 첫 파일을 재생</summary>
    void AddAndPlay(string[] files)
    {
        int first = AddFiles(files, out _);
        if (first >= 0) PlayAt(first);
    }

    /// <summary>재생목록에 추가만 하고 재생은 하지 않음</summary>
    // 드롭으로 무엇이 들어왔는지 마지막 1회분을 파일에 기록 (예: 다른 파일 관리자에서 끌 때 항목이 의도와 다르게 올 때 원인 확인용)
    static void LogDrop(DragEventArgs e, string[] files, bool addOnly)
    {
        try
        {
            var lines = new List<string>
            {
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  zone={(addOnly ? "add-only" : "play")}  items={files.Length}",
                "formats: " + string.Join(", ", e.Data?.GetFormats() ?? Array.Empty<string>()),
            };
            foreach (var f in files)
                lines.Add($"  [{(Directory.Exists(f) ? "DIR " : File.Exists(f) ? "FILE" : "????")}] {f}");
            Directory.CreateDirectory(PlayerSettings.Dir);
            File.WriteAllLines(Path.Combine(PlayerSettings.Dir, "last-drop.txt"), lines);
        }
        catch { /* 기록 실패는 무시 */ }
    }

    void AddToList(string[] files)
    {
        // 재생목록에 드롭한 파일은 정렬 기준을 무시하고 맨 아래에 붙임 (그래서 목록은 더 이상 정렬 상태가 아님)
        if (AddFiles(files, out int added, resort: false) < 0) return;
        if (added > 0 && _sortMode != SortMode.None) { _sortMode = SortMode.None; MarkSortButton(); }
        ShowOsd(added > 0 ? $"재생목록에 {added}개 추가" : "이미 재생목록에 있습니다");
    }

    /// <summary>
    /// 실행 인자로 받은 파일/폴더 경로를 해석. 여러 형태를 허용함:
    ///  - 전체 경로 또는 현재 폴더 기준 상대 경로 (따옴표 유무 무관), 인자가 여러 개면 모두 대상
    ///  - "경로" "파일명" 두 개로 나눠 준 경우 (폴더 + 그 안의 파일명)
    ///  - 따옴표 없이 공백이 들어간 경로가 여러 인자로 쪼개져 온 경우 (공백으로 이어 붙여서 존재하면 사용)
    /// 존재하지 않는 인자는 무시함.
    /// </summary>
    static string[] ResolveStartPaths(string[] args)
    {
        string? Existing(string raw)
        {
            try
            {
                var full = Path.GetFullPath(raw.Trim().Trim('"'));
                return File.Exists(full) || Directory.Exists(full) ? full : null;
            }
            catch { return null; } // 경로로 쓸 수 없는 문자열
        }

        var list = args.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
        if (list.Length == 0) return Array.Empty<string>();

        // 폴더 + 파일명 두 개로 나눠 준 경우
        if (list.Length == 2 && Directory.Exists(Existing(list[0]) ?? "") && Existing(Path.Combine(list[0].Trim().Trim('"'), list[1].Trim().Trim('"'))) is { } combined)
            return new[] { combined };

        var found = list.Select(Existing).OfType<string>().ToArray();
        if (found.Length == list.Length) return found;

        // 일부가 존재하지 않으면, 따옴표 없는 경로가 공백에서 쪼개졌을 가능성 → 이어 붙여 본다
        if (list.Length > 1 && Existing(string.Join(" ", list)) is { } joined) return new[] { joined };
        return found;
    }

    // 폴더 안에서 동영상으로 인정하는 확장자 (직접 끌어다 놓은 개별 파일은 확장자와 상관없이 허용)
    static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".ts", ".webm", ".m4v", ".mpg", ".mpeg", ".m2ts", ".mts",
        ".3gp", ".ogv", ".vob", ".rmvb", ".asf", ".divx",
    };

    [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int StrCmpLogicalW(string a, string b);

    /// <summary>
    /// 파일은 그대로, 폴더는 안의 동영상 파일을 하위 폴더까지 모두 펼쳐서 돌려줌.
    /// 한 폴더 안에서는 탐색기와 같은 자연 정렬(file2 &lt; file10), 폴더 자체의 파일이 하위 폴더 파일보다 먼저 옴.
    /// </summary>
    static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path)) { yield return path; continue; }
            if (!Directory.Exists(path)) continue;
            foreach (var f in EnumerateVideos(path)) yield return f;
        }
    }

    static IEnumerable<string> EnumerateVideos(string dir)
    {
        string[] files, subdirs;
        try
        {
            files = Directory.GetFiles(dir).Where(f => VideoExtensions.Contains(Path.GetExtension(f))).ToArray();
            subdirs = Directory.GetDirectories(dir);
        }
        catch (Exception) { yield break; } // 접근 권한이 없는 폴더는 건너뜀
        Array.Sort(files, StrCmpLogicalW);
        Array.Sort(subdirs, StrCmpLogicalW);
        foreach (var f in files) yield return f;
        foreach (var d in subdirs)
            foreach (var f in EnumerateVideos(d)) yield return f;
    }

    // 중복은 건너뛰고 새로 추가한 개수를 돌려줌. 반환값은 첫 파일의 목록 위치(없으면 -1)
    int AddFiles(string[] files, out int added, bool resort = true)
    {
        int first = -1;
        added = 0;
        foreach (var f in ExpandPaths(files))
        {
            int i = _items.FindIndex(x => string.Equals(x, f, StringComparison.OrdinalIgnoreCase));
            if (i < 0) { _items.Add(f); _baseOrder.Add(f); _listBox.Items.Add(f); i = _items.Count - 1; added++; }
            if (first < 0) first = i;
        }
        if (added > 0)
        {
            // 정렬 기준이 있으면 새 파일까지 포함해 다시 정렬 (첫 파일 위치는 경로로 다시 찾음)
            if (resort && _sortMode != SortMode.None && first >= 0)
            {
                var firstPath = _items[first];
                SortList(_sortMode, quiet: true);
                first = _items.FindIndex(x => string.Equals(x, firstPath, StringComparison.OrdinalIgnoreCase));
            }
            ProbeDurationsAsync();
        }
        return first;
    }

    // 목록에 추가만 하는 영역: 재생목록 패널, Clip 패널, 영상 창 상단 1/4(최소 80px)
    bool IsAddOnlyZone(Point screen)
    {
        if (_listPanel.Visible && _listPanel.RectangleToScreen(_listPanel.ClientRectangle).Contains(screen)) return true;
        if (_clip.Visible && _clip.RectangleToScreen(_clip.ClientRectangle).Contains(screen)) return true;
        var v = _video.RectangleToScreen(_video.ClientRectangle);
        return v.Contains(screen) && screen.Y < v.Top + Math.Max(S(80), v.Height / 4);
    }

    long _dropHintAt;
    bool _dropHintAdd;

    void OnFileDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true) return;
        e.Effect = DragDropEffects.Copy;

        // 드롭하면 어떻게 되는지 영상 위에 안내 (영역이 바뀌거나 1초마다 갱신)
        bool add = IsAddOnlyZone(new Point(e.X, e.Y));
        long now = Environment.TickCount64;
        if (add != _dropHintAdd || now - _dropHintAt > 1000)
        {
            _dropHintAdd = add;
            _dropHintAt = now;
            ShowOsd(add ? "놓으면 재생목록에 추가" : "놓으면 재생");
        }
    }

    // 지금 재생을 사용자가 직접 시작했는지(true) 자동으로 다음 파일로 넘어간 것인지(false). 실패 안내 방식이 다름
    bool _playInteractive = true;

    /// <param name="interactive">
    /// true: 더블클릭/Enter/Space 등 사용자가 직접 시작 → 실패하면 원인을 창으로 알려 줌.
    /// false: 앞 파일이 끝나 자동으로 다음 파일 → 창을 계속 띄우지 않도록 화면 안내만 하고 다음 파일로 넘어감
    /// </param>
    void PlayAt(int index, bool interactive = true, double? start = null)
    {
        if (_mpv == null || index < 0 || index >= _items.Count) return;
        var path = _items[index];

        // 목록에는 있지만 디스크에 파일이 없는 경우 (삭제/이동/드라이브 분리 등)
        if (!File.Exists(path))
        {
            // 메시지 박스 대신 영상 위 알림으로 표시 (자동으로 넘어가는 중이면 짧게)
            ShowError(interactive
                ? $"파일을 찾을 수 없습니다.\n{path}\n삭제되었거나 이동했거나 드라이브가 연결되어 있지 않을 수 있습니다. 목록에서 빼려면 항목을 선택하고 Del 키를 누르세요."
                : $"파일 없음: {Path.GetFileName(path)}", interactive ? 9000 : 3000);
            return;
        }

        _playInteractive = interactive;
        _index = index;
        _seekTarget = -1;
        ResetLoop(); // 다른 파일로 바뀌면 구간반복 해제
        double from = start ?? 0;
        if (start == null)
        {
            // 편집 구간 더블클릭으로 벗어났던 원래 파일을 직접 다시 열면 재생 중이던 지점부터. 다른 파일을 열면 기억 지점은 버림
            if (interactive && string.Equals(_resumePath, path, StringComparison.OrdinalIgnoreCase)) from = _resumePos;
            _resumePath = null;
        }
        _mpv.Load(path, from);
        _mpv.Pause = false;
        Text = $"{Path.GetFileName(path)} - HunbjPlayer";
        // 다중 선택 상태를 건드리지 않고, 재생 항목이 화면 밖이면 보이는 위치로 스크롤만 함
        int visible = Math.Max(1, _listBox.ClientSize.Height / Math.Max(1, _listBox.ItemHeight));
        if (index < _listBox.TopIndex || index >= _listBox.TopIndex + visible)
            _listBox.TopIndex = Math.Clamp(index - visible / 2, 0, Math.Max(0, _items.Count - 1));
        _listBox.Invalidate();
    }

    void OnEnded() => PlayNextExisting(_index + 1);

    // ------------------------------------------------------------ 반복재생 (R)

    bool _repeat;
    double _stopAt = -1; // 구간 더블클릭 재생 시 이 시간에서 일시정지 (미사용은 음수)

    // 기본은 현재 파일 반복. 구간반복(A-B)이 설정돼 있으면 mpv가 그 구간을 우선 반복하므로 구간이 반복의 처음과 끝이 됨
    void ToggleRepeat()
    {
        if (_mpv == null) return;
        _repeat = !_repeat;
        _stopAt = -1;
        _mpv.SetProperty("loop-file", _repeat ? "inf" : "no");
        // 편집 구간을 더블클릭해 재생하던 중이면: 켜면 그 구간을 반복, 끄면 다시 구간 끝에서 멈춤
        if (_segA >= 0 && _segB > _segA && !_mpv.Idle)
        {
            double cur = _mpv.TimePos;
            if (_repeat)
            {
                _loopA = _segA; _loopB = _segB;
                ApplyLoop();
                if (cur < _segA || cur >= _segB - 0.05) _mpv.SeekExact(_segA); // 구간 밖(끝에서 멈춘 상태 등)이면 구간 처음부터
                _mpv.Pause = false;
            }
            else
            {
                _loopA = _loopB = -1;
                ApplyLoop();
                if (cur < _segB) _stopAt = _segB;
            }
        }
        bool ab = _loopA >= 0 && _loopB > _loopA;
        ShowOsd(_repeat ? (ab ? $"반복재생 켜짐 (구간 {Fmt(_loopA)} ~ {Fmt(_loopB)})" : "반복재생 켜짐 (현재 파일)") : "반복재생 꺼짐");
    }

    // 편집(Clip) 목록 더블클릭: 그 구간의 파일을 시작 시간부터 재생
    // 편집 구간 재생 전에 보던 파일과 지점 (재생목록에서 그 파일을 더블클릭하면 여기서 이어 재생)
    string? _resumePath;
    double _resumePos;
    double _segA = -1, _segB = -1; // 더블클릭으로 재생 중인 편집 구간 (반복재생을 나중에 켜거나 꺼도 이 구간 기준)

    void PlaySegment(string file, double start, double end)
    {
        if (_mpv == null) return;
        // 구간을 연달아 더블클릭해도 처음 보던 지점을 유지 (구간 재생 중 위치로 덮어쓰지 않음)
        if (_resumePath == null && !_mpv.Idle && PlayingPath() is { } playing)
        {
            _resumePath = playing;
            _resumePos = _mpv.TimePos;
        }
        _stopAt = -1;
        if (!File.Exists(file)) { ShowError($"파일을 찾을 수 없습니다.\n{file}", 4000); return; }
        if (!_mpv.Idle && string.Equals(PlayingPath(), file, StringComparison.OrdinalIgnoreCase))
        {
            _mpv.SeekExact(start);
            _mpv.Pause = false;
        }
        else
        {
            int idx = _items.FindIndex(p => string.Equals(p, file, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) PlayAt(idx, interactive: true, start);
            else { ResetLoop(); _playInteractive = true; _mpv.Load(file, start); _mpv.Pause = false; Text = $"{Path.GetFileName(file)} - HunbjPlayer"; }
        }
        // 반복재생이 켜져 있으면 그 구간을 반복, 꺼져 있으면 종료 시간에서 멈춤
        if (end > start) { _segA = start; _segB = end; }
        if (_repeat && end > start)
        {
            _loopA = start; _loopB = end;
            ApplyLoop();
        }
        else if (end > start) _stopAt = end;
        FocusVideo(clicked: false);
        ShowOsd($"구간 재생  {Fmt(start)} ~ {Fmt(end)}");
    }

    void ResetLoop()
    {
        _stopAt = -1;
        _segA = _segB = -1;
        _loopA = _loopB = -1;
        _seek.SetLoop(-1, -1);
        _fsBar.Seek.SetLoop(-1, -1);
        _mpv?.SetProperty("ab-loop-a", "no");
        _mpv?.SetProperty("ab-loop-b", "no");
    }

    // 다음 파일로 자동 이동: 디스크에 없는 항목은 건너뛰고 처음 만나는 존재하는 파일을 재생
    void PlayNextExisting(int from)
    {
        int skipped = 0;
        for (int i = from; i < _items.Count; i++)
        {
            if (File.Exists(_items[i])) { PlayAt(i, interactive: false); if (skipped > 0) ShowOsd($"없는 파일 {skipped}개를 건너뛰었습니다", 3000); return; }
            skipped++;
        }
        if (skipped > 0) ShowOsd($"이어서 재생할 파일이 없습니다 (없는 파일 {skipped}개)", 3000);
    }

    // mpv가 파일을 열지 못했거나 재생 중 오류로 멈춤 (손상된 파일, 아직 녹화 중인 0바이트 파일, 사용 중인 파일 등)
    void OnLoadFailed(string reason)
    {
        var path = CurrentPath;
        var name = path == null ? "" : Path.GetFileName(path);
        if (_playInteractive)
        {
            ShowError($"재생하지 못했습니다.\n{path}\n원인: {reason}", 10000);
            return;
        }
        // 자동으로 넘어간 파일이 실패하면 안내만 하고 다음 파일로
        ShowError($"재생 실패: {name}\n원인: {reason}", 4000);
        PlayNextExisting(_index + 1);
    }

    int[]? _lbDragSet;
    Point _lbDragStart;

    // _items 기준으로 ListBox 내용을 다시 채우고, 주어진 파일들을 선택 상태로 복원 (스크롤 위치 유지)
    void SyncListBox(ICollection<string>? selectPaths = null)
    {
        int top = _listBox.TopIndex;
        _listBox.BeginUpdate();
        _listBox.Items.Clear();
        foreach (var path in _items) _listBox.Items.Add(path);
        if (selectPaths is { Count: > 0 })
            for (int i = 0; i < _items.Count; i++)
                if (selectPaths.Contains(_items[i], StringComparer.OrdinalIgnoreCase)) _listBox.SetSelected(i, true);
        if (_items.Count > 0) _listBox.TopIndex = Math.Clamp(top, 0, _items.Count - 1);
        _listBox.EndUpdate();
    }

    string? CurrentPath => _index >= 0 && _index < _items.Count ? _items[_index] : null;

    // 지금 실제로 재생 중인 파일 (mpv가 열고 있는 경로). 목록 위치(_index)와 어긋날 일이 없도록 mpv에 직접 물어봄
    string? PlayingPath()
    {
        var path = _mpv?.GetString("path");
        return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : CurrentPath;
    }

    // 선택한 여러 항목을 target 위치로 함께 이동 (서로의 상대 순서 유지). 아래로 옮기면 target 항목 뒤에, 위로 옮기면 앞에 놓임.
    // 재생 중인 항목(_index)은 경로로 다시 찾아 계속 같은 파일을 가리킴
    void MoveItems(int[] indices, int target)
    {
        var set = indices.Where(i => i >= 0 && i < _items.Count).Distinct().OrderBy(i => i).ToArray();
        if (set.Length == 0 || target < 0 || target >= _items.Count || set.Contains(target)) return;

        var current = CurrentPath;
        var moving = set.Select(i => _items[i]).ToList();
        int removedBefore = set.Count(i => i < target);
        bool down = target > set[0];
        foreach (var i in set.Reverse()) _items.RemoveAt(i);
        int insertAt = target - removedBefore + (down ? 1 : 0);
        _items.InsertRange(Math.Clamp(insertAt, 0, _items.Count), moving);

        _index = current == null ? -1 : _items.FindIndex(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase));
        _baseOrder = _items.ToList(); // 손으로 정한 순서를 "정렬없음" 기준으로 삼음
        _sortMode = SortMode.None;    // 직접 옮겼으므로 더 이상 정렬된 상태가 아님
        MarkSortButton();
        SyncListBox(moving);
    }

    // Shift+PgUp/PgDn: 선택한 항목들(다중 선택 가능)을 한 칸 위/아래로 이동. 떨어져 있는 항목은 각자 한 칸씩 움직임
    void MoveSelectedBy(int dir)
    {
        var sel = _listBox.SelectedIndices.Cast<int>().Where(i => i < _items.Count).ToHashSet();
        if (sel.Count == 0) return;
        if (dir < 0 ? sel.Min() == 0 : sel.Max() == _items.Count - 1) return; // 이미 맨 위/아래

        var current = CurrentPath;
        var moving = sel.Select(i => _items[i]).ToList();
        var order = dir < 0 ? sel.OrderBy(i => i) : sel.OrderByDescending(i => i);
        foreach (var i in order)
            (_items[i], _items[i + dir]) = (_items[i + dir], _items[i]);

        _index = current == null ? -1 : _items.FindIndex(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase));
        _baseOrder = _items.ToList();
        _sortMode = SortMode.None;
        MarkSortButton();
        SyncListBox(moving);
        // 움직인 항목이 화면 밖으로 나가면 따라가도록 스크롤
        int edge = dir < 0 ? sel.Min() + dir : sel.Max() + dir;
        int visible = Math.Max(1, _listBox.ClientSize.Height / Math.Max(1, _listBox.ItemHeight));
        if (edge < _listBox.TopIndex) _listBox.TopIndex = edge;
        else if (edge >= _listBox.TopIndex + visible) _listBox.TopIndex = edge - visible + 1;
    }

    // Shift+Del: 선택한 모든 항목의 실제 파일을 확인창 없이 바로 휴지통으로 보내고 목록에서도 뺌 (휴지통에서 복원 가능)
    void DeleteSelectedFile()
    {
        var paths = _listBox.SelectedIndices.Cast<int>().Where(i => i < _items.Count).Select(i => _items[i]).ToList();
        if (paths.Count == 0) return;

        // 재생 중인 파일은 mpv가 열고 있어 지워지지 않으므로 먼저 정지
        var current = CurrentPath;
        if (current != null && paths.Contains(current, StringComparer.OrdinalIgnoreCase)) _mpv?.Stop();

        var done = new List<string>();
        string? error = null;
        foreach (var path in paths)
        {
            try
            {
                // 정지 직후 파일 핸들이 풀릴 때까지 잠깐 재시도
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        if (File.Exists(path))
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        break;
                    }
                    catch (IOException) when (attempt < 10) { Thread.Sleep(100); }
                }
                done.Add(path);
            }
            catch (Exception ex) { error = $"{Path.GetFileName(path)}: {ex.Message}"; }
        }

        RemovePaths(done);
        if (error != null)
            ShowError($"일부 파일을 삭제하지 못했습니다.\n{error}", 9000);
        if (done.Count > 0)
            ShowOsd(done.Count == 1 ? $"휴지통으로 이동: {Path.GetFileName(done[0])}" : $"휴지통으로 이동: {done.Count}개");
    }

    // Del: 선택한 모든 항목을 목록에서만 뺌 (파일은 그대로)
    void RemoveSelected()
    {
        var paths = _listBox.SelectedIndices.Cast<int>().Where(i => i < _items.Count).Select(i => _items[i]).ToList();
        RemovePaths(paths);
    }

    // 목록에서 항목들을 빼고, 재생 중이던 항목이 포함되면 정지. 지운 자리 근처 항목을 선택
    void RemovePaths(IEnumerable<string> paths)
    {
        var remove = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        if (remove.Count == 0) return;

        int firstRemoved = _items.FindIndex(x => remove.Contains(x));
        var current = CurrentPath;
        if (current != null && remove.Contains(current)) { _mpv?.Stop(); current = null; }

        _items.RemoveAll(remove.Contains);
        _baseOrder.RemoveAll(remove.Contains);
        _index = current == null ? -1 : _items.FindIndex(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase));

        SyncListBox();
        if (_items.Count > 0) _listBox.SelectedIndex = Math.Clamp(firstRemoved, 0, _items.Count - 1);
    }

    // ------------------------------------------------------------ 정렬 (버튼을 누를 때 한 번만 정렬)

    // 이름순/최근순 각각 오름차순(↑)·내림차순(↓). 날짜 오름차순은 오래된 것이 위, 내림차순은 최근 것이 위
    enum SortMode { NameAsc, NameDesc, DateAsc, DateDesc, None }

    // 현재 정렬 기준. 파일을 새로 추가하면 이 기준으로 다시 정렬하고, 해당 버튼을 강조 표시함
    SortMode _sortMode = SortMode.None;

    void MarkSortButton()
    {
        foreach (var (b, m) in new[] { (_btnSortNameAsc, SortMode.NameAsc), (_btnSortNameDesc, SortMode.NameDesc),
                                       (_btnSortDateAsc, SortMode.DateAsc), (_btnSortDateDesc, SortMode.DateDesc), (_btnSortNone, SortMode.None) })
        {
            bool on = m == _sortMode;
            b.ForeColor = on ? Accent : Color.White;
            b.FlatAppearance.BorderColor = on ? Accent : Color.FromArgb(80, 80, 80);
        }
    }

    void SortList(SortMode mode, bool quiet = false)
    {
        _sortMode = mode;
        MarkSortButton();
        if (_items.Count == 0) return;
        var current = CurrentPath;
        var selected = _listBox.SelectedIndices.Cast<int>().Select(i => _items[i]).ToList();

        List<string> sorted = mode switch
        {
            // 파일 이름 기준 자연 정렬(file2 < file10). 같으면 전체 경로로 안정적으로 정리
            SortMode.NameAsc => _items.OrderBy(p => Path.GetFileName(p), Comparer<string>.Create(StrCmpLogicalW))
                                      .ThenBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
            SortMode.NameDesc => _items.OrderByDescending(p => Path.GetFileName(p), Comparer<string>.Create(StrCmpLogicalW))
                                       .ThenByDescending(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
            // 수정 날짜 기준 (읽을 수 없는 파일은 가장 오래된 것으로 취급). 같은 날짜는 이름순으로 안정적으로 정리
            SortMode.DateAsc => _items.OrderBy(p => ModifiedTime(p))
                                      .ThenBy(p => Path.GetFileName(p), Comparer<string>.Create(StrCmpLogicalW)).ToList(),
            SortMode.DateDesc => _items.OrderByDescending(p => ModifiedTime(p))
                                       .ThenByDescending(p => Path.GetFileName(p), Comparer<string>.Create(StrCmpLogicalW)).ToList(),
            // 정렬없음: 추가한(또는 직접 정한) 기준 순서로 되돌림
            _ => _baseOrder.Where(p => _items.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList(),
        };
        // 안전장치: 기준 순서에 빠진 항목이 있으면 맨 뒤에 붙임
        foreach (var p in _items) if (!sorted.Contains(p, StringComparer.OrdinalIgnoreCase)) sorted.Add(p);

        _items.Clear();
        _items.AddRange(sorted);
        _index = current == null ? -1 : _items.FindIndex(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase));
        SyncListBox(selected);
        if (quiet) return;
        ShowOsd(mode switch
        {
            SortMode.NameAsc => "정렬: 이름 오름차순 ↑",
            SortMode.NameDesc => "정렬: 이름 내림차순 ↓",
            SortMode.DateAsc => "정렬: 날짜 오름차순 ↑ (오래된 파일이 위)",
            SortMode.DateDesc => "정렬: 날짜 내림차순 ↓ (최근 파일이 위)",
            _ => "정렬 없음 (추가한 순서)",
        });
    }

    static DateTime ModifiedTime(string path) => File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.MinValue;

    // 2줄 배치: 윗줄 [이름순 ↑][이름순 ↓], 아랫줄 [최근순 ↑][최근순 ↓], 오른쪽 열에 [정렬없음]을 두 줄 높이로
    void LayoutSortButtons()
    {
        int pad = S(8), gap = S(6);
        int totalW = _listBottom.ClientSize.Width - pad * 2;
        int totalH = _listBottom.ClientSize.Height - pad * 2;
        int colW = Math.Max(10, (totalW - gap * 2) / 3);
        int rowH = Math.Max(10, (totalH - gap) / 2);
        int x1 = pad, x2 = pad + colW + gap, x3 = pad + (colW + gap) * 2, y1 = pad, y2 = pad + rowH + gap;
        _btnSortNameAsc.SetBounds(x1, y1, colW, rowH);
        _btnSortNameDesc.SetBounds(x2, y1, colW, rowH);
        _btnSortDateAsc.SetBounds(x1, y2, colW, rowH);
        _btnSortDateDesc.SetBounds(x2, y2, colW, rowH);
        _btnSortNone.SetBounds(x3, y1, Math.Max(10, totalW - (x3 - pad)), Math.Max(10, totalH));
    }

    void TogglePlay()
    {
        if (_mpv == null) return;
        if (_index < 0) { if (_items.Count > 0) PlayAt(0); else OpenDialog(); return; }
        if (_mpv.Idle) PlayAt(_index);
        else
        {
            bool pause = !_mpv.Pause;
            _mpv.Pause = pause;
            // 일시정지 중에는 상단에 현재 시간/진행률을 계속 표시, 다시 재생하면 지움
            if (pause) _mpv.ShowText($"일시정지  {TimeStatus(_mpv.TimePos, _mpv.Duration)}", 24 * 3600 * 1000);
            else _mpv.ShowText("", 1);
        }
        UpdateState();
    }

    void ToggleMute()
    {
        if (_mpv == null) return;
        _muted = !_muted;
        _mpv.Mute = _muted;
        ShowOsd(_muted ? "음소거" : $"음소거 해제  볼륨 {_mpv.Volume}%");
    }

    void Seek(double deltaSec)
    {
        if (_mpv == null || _mpv.Idle) return;
        _stopAt = -1; // 직접 이동하면 구간 끝 자동 정지는 해제
        double len = _mpv.Duration;
        // 실제 이동은 mpv가 키프레임 단위로 처리. 표시용 목표 시간은 연속 입력을 누적해 계산
        double baseT = SeekPending ? _seekTarget : _mpv.TimePos;
        double t = len > 0 ? Math.Clamp(baseT + deltaSec, 0, len) : Math.Max(0, baseT + deltaSec);
        MarkSeek(t);
        _mpv.SeekRelative(deltaSec);

        if (len > 0) _seek.Value = t / len;
        _time.Text = $"{Fmt(t)} / {Fmt(len)}";
        _seekOsd = $"{(deltaSec > 0 ? "▶▶ +" : "◀◀ -")}{Math.Abs(deltaSec):0}초";
        ShowOsd($"{_seekOsd}   {TimeStatus(t, len)}");
    }

    // 시크 완료 후 실제 도착 위치(키프레임 보정 반영)로 OSD·바·시간을 갱신
    void OnSeekDone()
    {
        if (_mpv == null || _seekOsd == null || !SeekPending) return;
        double len = _mpv.Duration, cur = _mpv.TimePos;
        ShowOsd($"{_seekOsd}   {TimeStatus(cur, len)}");
        _seekTarget = -1;
        UpdateState();
    }

    void MarkSeek(double t)
    {
        _seekTarget = t;
        _seekAt = Environment.TickCount64;
    }

    bool SeekPending => _seekTarget >= 0 && Environment.TickCount64 - _seekAt < 800;

    // F/D: 한 프레임 앞/뒤 (재생 중이면 일시정지됨). Space를 누르면 그대로 이어서 재생
    void FrameStep(bool forward)
    {
        if (_mpv == null || _mpv.Idle) return;
        _mpv.FrameStep(forward);
        _btnPlay.Text = GlyphPlay;
        // 프레임 이동은 비동기라 바로 읽으면 이전 시간이 나옴 → 잠깐 뒤에 위치를 읽어 시간과 함께 표시
        _frameLabel = forward ? "▶| 다음 프레임" : "|◀ 이전 프레임";
        // 키를 누르고 있으면 계속 들어오므로 타이머를 다시 시작하지 않음 (다시 시작하면 표시가 영영 안 뜨고 이전 표시가 사라짐)
        if (!_frameOsd.Enabled) _frameOsd.Start();
    }

    readonly System.Windows.Forms.Timer _frameOsd = new() { Interval = 120 };
    string _frameLabel = "";

    void ShowFrameOsd()
    {
        _frameOsd.Stop();
        if (_mpv == null || _mpv.Idle) return;
        double cur = _mpv.TimePos, len = _mpv.Duration;
        var t = TimeSpan.FromSeconds(Math.Max(0, cur));
        var text = $"{_frameLabel}  {(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds:000} / {Fmt(len)}";
        if (len > 0) text += $" ({Math.Clamp(cur / len * 100, 0, 100):0.0}%)";
        _mpv.ShowText(text, 2000);
    }

    const double MinSpeed = 0.2, MaxSpeed = 12.0;

    void AddSpeed(double delta)
    {
        if (_mpv == null) return;
        // 0.1 단위 누적 오차를 막기 위해 현재 값을 0.1 단위로 맞춘 뒤 가감
        double cur = Math.Round(_mpv.Speed * 10) / 10;
        SetSpeed(cur + delta);
    }

    void SetSpeed(double speed)
    {
        if (_mpv == null) return;
        speed = Math.Round(Math.Clamp(speed, MinSpeed, MaxSpeed) * 10) / 10;
        _mpv.Speed = speed;
        ShowSpeed(speed);
        ShowOsd($"재생속도 {SpeedText(speed)}");
    }

    static string SpeedText(double speed) => Math.Abs(speed - MaxSpeed) < 0.05 ? "최대(12.0x)" : $"{speed:0.0}x";

    // 콤보박스 표시를 현재 재생속도에 맞춤 (X/C/Z 키나 목록 선택 어느 쪽으로 바꿔도 동일)
    void ShowSpeed(double speed)
    {
        var text = Math.Abs(speed - MaxSpeed) < 0.05 ? "최대" : $"{speed:0.0}x";
        if (_speedLabel == text) return;
        _speedLabel = text;
        _speedBox.Invalidate();
    }

    void DrawSpeedItem(object? sender, DrawItemEventArgs e)
    {
        // 닫힌 상태의 표시 영역(ComboBoxEdit)은 현재 재생속도, 펼친 목록은 항목 문구
        bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
        string text = edit || e.Index < 0 ? _speedLabel : SpeedItems[e.Index].Text;
        bool hot = !edit && (e.State & DrawItemState.Selected) != 0;
        using (var bg = new SolidBrush(hot ? Color.FromArgb(90, 90, 90) : Color.FromArgb(56, 56, 56)))
            e.Graphics.FillRectangle(bg, e.Bounds);
        TextRenderer.DrawText(e.Graphics, text, _speedBox.Font, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height),
            edit ? Accent : Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    void AddVolume(int delta)
    {
        if (_mpv == null) return;
        // 10% 이하 구간은 1씩 세밀하게, 그 위는 delta(5)씩. 내려올 때 10을 건너뛰지 않도록 10에서 한 번 멈춤
        int cur = _mpv.Volume;
        int v = delta > 0
            ? (cur < 10 ? cur + 1 : cur + delta)
            : (cur <= 10 ? cur - 1 : Math.Max(10, cur + delta));
        v = Math.Clamp(v, 0, 100);
        _mpv.Volume = v;
        ShowOsd($"볼륨 {v}%");
    }

    /// <summary>영상 왼쪽 상단에 잠깐 표시되는 안내 문구 (mpv OSD)</summary>
    void ShowOsd(string text, int ms = 1500)
    {
        // 재생 중이 아닐 때(영상 없음)는 mpv 화면 안내가 보이지 않으므로 겹침 알림으로 대신 표시
        if (_mpv == null || _mpv.Idle) ShowToast(text, ms, error: false);
        else _mpv.ShowText(text, ms);
    }

    readonly ToastForm _toast = new();

    /// <summary>오류 등을 영상 왼쪽 위에 알림으로 표시. 메시지 박스처럼 작업을 막지 않고 저절로 사라짐</summary>
    void ShowToast(string text, int ms = 6000, bool error = true)
    {
        if (!IsHandleCreated || IsDisposed) return;
        _toast.ShowText(this, _video.RectangleToScreen(_video.ClientRectangle), text, ms, error);
    }

    void ShowError(string text, int ms = 8000) => ShowToast(text, ms, error: true);

    // 진행바(일반/전체화면 공통)를 클릭하거나 끌어서 이동
    void OnScrub(double ratio)
    {
        if (_mpv == null) return;
        double len = _mpv.Duration;
        if (len <= 0) return;
        double t = ratio * len;
        MarkSeek(t);
        _seekOsd = null;
        _mpv.SeekAbsolute(t);
        _time.Text = $"{Fmt(t)} / {Fmt(len)}";
        if (_fsBar.Visible) _fsBar.TimeLabel.Text = TimeStatus(t, len);
    }

    void UpdateState()
    {
        if (_mpv == null) return;
        if (_mpv.Idle) { ResetUi(); return; }

        _btnPlay.Text = _mpv.Pause ? GlyphPlay : GlyphPause;
        ShowSpeed(Math.Round(_mpv.Speed * 10) / 10);
        if (SeekPending || _seek.Dragging || _fsBar.Seek.Dragging) return; // 시크 직후 이전 위치로 바가 튀는 것 방지
        if (_fsBar.Visible) RefreshFsBar();
        double len = _mpv.Duration, cur = _mpv.TimePos;
        if (_stopAt >= 0 && cur >= _stopAt)
        {
            _stopAt = -1;
            _mpv.Pause = true;
            ShowOsd("구간 끝");
        }
        if (len > 0)
        {
            _seek.Value = cur / len;
            // 재생 중인 파일의 길이는 mpv가 정확히 알고 있으므로 재생목록 시간 표시를 바로 채움
            if (_index >= 0 && _index < _items.Count
                && (!_settings.Durations.TryGetValue(_items[_index], out var known) || Math.Abs(known - len) > 1))
            {
                _settings.Durations[_items[_index]] = len;
                _listBox.Invalidate();
            }
        }
        _time.Text = $"{Fmt(cur)} / {Fmt(len)}";
        LayoutRow();
    }

    void ResetUi()
    {
        _btnPlay.Text = GlyphPlay;
        _seek.Value = 0;
        _time.Text = "00:00:00 / 00:00:00";
        LayoutRow();
    }

    // 화면 안내용: "01:20:09 / 02:06:48 (63.2%)" - 전체 길이 대비 현재 위치 비율 포함
    static string TimeStatus(double cur, double len)
    {
        var text = $"{Fmt(cur)} / {Fmt(len)}";
        if (len <= 0) return text;
        double pct = Math.Clamp(cur / len * 100, 0, 100);
        return $"{text} ({pct:0.0}%)";
    }

    static string Fmt(double sec)
    {
        if (sec < 0 || double.IsNaN(sec)) sec = 0;
        var t = TimeSpan.FromSeconds(sec);
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }

    void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _prevBorder = FormBorderStyle; _prevState = WindowState;
            // 전체화면에서 돌아올 크기·위치: 테두리를 없애기 전의 "일반 상태" 영역을 미리 저장해 둠
            _prevBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _listWasVisible = _listPanel.Visible;
            _clipWasVisible = _clip.Visible;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            WindowState = FormWindowState.Maximized;
            _bar.Visible = false;                 // 전체화면에서는 하단 바를 숨기고, 마우스가 아래로 가면 겹침 창(FsBar)으로 보여줌
            _fsHideAt = 0;
            _fsTimer.Start();
            _listPanel.Visible = false;
            _clip.Visible = false;
        }
        else
        {
            _fsTimer.Stop();
            _fsBar.Hide();
            _fsTitle.Hide();
            _bar.Visible = true;
            ApplyDpi(); // 크기는 현재 값이 아닌 DPI 기준값으로 다시 계산
            _listPanel.Visible = _listWasVisible;
            _clip.Visible = _clipWasVisible;
            // 테두리를 되돌린 뒤 일반 상태로 내려와 저장해 둔 영역을 직접 지정.
            // (그냥 WindowState만 되돌리면 테두리 없는 상태에서 잡힌 영역이나 디자인 시점 크기로 돌아오는 경우가 있음)
            FormBorderStyle = _prevBorder;
            WindowState = FormWindowState.Normal;
            Bounds = _prevBounds;
            // 전체화면 들어가기 전에 최대화였다면 다시 최대화 (이때 "복원 크기"도 올바르게 유지됨)
            if (_prevState == FormWindowState.Maximized) WindowState = FormWindowState.Maximized;
        }
        _fullscreen = !_fullscreen;
    }

    Rectangle _prevBounds;

    // ------------------------------------------------------------ 전체화면 하단 진행바 (마우스가 아래로 가면 표시)

    readonly FsBar _fsBar = new();
    readonly FsTitle _fsTitle = new(); // 마우스가 위로 가면 재생 파일명 표시
    readonly System.Windows.Forms.Timer _fsTimer = new() { Interval = 80 };
    long _fsHideAt, _fsTitleHideAt;

    // 마우스 위치를 80ms마다 확인: 영상(mpv)은 별도 창이라 마우스 이벤트가 오지 않으므로 커서 좌표를 직접 읽음
    void FsTick()
    {
        if (!_fullscreen) return;
        var c = Cursor.Position;
        var b = Bounds; // 전체화면에서는 화면 전체
        int barH = S(56);
        bool nearBottom = b.Contains(c) && c.Y >= b.Bottom - barH - S(30);
        bool overBar = _fsBar.Visible && _fsBar.Bounds.Contains(c);
        bool active = ReferenceEquals(Form.ActiveForm, this); // 다른 프로그램으로 넘어가 있으면 띄우지 않음

        if ((nearBottom || overBar || _fsBar.Seek.Dragging) && active)
        {
            _fsHideAt = 0;
            if (!_fsBar.Visible)
            {
                _fsBar.SetBounds(b.Left, b.Bottom - barH, b.Width, barH);
                _fsBar.Seek.Height = S(24);
                RefreshFsBar();
                _fsBar.Show(this);
            }
        }
        else if (_fsBar.Visible)
        {
            // 영역을 벗어나도 0.5초 뒤에 숨겨서 가장자리에서 깜박이지 않게 함
            long now = Environment.TickCount64;
            if (_fsHideAt == 0) _fsHideAt = now + 500;
            else if (now >= _fsHideAt) { _fsBar.Hide(); _fsHideAt = 0; }
        }

        // 상단: 재생 중인 파일명 (창은 클릭을 통과시키므로 위치만으로 판단)
        int titleH = S(44);
        bool nearTop = b.Contains(c) && c.Y <= b.Top + titleH + S(20);
        var path = !nearTop || _mpv == null || _mpv.Idle ? null : PlayingPath();
        if (nearTop && active && path != null)
        {
            _fsTitleHideAt = 0;
            var name = Path.GetFileName(path);
            if (_fsTitle.TitleLabel.Text != name) _fsTitle.TitleLabel.Text = name;
            if (!_fsTitle.Visible)
            {
                _fsTitle.SetBounds(b.Left, b.Top, b.Width, titleH);
                _fsTitle.Show(this);
            }
        }
        else if (_fsTitle.Visible)
        {
            long now = Environment.TickCount64;
            if (_fsTitleHideAt == 0) _fsTitleHideAt = now + 500;
            else if (now >= _fsTitleHideAt) { _fsTitle.Hide(); _fsTitleHideAt = 0; }
        }
    }

    // 전체화면 바의 진행 위치/시간 표시를 현재 재생 상태에 맞춤
    void RefreshFsBar()
    {
        if (_mpv == null || _mpv.Idle) { _fsBar.Seek.Value = 0; _fsBar.TimeLabel.Text = "00:00:00 / 00:00:00"; return; }
        double len = _mpv.Duration, cur = _mpv.TimePos;
        if (!_fsBar.Seek.Dragging && len > 0) _fsBar.Seek.Value = cur / len;
        _fsBar.TimeLabel.Text = TimeStatus(cur, len);
    }

    bool _enterPlaysSelection;

    // PgDn/PgUp: 재생목록에서 다음/이전 파일을 선택만 함 (재생은 Enter). 목록이 닫혀 있어도 동작하며 선택한 파일명을 화면에 표시
    void SelectRelative(int delta)
    {
        if (_items.Count == 0) return;
        // 여러 개가 선택돼 있으면 아래로는 마지막 선택, 위로는 첫 선택 기준으로 한 칸 이동하고 하나만 선택
        var sel = _listBox.SelectedIndices.Cast<int>().ToArray();
        int cur = sel.Length > 0 ? (delta > 0 ? sel.Max() : sel.Min()) : _index;
        int next = Math.Clamp((cur < 0 ? (delta > 0 ? -1 : _items.Count) : cur) + delta, 0, _items.Count - 1);
        _listBox.ClearSelected();
        _listBox.SetSelected(next, true);
        _enterPlaysSelection = true;
        ShowOsd($"선택  {next + 1}/{_items.Count}  {Path.GetFileName(_items[next])}");
    }

    void PlaySelected()
    {
        int i = _listBox.SelectedIndex;
        _enterPlaysSelection = false;
        if (i >= 0) PlayAt(i);
    }

    // 영상 창을 눌렀을 때: 시간 입력칸/목록에 남아 있던 포커스를 풀어 재생 단축키가 먹도록 함
    void FocusVideo(bool clicked = true)
    {
        if (clicked) _enterPlaysSelection = false; // 영상을 눌렀다면 Enter는 다시 전체화면 (키 입력으로 들어온 경우는 유지)
        ActiveControl = null;
        Focus();
    }

    bool HandleKey(Keys keyData, bool fromVideo = false)
    {
        // 클립 시간 입력칸/구간 목록에서 편집 중일 때는 화살표·Space 등 재생 단축키를 가로채지 않음
        bool editing = !fromVideo && ActiveControl is MaskedTextBox or DataGridView;
        if (editing && keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Space or Keys.M or Keys.Enter
            or Keys.F or Keys.D or Keys.X or Keys.C or Keys.Z or Keys.R or Keys.O or Keys.P or Keys.PageDown or Keys.PageUp or (Keys.Shift | Keys.PageDown) or (Keys.Shift | Keys.PageUp)
            or (Keys.Control | Keys.Left) or (Keys.Control | Keys.Right) or (Keys.Shift | Keys.Left) or (Keys.Shift | Keys.Right))
            return false;

        switch (keyData)
        {
            case Keys.O:
            case Keys.Control | Keys.OemOpenBrackets: LoopHotkey('['); return true;
            case Keys.P:
            case Keys.Control | Keys.OemCloseBrackets: LoopHotkey(']'); return true;
            case Keys.Control | Keys.OemPipe:
            case Keys.Control | Keys.OemBackslash: LoopHotkey('\\'); return true;
            case Keys.Control | Keys.Shift | Keys.OemPipe:
            case Keys.Control | Keys.Shift | Keys.OemBackslash: LoopHotkey('|'); return true;
            case Keys.OemOpenBrackets: ClipHotkey('['); return true;
            case Keys.OemCloseBrackets: ClipHotkey(']'); return true;
            case Keys.OemPipe:
            case Keys.OemBackslash: ClipHotkey('\\'); return true;
            case Keys.Space: TogglePlay(); return true;
            case Keys.Left: Seek(-_settings.SkipShort); return true;
            case Keys.Right: Seek(_settings.SkipShort); return true;
            case Keys.Control | Keys.Left: Seek(-_settings.SkipMedium); return true;
            case Keys.Control | Keys.Right: Seek(_settings.SkipMedium); return true;
            case Keys.Shift | Keys.Left: Seek(-_settings.SkipLong); return true;
            case Keys.Shift | Keys.Right: Seek(_settings.SkipLong); return true;
            case Keys.Up: AddVolume(5); return true;
            case Keys.Down: AddVolume(-5); return true;
            case Keys.M: ToggleMute(); return true;
            case Keys.F: FrameStep(true); return true;
            case Keys.D: FrameStep(false); return true;
            case Keys.X: AddSpeed(-0.1); return true;
            case Keys.C: AddSpeed(0.1); return true;
            case Keys.Z: SetSpeed(1.0); return true;
            case Keys.R: ToggleRepeat(); return true;
            case Keys.F11: ToggleFullscreen(); return true;
            case Keys.Enter:
                // 목록에서 고른 파일이 지금 재생 중인 파일과 다를 때만 그 파일 재생, 아니면(이미 재생 중이면) 전체화면
                if ((_listBox.Focused || _enterPlaysSelection) && _listBox.SelectedIndex >= 0 && _listBox.SelectedIndex != _index)
                { PlaySelected(); return true; }
                _enterPlaysSelection = false;
                ToggleFullscreen(); return true;
            case Keys.PageDown: SelectRelative(+1); return true;
            case Keys.PageUp: SelectRelative(-1); return true;
            case Keys.Shift | Keys.PageDown: MoveSelectedBy(+1); return true;
            case Keys.Shift | Keys.PageUp: MoveSelectedBy(-1); return true;
            case Keys.Escape: if (_fullscreen) ToggleFullscreen(); return true;
            case Keys.Control | Keys.O: OpenDialog(); return true;
            case Keys.Control | Keys.A:
                // 재생목록에 포커스가 있을 때 전체 선택 (다른 곳에서는 기본 동작 유지)
                if (_listBox.Focused && _items.Count > 0)
                {
                    _listBox.BeginUpdate();
                    for (int i = 0; i < _listBox.Items.Count; i++) _listBox.SetSelected(i, true);
                    _listBox.EndUpdate();
                    return true;
                }
                break;
            case Keys.Delete: if (_listBox.Focused) { RemoveSelected(); return true; } break;
            case Keys.Shift | Keys.Delete: if (_listBox.Focused) { DeleteSelectedFile(); return true; } break;
        }
        return false;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        => HandleKey(keyData) || base.ProcessCmdKey(ref msg, keyData);
}
