using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Tree.Pattern;
using OpenGSCore;

namespace OpenGSServer
{
    public interface IMatchSubscriber
    {

        void OnMatchEnd()
        {
            Console.WriteLine("[Default] MatchEnd: Game has ended.");
        }



        void OnReceiveMatchResult(MatchResult eventData)
        {
            //Console.WriteLine($"[Default] MatchResult: Winner={eventData.Winner}, Score={eventData.Score}");
        }

    }

    public interface IMatchPublisher
    {

    }
    
    

    public class MatchRoomEventBus : AbstractEventBus
    {
        private OpenGSCore.MatchRoom room;
        private MatchRoomManager roomManager;
        
        public MatchRoomEventBus()
        {
            Console.WriteLine("Match RoomEventBus");

        }

        public void setMatchRoom(OpenGSCore.MatchRoom room)
        {
            this.room = room;
        }

        public void setMatchRoomManager(MatchRoomManager manager)
        {
            this.roomManager = manager;
        }

        public void setNetworkManager()
        {
            
        }
        
        
        
        public void PublishLoadingStart()
        {
            Console.WriteLine("LoadingStart");
        }

        public void PublishGameStart()
        {
            Console.WriteLine("GameStart");


            //Publish("GameStart", null);
        }

        // ゲーム終了イベントを発行
        public void PublishGameEnd()
        {
       
            //Publish("GameEnd", null);
        }

        public void PublishMatchResult(MatchResult result)
        {
            
            
        }

        public void PublishGameUpdateFromClient()
        {
            
        }
        
        
       
    }
}
