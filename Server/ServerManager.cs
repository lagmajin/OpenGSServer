using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGSServer
{
    interface IServerManager
    {

    }

    sealed class ServerManager
    {

        private static ServerManager _singleInstance = new ServerManager();

        ServerSettings settings = new ServerSettings();




        private static MatchServer matchServer_ = new MatchServer();
        private static GeneralServer generalServer_ = new GeneralServer();
        private static ManagementServer managementServer = new ManagementServer();

        private static List<ServerAdminAccount> adminAccounts = new List<ServerAdminAccount>();


        internal ServerSettings Settings { get => settings; set => settings = value; }



        public static ServerManager GetInstance()
        {
            return _singleInstance;
        }

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

    }
}
