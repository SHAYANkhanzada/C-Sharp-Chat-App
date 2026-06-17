using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FINAL_PROJECT_CLIENT
{
    public partial class ChatForm : Form
    {
        private TcpClient client;
        private NetworkStream stream;
        private string username;
        private string selectedUser = "";
        private bool isConnected = true;
        private string serverHost;
        private int serverPort;
        private string password;

        // Unread message tracking
        private readonly System.Collections.Generic.HashSet<string> unreadUsers =
            new(System.StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.Dictionary<string, int> unreadCounts =
            new(System.StringComparer.OrdinalIgnoreCase);

        // Win32 API for flashing taskbar
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;

        private class ChatMessage
        {
            public string Sender { get; set; }
            public string Text { get; set; }
            public DateTime Time { get; set; }
        }

        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ChatMessage>> chatHistories = 
            new(System.StringComparer.OrdinalIgnoreCase);

        public ChatForm(TcpClient tcpClient, NetworkStream networkStream, string user, string pass = "")
        {
            InitializeComponent();
            client = tcpClient;
            stream = networkStream;
            username = user;
            password = pass;
            this.Text = $"Chat - {username}";

            // Store server connection details for reconnect
            try
            {
                var endpoint = (System.Net.IPEndPoint)client.Client.RemoteEndPoint;
                serverHost = endpoint.Address.ToString();
                serverPort = endpoint.Port;
            }
            catch
            {
                serverHost = "127.0.0.1";
                serverPort = 9000;
            }
        }

        private async void ChatForm_Load(object sender, EventArgs e)
        {
            try
            {
                await GetOnlineUsers();
            }
            catch (Exception ex)
            {
                HandleDisconnect();
                return;
            }
            _ = Task.Run(ListenForMessages);
        }

        private async Task GetOnlineUsers()
        {
            await SendCommand(CommandType.GetUserList);
        }

        private void lstUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedUser = lstUsers.SelectedItem?.ToString() ?? "";
            rtbChat.Clear();

            // Clear unread state for selected user
            if (!string.IsNullOrEmpty(selectedUser))
            {
                unreadUsers.Remove(selectedUser);
                unreadCounts.Remove(selectedUser);
                lstUsers.Invalidate(); // Redraw to remove badge
            }

            if (!string.IsNullOrEmpty(selectedUser) && chatHistories.TryGetValue(selectedUser, out var messages))
            {
                foreach (var msg in messages)
                {
                    AppendMessageToUI(msg.Sender, msg.Text, msg.Time);
                }
            }
        }

        private void lstUsers_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            string userName = lstUsers.Items[e.Index].ToString();
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool hasUnread = unreadUsers.Contains(userName);
            int unreadCount = unreadCounts.ContainsKey(userName) ? unreadCounts[userName] : 0;

            // Background
            Color bgColor;
            if (isSelected)
                bgColor = Color.FromArgb(66, 70, 77);
            else if (hasUnread)
                bgColor = Color.FromArgb(44, 50, 40); // subtle green-tinted background for unread
            else
                bgColor = Color.FromArgb(47, 49, 54);

            using (SolidBrush bgBrush = new SolidBrush(bgColor))
                e.Graphics.FillRectangle(bgBrush, e.Bounds);

            // Green left bar for unread
            if (hasUnread)
            {
                using (SolidBrush barBrush = new SolidBrush(Color.FromArgb(67, 181, 129)))
                    e.Graphics.FillRectangle(barBrush, e.Bounds.X, e.Bounds.Y, 4, e.Bounds.Height);
            }

            // Username text
            Color textColor = hasUnread ? Color.White : Color.FromArgb(185, 187, 190);
            FontStyle fontStyle = hasUnread ? FontStyle.Bold : FontStyle.Regular;
            using (Font textFont = new Font("Segoe UI", 10F, fontStyle))
            {
                float textY = e.Bounds.Y + (e.Bounds.Height - textFont.GetHeight(e.Graphics)) / 2;
                using (SolidBrush textBrush = new SolidBrush(textColor))
                    e.Graphics.DrawString(userName, textFont, textBrush, e.Bounds.X + 10, textY);
            }

            // Unread count badge
            if (hasUnread && unreadCount > 0)
            {
                string countText = unreadCount > 99 ? "99+" : unreadCount.ToString();
                using (Font badgeFont = new Font("Segoe UI", 7.5F, FontStyle.Bold))
                {
                    SizeF countSize = e.Graphics.MeasureString(countText, badgeFont);
                    float badgeW = Math.Max(countSize.Width + 8, 20);
                    float badgeH = 18;
                    float badgeX = e.Bounds.Right - badgeW - 8;
                    float badgeY = e.Bounds.Y + (e.Bounds.Height - badgeH) / 2;

                    RectangleF badgeRect = new RectangleF(badgeX, badgeY, badgeW, badgeH);
                    using (GraphicsPath path = GetRoundedRect(badgeRect, 9))
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        using (SolidBrush badgeBrush = new SolidBrush(Color.FromArgb(240, 71, 71)))
                            e.Graphics.FillPath(badgeBrush, path);

                        using (SolidBrush badgeTextBrush = new SolidBrush(Color.White))
                        {
                            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            e.Graphics.DrawString(countText, badgeFont, badgeTextBrush, badgeRect, sf);
                        }
                        e.Graphics.SmoothingMode = SmoothingMode.Default;
                    }
                }
            }
        }

        private GraphicsPath GetRoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("You are disconnected from the server. Cannot send messages.", "Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedUser))
            {
                MessageBox.Show("Please select a user from the list.");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtMessage.Text))
                return;

            try
            {
                string messageText = txtMessage.Text.Trim();
                await SendCommand(CommandType.SendMessage, selectedUser, messageText);
                AppendMessage(selectedUser, "You", messageText, DateTime.Now);
                txtMessage.Clear();
            }
            catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
            {
                HandleDisconnect();
            }
        }

        private async void btnAttach_Click(object sender, EventArgs e)
        {
            if (!isConnected)
            {
                MessageBox.Show("You are disconnected from the server. Cannot send files.", "Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedUser))
            {
                MessageBox.Show("Please select a user from the list.");
                return;
            }

            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string filePath = ofd.FileName;
                    string fileName = Path.GetFileName(filePath);
                    long fileSize = new FileInfo(filePath).Length;

                    await SendCommand(CommandType.SendFile, selectedUser, $"{fileName}|{fileSize}");

                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await stream.WriteAsync(buffer, 0, bytesRead);
                        }
                    }

                    AppendMessage(selectedUser, "You", $"[Sent file: {fileName}]", DateTime.Now);
                }
                catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
                {
                    HandleDisconnect();
                }
            }
        }

        private async Task SendCommand(CommandType cmd, string to = "", string content = "")
        {
            if (!isConnected) return;
            string message = $"{cmd}|{to}|{content}\n";
            byte[] data = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(data, 0, data.Length);
        }

        private async Task ListenForMessages()
        {
            byte[] buffer = new byte[8192];
            StringBuilder pendingData = new StringBuilder();
            try
            {
                while (true)
                {
                    int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytes == 0) break;

                    pendingData.Append(Encoding.UTF8.GetString(buffer, 0, bytes));
                    string accumulated = pendingData.ToString();

                    // Process all complete messages (delimited by \n)
                    int lastNewline = accumulated.LastIndexOf('\n');
                    if (lastNewline == -1) continue; // No complete message yet

                    string completeData = accumulated.Substring(0, lastNewline);
                    pendingData.Clear();
                    if (lastNewline < accumulated.Length - 1)
                        pendingData.Append(accumulated.Substring(lastNewline + 1));

                    string[] messages = completeData.Split('\n');
                    foreach (string rawMsg in messages)
                    {
                        string msg = rawMsg.Trim();
                        if (string.IsNullOrEmpty(msg)) continue;

                        if (msg.StartsWith("USERLIST|"))
                        {
                            string list = msg.Substring("USERLIST|".Length);
                            this.Invoke((MethodInvoker)(() =>
                            {
                                lstUsers.Items.Clear();
                                foreach (string u in list.Split(','))
                                {
                                    if (!string.IsNullOrEmpty(u) && u != username)
                                        lstUsers.Items.Add(u);
                                }
                            }));
                        }
                        else if (msg.StartsWith("MESSAGE|"))
                        {
                            int firstPipe = msg.IndexOf('|', 8);
                            if (firstPipe > 8)
                            {
                                string from = msg.Substring(8, firstPipe - 8);
                                string text = msg.Substring(firstPipe + 1);
                                this.Invoke((MethodInvoker)(() =>
                                {
                                    AppendMessage(from, from, text, DateTime.Now);

                                    // If this message is NOT from the currently selected user, mark as unread
                                    if (!from.Equals(selectedUser, StringComparison.OrdinalIgnoreCase))
                                    {
                                        unreadUsers.Add(from);
                                        if (!unreadCounts.ContainsKey(from))
                                            unreadCounts[from] = 0;
                                        unreadCounts[from]++;
                                        lstUsers.Invalidate(); // Redraw list to show badge
                                    }

                                    // Flash taskbar if window is not focused
                                    if (!this.ContainsFocus)
                                    {
                                        FlashWindow();
                                    }
                                }));
                            }
                        }
                        else if (msg.StartsWith("FILE|"))
                        {
                            string[] parts = msg.Split('|');
                            if (parts.Length == 4)
                            {
                                string from = parts[1];
                                string fileName = parts[2];
                                long fileSize = long.Parse(parts[3]);

                                // Read the file bytes immediately
                                byte[] fileBytes = new byte[fileSize];
                                int totalReceived = 0;
                                while (totalReceived < fileSize)
                                {
                                    int r = await stream.ReadAsync(fileBytes, totalReceived, (int)(fileSize - totalReceived));
                                    if (r == 0) break;
                                    totalReceived += r;
                                }

                                this.Invoke((MethodInvoker)(() =>
                                {
                                    AppendMessage(from, from, $"[Received file: {fileName}]", DateTime.Now);
                                    _ = PromptSaveFile(fileName, fileBytes);
                                }));
                            }
                        }
                    }
                }
            }
            catch
            {
                this.Invoke((MethodInvoker)(() => HandleDisconnect()));
            }
        }

        private async Task PromptSaveFile(string fileName, byte[] fileBytes)
        {
            SaveFileDialog sfd = new SaveFileDialog
            {
                FileName = fileName,
                Title = "Save received file"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await File.WriteAllBytesAsync(sfd.FileName, fileBytes);
                    MessageBox.Show($"File {fileName} saved successfully!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving file: " + ex.Message);
                }
            }
        }

        private void AppendMessage(string conversationPartner, string from, string text, DateTime time)
        {
            if (string.IsNullOrEmpty(conversationPartner)) return;

            if (!chatHistories.TryGetValue(conversationPartner, out var messages))
            {
                messages = new System.Collections.Generic.List<ChatMessage>();
                chatHistories[conversationPartner] = messages;
            }

            messages.Add(new ChatMessage { Sender = from, Text = text, Time = time });

            if (selectedUser.Equals(conversationPartner, StringComparison.OrdinalIgnoreCase))
            {
                AppendMessageToUI(from, text, time);
            }
        }

        private void AppendMessageToUI(string from, string text, DateTime time)
        {
            string timeStr = time.ToString("HH:mm");
            
            rtbChat.SelectionStart = rtbChat.TextLength;
            rtbChat.SelectionLength = 0;

            // Time
            rtbChat.SelectionColor = Color.FromArgb(114, 118, 125);
            rtbChat.AppendText($"[{timeStr}] ");

            // Username
            if (from == "You")
                rtbChat.SelectionColor = Color.FromArgb(114, 137, 218);
            else
                rtbChat.SelectionColor = Color.FromArgb(67, 181, 129);
            
            rtbChat.SelectionFont = new Font(rtbChat.Font, FontStyle.Bold);
            rtbChat.AppendText($"{from}: ");

            // Message
            rtbChat.SelectionColor = Color.FromArgb(220, 221, 222);
            rtbChat.SelectionFont = new Font(rtbChat.Font, FontStyle.Regular);
            rtbChat.AppendText($"{text}{Environment.NewLine}");

            rtbChat.ScrollToCaret();
        }

        private void FlashWindow()
        {
            FLASHWINFO fwi = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                hwnd = this.Handle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = 3,
                dwTimeout = 0
            };
            FlashWindowEx(ref fwi);
        }

        private void HandleDisconnect()
        {
            if (!isConnected) return; // Prevent multiple disconnect popups
            isConnected = false;

            // Disable send controls
            btnSend.Enabled = false;
            btnAttach.Enabled = false;
            txtMessage.Enabled = false;
            txtMessage.Text = "";
            txtMessage.PlaceholderText = "Disconnected from server";

            // Update title bar
            this.Text = $"Chat - {username} (DISCONNECTED)";

            // Change status label
            lblStatus.Text = "⚠ DISCONNECTED";
            lblStatus.ForeColor = Color.FromArgb(240, 71, 71);

            // Show disconnection message in chat
            rtbChat.SelectionStart = rtbChat.TextLength;
            rtbChat.SelectionLength = 0;
            rtbChat.SelectionColor = Color.FromArgb(240, 71, 71);
            rtbChat.SelectionFont = new Font(rtbChat.Font, FontStyle.Bold);
            rtbChat.AppendText($"{Environment.NewLine}[Server connection lost. Click 'Reconnect' to try again.]{Environment.NewLine}");
            rtbChat.ScrollToCaret();

            // Show reconnect button
            btnReconnect.Visible = true;
            btnReconnect.BringToFront();

            MessageBox.Show("Connection to server has been lost.\nClick 'Reconnect' when the server is back online.",
                "Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private async void btnReconnect_Click(object sender, EventArgs e)
        {
            btnReconnect.Enabled = false;
            btnReconnect.Text = "⏳ Connecting...";

            try
            {
                // Close old connection
                try { stream?.Close(); } catch { }
                try { client?.Close(); } catch { }

                // Create new connection
                client = new TcpClient();
                await client.ConnectAsync(serverHost, serverPort);
                stream = client.GetStream();

                // Re-login
                string loginCmd = $"{CommandType.Login}|{username}|{password}\n";
                byte[] data = Encoding.UTF8.GetBytes(loginCmd);
                await stream.WriteAsync(data, 0, data.Length);

                // Read login response
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                if (response == "LOGIN_OK")
                {
                    // Reconnection successful!
                    isConnected = true;

                    // Re-enable UI
                    btnSend.Enabled = true;
                    btnAttach.Enabled = true;
                    txtMessage.Enabled = true;
                    txtMessage.PlaceholderText = "";
                    btnReconnect.Visible = false;

                    // Update title and status
                    this.Text = $"Chat - {username}";
                    lblStatus.Text = "ONLINE USERS";
                    lblStatus.ForeColor = Color.FromArgb(185, 187, 190);

                    // Show reconnected message in chat
                    rtbChat.SelectionStart = rtbChat.TextLength;
                    rtbChat.SelectionLength = 0;
                    rtbChat.SelectionColor = Color.FromArgb(67, 181, 129);
                    rtbChat.SelectionFont = new Font(rtbChat.Font, FontStyle.Bold);
                    rtbChat.AppendText($"[Reconnected to server successfully!]{Environment.NewLine}");
                    rtbChat.ScrollToCaret();

                    // Refresh online users and start listening again
                    await GetOnlineUsers();
                    _ = Task.Run(ListenForMessages);
                }
                else
                {
                    MessageBox.Show("Reconnection failed: Server rejected login.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnReconnect.Enabled = true;
                    btnReconnect.Text = "🔄 Reconnect to Server";
                }
            }
            catch (SocketException)
            {
                MessageBox.Show("Cannot connect to the server. It appears to be offline.\nPlease make sure the server is running and try again.",
                    "Server Offline", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                btnReconnect.Enabled = true;
                btnReconnect.Text = "🔄 Reconnect to Server";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Reconnection error: {ex.Message}",
                    "Reconnect Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnReconnect.Enabled = true;
                btnReconnect.Text = "🔄 Reconnect to Server";
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            isConnected = false;
            try
            {
                SendCommand(CommandType.Logout).Wait(500);
            }
            catch { }
            client?.Close();
            base.OnFormClosed(e);
        }
    }
}