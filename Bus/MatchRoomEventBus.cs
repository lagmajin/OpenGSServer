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
        private List<IMatchSubscriber> subscribers = new List<IMatchSubscriber>();

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

        public void AddSubscriber(IMatchSubscriber subscriber)
        {
            if (subscriber != null && !subscribers.Contains(subscriber))
            {
                subscribers.Add(subscriber);
            }
        }

        public void RemoveSubscriber(IMatchSubscriber subscriber)
        {
            if (subscriber != null && subscribers.Contains(subscriber))
            {
                subscribers.Remove(subscriber);
            }
        }

        public void PublishLoadingStart()
        {
            Console.WriteLine("LoadingStart");
        }

        public void PublishGameStart()
        {
            Console.WriteLine("GameStart");
            foreach (var subscriber in subscribers)
            {
                subscriber.OnMatchEnd(); // 必要に応じて適切なイベントメソッドに変更
            }
        }

        public void PublishGameEnd()
        {
            Console.WriteLine("GameEnd");
            foreach (var subscriber in subscribers)
            {
                subscriber.OnMatchEnd();
            }
        }

        public void PublishMatchResult(MatchResult result)
        {
            foreach (var subscriber in subscribers)
            {
                subscriber.OnReceiveMatchResult(result);
            }
        }

        public void PublishGameUpdateFromClient()
        {
        }

        public void PublishMatchStarted(OpenGSCore.MatchRoom room)
        {
            Console.WriteLine($"Match started: {room.RoomName}");
        }

        public void PublishMatchEnded(OpenGSCore.MatchRoom room)
        {
            Console.WriteLine($"Match ended: {room.RoomName}");
        }

        public void PublishPlayerJoined(OpenGSCore.MatchRoom room, PlayerAccount player)
        {
            Console.WriteLine($"Player joined: {player.Name} in {room.RoomName}");
        }

        public void PublishPlayerLeft(OpenGSCore.MatchRoom room, PlayerAccount player)
        {
            Console.WriteLine($"Player left: {player.Name} from {room.RoomName}");
        }
    }
}