



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

        public MatchSettings settings = new();

        private static string CreateRoomId()
        {
            var result = Guid.NewGuid().ToString("N");

            return result;
        }

        public WaitRoom(in string roomName)
        {



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


        public string DebugString()
        {
            var result = "";


            return result;
        }


    }
}
