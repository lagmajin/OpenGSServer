using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime.Tree.Pattern;
using OpenGSCore;
using Newtonsoft.Json.Linq;

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

    public class MatchRoomEventBus : OpenGSCore.MatchRoomEventBus
    {
        public void PublishLoadingStart()
        {
            Console.WriteLine("LoadingStart");
        }

        public void PublishGameStart()
        {
            Console.WriteLine("GameStart");
        }

        public void PublishGameEnd()
        {
            base.PublishGameEnd();
        }

        public void PublishGameEndWithResult(JObject result)
        {
            base.PublishGameEndWithResult(result);
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

        public void PublishItemSpawn(EFieldItemType type, int spawnPointId)
        {
            base.PublishItemSpawn(type, spawnPointId);
        }

        public void PublishItemDespawn()
        {
            base.PublishItemDespawn();
        }
    }
}
