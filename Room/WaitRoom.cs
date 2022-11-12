



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

        public void GameStart()
        {

        }

        public void GameIsOver()
        {
            MatchRoomLink = null;


        }


        public string DebugString()
        {
            var result = "";


            return result;
        }


    }
}
