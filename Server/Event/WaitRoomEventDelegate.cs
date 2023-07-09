﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;


namespace OpenGSServer
{
    internal class WaitRoomEventDelegate
    {
        public static void ChangePlayerSettting(in ClientSession session, IDictionary<string, JToken> dic)
        {
            string? playerId = "";
            string playerCharacter = "";

            playerCharacter=dic.GetValueOrDefaultString("PlayerCharacter", "misty");

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
            string roomId;
            string roomOwnerId;



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

        }

        public static void ExitRoomRequest(in ClientSession session)
        {

        }

        public static void GameStartRequest(in ClientSession session,IDictionary<string,JToken> dic)
        {

        }

    }
}
