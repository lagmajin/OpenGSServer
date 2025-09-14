



using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OpenGSCore;
namespace OpenGSServer
{

    
    public partial class WaitRoom
    {
        public MatchRoom? MatchRoomLink { get; set; } = null;

        public string RoomName { get; set; } = "";
        public bool NowPlaying { get; private set; } = false;
        public bool CanEnter { get; set; } = true;

        public bool AllowEnterWhenPlaying { get; set; } = false;

        public int PlayerCount { get; private set; } = 0;
        public int Capacity { get; } = 8;



        public string RoomId { get; set; } = CreateRoomId();

        public List<PlayerAccount> players = new();

        public AbstractMatchSetting Setting { get; }

        MatchLoadingTracker _matchLoadingTracker=new();

        private object _lock = new object();

        private static string CreateRoomId()
        {
            var result = Guid.NewGuid().ToString("N");

            return result;
        }

        public WaitRoom(in string roomName,int capacity=8)
        {
            RoomName = roomName;
            Capacity = capacity;

        }

        public WaitRoom(in string roomName, in int maxCapacity, in AbstractMatchSetting settings)
        {
            //RoomName = roomName;
            Capacity = maxCapacity;

           // Settings= settings;


        }


        public void AddPlayer(PlayerAccount account)
        {
            PlayerCount++;

           

        }

        public void RemovePlayer()
        {

        }

        public void LoadingStart()
        {

        }



        public void GameStart()
        {
            NowPlaying = true;

        }

        public void GameIsOver()
        {
            NowPlaying = false;
            MatchRoomLink = null;


        }

        

        public void ChangeMatchSetting(AbstractMatchSetting settings)
        {
            lock (_lock)
            {

            }
        }


        public string DebugString()
        {
            var result = "";


            return result;
        }

        public JObject ToJson()
        {
            var array = new JArray();
            foreach (var player in players)
            {
                var temp = new JObject();
                temp["PlayerName"] = "";
                temp["PlayerId"] = "";
                temp["PlayerCharacter"] = "";

                array.Add(temp);
            }

            var result = new JObject();
            result["RoomName"] = RoomName;

            result["RoomId"] = RoomId;
            result["RoomNumber"] = "";
            result["WaitingPlayerCount"] = PlayerCount;
            result["Capacity"] = Capacity;
            result["NowPlaying"] = NowPlaying;



            return result;
        }

        

    }
}
