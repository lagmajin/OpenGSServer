using OpenGSCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGSServer
{
    public partial class DeprecatedMatchRoom
    {
        public bool IsOwner(in string id)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(RoomOwnerId))
            {
                return false;
            }

            return string.Equals(RoomOwnerId, id, StringComparison.Ordinal);
        }

        public bool ChangeOwner(in string newOwnerId)
        {
            if (Players.Count <= 1 || string.IsNullOrEmpty(newOwnerId))
            {
                return false;
            }

            if (!IsMember(newOwnerId))
            {
                return false;
            }

            RoomOwnerId = newOwnerId;
            return true;
        }

        public PlayerInfo GetPlayer(PlayerID id)
        {
            return Players.FirstOrDefault(p => p.Id == id.ToString());
        }

#nullable enable
        public string? RoomOwnerName()
        {
            var owner = RoomOwnerInfo();
            return owner?.PlayerName;
        }

        public PlayerInfo? RoomOwnerInfo()
        {
            if (string.IsNullOrEmpty(RoomOwnerId))
            {
                return Players.Count > 0 ? Players[0] : null;
            }

            return Players.FirstOrDefault(p => p.Id == RoomOwnerId);
        }

        public List<string> RoomMembersNameList()
        {
            return Players.Select(p => p.PlayerName ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList();
        }

        public List<PlayerInfo> RoomMemberList()
        {
            return new List<PlayerInfo>(Players);
        }

        public bool IsMember(in string id)
        {
            if (string.IsNullOrEmpty(id) || Players.Count == 0)
            {
                return false;
            }

            return Players.Exists(p => p.Id == id);
        }
    }
}
