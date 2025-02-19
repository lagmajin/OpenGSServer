using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        // ゲーム開始イベントを発行

        public MatchRoomEventBus()
        {
            Console.WriteLine("Match RoomEventBus");

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

       
    }
}
