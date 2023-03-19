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
        public static GuildManager Instance
        {
            get;
            set;
        } = new();

        public GuildManager()
        {

        }

        public bool Exist(in string guildName)
        {
            var guildDatabaseManager = new GuildDatabaseManager();

            return guildDatabaseManager.ExistGuild(guildName);


        }

        public void CreateNewGuild(in string guildName)
        {
            var guildDatabaseManager = new GuildDatabaseManager();

            guildDatabaseManager.CreateNewGuild(guildName);


        }

        public void RemoveGuild(in string guildName)
        {
            var guildDatabaseManager = new GuildDatabaseManager();

        }

        public void RemoveAllGuild()
        {

        }

        public void AddGuildMember()
        {

        }

        public void AddGuildMembers()
        {

        }



    }
}
