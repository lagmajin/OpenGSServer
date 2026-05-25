using LiteDB;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace OpenGSServer
{
    internal class ServerInfoDatabaseManager
    {
        private const string ServerInfoCollectionName = "ServerInfo";

        private LiteDatabase db;
        public static readonly string serverDatabaseName = "Database/serverInfo.db";

        public static string connectionString = $"Filename={serverDatabaseName};connection=shared";
        public static ServerInfoDatabaseManager Instance { get; private set; }

        public ServerInfoDatabaseManager()
        {
            Instance ??= this;
        }

        public void Connect()
        {
            if (db != null)
            {
                return;
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(serverDatabaseName));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            db = new LiteDatabase(connectionString);
        }

        public void UpdateDatabase()
        {
            if (db == null)
            {
                Connect();
            }

            if (db == null)
            {
                return;
            }

            var collection = db.GetCollection<ServerInfoRecord>(ServerInfoCollectionName);
            collection.EnsureIndex(x => x.Id, true);

            var record = new ServerInfoRecord
            {
                Id = "current",
                ServerName = Environment.MachineName,
                ProcessName = Process.GetCurrentProcess().ProcessName,
                AssemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty,
                AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty,
                TimeZoneId = TimeZoneInfo.Local.Id,
                TimeZoneDisplayName = TimeZoneInfo.Local.DisplayName,
                UpdatedAtUtc = DateTime.UtcNow.ToString("O")
            };

            collection.Upsert(record);
        }

        public void ClearDatabase()
        {
            if (db == null)
            {
                Connect();
            }

            if (db == null)
            {
                return;
            }

            db.DropCollection(ServerInfoCollectionName);
        }

        public void RemoveDatabase()
        {
            db?.Dispose();
            db = null;

            var filePath = Path.GetFullPath(serverDatabaseName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private sealed class ServerInfoRecord
        {
            [BsonId]
            public string Id { get; set; } = "current";

            public string ServerName { get; set; } = string.Empty;
            public string ProcessName { get; set; } = string.Empty;
            public string AssemblyName { get; set; } = string.Empty;
            public string AssemblyVersion { get; set; } = string.Empty;
            public string TimeZoneId { get; set; } = string.Empty;
            public string TimeZoneDisplayName { get; set; } = string.Empty;
            public string UpdatedAtUtc { get; set; } = string.Empty;
        }
    }
}
