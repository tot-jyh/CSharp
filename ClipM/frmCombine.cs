using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Xabe.FFmpeg;

namespace ClipM
{
    public partial class frmCombine : Form
    {
        public frmCombine()
        {
            InitializeComponent();

            txtCombine.AllowDrop = true;
            txtCombine.DragEnter += TxtCombine_DragEnter;
            txtCombine.DragDrop += TxtCombine_DragDrop;
        }
        private void TxtCombine_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void TxtCombine_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                    return;

                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files == null || files.Length == 0)
                    return;

                // 기존 내용에 추가(append)하도록 변경
                var toAdd = string.Join(Environment.NewLine, files);
                if (string.IsNullOrEmpty(txtCombine.Text))
                {
                    txtCombine.Text = toAdd;
                }
                else
                {
                    // 기존 텍스트와 이어질 때 줄바꿈 처리
                    if (!txtCombine.Text.EndsWith(Environment.NewLine))
                        txtCombine.AppendText(Environment.NewLine);

                    txtCombine.AppendText(toAdd);
                }

                // 첫 경로를 클립보드에 복사(원하면 제거 가능)
                try
                {
                    Clipboard.SetText(files[0]);
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    // 클립보드 복사 실패는 무시
                }
            }
            catch (Exception)
            {
                // 드롭 처리 중 예외는 무시 또는 필요시 로깅 추가
            }
        }

        private async void btnCombine_Click(object sender, EventArgs e)
        {
            try
            {
                var lines = txtCombine.Lines
                    .Select(l => l?.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToArray();

                if (lines.Length == 0)
                {
                    MessageBox.Show("합칠 파일 목록이 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 유효한 파일만 남김
                var files = lines.Where(File.Exists).ToArray();
                if (files.Length == 0)
                {
                    MessageBox.Show("유효한 파일이 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 임시 리스트 파일 생성 (ffmpeg concat 포맷) — UTF8 BOM 없이 기록
                var listFile = Path.Combine(Path.GetTempPath(), $"ffconcat_{Guid.NewGuid():N}.txt");

                // 각 줄을 "file 'C:\path\to\file.ext'" 형식으로 생성
                var listLines = files.Select(path => $"file '{path.Replace("'", "\\'")}'");
                var listContent = string.Join(Environment.NewLine, listLines);

                // UTF8 BOM 없이 쓰기(중요)
                await File.WriteAllTextAsync(listFile, listContent, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                // 출력 파일 결정: 첫 파일 폴더, 확장자 유지
                var firstDir = Path.GetDirectoryName(files[0]) ?? Environment.CurrentDirectory;
                var ext = Path.GetExtension(files[0]);
                if (string.IsNullOrEmpty(ext)) ext = ".mp4";
                var outFile = Path.Combine(firstDir, $"combined_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");

                // FFmpeg 경로가 설정되어 있지 않다면 기본 실행폴더 시도
                try
                {
                    FFmpeg.SetExecutablesPath(AppDomain.CurrentDomain.BaseDirectory);
                }
                catch
                {
                    // 무시: EnsureFFmpeg 등으로 미리 준비해두는 것을 권장
                }

                // UI 차단 방지: 버튼 상태 변경
                var prevText = btnCombine.Text;
                btnCombine.Enabled = false;
                btnCombine.Text = "합치는 중...";

                // concat demuxer 사용해서 복사(재인코딩 없음)
                var args = $"-f concat -safe 0 -i \"{listFile}\" -c copy \"{outFile}\"";
                var conversion = FFmpeg.Conversions.New().AddParameter(args, ParameterPosition.PreInput);

                conversion.OnProgress += (_, prog) =>
                {
                    // 진행률을 간단히 타이틀에 표시(원하면 다른 UI로 변경)
                    this.BeginInvoke(new Action(() => this.Text = $"합치는 중... {prog.Percent:0}%"));
                };

                await conversion.Start();

                MessageBox.Show($"합치기 완료: {outFile}", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Text = "폼"; // 원래 타이틀로 복원하거나 필요에 맞게 설정
                btnCombine.Text = prevText;
                btnCombine.Enabled = true;

                // 임시 리스트 파일 삭제 시도
                try { File.Delete(listFile); } catch { /* 무시 */ }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"합치는 중 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnCombine.Enabled = true;
                btnCombine.Text = "합침";
            }
        }


        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
