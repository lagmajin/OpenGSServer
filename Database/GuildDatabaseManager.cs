
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

        public List<DBGuild> GetAllGuilds()
        {
            var result = new List<DBGuild>();

            var col = GuildCollection();
            result.AddRange(col.FindAll());

            return result;
        }

        public bool ExistGuild(string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName))
            {
                return false;
            }

            var col = GuildCollection();

            return col.Exists(g => g.GuildName == guildName);

        }

        public bool CreateNewGuild(string guildName, string guildShortName = null)
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

        public bool CreateNewGuild(string guildName, string leaderId, string guildShortName = null)
        {
            if (string.IsNullOrWhiteSpace(guildName) || string.IsNullOrWhiteSpace(leaderId))
            {
                return false;
            }

            var col = GuildCollection();

            if (col.Exists(g => g.GuildName == guildName))
            {
                return false;
            }

            var dbGuild = string.IsNullOrWhiteSpace(guildShortName) ? new DBGuild(guildName) : new DBGuild(guildName, guildShortName);
            dbGuild.LeaderId = leaderId;

            col.Insert(dbGuild);

            // リーダーをメンバーとして追加
            AddGuildMember(leaderId, guildName, "Leader");

            return true;
        }

        public bool UpdateGuild(DBGuild guild)
        {
            if (guild == null || string.IsNullOrWhiteSpace(guild.id))
            {
                return false;
            }

            return GuildCollection().Update(guild);
        }

        public bool RemoveGuild(string guildName)
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

        public bool AddGuildMember(string id, string guild, string role = "Member")
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

            memberCollection.Insert(new DBGuildMember(guildData.id, id, role));

            if (string.Equals(role, "Leader", StringComparison.OrdinalIgnoreCase))
            {
                guildData.LeaderId = id;
                GuildCollection().Update(guildData);
            }

            return true;
        }

        public bool RemoveGuildMember(string guildName, string memberId)
        {
            if (string.IsNullOrWhiteSpace(guildName) || string.IsNullOrWhiteSpace(memberId))
            {
                return false;
            }

            var guildData = FindGuildByName(guildName);
            if (guildData == null)
            {
                return false;
            }

            var memberCollection = GuildMemberCollection();
            var targetMember = memberCollection.FindOne(m => m.guildId == guildData.id && m.Id == memberId);
            if (targetMember == null)
            {
                return false;
            }

            if (string.Equals(guildData.LeaderId, memberId, StringComparison.OrdinalIgnoreCase))
            {
                memberCollection.DeleteMany(m => m.guildId == guildData.id && m.Id == memberId);

                EnsureGuildLeader(guildData, memberCollection, memberId);
                return true;
            }

            return memberCollection.DeleteMany(m => m.guildId == guildData.id && m.Id == memberId) > 0;
        }

        public bool JoinGuild(string guildName, string memberId, string role = "Member")
        {
            return AddGuildMember(memberId, guildName, role);
        }

        public bool LeaveGuild(string guildName, string memberId)
        {
            return RemoveGuildMember(guildName, memberId);
        }

        public bool UpdateGuildMemberRole(string guildName, string memberId, string role)
        {
            if (string.IsNullOrWhiteSpace(guildName) || string.IsNullOrWhiteSpace(memberId) || string.IsNullOrWhiteSpace(role))
            {
                return false;
            }

            var guildData = FindGuildByName(guildName);
            if (guildData == null)
            {
                return false;
            }

            var memberCollection = GuildMemberCollection();
            var member = memberCollection.FindOne(m => m.guildId == guildData.id && m.Id == memberId);
            if (member == null)
            {
                return false;
            }

            if (string.Equals(role, "Leader", StringComparison.OrdinalIgnoreCase))
            {
                var currentLeader = memberCollection.FindOne(m => m.guildId == guildData.id && m.Role == "Leader" && m.Id != memberId);
                if (currentLeader != null)
                {
                    currentLeader.Role = "Member";
                    currentLeader.TimeStamp = DateTime.UtcNow.ToString("o");
                    memberCollection.Update(currentLeader);
                }

                guildData.LeaderId = memberId;
                GuildCollection().Update(guildData);
            }
            member.Role = role;
            member.TimeStamp = DateTime.UtcNow.ToString("o");
            var updated = memberCollection.Update(member);

            if (!string.Equals(role, "Leader", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(guildData.LeaderId, memberId, StringComparison.OrdinalIgnoreCase))
            {
                EnsureGuildLeader(guildData, memberCollection, memberId);
            }

            return updated;
        }

        private void EnsureGuildLeader(DBGuild guildData, ILiteCollection<DBGuildMember> memberCollection, string excludedMemberId = "")
        {
            if (guildData == null || memberCollection == null)
            {
                return;
            }

            var currentLeader = memberCollection.FindOne(m =>
                m.guildId == guildData.id &&
                string.Equals(m.Role, "Leader", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(m.Id, excludedMemberId, StringComparison.OrdinalIgnoreCase));

            if (currentLeader != null)
            {
                guildData.LeaderId = currentLeader.Id;
                GuildCollection().Update(guildData);
                return;
            }

            var fallbackMember = memberCollection.FindOne(m =>
                m.guildId == guildData.id &&
                !string.Equals(m.Id, excludedMemberId, StringComparison.OrdinalIgnoreCase));

            if (fallbackMember != null)
            {
                fallbackMember.Role = "Leader";
                fallbackMember.TimeStamp = DateTime.UtcNow.ToString("o");
                memberCollection.Update(fallbackMember);
                guildData.LeaderId = fallbackMember.Id;
            }
            else
            {
                guildData.LeaderId = string.Empty;
            }

            GuildCollection().Update(guildData);
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

        public DBGuild? FindGuildByName(string guildName)
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
