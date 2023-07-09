using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{


    public class WaitRoomManager
    {
        private static WaitRoomManager _singleInstance = new WaitRoomManager();

        private SortedDictionary<string, WaitRoom> _rooms = new();

        private WaitRoomDatabase _allRoom=new ();

        private int RoomLimit { get; set; } = 20;

        private List<string> DefaultRoomNames { get; set; } = new List<string>() { "One Shot One Kill", "" };

        private readonly object _lockObj = new object();

        private JObject RoomInfoCache { get; set; } = new();

        //public readonly List<string> defaultRoomName = new();

        private List<WaitRoom> waitRooms = new();

        private string CreateRoomId()
        {
            var id = Guid.NewGuid().ToString("N");

           
            return id;
        }
        public static WaitRoomManager Instance()
        {
            return _singleInstance;
        }

        public CreateNewWaitRoomResult CreateWaitRoom(string roomName,int capacity=8)
        {
            CreateNewWaitRoomResult result;

            //roomNameが空ならRandomRoomNameでうめる
            if (roomName == "")
            {
                roomName = Template.RandomRoomName();
            }

            if (RoomLimit > _rooms.Count)
            {
                var id = CreateRoomId();
                var room = new WaitRoom(roomName);
                lock (_lockObj)
                {
                    _rooms.Add(room.RoomId, room);
                }
                result = new CreateNewWaitRoomResult("Successful", room);
            }
            else
            {
                result = new CreateNewWaitRoomResult("Fail", null);
            }



            
            return result;
        }

        public CreateNewWaitRoomResult CreateNewWaitRoom(in string roomName)
        {
            CreateNewWaitRoomResult result;

            if (RoomLimit > _rooms.Count)
            {
                var id = CreateRoomId();
                var room = new WaitRoom(roomName);
                lock (_lockObj)
                {
                    _rooms.Add(room.RoomId, room);
                }
                result = new CreateNewWaitRoomResult("Successful", room);
            }
            else
            {
                result = new CreateNewWaitRoomResult("Server RoomLimit Over", null);
                
                
            }



            return result;
        }

        public WaitRoom? CreateNewWaitRoom(string roomName, int capacity = 8)
        {
            if (roomName=="")
            {
                roomName = Template.RandomRoomName();
            }


            if (RoomLimit > _rooms.Count)
            {


            }


            var id = CreateRoomId();


            var room = new WaitRoom(roomName);

            lock (_lockObj)
            {

                _rooms.Add(room.RoomId, room);
            }

            return room;

        }

        



        public WaitRoom? FindWaitRoom(in string roomId)
        {
            foreach(var room in waitRooms)
            {
                if(room.RoomId == roomId)
                {
                    return room;
                }

            }


            return null;
        }

        public void FindWaitRoomsByGameMode()
        {

        }

        public void CreateRoomCache()
        {

        }

        public string Info()
        {
            var utfNow = DateTime.UtcNow;




            var count = _rooms.Count;

            string result = $"WaitRoomCount:{count}/{RoomLimit}";


            return result;
        }

        public JObject RoomManagerInfo()
        {
            var result = new JObject();
            result["WaitRoomCount"] = _rooms.Count;
            result["RoomCapacity"] = _rooms.Count.ToString() + "/" + RoomLimit.ToString();

            var array = new JArray();

            foreach (var room in _rooms)
            {
                var roomJson = new JObject();

                var value = room.Value;

                roomJson["RoomNumber"] = 0;
                roomJson["RoomID"] = value.RoomId.ToString();
                roomJson["RoomName"] = "";
                roomJson["Capacity"] = "";
                roomJson["NowPlaying"] = value.NowPlaying.ToString();
                roomJson["CanEnter"] = value.CanEnter.ToString();

                var gameModeJson = new JObject();

                gameModeJson["GameMode"] = "";
                gameModeJson["Rule"] = "";
                gameModeJson["aaa"] = "";


                roomJson["GameMode"] = gameModeJson;


                array.Add(roomJson);

            }

            result["WaitRooms"] = array;

            RoomInfoCache = result;


            return result;
        }

    }
}
