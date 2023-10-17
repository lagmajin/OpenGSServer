using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public class DBGuildMember
    {
        public string guildId { get; set; }

        public string Id { get; set; }

        //public string Name { get; set; }

        public string TimeStamp { get; set; }


        public DBGuildMember(DBGuildMember member)
        {
            this.guildId = member.guildId;
            this.Id = member.Id;


        }

    }
}
