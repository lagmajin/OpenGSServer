using LiteDB;
using System;
using System.Collections.Generic;

namespace OpenGSServer
{
    public class MatchDatabaseManager : IDisposable
    {
        public static MatchDatabaseManager Instance { get; } = new();

        private LiteDatabase db;

        private static string matchDatabaseFilename = "Database/match.db";
        public static string connectionString = $"Filename={matchDatabaseFilename};connection=shared";

        private MatchDatabaseManager()
        {
        }

        ~MatchDatabaseManager()
        {
            Dispose(false);
        }

        private LiteDatabase EnsureDatabase()
        {
            if (db == null)
            {
                Connect();
            }

            return db;
        }

        public void Connect()
        {
            if (db != null)
            {
                return;
            }

            db = new LiteDatabase(connectionString);
        }

        public void Disconnect()
        {
            db?.Dispose();
            db = null;
        }

        public void AddDMMatchDatabase(in DBMatchDeathMatchDatabaseData data)
        {
            var col = EnsureDatabase().GetCollection<DBMatchDeathMatchDatabaseData>("DBDeathMatch");
            col.Insert(data);
        }

        public void AddSuvMatchDatabase(in DSuvMatchDataData data)
        {
            var col = EnsureDatabase().GetCollection<DSuvMatchDataData>("DBSurvivalMatch");
            col.Insert(data);
        }

        public void AddTSuvMatchDatabase(in DBTeamDeathMatchData data)
        {
            var col = EnsureDatabase().GetCollection<DBTeamDeathMatchData>("DBTeamDeathMatch");
            col.Insert(data);
        }

        public void AddCTFMatchDatabase()
        {
            var col = EnsureDatabase().GetCollection<DBCTFMatchDataData>("DBCTFMatch");
            col.Insert(new DBCTFMatchDataData());
        }

        public void HourUpdate()
        {
            var database = EnsureDatabase();
            var stats = new Dictionary<string, int>
            {
                ["DBDeathMatch"] = database.GetCollection<DBMatchDeathMatchDatabaseData>("DBDeathMatch").Count(),
                ["DBSurvivalMatch"] = database.GetCollection<DSuvMatchDataData>("DBSurvivalMatch").Count(),
                ["DBTeamDeathMatch"] = database.GetCollection<DBTeamDeathMatchData>("DBTeamDeathMatch").Count(),
                ["DBCTFMatch"] = database.GetCollection<DBCTFMatchDataData>("DBCTFMatch").Count()
            };

            ConsoleWrite.WriteMessage(
                $"[MatchDB] Hour update => DM:{stats["DBDeathMatch"]} SV:{stats["DBSurvivalMatch"]} TDM:{stats["DBTeamDeathMatch"]} CTF:{stats["DBCTFMatch"]}",
                ConsoleColor.DarkCyan);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            db?.Dispose();
            db = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
