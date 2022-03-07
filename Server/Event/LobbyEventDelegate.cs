using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;




namespace OpenGSServer
{
    public static class LobbyEventDelegate
    {
        public static void CreateNewRoom(in ClientSession session,in IDictionary<string, JToken> dic)
        {
            string? playerName;
            string? playerID;
            string? gameMode;

            int maxCapacity = 0;

            var result = eCreateNewRoomResult.Fail;
            var reason = eCreateNewRoomReason.NoReason;


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

            if (dic.ContainsKey("PlayerName"))
            {
                playerName = dic["PlayerName"].ToString();

                if (string.IsNullOrEmpty(playerName))
                {

                    return;
                }
            }
            else
            {

            }

            

            if(dic.ContainsKey("MaxCapacity"))
            {
                //maxCapacity=dic["MaxCapacity"].ToString();

                if (Int32.TryParse(dic["MaxCapacity"].ToString(), out maxCapacity))
                {

                }


            }
            else
            {

            }


            if (dic.ContainsKey("GameMode"))
            {

                gameMode=dic["GameMode"].ToString();
            }
            else
            {
                gameMode = null;
            }




            var matchRoomManager = GameRoomManager.GetInstance();



            if (string.IsNullOrEmpty(gameMode))
            {
                var errorMessage = "Invalid game mode";
            }

            if (gameMode == "dm" || gameMode == "deathmatch")
            {
                var  winConditionKill = 10;


                if (dic.ContainsKey("WinConditionKill"))
                {
                    if (Int32.TryParse(dic["WinConditionKill"].ToString(), out var con))
                    {

                    }


                }
                else
                {

                }

                matchRoomManager.CreateNewDMRoom("", "", 10, true);





            }

            if (gameMode == "tdm" || gameMode == "teamdeathMatch")
            {

                var winConditionKill=0;

                if (dic.ContainsKey("WinConditionKill"))
                {
                    if (Int32.TryParse(dic["WinConditionKill"].ToString(), out winConditionKill))
                    {

                    }


                }
                else
                {
                    winConditionKill = 10;

                }

                matchRoomManager.CreateNewTDMRoom("","",10,true);

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


        public static void CreateNewMissionRoom(in ClientSession sesion,in IDictionary<string,JToken> dic)
        {
            string? playerName;
            string? playerID;






        }
        public static void EnterRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string ?playerName;
            string ?playerID;

           if(dic.ContainsKey("PlayerName"))
            {

            }
            else
            {

            }




        }

        public static void ExitRoom(in ClientSession session,in IDictionary<string,JToken> dic)
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



            var matchRoomManager = GameRoomManager.GetInstance();

            var rooms = matchRoomManager.AllRooms();

            var result = new JObject();

            result["MessageType"] = "UpdateRoomResult";
            result["RoomCount"] = rooms.Count;



            var roomArray = new JArray();


            foreach (var item in rooms)
            {
               


                var json = new JObject();

                json["RoomNumber"] = item.RoomNumber;
                json["RoomID"] = item.Id;
                json["GameMode"]="";
                json["MacCapacity"] = 10;

                roomArray.Add(json);
            }

            result["AllRoom"] = roomArray;



            var str = result.ToString();

            session.SendAsync(result.ToString());
        }

        public static void MatchStart(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string? roomID=null;
            string? playerID=null;

            if(dic.ContainsKey("RoomID"))
            {


            }
            else
            {
                roomID = null;
            }

            if(dic.ContainsKey("PlayerID"))
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
