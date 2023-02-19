using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;

namespace OpenGSServer
{

    public class DBGuild
    {

        [BsonId] public string id { get; set; }

        public string GuildShortName { get; set; }
        public string GuildName { get; set; }

        public string CreationTime { get; set; }

        public DBGuild(in string guildName)
        {
            id = Guid.NewGuid().ToString("N");
            GuildName = guildName;


        }

        public DBGuild(in DBGuild guild)
        {

        }
    }
}
