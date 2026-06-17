using System;
using System.Drawing;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FINAL_PROJECT_CLIENT
{
    public partial class LoginForm : Form
    {
        private TcpClient client;
        private NetworkStream stream;
        private string currentUser = "";

        public LoginForm()
        {
            InitializeComponent();
            lblStatus.Text = "";
        }

        private async void btnRegister_Click(object sender, EventArgs e)
        {
            await ProcessCommand(CommandType.Register);
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            await ProcessCommand(CommandType.Login);
        }

        private async Task ProcessCommand(CommandType cmd)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                lblStatus.ForeColor = Color.Red;
                lblStatus.Text = "Please enter username and password";
                return;
            }

            try
            {
                client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", 9000);
                stream = client.GetStream();

                string command = $"{cmd}|{username}|{password}\n";
                byte[] data = Encoding.UTF8.GetBytes(command);
                await stream.WriteAsync(data, 0, data.Length);

                // Read response (newline-delimited)
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                if (response == "REGISTER_OK" || response == "LOGIN_OK")
                {
                    currentUser = username;
                    lblStatus.ForeColor = Color.Green;
                    lblStatus.Text = response == "REGISTER_OK" ? "Registered successfully! Now login." : "Login successful!";

                    if (cmd == CommandType.Login)
                    {
                        this.Hide();
                        var chatForm = new ChatForm(client, stream, currentUser, password);
                        chatForm.FormClosed += (s, args) => this.Close();
                        chatForm.Show();
                    }
                }
                else
                {
                    lblStatus.ForeColor = Color.Red;
                    lblStatus.Text = response == "REGISTER_FAIL" ? "Username already taken" : "Invalid credentials";
                }
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = Color.Red;
                lblStatus.Text = "Connection error: " + ex.Message;
            }
        }
    }
}