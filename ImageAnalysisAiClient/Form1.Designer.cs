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
            components = new System.ComponentModel.Container();
            lblProvider = new Label();
            cmbProvider = new ComboBox();
            lblModel = new Label();
            cmbModel = new ComboBox();
            lblImage = new Label();
            btnSelectImage = new Button();
            lblImagePath = new Label();
            picImage = new PictureBox();
            btnSend = new Button();
            lblReceiveMessage = new Label();
            txbReceiveMessage = new TextBox();
            ((System.ComponentModel.ISupportInitialize)picImage).BeginInit();
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
            btnSelectImage.Text = "画像を選択...";
            btnSelectImage.UseVisualStyleBackColor = true;
            btnSelectImage.Click += btnSelectImage_Click;
            //
            // lblImagePath
            //
            lblImagePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblImagePath.AutoEllipsis = true;
            lblImagePath.Location = new Point(160, 88);
            lblImagePath.Name = "lblImagePath";
            lblImagePath.Size = new Size(628, 20);
            lblImagePath.TabIndex = 4;
            lblImagePath.Text = "画像が選択されていません";
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
            // btnSend
            //
            btnSend.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            btnSend.Location = new Point(12, 360);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(140, 40);
            btnSend.TabIndex = 6;
            btnSend.Text = "画像を解析";
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
            // txbReceiveMessage
            //
            txbReceiveMessage.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txbReceiveMessage.Location = new Point(12, 433);
            txbReceiveMessage.Multiline = true;
            txbReceiveMessage.Name = "txbReceiveMessage";
            txbReceiveMessage.ReadOnly = true;
            txbReceiveMessage.ScrollBars = ScrollBars.Vertical;
            txbReceiveMessage.Size = new Size(776, 215);
            txbReceiveMessage.TabIndex = 8;
            //
            // Form1
            //
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 660);
            Controls.Add(cmbProvider);
            Controls.Add(lblProvider);
            Controls.Add(txbReceiveMessage);
            Controls.Add(lblReceiveMessage);
            Controls.Add(btnSend);
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
        private Label lblImagePath;
        private PictureBox picImage;
        private Button btnSend;
        private Label lblReceiveMessage;
        private TextBox txbReceiveMessage;
    }
}
