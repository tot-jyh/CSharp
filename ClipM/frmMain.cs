using System;
using System.Drawing;
using System.Windows.Forms;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using static System.Net.Mime.MediaTypeNames;


namespace ClipM
{
    public partial class frmClipM : Form
    {
        private string _lastClipboardText = string.Empty;

        public frmClipM()
        {
            InitializeComponent();
            CLog("프로그램 시작");

            // 폼이 표시된 직후 비동기 초기화 실행
            this.Shown += FrmClipM_Shown;

            // 드래그 앤 드롭 허용 설정
            rtxtLog.AllowDrop = true;
            rtxtLog.DragEnter += RtxtLog_DragEnter;
            rtxtLog.DragDrop += RtxtLog_DragDrop;
        }

        private async void FrmClipM_Shown(object sender, EventArgs e)
        {
            // 한 번만 실행되도록 이벤트 제거
            this.Shown -= FrmClipM_Shown;

            await EnsureFFmpegAsync();
        }

        // Form1 클래스 내부에 추가
        private async Task EnsureFFmpegAsync()
        {
            try
            {
                // 1) 먼저 현재 지정된 경로(또는 실행 디렉터리)에 ffmpeg가 있는지 확인
                string FindFfmpegFolder()
                {
                    var searchRoots = new[]
                    {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg"),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

                    foreach (var root in searchRoots.Where(Directory.Exists))
                    {
                        try
                        {
                            var exe = Directory.EnumerateFiles(root, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                            if (exe != null) return Path.GetDirectoryName(exe);
                        }
                        catch { /* 접근 권한 등 예외 무시하고 다음 루트로 */ }
                    }

                    return null;
                }

                var ffmpegFolder = FindFfmpegFolder();
                if (ffmpegFolder != null)
                {
                    FFmpeg.SetExecutablesPath(ffmpegFolder);
                    CLog($"FFmpeg 실행파일 발견: {ffmpegFolder}", Color.DarkGreen);
                    return;
                }

                // 2) 없으면 Xabe.Downloader로 내려받기 시도
                CLog("FFmpeg 실행파일을 찾지 못했습니다. 다운로드를 시도합니다...", Color.Gray);
                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);

                // 다운로드 후 재검색
                ffmpegFolder = FindFfmpegFolder();
                if (ffmpegFolder != null)
                {
                    FFmpeg.SetExecutablesPath(ffmpegFolder);
                    CLog($"FFmpeg 다운로드 및 경로 설정 완료: {ffmpegFolder}", Color.DarkGreen);
                    return;
                }

                CLog("다운로드는 완료되었지만 ffmpeg.exe를 찾지 못했습니다. 수동으로 ffmpeg 실행파일 경로를 지정하세요.", Color.Red);
            }
            catch (System.Exception ex)
            {
                CLog($"FFmpeg 준비 중 오류: {ex.Message}", Color.Red);
            }
        }

        private void ReadClipboard()
        {
            try
            {
                // 텍스트 우선 처리
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    if (string.IsNullOrEmpty(text))
                    {
                        CLog("클립보드에 텍스트가 존재하지만 내용이 비어있음", Color.Gray);
                        return;
                    }

                    // 중복 로그 방지: 마지막과 같으면 스킵
                    if (text == _lastClipboardText)
                    {
                        //CLog("클립보드 텍스트가 이전과 동일하여 로그를 생략합니다.", Color.Gray);
                        return;
                    }

                    _lastClipboardText = text;

                    // 미리보기와 전체 로그 분리
                    var preview = text.Length > 200 ? text.Substring(0, 200) + "..." : text;
                    CLog($"클립보드 텍스트 읽음 (미리보기): {preview}", Color.DarkGreen);

                    // 필요하면 전체 텍스트도 출력
                    CLog(text);
                    return;
                }

                if (Clipboard.ContainsImage())
                {
                    CLog("클립보드에 이미지가 있습니다.", Color.Purple);
                    return;
                }

                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    CLog($"클립보드에 파일 목록이 있습니다: {files.Count}개", Color.Purple);
                    foreach (string f in files)
                        CLog(f);
                    return;
                }

                CLog("클립보드에 텍스트/이미지/파일 정보가 없습니다.", Color.Gray);
            }
            catch (System.Runtime.InteropServices.ExternalException ex)
            {
                // 다른 프로세스가 클립보드를 점유하고 있을 때 발생 가능
                CLog($"클립보드 접근 실패: {ex.Message}", Color.Red);
            }
            catch (Exception ex)
            {
                CLog($"클립보드 처리 중 예외: {ex.Message}", Color.Red);
            }
        }

        private void CLog(string message, Color? color = null)
        {
            if (rtxtLog.InvokeRequired)
            {
                rtxtLog.BeginInvoke(new Action(() => CLog(message, color)));
                return;
            }

            var S = $"[{DateTime.Now:HH:mm:ss.fff}]  {message}{Environment.NewLine}";
            var C = color ?? Color.Black;

            // 색상 포맷으로 추가
            rtxtLog.SelectionStart = rtxtLog.TextLength;
            rtxtLog.SelectionLength = 0;
            rtxtLog.SelectionColor = C;
            rtxtLog.AppendText(S);
            rtxtLog.SelectionColor = rtxtLog.ForeColor;

            // 자동 스크롤
            rtxtLog.SelectionStart = rtxtLog.TextLength;
            rtxtLog.ScrollToCaret();

            // 메모리 보호: 너무 길면 앞부분 자르기
            const int maxChars = 200_000;
            if (rtxtLog.TextLength > maxChars)
            {
                // 예: 상위 텍스트 절반 제거 (필요에 맞게 조정)
                rtxtLog.Select(0, rtxtLog.TextLength / 2);
                rtxtLog.SelectedText = string.Empty;
            }
        }

        private void frmClipM_Activated(object sender, EventArgs e)
        {
//            CLog("Form Activated..");
            ReadClipboard();
        }

        private void btnPath_Click(object sender, EventArgs e)
        {
            edtPathName.Text = Clipboard.GetText();
        }

        private async void btnExtract_Click(object sender, EventArgs e)
        {
            var input = edtPathName.Text;
            if (string.IsNullOrWhiteSpace(input) || !System.IO.File.Exists(input))
            {
                CLog("유효한 입력 파일 경로를 지정하세요.", Color.Red);
                return;
            }

            // MaskedTextBox 이름을 mtxtStart, mtxtEnd로 가정 (형식: HH:mm:ss)
            var sStart = medtStart.Text;
            var sEnd = medtEnd.Text;

            if (sStart.Contains(medtStart.PromptChar) || sEnd.Contains(medtEnd.PromptChar))
            {
                CLog("시작/종료 시간을 모두 입력하세요 (HH:mm:ss).", Color.Orange);
                return;
            }

            if (!TimeSpan.TryParseExact(sStart, @"hh\:mm\:ss", null, out var start) ||
                !TimeSpan.TryParseExact(sEnd, @"hh\:mm\:ss", null, out var end))
            {
                CLog("시간 형식이 잘못되었습니다. 형식: HH:mm:ss", Color.Red);
                return;
            }

            if (end <= start)
            {
                CLog("종료시간은 시작시간보다 커야 합니다.", Color.Red);
                return;
            }

            try
            {
                FFmpeg.SetExecutablesPath(AppDomain.CurrentDomain.BaseDirectory);
                await ExtractClipAsync(input, start, end - start);
                CLog("추출 작업 완료", Color.Green);
            }
            catch (Exception ex)
            {
                CLog($"추출 중 오류: {ex.Message}", Color.Red);
            }
        }


        private async System.Threading.Tasks.Task ExtractClipAsync(string inputPath, TimeSpan start, TimeSpan duration)
        {
            var startStr = start.ToString(@"hh\:mm\:ss");
            var durationStr = duration.ToString(@"hh\:mm\:ss");
            var dir = System.IO.Path.GetDirectoryName(inputPath) ?? ".";
            var nowTime = DateTime.Now.ToString("yyyyMMdd_HHmm");
            //var outFile = System.IO.Path.Combine(dir, System.IO.Path.GetFileNameWithoutExtension(inputPath) + $"_clip_{startStr.Replace(':', '-')}" + System.IO.Path.GetExtension(inputPath));
            var outFile = System.IO.Path.Combine(dir, System.IO.Path.GetFileNameWithoutExtension(inputPath) + $"_{nowTime}" + System.IO.Path.GetExtension(inputPath));

            CLog($"클립 생성: {startStr} (+{durationStr}) → {outFile}", Color.DarkBlue);

            var args = $"-ss {startStr} -i \"{inputPath}\" -t {durationStr} -c copy \"{outFile}\"";
            var conversion = FFmpeg.Conversions.New().AddParameter(args, ParameterPosition.PreInput);

            conversion.OnProgress += (_, e) => CLog($"진행률: {e.Percent:0}%", Color.Gray);

            await conversion.Start();
            CLog($"파일 저장 완료: {outFile}", Color.DarkGreen);
        }

        // 드래그 앤 드롭 처리 부분
        // 드래그 엔터 이벤트: 파일이면 복사 커서 표시
        private void RtxtLog_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        // 드롭 이벤트: 첫 번째 파일 경로를 클립보드에 넣고 로그에 표시
        private void RtxtLog_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        var path = files[0];
                        try
                        {
                            Clipboard.SetText(path);
                            CLog($"파일 경로 복사됨: {path}", Color.DarkCyan);
                        }
                        catch (System.Runtime.InteropServices.ExternalException ex)
                        {
                            CLog($"클립보드 설정 실패: {ex.Message}", Color.Red);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CLog($"드래그앤드롭 처리 중 예외: {ex.Message}", Color.Red);
            }
        }

        private void btnCombine_Click(object sender, EventArgs e)
        {
            //새로운 폼 열기
            var combineForm = new frmCombine();
            combineForm.Show(this);
        }

        private void btnSegmentCombine_Click(object sender, EventArgs e)
        {
            var segForm = new frmSegmentCombine();
            segForm.Show(this);
        }

        private void medtStart_Leave(object sender, EventArgs e)
        {
            medtEnd.Text = medtStart.Text;
        }
    }
}