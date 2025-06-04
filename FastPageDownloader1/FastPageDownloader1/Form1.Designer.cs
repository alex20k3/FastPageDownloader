namespace FastPageDownloader1
{
    partial class Form1
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
            label1 = new Label();
            txtUrls = new RichTextBox();
            label2 = new Label();
            txtSaveFolder = new TextBox();
            btnBrowseFolder = new Button();
            btnDownload = new Button();
            label3 = new Label();
            progressBar = new ProgressBar();
            lblStatus = new Label();
            label4 = new Label();
            lstLog = new ListBox();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(56, 19);
            label1.Name = "label1";
            label1.Size = new Size(110, 25);
            label1.TabIndex = 0;
            label1.Text = "URL-адреса:";
            // 
            // txtUrls
            // 
            txtUrls.Location = new Point(40, 74);
            txtUrls.Name = "txtUrls";
            txtUrls.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtUrls.Size = new Size(555, 159);
            txtUrls.TabIndex = 1;
            txtUrls.Text = "";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(56, 250);
            label2.Name = "label2";
            label2.Size = new Size(199, 25);
            label2.TabIndex = 2;
            label2.Text = "Папка для сохранения:";
            // 
            // txtSaveFolder
            // 
            txtSaveFolder.Location = new Point(40, 291);
            txtSaveFolder.Name = "txtSaveFolder";
            txtSaveFolder.Size = new Size(362, 31);
            txtSaveFolder.TabIndex = 3;
            // 
            // btnBrowseFolder
            // 
            btnBrowseFolder.Font = new Font("Segoe UI", 8F);
            btnBrowseFolder.Location = new Point(455, 291);
            btnBrowseFolder.Name = "btnBrowseFolder";
            btnBrowseFolder.Size = new Size(140, 31);
            btnBrowseFolder.TabIndex = 4;
            btnBrowseFolder.Text = "Обзор...";
            btnBrowseFolder.UseVisualStyleBackColor = true;
            btnBrowseFolder.Click += btnBrowseFolder_Click;
            // 
            // btnDownload
            // 
            btnDownload.Location = new Point(40, 356);
            btnDownload.Name = "btnDownload";
            btnDownload.Size = new Size(555, 72);
            btnDownload.TabIndex = 5;
            btnDownload.Text = "Скачать";
            btnDownload.UseVisualStyleBackColor = true;
            btnDownload.Click += btnDownload_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(56, 457);
            label3.Name = "label3";
            label3.Size = new Size(102, 25);
            label3.TabIndex = 6;
            label3.Text = "Прогресс...";
            // 
            // progressBar
            // 
            progressBar.Location = new Point(40, 507);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(319, 30);
            progressBar.TabIndex = 7;
            // 
            // lblStatus
            // 
            lblStatus.Location = new Point(403, 507);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(192, 30);
            lblStatus.TabIndex = 8;
            lblStatus.Text = "...";
            lblStatus.TextAlign = ContentAlignment.TopRight;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(56, 551);
            label4.Name = "label4";
            label4.Size = new Size(46, 25);
            label4.TabIndex = 9;
            label4.Text = "Лог:";
            // 
            // lstLog
            // 
            lstLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lstLog.FormattingEnabled = true;
            lstLog.ItemHeight = 25;
            lstLog.Location = new Point(40, 589);
            lstLog.Name = "lstLog";
            lstLog.ScrollAlwaysVisible = true;
            lstLog.Size = new Size(555, 129);
            lstLog.TabIndex = 10;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(636, 724);
            Controls.Add(lstLog);
            Controls.Add(label4);
            Controls.Add(lblStatus);
            Controls.Add(progressBar);
            Controls.Add(label3);
            Controls.Add(btnDownload);
            Controls.Add(btnBrowseFolder);
            Controls.Add(txtSaveFolder);
            Controls.Add(label2);
            Controls.Add(txtUrls);
            Controls.Add(label1);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private RichTextBox txtUrls;
        private Label label2;
        private TextBox txtSaveFolder;
        private Button btnBrowseFolder;
        private Button btnDownload;
        private Label label3;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Label label4;
        private ListBox lstLog;
    }
}
