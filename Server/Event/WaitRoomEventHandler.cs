using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;


namespace OpenGSServer
{
    internal interface IWaitRoomEventHandler
    {

    }

    internal class WaitRoomEventHandler
    {
        public static void ChangePlayerSettting(in ClientSession session, IDictionary<string, JToken> dic)
        {
            string? playerId = dic.GetStringOrNull("PlayerId");

            if(playerId == null) { return; }

            

            string playerCharacter = dic.GetStringOrNull("PlayerCharacter");




            if(dic.TryGetValue("EquipInstantItems",out var instantItemToken))
            {
                if(instantItemToken.Type == JTokenType.Array)
                {

                }

                //var list=instantItemToken.ToArray<string>();

              //  var equipInstantItemlist = new List<string>();

            }
            
            


        }

        public static void ChangeRoomSetting(in ClientSession session,IDictionary<string, JToken> dic)
        {
            string? roomId;
            string? roomOwnerId;



            roomId = JsonHelper.GetStringOrNull(dic,"RoomId");

           if(roomId == null )
            {
                var failMessageJson = new JObject();

                failMessageJson["MessageType"] = "InvalidRoomId";
                failMessageJson["RoomId"] = "";

                session.SendAsyncJsonWithTimeStamp(failMessageJson);
                return;

            }




            var waitRoomManager = WaitRoomManager.Instance();

            var waitRoom = waitRoomManager.FindWaitRoom(roomId);

            if(waitRoom==null)
            {
                var failMessageJson=new JObject();

                failMessageJson["MessageType"] = "RoomNotFound";
                failMessageJson["RoomId"] = "";

                session.SendAsyncJsonWithTimeStamp(failMessageJson);
                return;
            }

            

           var roomJson= waitRoom.ToJson();

            var value = dic.TryGetValue("room", out var result) ? result : "test";

            var test = dic.GetValueOrDefaultString("room");

            


            //var waitRoom=WaitRoomManager.GetInstance().wait

            var json=new JObject();

            json["Message"] = "ChangeRoom";
            json["MessageType"] = "RoomSettingChanged";

            json["GameMode"] = roomJson["GameMode"];

            //waitRoom.a
            


        }

        public static void SendUpdateWaitRoom(in ClientSession session,IDictionary<string, JToken> dic)
        {
            string  roomId;


            var roomManager = WaitRoomManager.Instance();

            //var room = roomManager.FindWaitRoom(roomId);

            var json = new JObject();



            //var serverManager=ServerManager.Instance;


        }


        public static void CloseRoomRequest(in ClientSession session,IDictionary<string, JToken> dic)
        {
            string? roomId = dic.GetStringOrNull("RoomId");
            
            if(roomId==null)
            {

            }


        }

        public static void ExitRoomRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            string? roomId = dic.GetStringOrNull("RoomId");

            if(roomId==null)
            {

            }

            WaitRoomManager.Instance().FindWaitRoom(roomId);


        public static void ReadyRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            string? roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            string? playerId = dic.GetStringOrNull("PlayerId") ?? dic.GetStringOrNull("PlayerID");
            string? type = dic.GetStringOrNull("MessageType");

            if (roomId == null || playerId == null) return;

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            if (waitRoom == null) return;

            lock (waitRoom)
            {
                if (waitRoom.Players.TryGetValue(playerId, out var player))
                {
                    player.IsReady = (type == "WaitRoomPlayerReady");
                    
                    // 部屋の全プレイヤーに通知
                    var updateJson = new JObject();
                    updateJson["MessageType"] = "WaitRoomUpdateNotification";
                    updateJson["RoomInfo"] = waitRoom.ToJson();

                    foreach (var p in waitRoom.Players.Values)
                    {
                        var targetSession = LobbyServerManager.Instance().FindSessionByPlayerId(p.Id);
                        targetSession?.SendAsyncJsonWithTimeStamp(updateJson);
                    }
                }
            }
        }
    }
}
