namespace FINAL_PROJECT_CLIENT
{
    partial class ChatForm
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
            lstUsers = new ListBox();
            rtbChat = new RichTextBox();
            txtMessage = new TextBox();
            btnSend = new Button();
            btnAttach = new Button();
            panelTop = new Panel();
            lblStatus = new Label();
            btnReconnect = new Button();
            panelBottom = new Panel();
            SuspendLayout();
            // 
            // lstUsers
            // 
            lstUsers.BackColor = Color.FromArgb(47, 49, 54);
            lstUsers.BorderStyle = BorderStyle.None;
            lstUsers.DrawMode = DrawMode.OwnerDrawFixed;
            lstUsers.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            lstUsers.ForeColor = Color.White;
            lstUsers.FormattingEnabled = true;
            lstUsers.ItemHeight = 36;
            lstUsers.Location = new Point(12, 55);
            lstUsers.Name = "lstUsers";
            lstUsers.Size = new Size(150, 323);
            lstUsers.TabIndex = 0;
            lstUsers.DrawItem += new DrawItemEventHandler(lstUsers_DrawItem);
            lstUsers.SelectedIndexChanged += new EventHandler(lstUsers_SelectedIndexChanged);
            // 
            // rtbChat
            // 
            rtbChat.BackColor = Color.FromArgb(54, 57, 63);
            rtbChat.BorderStyle = BorderStyle.None;
            rtbChat.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            rtbChat.ForeColor = Color.FromArgb(220, 221, 222);
            rtbChat.Location = new Point(175, 55);
            rtbChat.Name = "rtbChat";
            rtbChat.ReadOnly = true;
            rtbChat.Size = new Size(413, 323);
            rtbChat.TabIndex = 1;
            rtbChat.Text = "";
            // 
            // txtMessage
            // 
            txtMessage.BackColor = Color.FromArgb(64, 68, 75);
            txtMessage.BorderStyle = BorderStyle.FixedSingle;
            txtMessage.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point);
            txtMessage.ForeColor = Color.White;
            txtMessage.Location = new Point(175, 395);
            txtMessage.Name = "txtMessage";
            txtMessage.Size = new Size(313, 27);
            txtMessage.TabIndex = 2;
            // 
            // btnSend
            // 
            btnSend.BackColor = Color.FromArgb(114, 137, 218);
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.FlatStyle = FlatStyle.Flat;
            btnSend.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            btnSend.ForeColor = Color.White;
            btnSend.Location = new Point(503, 395);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(85, 27);
            btnSend.TabIndex = 3;
            btnSend.Text = "Send";
            btnSend.UseVisualStyleBackColor = false;
            btnSend.Click += new EventHandler(btnSend_Click);
            // 
            // btnAttach
            // 
            btnAttach.BackColor = Color.FromArgb(79, 84, 92);
            btnAttach.FlatAppearance.BorderSize = 0;
            btnAttach.FlatStyle = FlatStyle.Flat;
            btnAttach.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            btnAttach.ForeColor = Color.White;
            btnAttach.Location = new Point(12, 395);
            btnAttach.Name = "btnAttach";
            btnAttach.Size = new Size(150, 27);
            btnAttach.TabIndex = 4;
            btnAttach.Text = "📎 Attach File";
            btnAttach.UseVisualStyleBackColor = false;
            btnAttach.Click += new EventHandler(btnAttach_Click);
            // 
            // btnReconnect
            // 
            btnReconnect.BackColor = Color.FromArgb(67, 181, 129);
            btnReconnect.FlatAppearance.BorderSize = 0;
            btnReconnect.FlatStyle = FlatStyle.Flat;
            btnReconnect.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            btnReconnect.ForeColor = Color.White;
            btnReconnect.Location = new Point(175, 395);
            btnReconnect.Name = "btnReconnect";
            btnReconnect.Size = new Size(413, 27);
            btnReconnect.TabIndex = 8;
            btnReconnect.Text = "🔄 Reconnect to Server";
            btnReconnect.UseVisualStyleBackColor = false;
            btnReconnect.Visible = false;
            btnReconnect.Click += new EventHandler(btnReconnect_Click);
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(32, 34, 37);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(600, 40);
            panelTop.TabIndex = 5;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            lblStatus.ForeColor = Color.FromArgb(185, 187, 190);
            lblStatus.Location = new Point(12, 12);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(82, 15);
            lblStatus.Text = "ONLINE USERS";
            panelTop.Controls.Add(lblStatus);
            // 
            // ChatForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(32, 34, 37);
            ClientSize = new Size(600, 450);
            Controls.Add(panelTop);
            Controls.Add(btnReconnect);
            Controls.Add(btnAttach);
            Controls.Add(btnSend);
            Controls.Add(txtMessage);
            Controls.Add(rtbChat);
            Controls.Add(lstUsers);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "ChatForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ChatForm";
            Load += new EventHandler(ChatForm_Load);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ListBox lstUsers;
        private RichTextBox rtbChat;
        private TextBox txtMessage;
        private Button btnSend;
        private Button btnAttach;
        private Panel panelTop;
        private Label lblStatus;
        private Panel panelBottom;
        private Button btnReconnect;
    }
}