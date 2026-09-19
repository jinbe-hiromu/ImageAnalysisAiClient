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
            lblModel = new Label();
            cmbModel = new ComboBox();
            lblSendMessage = new Label();
            txbSendMessage = new TextBox();
            btnSend = new Button();
            lblReceiveMessage = new Label();
            txbReceiveMessage = new TextBox();
            SuspendLayout();
            //
            // lblModel
            //
            lblModel.AutoSize = true;
            lblModel.Location = new Point(12, 9);
            lblModel.Name = "lblModel";
            lblModel.Size = new Size(40, 15);
            lblModel.TabIndex = 0;
            lblModel.Text = "モデル";
            //
            // cmbModel
            //
            cmbModel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cmbModel.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbModel.Location = new Point(12, 27);
            cmbModel.Name = "cmbModel";
            cmbModel.Size = new Size(776, 23);
            cmbModel.TabIndex = 1;
            cmbModel.SelectedIndexChanged += cmbModel_SelectedIndexChanged;
            //
            // lblSendMessage
            //
            lblSendMessage.AutoSize = true;
            lblSendMessage.Location = new Point(12, 62);
            lblSendMessage.Name = "lblSendMessage";
            lblSendMessage.Size = new Size(76, 15);
            lblSendMessage.TabIndex = 2;
            lblSendMessage.Text = "送信メッセージ";
            //
            // txbSendMessage
            //
            txbSendMessage.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txbSendMessage.Location = new Point(12, 80);
            txbSendMessage.Multiline = true;
            txbSendMessage.Name = "txbSendMessage";
            txbSendMessage.ScrollBars = ScrollBars.Vertical;
            txbSendMessage.Size = new Size(776, 100);
            txbSendMessage.TabIndex = 3;
            //
            // btnSend
            //
            btnSend.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSend.Location = new Point(668, 186);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(120, 35);
            btnSend.TabIndex = 4;
            btnSend.Text = "送信";
            btnSend.UseVisualStyleBackColor = true;
            btnSend.Click += btnSend_Click;
            //
            // lblReceiveMessage
            //
            lblReceiveMessage.AutoSize = true;
            lblReceiveMessage.Location = new Point(12, 233);
            lblReceiveMessage.Name = "lblReceiveMessage";
            lblReceiveMessage.Size = new Size(76, 15);
            lblReceiveMessage.TabIndex = 5;
            lblReceiveMessage.Text = "受信メッセージ";
            //
            // txbReceiveMessage
            //
            txbReceiveMessage.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txbReceiveMessage.Location = new Point(12, 251);
            txbReceiveMessage.Multiline = true;
            txbReceiveMessage.Name = "txbReceiveMessage";
            txbReceiveMessage.ReadOnly = true;
            txbReceiveMessage.ScrollBars = ScrollBars.Vertical;
            txbReceiveMessage.Size = new Size(776, 187);
            txbReceiveMessage.TabIndex = 6;
            //
            // Form1
            //
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(txbReceiveMessage);
            Controls.Add(lblReceiveMessage);
            Controls.Add(btnSend);
            Controls.Add(txbSendMessage);
            Controls.Add(lblSendMessage);
            Controls.Add(cmbModel);
            Controls.Add(lblModel);
            Name = "Form1";
            Text = "ChatAppAI";
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblModel;
        private ComboBox cmbModel;
        private Label lblSendMessage;
        private TextBox txbSendMessage;
        private Button btnSend;
        private Label lblReceiveMessage;
        private TextBox txbReceiveMessage;
    }
}
