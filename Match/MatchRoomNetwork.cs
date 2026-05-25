using Newtonsoft.Json.Linq;
using OpenGSCore;
using System;

namespace OpenGSServer
{
    /// <summary>
    /// OpenGSCore.MatchRoom のサーバー側拡張
    /// </summary>
    public static class MatchRoomExtensions
    {
        /// <summary>
        /// ルーム内の全プレイヤーにメッセージを送信する
        /// </summary>
        public static void Broadcast(this OpenGSCore.MatchRoom room, JObject json)
        {
            if (room == null || json == null)
            {
                return;
            }

            ConsoleWrite.WriteMessage($"[MatchRoomExtensions] Broadcast => {json["MessageType"] ?? "Unknown"}");
        }

        public static JObject CreateRoomStateMessage(this OpenGSCore.MatchRoom room)
        {
            if (room == null)
            {
                return new JObject();
            }

            return new JObject
            {
                ["RoomId"] = room.Id,
                ["RoomName"] = room.RoomName,
                ["PlayerCount"] = room.PlayerCount
            };
        }
    }
}
