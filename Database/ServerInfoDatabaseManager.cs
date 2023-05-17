using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    internal class ServerInfoDatabaseManager
    {
        private LiteDatabase db;
        public static readonly string serverDatabaseName = "server";

        public static string connectionString = $"Filename={serverDatabaseName};connection=shared";
        public static ServerInfoDatabaseManager Instance { get; private set; }

        
        public ServerInfoDatabaseManager()
        {

        }

        public void Connect()
        {
            db = new LiteDatabase(connectionString);
        }

        public void UpdateDatabase()
        {

        }

        public void ClearDatabase()
        {

        }

        public void RemoveDatabase()
        {

        }

    }
}
