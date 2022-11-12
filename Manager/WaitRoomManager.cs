using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    public class WaitRoomManager
    {
        private static WaitRoomManager _singleInstance = new WaitRoomManager();

        private SortedDictionary<string, WaitRoom> rooms = new();

        private int RoomLimit { get; set; } = 20;

        private List<string> defaultRoomNames { get; set; } = new List<string>() { "One Shot One Kill", "" };

        private string CreateRoomID()
        {
            var id = Guid.NewGuid().ToString("N");


            return id;
        }
        public static WaitRoomManager GetInstance()
        {
            return _singleInstance;
        }

        public WaitRoom? CreateNewWaitRoom(in string roomName, int capacity = 8)
        {
            if (RoomLimit > rooms.Count)
            {


            }


            var id = CreateRoomID();


            var room = new WaitRoom(roomName);

            rooms.Add(room.RoomId, room);


            return room;

        }



        public void FindWaitRooms()
        {

        }

        public void FindWaitRoomsByGameMode()
        {

        }


        public string Info()
        {
            var utfNow = DateTime.UtcNow;




            var count = rooms.Count;

            string result = $"WaitRoomCount:{count}/{RoomLimit}";


            return result;
        }

        public JObject Info2()
        {
            var result = new JObject();
            result["WaitRoomCount"] = rooms.Count;
            result["RoomCapacity"] = rooms.Count.ToString() + "/" + RoomLimit.ToString();

            foreach (var room in rooms)
            {
                
            }

            return result;
        }

    }
}
