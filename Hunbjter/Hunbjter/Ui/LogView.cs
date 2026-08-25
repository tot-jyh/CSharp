using System.Runtime.InteropServices;

namespace Hunbjter;

/// <summary>
/// Append-only log pane. Colors each line by inferred severity and caps the buffer so a
/// session that runs for days cannot grow it without bound.
/// </summary>
public sealed class LogView : RichTextBox
{
    private const int WmSetRedraw = 0x000B;
    private const int MaxLines = 600;
    private const int TrimBatch = 200;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private int lineCount;

    public LogView()
    {
        BackColor = Theme.Surface;
        ForeColor = Theme.TextSecondary;
        BorderStyle = BorderStyle.None;
        Font = Theme.Mono;
        ReadOnly = true;
        DetectUrls = false;
        WordWrap = false;
        HideSelection = true;
        ScrollBars = RichTextBoxScrollBars.Vertical;
        Margin = new Padding(0);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeTheme.ApplyScrollBars(this);
    }

    public void Append(string message)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        // Captured before appending: someone scrolled up to read history should stay there
        // instead of being yanked to the bottom every time a timer fires.
        var wasAtBottom = IsScrolledToBottom();

        Freeze(true);
        try
        {
            AppendSegment($"{DateTime.Now:HH:mm:ss}  ", Theme.TextMuted);
            AppendSegment(message + Environment.NewLine, ResolveSeverityColor(message));
            lineCount++;
            TrimBuffer();
        }
        finally
        {
            Freeze(false);
        }

        Invalidate();

        if (wasAtBottom)
        {
            SelectionStart = TextLength;
            SelectionLength = 0;
            ScrollToCaret();
        }
    }

    public void ClearLog()
    {
        Clear();
        lineCount = 0;
    }

    private void AppendSegment(string text, Color color)
    {
        SelectionStart = TextLength;
        SelectionLength = 0;
        SelectionColor = color;
        AppendText(text);
        SelectionColor = ForeColor;
    }

    /// <summary>
    /// Trims in batches so the expensive delete runs once every <see cref="TrimBatch"/> lines
    /// rather than on every append. The running count avoids touching <see cref="RichTextBox.Lines"/>,
    /// which rebuilds the whole buffer into a string array each time it is read.
    /// </summary>
    private void TrimBuffer()
    {
        if (lineCount <= MaxLines + TrimBatch)
        {
            return;
        }

        var cutIndex = GetFirstCharIndexFromLine(TrimBatch);
        if (cutIndex <= 0)
        {
            return;
        }

        Select(0, cutIndex);
        SelectedText = "";
        lineCount -= TrimBatch;
    }

    private bool IsScrolledToBottom()
    {
        if (TextLength == 0)
        {
            return true;
        }

        return GetPositionFromCharIndex(TextLength).Y <= ClientSize.Height;
    }

    private void Freeze(bool suspend)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        SendMessage(Handle, WmSetRedraw, suspend ? IntPtr.Zero : new IntPtr(1), IntPtr.Zero);
    }

    /// <summary>Failure keywords win, so "확인 실패" reads as an error rather than a completion.</summary>
    private static Color ResolveSeverityColor(string message)
    {
        if (Contains(message, "실패", "오류", "에러", "거부", "불일치", "없습니다", "못했습니다"))
        {
            return Theme.Danger;
        }

        if (Contains(message, "건너뜁니다", "미지원", "필요합니다", "종료", "중지"))
        {
            return Theme.Warning;
        }

        if (Contains(message, "녹화 시작", "방송중", "재시작"))
        {
            return Theme.Live;
        }

        if (Contains(message, "완료", "저장", "추가", "불러왔습니다"))
        {
            return Theme.TextPrimary;
        }

        return Theme.TextSecondary;
    }

    private static bool Contains(string message, params string[] keywords)
    {
        return keywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
