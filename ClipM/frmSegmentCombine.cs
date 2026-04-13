using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xabe.FFmpeg;

namespace ClipM
{
    public partial class frmSegmentCombine : Form
    {
        private readonly ImageList _rowHeightImageList = new();
        private string _currentFilePath = string.Empty;

        private sealed class SegmentRow
        {
            public required string FilePath { get; init; }
            public required TimeSpan Start { get; init; }
            public required TimeSpan End { get; init; }
        }

        public frmSegmentCombine()
        {
            InitializeComponent();
        }

        private void frmSegmentCombine_Load(object sender, EventArgs e)
        {
            // ListView row-height tweak: default보다 약 1.5배 가독성 확보
            _rowHeightImageList.ImageSize = new Size(1, 30);
            _rowHeightImageList.ColorDepth = ColorDepth.Depth32Bit;
            listView1.SmallImageList = _rowHeightImageList;

            richTextBox1.Text = "여기에 동영상 파일을 드래그 앤 드롭하세요.";
        }

        private void richTextBox1_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }

            e.Effect = DragDropEffects.None;
        }

        private void richTextBox1_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0)
            {
                return;
            }

            var validFiles = files.Where(File.Exists).ToArray();
            if (validFiles.Length == 0)
            {
                MessageBox.Show("유효한 파일이 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _currentFilePath = validFiles[0];

            var sb = new StringBuilder();
            sb.AppendLine($"선택 파일: {Path.GetFileName(_currentFilePath)}");
            sb.AppendLine();
            sb.AppendLine("드롭된 파일명:");
            foreach (var path in validFiles)
            {
                sb.AppendLine(Path.GetFileName(path));
            }
            richTextBox1.Text = sb.ToString();
        }

        private void btnAdd_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentFilePath) || !File.Exists(_currentFilePath))
            {
                MessageBox.Show("상단 박스에 파일을 먼저 드래그 앤 드롭하세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!TryGetStartEnd(out var start, out var end))
            {
                return;
            }

            var item = new ListViewItem(Path.GetFileName(_currentFilePath));
            item.SubItems.Add(start.ToString(@"hh\:mm\:ss"));
            item.SubItems.Add(end.ToString(@"hh\:mm\:ss"));
            item.Tag = new SegmentRow
            {
                FilePath = _currentFilePath,
                Start = start,
                End = end
            };

            listView1.Items.Add(item);
        }

        private async void btnCombine_Click(object? sender, EventArgs e)
        {
            if (listView1.Items.Count == 0)
            {
                MessageBox.Show("합칠 항목이 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var rows = listView1.Items
                .Cast<ListViewItem>()
                .Select(i => i.Tag as SegmentRow)
                .Where(i => i != null)
                .Cast<SegmentRow>()
                .ToList();

            if (rows.Count == 0)
            {
                MessageBox.Show("유효한 구간 데이터가 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnAdd.Enabled = false;
            btnCombine.Enabled = false;
            var oldText = btnCombine.Text;
            btnCombine.Text = "합치는 중...";

            var tempFiles = new List<string>();
            string? listFile = null;
            try
            {
                ConfigureFfmpegPath();

                foreach (var row in rows)
                {
                    var tempClip = Path.Combine(Path.GetTempPath(), $"seg_{Guid.NewGuid():N}.mp4");
                    tempFiles.Add(tempClip);
                    await ExtractSegmentAsync(row.FilePath, row.Start, row.End - row.Start, tempClip);
                }

                listFile = Path.Combine(Path.GetTempPath(), $"concat_{Guid.NewGuid():N}.txt");
                var listLines = tempFiles.Select(path => $"file '{path.Replace("'", "\\'")}'");
                await File.WriteAllTextAsync(listFile, string.Join(Environment.NewLine, listLines), new UTF8Encoding(false));

                var outDir = Path.GetDirectoryName(rows[0].FilePath) ?? Environment.CurrentDirectory;
                var outFile = Path.Combine(outDir, $"{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

                var args = $"-f concat -safe 0 -i \"{listFile}\" -c copy \"{outFile}\"";
                var conversion = FFmpeg.Conversions.New().AddParameter(args, ParameterPosition.PreInput);
                await conversion.Start();

                MessageBox.Show($"완료: {outFile}", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"합치기 중 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    }
                }

                if (!string.IsNullOrWhiteSpace(listFile) && File.Exists(listFile))
                {
                    try
                    {
                        File.Delete(listFile);
                    }
                    catch
                    {
                    }
                }

                btnCombine.Text = oldText;
                btnAdd.Enabled = true;
                btnCombine.Enabled = true;
            }
        }

        private bool TryGetStartEnd(out TimeSpan start, out TimeSpan end)
        {
            start = default;
            end = default;

            if (medtStart.Text.Contains(medtStart.PromptChar) || medtEnd.Text.Contains(medtEnd.PromptChar))
            {
                MessageBox.Show("시작/종료 시간을 모두 입력하세요. (HH:mm:ss)", "입력 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!TimeSpan.TryParseExact(medtStart.Text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out start)
                || !TimeSpan.TryParseExact(medtEnd.Text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out end))
            {
                MessageBox.Show("시간 형식이 잘못되었습니다. (HH:mm:ss)", "입력 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (end <= start)
            {
                MessageBox.Show("종료시간은 시작시간보다 커야 합니다.", "입력 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private static async Task ExtractSegmentAsync(string inputPath, TimeSpan start, TimeSpan duration, string outputPath)
        {
            var startStr = start.ToString(@"hh\:mm\:ss");
            var durationStr = duration.ToString(@"hh\:mm\:ss");
            var args = $"-ss {startStr} -i \"{inputPath}\" -t {durationStr} -c copy \"{outputPath}\"";
            var conversion = FFmpeg.Conversions.New().AddParameter(args, ParameterPosition.PreInput);
            await conversion.Start();
        }

        private void ConfigureFfmpegPath()
        {
            string? FindFfmpegFolder()
            {
                var roots = new[]
                {
                    AppDomain.CurrentDomain.BaseDirectory,
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg"),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };

                foreach (var root in roots.Where(Directory.Exists))
                {
                    try
                    {
                        var exe = Directory.EnumerateFiles(root, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(exe))
                        {
                            return Path.GetDirectoryName(exe);
                        }
                    }
                    catch
                    {
                    }
                }

                return null;
            }

            var ffmpegFolder = FindFfmpegFolder();
            FFmpeg.SetExecutablesPath(ffmpegFolder ?? AppDomain.CurrentDomain.BaseDirectory);
        }

        private void listView1_ItemDrag(object? sender, ItemDragEventArgs e)
        {
            if (e.Item is ListViewItem item)
            {
                listView1.DoDragDrop(item, DragDropEffects.Move);
            }
        }

        private void listView1_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                e.Effect = DragDropEffects.Move;
                return;
            }

            e.Effect = DragDropEffects.None;
        }

        private void listView1_DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                e.Effect = DragDropEffects.Move;
                return;
            }

            e.Effect = DragDropEffects.None;
        }

        private void listView1_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                return;
            }

            if (e.Data.GetData(typeof(ListViewItem)) is not ListViewItem dragged)
            {
                return;
            }

            var pt = listView1.PointToClient(new Point(e.X, e.Y));
            var target = listView1.GetItemAt(pt.X, pt.Y);
            var targetIndex = target?.Index ?? (listView1.Items.Count - 1);

            if (targetIndex < 0)
            {
                return;
            }

            var sourceIndex = dragged.Index;
            if (sourceIndex == targetIndex)
            {
                return;
            }

            var clone = (ListViewItem)dragged.Clone();
            listView1.Items.RemoveAt(sourceIndex);

            if (targetIndex > sourceIndex)
            {
                targetIndex--;
            }

            listView1.Items.Insert(targetIndex, clone);
            clone.Selected = true;
        }
    }
}
