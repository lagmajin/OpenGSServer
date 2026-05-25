using Newtonsoft.Json.Linq;
using OpenGSCore;

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
    }
}
