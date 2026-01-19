using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public class GuildManager
    {
        private readonly GuildDatabaseManager _database = GuildDatabaseManager.GetInstance();
        private readonly object _syncRoot = new();

        public static GuildManager Instance { get; } = new();

        public bool Exist(in string guildName)
        {
            return _database.ExistGuild(guildName);
        }

        public bool CreateNewGuild(in string guildName, string guildShortName = null)
        {
            if (string.IsNullOrWhiteSpace(guildName))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _database.CreateNewGuild(guildName, guildShortName);
            }
        }

        public bool RemoveGuild(in string guildName)
        {
            if (string.IsNullOrWhiteSpace(guildName))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _database.RemoveGuild(guildName);
            }
        }

        public void RemoveAllGuild()
        {
            lock (_syncRoot)
            {
                _database.RemoveAllGuild();
            }
        }

        public bool AddGuildMember(in string memberId, in string guildName)
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _database.AddGuildMember(memberId, guildName);
            }
        }

        public int AddGuildMembers(IEnumerable<string> memberIds, in string guildName)
        {
            if (memberIds == null)
            {
                return 0;
            }

            lock (_syncRoot)
            {
                return _database.AddGuildMembers(memberIds, guildName);
            }
        }

        public DBGuild? FindGuild(in string guildName)
        {
            return _database.FindGuildByName(guildName);
        }

        public IReadOnlyList<DBGuildMember> GetGuildMembers(in string guildName)
        {
            return _database.GetGuildMember(guildName);
        }
    }
}
