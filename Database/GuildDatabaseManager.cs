
using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGSServer
{



    public class GuildDatabaseManager : IAbstractDatabaseManager
    {
        private LiteDatabase db;
        private readonly object _syncRoot = new();

        static GuildDatabaseManager instance = new GuildDatabaseManager();

        public static string filename = "Database/guild.db";
        public static string connectionString = $"Filename={filename};connection=shared";

        public readonly string dbGuildTableName = "guild";
        public readonly string dbGuildMemberName = "guild_member";

        public static GuildDatabaseManager GetInstance()
        {
            return instance;
        }
        private LiteDatabase EnsureDatabase()
        {
            if (db == null)
            {
                lock (_syncRoot)
                {
                    db ??= new LiteDatabase(connectionString);
                }
            }

            return db;
        }
        private ILiteCollection<DBGuild> GuildCollection()
        {
            var collection = EnsureDatabase().GetCollection<DBGuild>(dbGuildTableName);
            collection.EnsureIndex(g => g.GuildName, true);
            return collection;
        }

        private ILiteCollection<DBGuildMember> GuildMemberCollection()
        {
            var collection = EnsureDatabase().GetCollection<DBGuildMember>(dbGuildMemberName);
            collection.EnsureIndex(m => m.guildId);
            collection.EnsureIndex(m => m.Id);
            return collection;
        }
        public void Connect()
        {
            EnsureDatabase();
        }


        public void Disconnect()
        {
            lock (_syncRoot)
            {
                db?.Dispose();
                db = null;
            }
        }

        public bool ExistGuild(in string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName))
            {
                return false;
            }

            var col = GuildCollection();

            return col.Exists(g => g.GuildName == guildName);

        }

        public bool CreateNewGuild(in string guildName, string guildShortName = null)
        {
            if (string.IsNullOrWhiteSpace(guildName))
            {
                return false;
            }

            var col = GuildCollection();

            if (col.Exists(g => g.GuildName == guildName))
            {
                return false;
            }

            var dbGuild = string.IsNullOrWhiteSpace(guildShortName) ? new DBGuild(guildName) : new DBGuild(guildName, guildShortName);

            col.Insert(dbGuild);

            return true;
        }

        public bool RemoveGuild(in string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName))
            {
                return false;
            }

            var guild = FindGuildByName(guildName);

            if (guild == null)
            {
                return false;
            }

            var removed = GuildCollection().Delete(guild.id);
            GuildMemberCollection().DeleteMany(m => m.guildId == guild.id);

            return removed;
        }

        public void RemoveAllGuild()
        {
            GuildCollection().DeleteAll();
            GuildMemberCollection().DeleteAll();
        }

        public bool AddGuildMember(in string id,in string guild)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(guild))
            {
                return false;
            }

            var guildData = FindGuildByName(guild);

            if (guildData == null)
            {
                return false;
            }

            var memberCollection = GuildMemberCollection();

            if (memberCollection.Exists(m => m.guildId == guildData.id && m.Id == id))
            {
                return false;
            }

            memberCollection.Insert(new DBGuildMember(guildData.id, id));

            return true;
        }

        public int AddGuildMembers(IEnumerable<string> ids, in string guild)
        {
            if (ids == null)
            {
                return 0;
            }

            var added = 0;

            foreach (var id in ids.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (AddGuildMember(id, guild))
                {
                    added++;
                }
            }

            return added;
        }

        public List<DBGuildMember> GetGuildMember(in string guildName)
        {
            var result=new List<DBGuildMember>();

            var guild = FindGuildByName(guildName);

            if (guild == null)
            {
                return result;
            }

            var col = GuildMemberCollection();

            result.AddRange(col.Find(m => m.guildId == guild.id));

            return result;
        }

        public DBGuild? FindGuildByName(in string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName))
            {
                return null;
            }

            var col = GuildCollection();

            return col.FindOne(g => g.GuildName == guildName);
        }

        public List<DBGuildMember> GetAllGuildMembers()
        {
            var result=new List<DBGuildMember>();

            var col = GuildMemberCollection();

            result.AddRange(col.FindAll());

            return result;
        }


        public int GuildCount()
        {
            return GuildCollection().Count();

        }




    }



}
