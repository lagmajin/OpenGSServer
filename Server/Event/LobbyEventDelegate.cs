using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

using System.Diagnostics;
using OpenGSCore;


namespace OpenGSServer
{
    public static class LobbyEventDelegate
    {
        //#CreateNewWaitRoom
        public static void CreateNewWaitRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string? playerName;
            string? playerID;
            //string? gameMode;

            

            var teamBalance = true;

            int maxCapacity = 0;

            //var result = eCreateNewRoomResult.Fail;
           // var reason = ECreateNewRoomReason.NoReason;

            var gameRoomManager = MatchRoomManager.Instance;

            var waitRoomManager = WaitRoomManager.Instance();

          

            if (dic.ContainsKey("PlayerID"))
            {
                playerID = dic["PlayerID"].ToString();
                if (string.IsNullOrEmpty(playerID))
                {



                }
                else
                {

                }
            }
            else
            {

            }

            var id=dic.GetValueOrDefaultString("PlayerID");

            ConsoleWrite.WriteMessage("aaa:"+id);


            //var t2=dic.GetOrDefault()

            var test = dic.GetValueOrDefaultString("TeamBalance", "true");

            var roomCapacity = dic.GetValueDefaultInt("RoomCapacity", 8);


            var boosterPower = dic.GetValueDefaultFloat("BoosterPower", 1.0f);


            var gameMode=dic.GetValueOrDefaultString("GameMode", "dm");





            if (gameMode == "dm" || gameMode == "deathmatch")
            {
                var result=waitRoomManager.CreateNewWaitRoom("");

                var room=result.Room;

                var setting = new DeathMatchSetting();

                room.ChangeMatchSetting(setting);

               

                //ConsoleWrite.WriteMessage("");
                var roomInfoJson = room.ToJson();
                //roomInfoJson["RoomName"] = room.RoomName;
                //roomInfoJson["RoomId"] = room.RoomId;
                //roomInfoJson["RoomNumber"] = "";
               // roomInfoJson["WaitingPlayerCount"] = room.PlayerCount;
                //roomInfoJson["Capacity"] = room.Capacity;

                roomInfoJson["GameMode"] = "";
                roomInfoJson["Map"]="";


                var json = new JObject();
                json["MessageType"] = "CreateNewWaitRoomSuccess";
                json["RoomInfo"]=roomInfoJson;

                session.SendAsyncJsonWithTimeStamp(json);

                

            }

            if (gameMode == "tdm" || gameMode == "teamdeathmatch")
            {

                var winConditionKill = dic.GetValueDefaultInt("WinConditionKill", 20);

                var setting = new TDMMatchSetting();







            }

            if (gameMode == "suv" || gameMode == "survival")
            {
               // var setting = new TDMMatchSetting();
                //if(dic.TryGetValue("MatchTime"))






            }

            if (gameMode == "tsuv"||gameMode=="TeamSurvival")
            {


            }

            if (gameMode == "ctf" || gameMode == "capturetheflag")
            {
               






            }



            return;

            ErrorResult:
            {







                return;
            }



        }


        public static void CreateNewMissionRoom(in ClientSession sesion, in IDictionary<string, JToken> dic)
        {
            string? playerName;
            string? playerID;

            var capacity = dic.GetValueDefaultInt("Capacity", 4);

            var missionRoomManager = MissionWaitRoomManager.Instance;



        }

        public static void QuickStartRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            //var matchRoomManager = MatchRoomManager.GetInstance();





        }
        public static void EnterRoomRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string? playerName;
            //string? playerID;

            string playerAccountID = "";




            var roomId = dic["RoomID"].ToString();


            var roomManager = WaitRoomManager.Instance();

            var room=roomManager.FindWaitRoom(roomId);

            


            //roomManager.EnterRoom()

            //roomManager.



        }

        public static void ExitRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string playerName;
            string playerID;
        }

        public static void RemoveRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string playerName;
            string playerID;
        }

        //#UpdateRoom
        public static void UpdateRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string playerName;
            string playerID;


            ConsoleWrite.WriteMessage("Recv update request");



            //dic.ContainsKey("WaitingRoom");



            var matchRoomManager = MatchRoomManager.GetInstance();

            var rooms = matchRoomManager.AllRooms();

            var result = new JObject();

            result["MessageType"] = "UpdateRoomResult";
            //result["LobbyUserCount"] = "";
            result["RoomCount"] = rooms.Count;



            var roomArray = new JArray();


            foreach (var item in rooms)
            {



                var json = new JObject();

                json["RoomNumber"] = item.RoomNumber;
                json["RoomID"] = item.Id;
                json["GameMode"] = "";
                json["MacCapacity"] = 10;

                roomArray.Add(json);
            }

            result["AllRoom"] = roomArray;



            var str = result.ToString();

            session.SendAsync(result.ToString());
        }


        public static void MatchStart(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string? roomID = null;
            string? playerID = null;

            if (dic.ContainsKey("RoomID"))
            {


            }
            else
            {
                roomID = null;
            }

            if (dic.ContainsKey("PlayerID"))
            {

            }
            else
            {

            }


            if (string.IsNullOrEmpty(playerID))
            {
                var resultJson = new JObject();

            }

            if (string.IsNullOrEmpty(roomID))
            {
                var resultJson = new JObject();

            }

        }


    }
}
