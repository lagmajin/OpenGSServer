using System;
using OpenGSCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;


namespace OpenGSServer
{
    /*
    public interface IMatchRoom
    {

        string RoomName { get; set; }


    }
    */
    public class OldMatchRoom2 : AbstractGameRoom, IMatchRoom, IDisposable
    {


        AbstractMatchRule? rule;


        private AbstractMatchSetting Setting { get; set; }

        private AbstractMatchSituation situation { get; set; } = null;


        public GameScene GameScene { get; set; } = new();

        //[CanBeNull]
        public WaitRoom? WaitRoomLink { get; private set; } = null;
        public string RoomName { get; set; }



        public bool MatchEnd { get; } = false;

        public int PlayerCount { get; } = 0;

        public int Capacity { get; } = 20;


        public bool Playing { get; private set; } = false;
        public bool Finished { get; } = false;



        public OldMatchRoom2(int roomNumber, in string roomName, in string roomOwnerId, in AbstractMatchRule rule) : base(roomNumber, roomOwnerId)
        {



            //ConsoleWrite.WriteMessage("Match Room Name:" + "" + "Capacity:" + "Room ID:" + id.ToString() + "GameMode:", ConsoleColor.Yellow);
        }


        public bool IsOwner(in string id)
        {
            return false;
        }

        public bool ChangeOwner()
        {
            if (Players.Count <= 1)
            {
                return false;
            }



            return false;
        }

        public bool ChangeOwnerRandom()
        {
            if (Players.Count < 1)
            {
                return false;
            }

            //var currentOwnerID=owner



            return false;
        }

#nullable enable
        public string? RoomOwnerName()
        {
            if (Players.Count == 0)
            {
                return null;
            }
            else
            {

            }





            return "";
        }

        public PlayerInfo? RoomOwnerInfo()
        {

            return null;
        }

        public List<string> RoomMembersNameList()
        {
            var result = new List<string>();

            return result;
        }

        public List<PlayerInfo>? RoomMemberList()
        {



            return Players;
        }

        public bool IsMember(in string id)
        {
            if (Players.Count == 0)
            {
                return false;
            }
            else
            {

            }

            return false;
        }




        public void AddNewPlayer(PlayerInfo info)
        {

            if (Playing)
            {

            }
            else
            {

            }



        }

        public override void GameUpdate()
        {






        }

        public void Start()
        {
            ///sw.Start();

            
            if (Setting.TimeLimit)
            {
                //setting.MatchTime;

            }

            

            Playing = true;

            /*
            foreach (var sub in matchSubscriber)
            {
                MatchResult result = new();
                result.type = EMatchRoomEventType.MatchStarted;


                sub.OnNext(result);

            }
            */
            OnMatchStarted();

        }

        public void Finish()
        {



            Playing = false;

            /*
            foreach (var s in matchSubscriber)
            {
                MatchResult result = new();

                result.type = EMatchRoomEventType.Ended;
                result.room = this;

                s.OnNext(result);



            }

            OnMatchFinished();
            */
        }

         void OnMatchStarted()
        {



        }





        public JObject ToJson()
        {
            var json = new JObject();

            json["RoomName"] = RoomName;
            json["RoomID"] = Id.ToString();
            json["MaxCapacity"] = 8;
            json["PlayerCount"] = PlayerCount;


            return json;

        }

        public void Dispose()
        {



        }
    }
}
