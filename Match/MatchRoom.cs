using System;
using OpenGSCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
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

    //#bookmark
    public partial class MatchRoom : AbstractGameRoom, IMatchRoom
    {

        private readonly MatchRoomEventBus eventBus;


        //public List<IObserver<MatchResult>> matchSubscriber = new();


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
        
        PlayerLifeTimeScore LifeTimeScore { get; set; } = new PlayerLifeTimeScore();


        private Stopwatch sw = new();

        private HighPrecisionGameTimer timer;
        
        private IMatchLogic logic { get; set; }
        public MatchRoom(int roomNumber, in string roomName, in string roomOwnerId,AbstractMatchSetting setting, MatchRoomEventBus bus) : base(roomNumber, roomOwnerId)
        {
            
            Setting = setting;  
            
            eventBus = bus;

            switch (setting.Mode)
            {
                case EGameMode.DeathMatch:
                    if (setting is DeathMatchSetting deathMatchSetting)
                    {
                        
                    }

                    break;
                    
            case EGameMode.TeamDeathMatch:

                    break;
                    
         
            }
            
            

            //setting.Mode

        }




        public bool ChangeOwnerRandom()
        {
            if (Players.Count < 1)
            {
                return false;
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

        public void AddNewPlayers(List<PlayerInfo> list)
        {

        }

        public void OnGameUpdateFromClient()
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
            
            logic.StartMatch();

            var timer = new HighPrecisionGameTimer(100);

            timer.OnTick += () => GameUpdate();

            Playing = true;

            eventBus.PublishGameStart();

            

            
        }

        public void Finish()
        {

            timer.Stop();

            logic.EndMatch();
            
            eventBus.PublishGameEnd();
            
            
            
            
            Playing = false;


            

            
            

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
