using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        private static List<ServerAdminAccount> adminAccounts = new List<ServerAdminAccount>();


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

            return null;
        }

        void AddRegisterAdminAccount(string id, string pass)
        {
            var aa = new ServerAdminAccount(id, pass);

            adminAccounts.Add(aa);

        }

        void RemoveAdminAccount(String id, String pass)
        {


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
