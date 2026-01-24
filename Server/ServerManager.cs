using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autofac;
using static System.Net.Mime.MediaTypeNames;

namespace OpenGSServer
{
    interface IServerManager
    {

    }

    sealed class ServerManager
    {

        public static ServerManager Instance { get; private set; } = new();

        ServerSettings settings = new ServerSettings();




        private static MatchServer matchServer_ = new MatchServer();
        private static GeneralServer generalServer_ = new GeneralServer();
        private static ManagementServer managementServer = new ManagementServer();

        // 管理者アカウントは AdminManager で管理
        private readonly AdminManager adminManager = AdminManager.CreateDefault();


        internal ServerSettings Settings { get => settings; set => settings = value; }





        public MatchServer GetMatchServer()
        {

            return matchServer_;
        }

        public GeneralServer GetGeneralServer()
        {

            return generalServer_;
        }

        public ManagementServer GetManagementServer()
        {
            return managementServer;
        }

        void AddRegisterAdminAccount(string id, string pass)
        {
            try
            {
                var added = adminManager.AddAdmin(id, pass);
                if (!added)
                {
                    ConsoleWrite.WriteMessage($"[SERVER] Admin '{id}' already exists", ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[SERVER] Failed to add admin: {ex.Message}", ConsoleColor.Red);
            }
        }

        void RemoveAdminAccount(String id, String pass)
        {
            try
            {
                var removed = adminManager.RemoveAdmin(id);
                if (!removed)
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

            string openGSServerDirectory=System.IO.Path.Combine(path, "OpenGSServer");


            string absolutePath = System.IO.Path.Combine(openGSServerDirectory, "OpenGSServerSetting.json");

        }

        public void SaveSetting()
        {
            var json=settings.ToJson();

            var st = json.ToString();

            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            string openGSServerDirectory=System.IO.Path.Combine(path, "OpenGSServer");

            
            Directory.CreateDirectory(openGSServerDirectory);


            string absolutePath = System.IO.Path.Combine(openGSServerDirectory, "OpenGSServerSetting.json");

            try
            {
                //ファイルをオープンする
                using (StreamWriter sw = new StreamWriter(absolutePath, false, Encoding.UTF8))
                {
                    sw.Write(st);

                    sw.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());

            }


        }

    }
}
