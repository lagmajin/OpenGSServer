using Newtonsoft.Json.Linq;
using OpenGSCore;
using System;

namespace OpenGSServer
{
    public static class PlayerEventHandler
    {
        public static void UpdatePlayerRequest(in ClientSession session)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.PlayerID))
            {
                return;
            }

            var playerId = session.PlayerID;
            var account = AccountManager.GetInstance().GetPlayerAccount(playerId);
            var serverInfo = AccountManager.GetInstance().GetPlayerInformation(playerId);

            if (account == null)
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.PlayerInfoResponse,
                    ["Success"] = false,
                    ["Error"] = "PlayerNotFound",
                    ["TargetPlayerID"] = playerId
                });
                return;
            }

            var resultAccount = new PlayerAccount(account.Id, account.Name ?? string.Empty, string.Empty)
            {
                Wins = account.Wins,
                Kill = account.Kill,
                Death = account.Death,
                FlagReturn = account.FlagReturn,
                Lv = account.Lv,
                Exp = account.Exp,
                Character = account.Character,
                Matches = account.Matches,
                defaultPlayer = account.defaultPlayer
            };

            var result = new UserInfoResult(resultAccount, serverInfo);
            session.SendAsyncJsonWithTimeStamp(result.ToJson());
        }

        public static void OtherPlayerInfoRequest(in ClientSession session)
        {
            UpdatePlayerRequest(session);
        }
    }
}
