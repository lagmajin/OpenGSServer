using System;
using OpenGSCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;



namespace OpenGSServer
{
    public enum EMatchRoomEventType2
    {
        Unknown,
        MatchStarted,
        MathEnded,

    }


    public interface IMatchRoom
    {

        string RoomName { get; set; }
        //IObservable<int >

    }


    public partial class MatchRoom : AbstractGameRoom, IMatchRoom,IObservable<MatchResult>, IDisposable
    {

        class Unsubscriber : IDisposable
        {
            private List<IObserver<MatchResult>> m_observers;
            
            private IObserver<MatchResult> m_observer;

            public Unsubscriber(List<IObserver<MatchResult>> observers, IObserver<MatchResult> observer)
            {
                m_observers = observers;
                m_observer = observer;
            }
            public void Dispose()
            {
                m_observers.Remove(m_observer);
            }
        }


        public List<IObserver<MatchResult>> matchSubscriber = new();


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


        private Stopwatch sw = new();
        public MatchRoom(int roomNumber, in string roomName, in string roomOwnerId, in AbstractMatchRule rule) : base(roomNumber, roomOwnerId)
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
                if(true)
                {

                    return true;
                }

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

        public void AddNewPlayers()
        {

        }

        public override void GameUpdate()
        {

            if (!Finished)
            {
                GameScene.UpdateFrame();
            }

            






        }

        public void GameStart()
        {
            sw.Start();

            
            if (Setting.TimeLimit)
            {
                //setting.MatchTime;

            }
            
            

            Playing = true;

            foreach (var sub in matchSubscriber)
            {
                MatchResult result = new();
                result.type = EMatchRoomEventType.Started;


                sub.OnNext(result);

            }

            OnMatchStarted();

        }

        public void Finish()
        {



            Playing = false;

            foreach (var s in matchSubscriber)
            {
                MatchResult result = new();

                result.type = EMatchRoomEventType.Ended;
                result.room = this;

                s.OnNext(result);



            }

            OnMatchFinished();

        }

         void OnMatchStarted()
        {



        }

         void OnMatchFinished()
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

        public IDisposable Subscribe(IObserver<MatchResult> observer)
        {
   
            matchSubscriber.Add(observer);

            return new Unsubscriber(matchSubscriber, observer);
        }
    }
}
