using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using System.Diagnostics;
using OpenGSCore;


namespace OpenGSServer
{
    public static class LobbyEventHandler
    {
        public static void CreateNewWaitRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string? playerName = null;
            string? playerID = null;
            string? gameMode = null;
            bool teamBalance = true; // デフォルト値は true
            int maxCapacity = 0;     // デフォルト値

            //var result = eCreateNewRoomResult.Fail;
            // var reason = ECreateNewRoomReason.NoReason;
            ECreateNewRoomReason reason = ECreateNewRoomReason.NoReason;


            var gameRoomManager = MatchRoomManager.Instance;

            var waitRoomManager = WaitRoomManager.Instance();



            if (dic.TryGetValue("PlayerID", out JToken? playerIDToken) && !string.IsNullOrEmpty(playerID = playerIDToken?.ToString()))
            {
                // PlayerID は有効
                Console.WriteLine($"[CreateWaitRoom] PlayerID: {playerID}");
            }
            else
            {
                // PlayerID が dic にない、または空文字列の場合
                //reason = ECreateNewRoomReason.InvalidPlayerID;
                //session.SendErrorMessage($"待機部屋作成に失敗しました: {reason} - PlayerIDが不正です。");
                //Console.WriteLine($"[WARN] Client {session.Peer.EndPoint} からの待機部屋作成リクエスト: PlayerIDが不正です。");
                return; // 必須データが不足しているため、ここで処理を終了
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
                if (roomCapacityToken.Type == JTokenType.Integer)
                {
                    
                    maxCapacity = roomCapacityToken.Value<int>();
                }
                // または、ToString() を経由してTryParseを使う場合
                else if (roomCapacityToken.Type == JTokenType.String) // 文字列形式の数値も許容する場合
                {
                    if (!Int32.TryParse(roomCapacityToken.ToString(), out maxCapacity))
                    {
                      
                    }
                }
                else 
                {
                    
                    Console.WriteLine($"Warning: RoomCapacity has unexpected type: {roomCapacityToken.Type}. Using default value 10.");
                    
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



            var boosterPower = dic.GetValueDefaultFloat("BoosterPower", 1.0f);

            //var matchRoomManager = GameRoomManager.GetInstance();





            if (string.IsNullOrEmpty(gameMode))
            {
                var errorMessage = "Invalid game mode";


                goto ErrorResult;
            }

            if (gameMode == "dm" || gameMode == "deathmatch")
            {
                var result=waitRoomManager.CreateNewWaitRoom("");

                var room=result.Room;

                var setting = new DeathMatchSetting();

                //room.ChangeMatchSetting(setting);

               

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

                var winConditionKill = 0;

                if (dic.ContainsKey("WinConditionKill"))
                {
                    if (Int32.TryParse(dic["WinConditionKill"].ToString(), out winConditionKill))
                    {

                    }
                    else
                    {
                        winConditionKill = 10;

                        var setting = new TDMMatchSetting();

                    }
                }
                else
                {
                    winConditionKill = 10;

                }





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
                //var winConditionFlag = json["WinConditionKill"].ToString();






            }



            return;

            ErrorResult:
            {


                //var createResult = new CreateNewRoomResult(result,reason);


                //session.SendAsync(createResult.ToJson().ToString());



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

        public static void UpdateRoom(in ClientSession session, in IDictionary<string, JToken> dic)
        {
            string playerName;
            string playerID;


            ConsoleWrite.WriteMessage("Recv update request");



            //dic.ContainsKey("WaitingRoom");



            var matchRoomManager = MatchRoomManager.Instance;

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
