using System;
using System.Collections.Generic;
using System.Threading;
using OpenGSCore;


namespace OpenGSServer
{
   


    public class GameRoomManager
    {
        //private List<MatchRoom> matchRoomList=new List<MatchRoom>();

        private Dictionary<string, MatchRoom> testRooms = new Dictionary<string, MatchRoom>();

        private Dictionary<string,MatchRoom> matchRooms=new Dictionary<string,MatchRoom>();

        private int roomNumberCount = 0;

        
        private static GameRoomManager _singleInstance = new GameRoomManager();

        public static GameRoomManager GetInstance()
        {
            return _singleInstance;
        }

        private void IncreaseRoomCounter()
        {
            Interlocked.Increment(ref roomNumberCount);
        }

        public List<MatchRoom> TestRoom()
        {

          // var room=  new MatchRoom(0,"aaa",new AbstractMatchSetting(eGameMode.TDM,10,true,false));

           var rooms = new List<MatchRoom>();


            //rooms.Add(room);


            return rooms;
        }

        public List<MatchRoom> AllTestRoom()
        {
            var rooms = new List<MatchRoom>();


            //rooms.Add(room);


            return rooms;

        }
        public GameRoomManager()
        {

        }

        public List<AbstractGameRoom> AllRooms()
        {
            var rooms = new List<AbstractGameRoom>(matchRooms.Values);

            
            

            return rooms;
        }


        /*
        public void CreateNewRoom(string roomName,GameMode mode,int playerCount=8)
        {
            if(!(playerCount%2==0))
            {
                playerCount++;

            }

            if(playerCount<12)
            {
                playerCount = 12;

            }

            if(string.IsNullOrEmpty(roomName))
            {

                roomName = "One Shot,One Kill";

            }


            var id = Guid.NewGuid().ToString("N");

            var setting=new MatchSettings();

            var matchRoom = new MatchRoom(count,roomName,id,setting);

            matchRooms.Add(id.ToString(), matchRoom);

            ConsoleWrite.WriteMessage("New match room generated...", ConsoleColor.Blue);


        }
        */
        public CreateNewRoomResult CreateNewDMRoom(in string roomName,in string ownerID,int capacity=10,bool teamBalance=true)
        {
            var matchSetting = new DMMatchSetting(10);

            var matchRule = new DeathMatchRule();

            

            var matchRoom = new MatchRoom(roomNumberCount, roomName, ownerID, matchRule);

            matchRooms.Add(matchRoom.Id, matchRoom);

            IncreaseRoomCounter();


            var createNewRoomResult = new CreateNewRoomResult(eCreateNewRoomResult.Succeessful,eCreateNewRoomReason.NoReason);

            return createNewRoomResult;
        }

        public void CreateNewTDMRoom(in string roomName,in string ownerID,int capacity=10,bool teamBalance=true)
        {
            var tdmMatchSetting = new TDMMatchSetting();

            var matchRule = new TDMMatchRule();
            IncreaseRoomCounter();

            var matchRoom = new MatchRoom(0,roomName,ownerID,matchRule);

            matchRooms.Add(matchRoom.Id, matchRoom);

            //var tdmMatchRule = new TDMMatchRule();

            //var matchSetting = new TDMMatchSetting(capacity,);
        }


        public void CreateNewSuvRoom(in string roomName, in string OwnerID,int capacity = 10)
        {
            //var suvMatchRule = new SuvMatchRule();
            var matchSetting = new SuvMatchSetting(capacity,true);

            //var matchRoom = new MatchRoom(OwnerID, matchSetting);



        }

        public void CreateNewTSuvRoom(in string roomName)
        {

            var matchSetting = new TSuvMatchRule();


            //var matchRoom = new MatchRoom(0, "", "",);


        }

        public void CreateNewCTFMatchRoom(string roomName, int capacity = 10)
        {

        }
        
       
        public void CreateNewMissionRoom(in string roomName,in string ownerID,int capacity=3)
        {
            //var missionRoom = new MissionRoom();



        }

        public MatchEnterResult EnterRoom(in Guid id)
        {
            id.ToString();

            bool succeeded = false;

            String message = "";

            if(matchRooms.ContainsKey(id.ToString()))
            {
                var room=matchRooms[id.ToString()];

                

                succeeded = true;

            }
            else
            {

                message = "No room";
                succeeded = false;
            }

            var result = new MatchEnterResult(succeeded,message);


            return result;
        }

        public void ExitRoom(string userid)
        {


        }

        public MatchRoom? SearchRoomByMemberID(string id)
        {
            if(string.IsNullOrEmpty(id))
            {
                return null;
            }


            foreach (var item in matchRooms)
            {
                

            }



            return null;
        }

        public bool MatchStart(in string id)
        {
            string message = "";

            if(matchRooms.ContainsKey(id))
            {
                lock(matchRooms)
                {
                    var room=matchRooms[id];

                    room.Start();

                }


                return true;
            }
            else
            {

            }



            return false;
        }

        public void UpdateAllRoom()
        {
            

        }

        public int RoomCount()
        {

            return matchRooms.Count;
        }

        public void RemoveRoom(in string roomId, bool forceShutdownNowPlayingRooms = false)
        {


        }

        public void RemoveAllRooms(bool forceShutdownNowPlayingRooms=false)
        {
            if(forceShutdownNowPlayingRooms)
            {

            }
            else
            {


            }


        }

        

    }
}
