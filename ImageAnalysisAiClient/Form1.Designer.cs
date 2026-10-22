namespace ImageAnalysisAiClient
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
            if (disposing)
            {
                _isClosing = true;
                ReleaseCamera();
                picImage.Image?.Dispose();
                picImage.Image = null;
                components?.Dispose();
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
            components = new System.ComponentModel.Container();
            lblProvider = new Label();
            cmbProvider = new ComboBox();
            lblModel = new Label();
            cmbModel = new ComboBox();
            lblImage = new Label();
            btnSelectImage = new Button();
            captureTimer = new System.Windows.Forms.Timer(components);
            captureDurationTimer = new System.Windows.Forms.Timer(components);
            lblImagePath = new Label();
            picImage = new PictureBox();
            lblAdditionalInfo = new Label();
            txbAdditionalInfo = new TextBox();
            btnSend = new Button();
            lblReceiveMessage = new Label();
            webViewResults = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)picImage).BeginInit();
            ((System.ComponentModel.ISupportInitialize)webViewResults).BeginInit();
            SuspendLayout();
            //
            // lblProvider
            //
            lblProvider.AutoSize = true;
            lblProvider.Location = new Point(500, 9);
            lblProvider.Name = "lblProvider";
            lblProvider.Size = new Size(43, 15);
            lblProvider.TabIndex = 0;
            lblProvider.Text = "接続先";
            //
            // cmbProvider
            //
            cmbProvider.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbProvider.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbProvider.Items.AddRange(new object[] { "Local (Ollama)", "Azure AI Foundry" });
            cmbProvider.Location = new Point(550, 5);
            cmbProvider.Name = "cmbProvider";
            cmbProvider.Size = new Size(238, 23);
            cmbProvider.TabIndex = 1;
            cmbProvider.SelectedIndexChanged += cmbProvider_SelectedIndexChanged;
            //
            // lblModel
            //
            lblModel.AutoSize = true;
            lblModel.Location = new Point(12, 9);
            lblModel.Name = "lblModel";
            lblModel.Size = new Size(40, 15);
            lblModel.TabIndex = 2;
            lblModel.Text = "モデル";
            //
            // cmbModel
            //
            cmbModel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cmbModel.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbModel.Location = new Point(12, 27);
            cmbModel.Name = "cmbModel";
            cmbModel.Size = new Size(470, 23);
            cmbModel.TabIndex = 3;
            cmbModel.SelectedIndexChanged += cmbModel_SelectedIndexChanged;
            //
            // lblImage
            //
            lblImage.AutoSize = true;
            lblImage.Location = new Point(12, 62);
            lblImage.Name = "lblImage";
            lblImage.Size = new Size(31, 15);
            lblImage.TabIndex = 2;
            lblImage.Text = "画像";
            //
            // btnSelectImage
            //
            btnSelectImage.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            btnSelectImage.Location = new Point(12, 80);
            btnSelectImage.Name = "btnSelectImage";
            btnSelectImage.Size = new Size(140, 35);
            btnSelectImage.TabIndex = 3;
            btnSelectImage.Text = "開始";
            btnSelectImage.UseVisualStyleBackColor = true;
            btnSelectImage.Click += btnSelectImage_Click;
            //
            // captureTimer
            //
            captureTimer.Interval = 500;
            captureTimer.Tick += captureTimer_Tick;
            //
            // captureDurationTimer
            //
            captureDurationTimer.Interval = 3000;
            captureDurationTimer.Tick += captureDurationTimer_Tick;
            //
            // lblImagePath
            //
            lblImagePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblImagePath.AutoEllipsis = true;
            lblImagePath.Location = new Point(308, 88);
            lblImagePath.Name = "lblImagePath";
            lblImagePath.Size = new Size(480, 20);
            lblImagePath.TabIndex = 5;
            lblImagePath.Text = "取得枚数: 0枚 - 停止中";
            //
            // picImage
            //
            picImage.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            picImage.BorderStyle = BorderStyle.FixedSingle;
            picImage.Location = new Point(12, 125);
            picImage.Name = "picImage";
            picImage.Size = new Size(320, 220);
            picImage.SizeMode = PictureBoxSizeMode.Zoom;
            picImage.TabIndex = 5;
            picImage.TabStop = false;
            //
            // lblAdditionalInfo
            //
            lblAdditionalInfo.AutoSize = true;
            lblAdditionalInfo.Location = new Point(350, 125);
            lblAdditionalInfo.Name = "lblAdditionalInfo";
            lblAdditionalInfo.Size = new Size(100, 15);
            lblAdditionalInfo.TabIndex = 6;
            lblAdditionalInfo.Text = "ゴミの補足情報（任意）";
            //
            // txbAdditionalInfo
            //
            txbAdditionalInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txbAdditionalInfo.Location = new Point(350, 147);
            txbAdditionalInfo.Multiline = true;
            txbAdditionalInfo.Name = "txbAdditionalInfo";
            txbAdditionalInfo.ScrollBars = ScrollBars.Vertical;
            txbAdditionalInfo.Size = new Size(438, 198);
            txbAdditionalInfo.TabIndex = 7;
            //
            // btnSend
            //
            btnSend.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            btnSend.Enabled = false;
            btnSend.Location = new Point(350, 353);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(140, 35);
            btnSend.TabIndex = 6;
            btnSend.Text = "送信";
            btnSend.UseVisualStyleBackColor = true;
            btnSend.Click += btnSend_Click;
            //
            // lblReceiveMessage
            //
            lblReceiveMessage.AutoSize = true;
            lblReceiveMessage.Location = new Point(12, 415);
            lblReceiveMessage.Name = "lblReceiveMessage";
            lblReceiveMessage.Size = new Size(76, 15);
            lblReceiveMessage.TabIndex = 7;
            lblReceiveMessage.Text = "解析結果";
            //
            // webViewResults
            //
            webViewResults.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            webViewResults.Location = new Point(12, 433);
            webViewResults.Name = "webViewResults";
            webViewResults.Size = new Size(776, 215);
            webViewResults.TabIndex = 8;
            //
            // Form1
            //
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 660);
            Controls.Add(cmbProvider);
            Controls.Add(lblProvider);
            Controls.Add(webViewResults);
            Controls.Add(lblReceiveMessage);
            Controls.Add(btnSend);
            Controls.Add(txbAdditionalInfo);
            Controls.Add(lblAdditionalInfo);
            Controls.Add(picImage);
            Controls.Add(lblImagePath);
            Controls.Add(btnSelectImage);
            Controls.Add(lblImage);
            Controls.Add(cmbModel);
            Controls.Add(lblModel);
            Name = "Form1";
            Text = "ImageAnalysisAiClient";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)picImage).EndInit();
            ((System.ComponentModel.ISupportInitialize)webViewResults).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblProvider;
        private ComboBox cmbProvider;
        private Label lblModel;
        private ComboBox cmbModel;
        private Label lblImage;
        private Button btnSelectImage;
        private System.Windows.Forms.Timer captureTimer;
        private System.Windows.Forms.Timer captureDurationTimer;
        private Label lblImagePath;
        private PictureBox picImage;
        private Label lblAdditionalInfo;
        private TextBox txbAdditionalInfo;
        private Button btnSend;
        private Label lblReceiveMessage;
        private Microsoft.Web.WebView2.WinForms.WebView2 webViewResults;
    }
}
