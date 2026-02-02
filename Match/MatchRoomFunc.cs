using OpenGSCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public partial class DeprecatedMatchRoom
    {
        public bool IsOwner(in string id)
        {
            return false;
        }

        public bool ChangeOwner()
        {
            if (Players.Count <= 1)
            {
                return false;
            }



            return false;
        }

        public void GetPlayer(PlayerID id)
        {

        }

#nullable enable
        public string? RoomOwnerName()
        {
            if (Players.Count == 0)
            {
                return null;
            }
            else
            {

            }





            return null;
        }
        public PlayerInfo? RoomOwnerInfo()
        {

            return null;
        }

        public List<string> RoomMembersNameList()
        {
            var result = new List<string>();

            return result;
        }

        public List<PlayerInfo>? RoomMemberList()
        {
            var info = new PlayerInfo();


            return Players;
        }

        public bool IsMember(in string id)
        {
            if (Players.Count == 0)
            {
                return false;
            }
            else
            {
                if (true)
                {

                    return true;
                }

            }

            return false;
        }

    }



}
