namespace ClipM
{
    partial class frmClipM
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            pnlBottom = new Panel();
            btnCombine = new Button();
            btnSegmentCombine = new Button();
            label2 = new Label();
            label1 = new Label();
            btnExtract = new Button();
            medtEnd = new MaskedTextBox();
            medtStart = new MaskedTextBox();
            edtPathName = new TextBox();
            btnPath = new Button();
            rtxtLog = new RichTextBox();
            pnlBottom.SuspendLayout();
            SuspendLayout();
            // 
            // pnlBottom
            // 
            pnlBottom.Controls.Add(btnCombine);
            pnlBottom.Controls.Add(btnSegmentCombine);
            pnlBottom.Controls.Add(label2);
            pnlBottom.Controls.Add(label1);
            pnlBottom.Controls.Add(btnExtract);
            pnlBottom.Controls.Add(medtEnd);
            pnlBottom.Controls.Add(medtStart);
            pnlBottom.Controls.Add(edtPathName);
            pnlBottom.Controls.Add(btnPath);
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Location = new Point(0, 318);
            pnlBottom.Name = "pnlBottom";
            pnlBottom.Size = new Size(553, 110);
            pnlBottom.TabIndex = 4;
            // 
            // btnCombine
            // 
            btnCombine.Location = new Point(387, 60);
            btnCombine.Name = "btnCombine";
            btnCombine.Size = new Size(75, 23);
            btnCombine.TabIndex = 6;
            btnCombine.Text = "합침";
            btnCombine.UseVisualStyleBackColor = true;
            btnCombine.Click += btnCombine_Click;
            // 
            // btnSegmentCombine
            // 
            btnSegmentCombine.Location = new Point(468, 60);
            btnSegmentCombine.Name = "btnSegmentCombine";
            btnSegmentCombine.Size = new Size(75, 23);
            btnSegmentCombine.TabIndex = 8;
            btnSegmentCombine.Text = "구간합침";
            btnSegmentCombine.UseVisualStyleBackColor = true;
            btnSegmentCombine.Click += btnSegmentCombine_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(132, 44);
            label2.Name = "label2";
            label2.Size = new Size(31, 15);
            label2.TabIndex = 7;
            label2.Text = "종료";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(15, 44);
            label1.Name = "label1";
            label1.Size = new Size(31, 15);
            label1.TabIndex = 6;
            label1.Text = "시작";
            // 
            // btnExtract
            // 
            btnExtract.Location = new Point(254, 61);
            btnExtract.Name = "btnExtract";
            btnExtract.Size = new Size(75, 23);
            btnExtract.TabIndex = 5;
            btnExtract.Text = "추출";
            btnExtract.UseVisualStyleBackColor = true;
            btnExtract.Click += btnExtract_Click;
            // 
            // medtEnd
            // 
            medtEnd.Location = new Point(132, 61);
            medtEnd.Mask = "99:99:99";
            medtEnd.Name = "medtEnd";
            medtEnd.Size = new Size(100, 23);
            medtEnd.TabIndex = 4;
            medtEnd.Text = "000000";
            medtEnd.ValidatingType = typeof(DateTime);
            // 
            // medtStart
            // 
            medtStart.Location = new Point(12, 61);
            medtStart.Mask = "99:99:99";
            medtStart.Name = "medtStart";
            medtStart.Size = new Size(100, 23);
            medtStart.TabIndex = 3;
            medtStart.Text = "000000";
            medtStart.Leave += medtStart_Leave;
            // 
            // edtPathName
            // 
            edtPathName.Location = new Point(12, 13);
            edtPathName.Name = "edtPathName";
            edtPathName.Size = new Size(450, 23);
            edtPathName.TabIndex = 1;
            // 
            // btnPath
            // 
            btnPath.Location = new Point(468, 13);
            btnPath.Name = "btnPath";
            btnPath.Size = new Size(75, 23);
            btnPath.TabIndex = 2;
            btnPath.Text = "경로";
            btnPath.UseVisualStyleBackColor = true;
            btnPath.Click += btnPath_Click;
            // 
            // rtxtLog
            // 
            rtxtLog.Dock = DockStyle.Fill;
            rtxtLog.Location = new Point(0, 0);
            rtxtLog.Name = "rtxtLog";
            rtxtLog.Size = new Size(553, 318);
            rtxtLog.TabIndex = 5;
            rtxtLog.Text = "";
            // 
            // frmClipM
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(553, 428);
            Controls.Add(rtxtLog);
            Controls.Add(pnlBottom);
            Name = "frmClipM";
            Text = "Form1";
            Activated += frmClipM_Activated;
            pnlBottom.ResumeLayout(false);
            pnlBottom.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnlBottom;
        private Button btnCombine;
        private Button btnSegmentCombine;
        private Label label2;
        private Label label1;
        private Button btnExtract;
        private MaskedTextBox medtEnd;
        private MaskedTextBox medtStart;
        private TextBox edtPathName;
        private Button btnPath;
        private RichTextBox rtxtLog;
    }
}
