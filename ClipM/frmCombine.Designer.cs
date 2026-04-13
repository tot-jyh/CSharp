namespace ClipM
{
    partial class frmCombine
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panel1 = new Panel();
            btnClose = new Button();
            btnCombine = new Button();
            txtCombine = new TextBox();
            panel1.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.Controls.Add(btnClose);
            panel1.Controls.Add(btnCombine);
            panel1.Dock = DockStyle.Bottom;
            panel1.Location = new Point(0, 208);
            panel1.Name = "panel1";
            panel1.Size = new Size(416, 44);
            panel1.TabIndex = 1;
            // 
            // btnClose
            // 
            btnClose.Location = new Point(330, 9);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(75, 23);
            btnClose.TabIndex = 1;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // btnCombine
            // 
            btnCombine.Location = new Point(249, 9);
            btnCombine.Name = "btnCombine";
            btnCombine.Size = new Size(75, 23);
            btnCombine.TabIndex = 0;
            btnCombine.Text = "합침";
            btnCombine.UseVisualStyleBackColor = true;
            btnCombine.Click += btnCombine_Click;
            // 
            // txtCombine
            // 
            txtCombine.AllowDrop = true;
            txtCombine.Dock = DockStyle.Fill;
            txtCombine.Location = new Point(0, 0);
            txtCombine.Multiline = true;
            txtCombine.Name = "txtCombine";
            txtCombine.Size = new Size(416, 208);
            txtCombine.TabIndex = 2;
            // 
            // frmCombine
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(416, 252);
            Controls.Add(txtCombine);
            Controls.Add(panel1);
            Name = "frmCombine";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Form1";
            panel1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Panel panel1;
        private Button btnClose;
        private Button btnCombine;
        private TextBox txtCombine;
    }
}