namespace ClipM
{
    partial class frmSegmentCombine
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
            btnAdd = new Button();
            medtEnd = new MaskedTextBox();
            lblEnd = new Label();
            medtStart = new MaskedTextBox();
            lblStart = new Label();
            listView1 = new ListView();
            colFileName = new ColumnHeader();
            colStart = new ColumnHeader();
            colEnd = new ColumnHeader();
            richTextBox1 = new RichTextBox();
            pnlBottom.SuspendLayout();
            SuspendLayout();
            // 
            // pnlBottom
            // 
            pnlBottom.Controls.Add(btnCombine);
            pnlBottom.Controls.Add(btnAdd);
            pnlBottom.Controls.Add(medtEnd);
            pnlBottom.Controls.Add(lblEnd);
            pnlBottom.Controls.Add(medtStart);
            pnlBottom.Controls.Add(lblStart);
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Location = new Point(0, 564);
            pnlBottom.Name = "pnlBottom";
            pnlBottom.Size = new Size(920, 76);
            pnlBottom.TabIndex = 0;
            // 
            // btnCombine
            // 
            btnCombine.Location = new Point(810, 24);
            btnCombine.Name = "btnCombine";
            btnCombine.Size = new Size(90, 28);
            btnCombine.TabIndex = 5;
            btnCombine.Text = "합침";
            btnCombine.UseVisualStyleBackColor = true;
            btnCombine.Click += btnCombine_Click;
            // 
            // btnAdd
            // 
            btnAdd.Location = new Point(710, 24);
            btnAdd.Name = "btnAdd";
            btnAdd.Size = new Size(90, 28);
            btnAdd.TabIndex = 4;
            btnAdd.Text = "Add";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += btnAdd_Click;
            // 
            // medtEnd
            // 
            medtEnd.Location = new Point(490, 27);
            medtEnd.Mask = "99:99:99";
            medtEnd.Name = "medtEnd";
            medtEnd.Size = new Size(100, 23);
            medtEnd.TabIndex = 3;
            medtEnd.Text = "000000";
            // 
            // lblEnd
            // 
            lblEnd.AutoSize = true;
            lblEnd.Location = new Point(440, 31);
            lblEnd.Name = "lblEnd";
            lblEnd.Size = new Size(31, 15);
            lblEnd.TabIndex = 2;
            lblEnd.Text = "종료";
            // 
            // medtStart
            // 
            medtStart.Location = new Point(310, 27);
            medtStart.Mask = "99:99:99";
            medtStart.Name = "medtStart";
            medtStart.Size = new Size(100, 23);
            medtStart.TabIndex = 1;
            medtStart.Text = "000000";
            medtStart.Leave += medtStart_Leave;
            // 
            // lblStart
            // 
            lblStart.AutoSize = true;
            lblStart.Location = new Point(260, 31);
            lblStart.Name = "lblStart";
            lblStart.Size = new Size(31, 15);
            lblStart.TabIndex = 0;
            lblStart.Text = "시작";
            // 
            // listView1
            // 
            listView1.AllowDrop = true;
            listView1.Columns.AddRange(new ColumnHeader[] { colFileName, colStart, colEnd });
            listView1.Dock = DockStyle.Fill;
            listView1.FullRowSelect = true;
            listView1.GridLines = true;
            listView1.Location = new Point(0, 160);
            listView1.Name = "listView1";
            listView1.Size = new Size(920, 404);
            listView1.TabIndex = 2;
            listView1.UseCompatibleStateImageBehavior = false;
            listView1.View = View.Details;
            listView1.ItemDrag += listView1_ItemDrag;
            listView1.DragDrop += listView1_DragDrop;
            listView1.DragEnter += listView1_DragEnter;
            listView1.DragOver += listView1_DragOver;
            listView1.KeyDown += listView1_KeyDown;
            // 
            // colFileName
            // 
            colFileName.Text = "파일명";
            colFileName.Width = 620;
            // 
            // colStart
            // 
            colStart.Text = "시작시간";
            colStart.Width = 130;
            // 
            // colEnd
            // 
            colEnd.Text = "종료시간";
            colEnd.Width = 130;
            // 
            // richTextBox1
            // 
            richTextBox1.AllowDrop = true;
            richTextBox1.Dock = DockStyle.Top;
            richTextBox1.Location = new Point(0, 0);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(920, 160);
            richTextBox1.TabIndex = 1;
            richTextBox1.Text = "";
            richTextBox1.DragDrop += richTextBox1_DragDrop;
            richTextBox1.DragEnter += richTextBox1_DragEnter;
            // 
            // frmSegmentCombine
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(920, 640);
            Controls.Add(listView1);
            Controls.Add(richTextBox1);
            Controls.Add(pnlBottom);
            Name = "frmSegmentCombine";
            Text = "구간합침";
            Load += frmSegmentCombine_Load;
            pnlBottom.ResumeLayout(false);
            pnlBottom.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnlBottom;
        private Button btnCombine;
        private Button btnAdd;
        private MaskedTextBox medtEnd;
        private Label lblEnd;
        private MaskedTextBox medtStart;
        private Label lblStart;
        private ListView listView1;
        private ColumnHeader colFileName;
        private ColumnHeader colStart;
        private ColumnHeader colEnd;
        private RichTextBox richTextBox1;
    }
}
