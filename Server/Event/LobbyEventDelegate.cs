using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenGSCore;


namespace OpenGSServer
{
    public static class LobbyEventDelegate
    {
        public static void CreateNewWaitRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string? playerName;
            string? playerID;
            string? gameMode;

            var teamBalance = true;

            int maxCapacity = 0;

            var result = eCreateNewRoomResult.Fail;
            var reason = eCreateNewRoomReason.NoReason;

            var gameRoomManager = MatchRoomManager.Instance;


            //var createNewRoomResult = new CreateNewRoomResult();

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






            if (dic.TryGetValue("TeamBalance", out var playerNameToken))
            {


            }
            else
            {
                

                return;
            }



            if (dic.TryGetValue("TeamBalance", out var teamBalanceToken))
            {
                if (bool.TryParse(teamBalanceToken.ToString(), out teamBalance))
                {

                }
                else
                {

                }

            }
            else
            {
                ConsoleWrite.WriteMessage("Test");

                return;
            }



            if (dic.TryGetValue("RoomCapacity", out var roomCapacityToken))
            {


                if (Int32.TryParse(roomCapacityToken.ToString(), out maxCapacity))
                {


                }
                else
                {
                    maxCapacity = 10;
                }

            }
            else
            {
                return;
            }


            if (dic.TryGetValue("GameMode", out var gamemodeToken))
            {
                gameMode = gamemodeToken.ToString();


            }
            else
            {
                gameMode = null;

                return;
            }





            //var matchRoomManager = GameRoomManager.GetInstance();





            if (string.IsNullOrEmpty(gameMode))
            {
                var errorMessage = "Invalid game mode";


                goto ErrorResult;
            }

            if (gameMode == "dm" || gameMode == "deathmatch")
            {
                var winConditionKill = 10;



                if (dic.TryGetValue("WinConditionKill", out var conditionKillToken))
                {

                    winConditionKill = 10;


                }
                else
                {
                    winConditionKill = 10;
                }

                var deathMatchSetting = new DeathMatchSetting(winConditionKill, true);



                gameRoomManager.CreateNewDeathMatchRoom("", "", 10, true);





            }

            if (gameMode == "tdm" || gameMode == "teamdeathmatch")
            {

                var winConditionKill = 0;

                if (dic.ContainsKey("WinConditionKill"))
                {
                    if (Int32.TryParse(dic["WinConditionKill"].ToString(), out winConditionKill))
                    {

                    }
                    else
                    {
                        winConditionKill = 10;

                    }


                }
                else
                {
                    winConditionKill = 10;

                }





                //gameRoomManager.CreateNewTDMRoom("", "", 10, true);

            }

            if (gameMode == "suv" || gameMode == "survival")
            {








            }

            if (gameMode == "tsuv")
            {


            }

            if (gameMode == "ctf" || gameMode == "capturetheflag")
            {
                //var winConditionFlag = json["WinConditionKill"].ToString();






            }


            ErrorResult:
            {


                //var createResult = new CreateNewRoomResult(result,reason);


                //session.SendAsync(createResult.ToJson().ToString());




            }



        }


        public static void CreateNewMissionRoom(in ClientSession sesion, in IDictionary<string, JToken> dic)
        {
            string? playerName;
            string? playerID;






        }

        public static void QuickStartRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            var matchRoomManager = MatchRoomManager.GetInstance();





        }
        public static void EnterRoomRequest(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string? playerName;
            //string? playerID;

            string playerAccountID = "";



            var roomId = dic["RoomID"];


            var roomManager = MatchRoomManager.Instance;

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
