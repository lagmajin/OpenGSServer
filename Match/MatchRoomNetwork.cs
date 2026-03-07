using Newtonsoft.Json.Linq;
using OpenGSCore;
using System;
using System.Collections.Generic;

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
            foreach (var player in room.Players)
            {
                // ここで実際の送信処理を呼び出す
                // サーバー側には ClientSession が紐づいているはず
                // 注意: PlayerInfo に Session を持たせるか、管理クラスから引く必要がある
            }
        }
    }
}
