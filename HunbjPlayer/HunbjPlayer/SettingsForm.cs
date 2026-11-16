namespace HunbjPlayer;

/// <summary>
/// 환경설정 창. 건너뛰기 시간(←/→, Ctrl+←/→, Shift+←/→)을 조정하고, 정의된 단축키 전체를 보여 줌(단축키는 고정, 표시만).
/// 확인을 누르면 값을 호출한 쪽(PlayerSettings)에 반영하고 저장함.
/// </summary>
public sealed class SettingsForm : Form
{
    static readonly Color Back = Color.FromArgb(32, 32, 32);
    static readonly Color Field = Color.FromArgb(52, 52, 52);

    // (분류, 키, 기능). 분류가 바뀌는 곳에 제목 행을 넣어 묶어 보여 줌
    static readonly (string Group, string Key, string Action)[] Shortcuts =
    {
        ("재생", "Space", "재생 / 일시정지 (일시정지 중 상단에 현재 시간·진행률 표시)"),
        ("재생", "영상 더블클릭", "재생 / 일시정지"),
        ("재생", "Enter", "전체화면 (재생목록에서 재생 중이 아닌 파일을 골랐으면 그 파일 재생)"),
        ("재생", "F11", "전체화면"),
        ("재생", "Esc", "전체화면 해제"),
        ("재생", "Ctrl + O", "파일 열기"),
        ("이동", "← / →", "뒤로 / 앞으로 (위 설정 시간)"),
        ("이동", "Ctrl + ← / →", "뒤로 / 앞으로 (위 설정 시간)"),
        ("이동", "Shift + ← / →", "뒤로 / 앞으로 (위 설정 시간)"),
        ("이동", "F / D", "다음 / 이전 프레임 (현재 시간·진행률 표시)"),
        ("소리", "↑ / ↓", "볼륨 +5 / -5"),
        ("소리", "M", "음소거 켜기 / 끄기"),
        ("속도", "C / X", "재생 속도 +0.1 / -0.1"),
        ("속도", "Z", "재생 속도 1.0 (원래 속도)"),
        ("반복", "R", "반복재생 켜기 / 끄기 (현재 파일, 구간반복이 있으면 그 구간)"),
        ("구간반복", "Ctrl + [  또는  O", "구간반복 시작"),
        ("구간반복", "Ctrl + ]  또는  P", "구간반복 종료 (시작~종료 반복)"),
        ("구간반복", "Ctrl + \\", "구간반복 해제"),
        ("구간반복", "Ctrl + Shift + \\", "구간반복 범위를 편집 시작/종료로 보내기"),
        ("편집", "[", "편집 시작 시간 (현재 시간)"),
        ("편집", "]", "편집 종료 시간 (현재 시간)"),
        ("편집", "-1 / +1 버튼", "시작/종료 칸 옆: 시간 1초 빼기 / 더하기"),
        ("편집", "\\", "편집 목록에 추가"),
        ("편집", "목록 클릭", "그 구간을 시작/종료 칸에 설정"),
        ("편집", "목록 더블클릭", "그 구간 재생 (종료 시간에 멈춤, 반복재생 중이면 구간 반복)"),
        ("편집", "Del", "선택한 구간 삭제"),
        ("재생목록", "PgUp / PgDn", "이전 / 다음 파일 선택 (Enter로 재생)"),
        ("재생목록", "Shift + PgUp / PgDn", "선택한 파일(여러 개 가능)을 한 칸 위 / 아래로 이동"),
        ("재생목록", "더블클릭", "재생 (편집 구간 재생 전에 보던 파일이면 보던 지점부터)"),
        ("재생목록", "Ctrl + A", "전체 선택"),
        ("재생목록", "Del", "목록에서 빼기"),
        ("재생목록", "Shift + Del", "파일을 휴지통으로 삭제"),
    };

    readonly NumericUpDown _short = Seconds();
    readonly NumericUpDown _medium = Seconds();
    readonly NumericUpDown _long = Seconds();
    readonly NumericUpDown _prefix = new()
    {
        Minimum = 0, Maximum = 100, Width = 90, TextAlign = HorizontalAlignment.Right,
        BackColor = Field, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle
    };

    public int SkipShort => (int)_short.Value;
    public int SkipMedium => (int)_medium.Value;
    public int SkipLong => (int)_long.Value;
    public int SimilarPrefix => (int)_prefix.Value;

    public SettingsForm(int skipShort, int skipMedium, int skipLong, int similarPrefix)
    {
        Text = "환경설정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Back; ForeColor = Color.Gainsboro;
        Font = new Font("Malgun Gothic", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 714);

        var group = new GroupBox
        {
            Text = "건너뛰기 시간 (초)", ForeColor = Color.Gainsboro, BackColor = Back,
            Left = 16, Top = 14, Width = 528, Height = 140
        };
        AddRow(group, "← / →", _short, 28);
        AddRow(group, "Ctrl + ← / →", _medium, 62);
        AddRow(group, "Shift + ← / →", _long, 96);
        group.Controls.Add(new Label
        {
            Text = "← 는 뒤로, → 는 앞으로 이동합니다.\n1~3600초", AutoSize = true, ForeColor = Color.Gray, BackColor = Back,
            Left = 300, Top = 32
        });

        // 재생창에 파일 1개를 드롭할 때 같은 폴더의 비슷한 이름 파일을 함께 추가
        var drop = new GroupBox
        {
            Text = "재생창에 파일 드롭", ForeColor = Color.Gainsboro, BackColor = Back,
            Left = 16, Top = 162, Width = 528, Height = 66
        };
        drop.Controls.Add(new Label { Text = "비슷한 이름 (앞 글자 수)", AutoSize = true, Left = 18, Top = 30, ForeColor = Color.Gainsboro, BackColor = Back });
        _prefix.Left = 190; _prefix.Top = 26;
        drop.Controls.Add(_prefix);
        drop.Controls.Add(new Label
        {
            Text = "같은 폴더에서 앞부분이 같은 파일\n함께 추가 (0이면 사용 안 함)", AutoSize = true, ForeColor = Color.Gray, BackColor = Back,
            Left = 290, Top = 20
        });

        var keysTitle = new Label { Text = "단축키", AutoSize = true, ForeColor = Color.Gainsboro, Left = 18, Top = 240 };
        var keys = new ListView
        {
            View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.None, MultiSelect = false,
            BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.FixedSingle,
            Left = 16, Top = 264, Width = 528, Height = 390
        };
        keys.Columns.Add("키", 150);
        keys.Columns.Add("기능", 355);
        string? last = null;
        foreach (var (g, k, a) in Shortcuts)
        {
            if (g != last)
            {
                keys.Items.Add(new ListViewItem($"[{g}]") { ForeColor = Color.FromArgb(250, 204, 21) });
                last = g;
            }
            keys.Items.Add(new ListViewItem(new[] { "   " + k, a }));
        }

        var ok = Btn("확인", DialogResult.OK, 370);
        var cancel = Btn("취소", DialogResult.Cancel, 462);
        AcceptButton = ok; CancelButton = cancel;
        Controls.AddRange(new Control[] { group, drop, keysTitle, keys, ok, cancel });

        _short.Value = Math.Clamp(skipShort, 1, 3600);
        _medium.Value = Math.Clamp(skipMedium, 1, 3600);
        _long.Value = Math.Clamp(skipLong, 1, 3600);
        _prefix.Value = Math.Clamp(similarPrefix, 0, 100);
    }

    static NumericUpDown Seconds() => new()
    {
        Minimum = 1, Maximum = 3600, Width = 90, TextAlign = HorizontalAlignment.Right,
        BackColor = Field, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle
    };

    static void AddRow(GroupBox group, string label, NumericUpDown box, int top)
    {
        group.Controls.Add(new Label { Text = label, AutoSize = true, Left = 18, Top = top + 4, ForeColor = Color.Gainsboro, BackColor = Back });
        box.Left = 140; box.Top = top;
        group.Controls.Add(box);
        group.Controls.Add(new Label { Text = "초", AutoSize = true, Left = 240, Top = top + 4, ForeColor = Color.Gray, BackColor = Back });
    }

    Button Btn(string text, DialogResult result, int left)
    {
        var b = new Button
        {
            Text = text, DialogResult = result, Left = left, Top = 668, Width = 82, Height = 32,
            FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
            BackColor = result == DialogResult.OK ? Color.FromArgb(59, 130, 246) : Color.FromArgb(56, 56, 56),
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderColor = result == DialogResult.OK ? Color.FromArgb(59, 130, 246) : Color.FromArgb(90, 90, 90);
        return b;
    }
}
