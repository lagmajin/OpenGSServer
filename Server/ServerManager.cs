using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    interface IServerManager
    {
    }

    sealed class ServerManager
    {
        public static ServerManager Instance { get; private set; } = new();

        private ServerSettings settings = new ServerSettings();

        private static MatchServer matchServer_ = new MatchServer();
        private static GeneralServer generalServer_ = new GeneralServer();
        private static ManagementServer managementServer = new ManagementServer();

        // 管理者アカウントは AdminManager で管理
        private readonly AdminManager adminManager = AdminManager.CreateDefault();
        private readonly object _adminInitLock = new();
        private bool _adminInitialized;

        internal ServerSettings Settings { get => settings; set => settings = value; }

        private ServerManager()
        {
            InitializeAdminAccounts();
        }

        public void InitializeAdminAccounts()
        {
            lock (_adminInitLock)
            {
                if (_adminInitialized)
                {
                    return;
                }

                adminManager.Load();

                if (adminManager.ListAdminIds().Count == 0)
                {
                    var envAdminId = Environment.GetEnvironmentVariable("OPENGS_ADMIN_ID");
                    var envAdminPassword = Environment.GetEnvironmentVariable("OPENGS_ADMIN_PASSWORD");

                    if (!string.IsNullOrWhiteSpace(envAdminId) && !string.IsNullOrWhiteSpace(envAdminPassword))
                    {
                        adminManager.AddAdmin(envAdminId, envAdminPassword);
                        adminManager.Save();
                        ConsoleWrite.WriteMessage("[SERVER] Admin account initialized from environment variables.", ConsoleColor.Green);
                    }
                    else
                    {
                        ConsoleWrite.WriteMessage(
                            "[SERVER] No admin account found. Set OPENGS_ADMIN_ID and OPENGS_ADMIN_PASSWORD to enable management login.",
                            ConsoleColor.Yellow);
                    }
                }

                _adminInitialized = true;
            }
        }

        public bool VerifyAdminCredential(string id, string pass)
        {
            InitializeAdminAccounts();
            return adminManager.VerifyAdmin(id, pass);
        }

        public bool HasAdminAccount()
        {
            InitializeAdminAccounts();
            return adminManager.ListAdminIds().Count > 0;
        }

        public MatchServer GetMatchServer() => matchServer_;
        public GeneralServer GetGeneralServer() => generalServer_;
        public ManagementServer GetManagementServer() => managementServer;

        public void AddRegisterAdminAccount(string id, string pass)
        {
            try
            {
                var added = adminManager.AddAdmin(id, pass);
                if (added)
                {
                    adminManager.Save();
                }
                else
                {
                    ConsoleWrite.WriteMessage($"[SERVER] Admin '{id}' already exists", ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[SERVER] Failed to add admin: {ex.Message}", ConsoleColor.Red);
            }
        }

        public void RemoveAdminAccount(string id)
        {
            try
            {
                var removed = adminManager.RemoveAdmin(id);
                if (removed)
                {
                    adminManager.Save();
                }
                else
                {
                    ConsoleWrite.WriteMessage($"[SERVER] Admin '{id}' not found", ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[SERVER] Failed to remove admin: {ex.Message}", ConsoleColor.Red);
            }
        }

        public void LoadSetting()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string openGSServerDirectory = Path.Combine(path, "OpenGSServer");
            string absolutePath = Path.Combine(openGSServerDirectory, "OpenGSServerSetting.json");

            if (!File.Exists(absolutePath))
            {
                ConsoleWrite.WriteMessage($"[SERVER] Settings file not found at {absolutePath}. Using defaults.", ConsoleColor.Yellow);
                return;
            }

            try
            {
                string jsonStr = File.ReadAllText(absolutePath, Encoding.UTF8);
                var json = JObject.Parse(jsonStr);
                settings.SetFromJson(json);
                ConsoleWrite.WriteMessage($"[SERVER] Settings loaded from {absolutePath}.", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[SERVER] LoadSetting failed: {ex.Message}", ConsoleColor.Red);
            }
        }

        public void SaveSetting()
        {
            var json = settings.ToJson();
            var st = json.ToString();

            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string openGSServerDirectory = Path.Combine(path, "OpenGSServer");

            try
            {
                Directory.CreateDirectory(openGSServerDirectory);
                string absolutePath = Path.Combine(openGSServerDirectory, "OpenGSServerSetting.json");

                using (StreamWriter sw = new StreamWriter(absolutePath, false, Encoding.UTF8))
                {
                    sw.Write(st);
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[SERVER] SaveSetting failed: {ex.Message}", ConsoleColor.Red);
            }
        }
    }
}
