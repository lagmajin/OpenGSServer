using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        // The base class declares these as non-virtual, so they can only be
        // hidden, not overridden. The base implementations raise the events the
        // manager subscribes to, so each method here has to reach them.
        // PublishLoadingStart and PublishGameStart used to stop at a
        // Console.WriteLine, which silently swallowed the event for anything
        // holding a server typed bus reference.
        public new void PublishLoadingStart()
        {
            base.PublishLoadingStart();
        }

        public new void PublishGameStart()
        {
            base.PublishGameStart();
        }

        public new void PublishGameEnd()
        {
            base.PublishGameEnd();
        }

        public new void PublishGameEndWithResult(JObject result)
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

        public new void PublishItemSpawn(EFieldItemType type, int spawnPointId)
        {
            base.PublishItemSpawn(type, spawnPointId);
        }

        public new void PublishItemDespawn()
        {
            base.PublishItemDespawn();
        }
    }
}
