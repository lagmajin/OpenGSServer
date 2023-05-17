



using System;
using System.Collections.Generic;

namespace OpenGSServer
{
    public class WaitRoom
    {
        public MatchRoom? MatchRoomLink { get; set; } = null;

        public bool NowPlaying { get; private set; } = false;
        public bool CanEnter { get; set; } = true;

        public bool CanEnterWhenPlaying { get; set; } = false;

        public int PlayerCount { get; private set; } = 0;
        public int MaxCapacity { get; } = 8;



        public string RoomId { get; set; } = CreateRoomId();

        public Dictionary<string, PlayerAccount> players = new();

        public MatchSettings Settings { get; } = new();

        private static string CreateRoomId()
        {
            var result = Guid.NewGuid().ToString("N");

            return result;
        }

        public WaitRoom(in string roomName)
        {



        }

        public WaitRoom(in string roomName, in int maxCapacity)
        {

        }

        public WaitRoom(in string roomName, in int maxCapacity, in MatchSettings settings)
        {
            //RoomName = roomName;
            MaxCapacity = maxCapacity;

            Settings= settings;


        }


        public void AddPlayer(PlayerAccount account)
        {

        }

        public void RemovePlayer()
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



        public void ChangeMatchSetting(MatchSettings settings)
        {

        }


        public string DebugString()
        {
            var result = "";


            return result;
        }


    }
}
