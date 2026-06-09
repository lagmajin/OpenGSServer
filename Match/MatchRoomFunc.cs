using OpenGSCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGSServer
{
    public partial class DeprecatedMatchRoom
    {
        public bool IsOwner(string id)
        {
            if (string.IsNullOrEmpty(id) || Players.Count == 0)
            {
                return false;
            }

            return Players.Count > 0 && string.Equals(Players[0].Id, id, StringComparison.Ordinal);
        }

        public bool ChangeOwner(string newOwnerId)
        {
            if (Players.Count <= 1 || string.IsNullOrEmpty(newOwnerId))
            {
                return false;
            }

            if (!IsMember(newOwnerId))
            {
                return false;
            }

            var target = Players.First(p => p.Id == newOwnerId);
            Players.Remove(target);
            Players.Insert(0, target);
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
            return owner?.Name;
        }

        public PlayerInfo? RoomOwnerInfo()
        {
            return Players.Count > 0 ? Players[0] : null;
        }

        public List<string> RoomMembersNameList()
        {
            return Players.Select(p => p.Name ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList();
        }

        public List<PlayerInfo> RoomMemberList()
        {
            return new List<PlayerInfo>(Players);
        }

        public bool IsMember(string id)
        {
            if (string.IsNullOrEmpty(id) || Players.Count == 0)
            {
                return false;
            }

            return Players.Exists(p => p.Id == id);
        }
    }
}
