using OpenGSCore;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OpenGSServer.Player;

namespace OpenGSServer
{
    public class MissionWaitRoom
    {
        private readonly List<WaitRoomPlayerInfo> players = new();
        private int capacity = 8;
        private string roomName_ = "";
        private string ownerId_ = string.Empty;

        public int Capacity() => capacity;
        public string RoomName() => roomName_;
        public string OwnerId() => ownerId_;

        public void SetRoomName(string roomName) => roomName_ = roomName ?? string.Empty;
        public void SetCapacity(int value) => capacity = System.Math.Max(1, value);
        public IReadOnlyList<WaitRoomPlayerInfo> Players => players;

        public bool IsAllReady()
        {
            return players.Count > 0 && players.All(p => p.IsReady);
        }

        public void AddPlayer(WaitRoomPlayerInfo player)
        {
            if (player == null) return;
            if (players.Any(p => p.PlayerId == player.PlayerId)) return;
            players.Add(player);
            if (string.IsNullOrWhiteSpace(ownerId_))
            {
                ownerId_ = player.PlayerId;
            }
        }

        public void RemovePlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return;
            players.RemoveAll(p => p.PlayerId == playerId);
            if (ownerId_ == playerId)
            {
                ChangeRoomOwnerRandom();
            }
        }

        public void ChangeRoomOwnerRandom()
        {
            ownerId_ = players.Count > 0 ? players[0].PlayerId : string.Empty;
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["RoomName"] = RoomName(),
                ["RoomId"] = string.Empty,
                ["RoomOwnerId"] = OwnerId(),
                ["Capacity"] = Capacity(),
                ["PlayerCount"] = players.Count,
                ["TeamBalance"] = true,
                ["Players"] = new JArray(players.Select(p => p.ToJson()))
            };
        }
    }
}
