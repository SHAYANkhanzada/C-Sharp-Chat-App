using System;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FINAL_PROJECT_SERVER
{
    internal class Program
    {
        private static TcpListener listener;
        private static readonly ConcurrentDictionary<string, TcpClient> onlineUsers = new();
        private static readonly string dbPath = "chat.db";

        static async Task Main(string[] args)
        {
            InitializeDatabase();

            listener = new TcpListener(IPAddress.Any, 9000);
            listener.Start();
            Console.WriteLine("=== CHAT SERVER STARTED on port 9000 ===\n");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private static void InitializeDatabase()
        {
            if (!File.Exists(dbPath))
                SQLiteConnection.CreateFile(dbPath);

            using var conn = new SQLiteConnection($"Data Source={dbPath}");
            conn.Open();

            string sqlUsers = "CREATE TABLE IF NOT EXISTS Users (Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT UNIQUE NOT NULL, PasswordHash TEXT NOT NULL);";
            string sqlMessages = "CREATE TABLE IF NOT EXISTS Messages (Id INTEGER PRIMARY KEY AUTOINCREMENT, FromUser TEXT NOT NULL, ToUser TEXT NOT NULL, Content TEXT, IsFile INTEGER DEFAULT 0, FileName TEXT, IsRead INTEGER DEFAULT 0);";

            using (var cmd = new SQLiteCommand(sqlUsers, conn)) cmd.ExecuteNonQuery();
            using (var cmd = new SQLiteCommand(sqlMessages, conn)) cmd.ExecuteNonQuery();
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            string username = null;
            StringBuilder pendingData = new StringBuilder();

            try
            {
                byte[] buffer = new byte[8192];
                while (client.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    pendingData.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
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
                        string data = rawMsg.Trim();
                        if (string.IsNullOrEmpty(data)) continue;

                        string[] parts = data.Split('|');
                        if (parts.Length < 1) continue;

                        CommandType cmd;
                        if (!Enum.TryParse(parts[0], out cmd)) continue;

                        switch (cmd)
                        {
                            case CommandType.Register:
                                if (parts.Length == 3)
                                {
                                    bool success = RegisterUser(parts[1], parts[2]);
                                    await Send(stream, success ? "REGISTER_OK" : "REGISTER_FAIL");
                                }
                                break;

                            case CommandType.Login:
                                if (parts.Length == 3 && LoginUser(parts[1], parts[2]))
                                {
                                    username = parts[1];
                                    onlineUsers[username] = client;
                                    Console.WriteLine($"{username} logged in");

                                    await Send(stream, "LOGIN_OK");
                                    await SendOfflineMessages(stream, username);
                                    await BroadcastUserList();
                                }
                                else
                                {
                                    await Send(stream, "LOGIN_FAIL");
                                }
                                break;

                            case CommandType.GetUserList:
                                string list = string.Join(",", onlineUsers.Keys);
                                await Send(stream, "USERLIST|" + list);
                                break;

                            case CommandType.SendMessage:
                                if (parts.Length >= 3 && username != null)
                                {
                                    string toUser = parts[1];
                                    string content = string.Join("|", parts, 2, parts.Length - 2);

                                    SaveMessage(username, toUser, content);

                                    if (onlineUsers.TryGetValue(toUser, out TcpClient receiver))
                                    {
                                        await Send(receiver.GetStream(), $"MESSAGE|{username}|{content}");
                                    }
                                }
                                break;

                            case CommandType.SendFile:
                                if (parts.Length == 4 && username != null)
                                {
                                    string toUser = parts[1];
                                    string fileName = parts[2];
                                    long fileSize = long.Parse(parts[3]);

                                    // Receive file bytes from sender
                                    byte[] fileBytes = new byte[fileSize];
                                    int received = 0;
                                    while (received < fileSize)
                                    {
                                        int r = await stream.ReadAsync(fileBytes, received, (int)(fileSize - received));
                                        if (r == 0) break;
                                        received += r;
                                    }

                                    // Forward notification AND file bytes to receiver
                                    if (onlineUsers.TryGetValue(toUser, out TcpClient receiver))
                                    {
                                        NetworkStream recStream = receiver.GetStream();
                                        await Send(recStream, $"FILE|{username}|{fileName}|{fileSize}");
                                        
                                        // Small delay to ensure the receiver processes the notification string first
                                        await Task.Delay(100); 
                                        
                                        await recStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                                        await recStream.FlushAsync();
                                    }
                                }
                                break;

                            case CommandType.Logout:
                                if (username != null)
                                {
                                    onlineUsers.TryRemove(username, out _);
                                    Console.WriteLine($"{username} logged out");
                                    await BroadcastUserList();
                                }
                                return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                if (username != null)
                {
                    onlineUsers.TryRemove(username, out _);
                    await BroadcastUserList();
                }
                client.Close();
            }
        }

        private static void SaveMessage(string from, string to, string content, bool isFile = false)
        {
            using var conn = new SQLiteConnection($"Data Source={dbPath}");
            conn.Open();
            string sql = "INSERT INTO Messages (FromUser, ToUser, Content, IsFile) VALUES (@f, @t, @c, @isfile)";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@f", from);
            cmd.Parameters.AddWithValue("@t", to);
            cmd.Parameters.AddWithValue("@c", content);
            cmd.Parameters.AddWithValue("@isfile", isFile ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        private static async Task SendOfflineMessages(NetworkStream stream, string user)
        {
            using var conn = new SQLiteConnection($"Data Source={dbPath}");
            conn.Open();
            string sql = "SELECT FromUser, Content, IsFile FROM Messages WHERE ToUser = @u AND IsRead = 0";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", user);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string from = reader.GetString(0);
                string content = reader.GetString(1);
                bool isFile = reader.GetInt32(2) == 1;

                await Send(stream, isFile ? $"FILE|{from}|{content}" : $"MESSAGE|{from}|{content}");
            }

            string update = "UPDATE Messages SET IsRead = 1 WHERE ToUser = @u";
            using var updateCmd = new SQLiteCommand(update, conn);
            updateCmd.Parameters.AddWithValue("@u", user);
            updateCmd.ExecuteNonQuery();
        }

        private static async Task BroadcastUserList()
        {
            string list = string.Join(",", onlineUsers.Keys);
            foreach (var cl in onlineUsers.Values)
            {
                try
                {
                    await Send(cl.GetStream(), "USERLIST|" + list);
                }
                catch { }
            }
        }

        private static bool RegisterUser(string username, string password)
        {
            string hash = HashPassword(password);
            using var conn = new SQLiteConnection($"Data Source={dbPath}");
            conn.Open();
            string sql = "INSERT INTO Users (Username, PasswordHash) VALUES (@u, @p)";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@p", hash);
            try
            {
                cmd.ExecuteNonQuery();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool LoginUser(string username, string password)
        {
            string hash = HashPassword(password);
            using var conn = new SQLiteConnection($"Data Source={dbPath}");
            conn.Open();
            string sql = "SELECT 1 FROM Users WHERE Username = @u AND PasswordHash = @p";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@p", hash);
            return cmd.ExecuteScalar() != null;
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static async Task Send(NetworkStream stream, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
        }
    }
}