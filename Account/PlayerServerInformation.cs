using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    //public interface IPlayerInformation { }

    public class PlayerServerInformation
    {
        public string? RoomId { get; private set; } = null;
        public EPlayerPlayingStatus PlayingStatus { get; set; } = EPlayerPlayingStatus.Unknown;
        public EPlayerLocation PlayerLocation { get; set; } = EPlayerLocation.Unknown;

        public PlayerServerInformation(EPlayerPlayingStatus status = EPlayerPlayingStatus.Unknown)
        {
            PlayingStatus = status;
            PlayerLocation = EPlayerLocation.Unknown;
        }

        public PlayerServerInformation(EPlayerPlayingStatus status, EPlayerLocation location)
        {
            PlayingStatus = status;
            PlayerLocation = location;
        }

        public void SetLocationLobby()
        {
            RoomId = null;
            PlayingStatus = EPlayerPlayingStatus.Unknown;
            PlayerLocation = EPlayerLocation.Lobby;
        }

        public void SetLocation(EPlayerLocation location)
        {
            PlayerLocation = location;
        }

        public void SetPlayingStatus(EPlayerPlayingStatus status)
        {
            PlayingStatus = status;
        }

        public void SetRoomId(string? roomId)
        {
            RoomId = string.IsNullOrWhiteSpace(roomId) ? null : roomId;
            if (!string.IsNullOrWhiteSpace(RoomId))
            {
                PlayerLocation = EPlayerLocation.WaitRoom;
            }
        }

        public bool InLobby()
        {
            return PlayerLocation == EPlayerLocation.Lobby;
        }

        public bool InTheRoom()
        {
            return PlayerLocation == EPlayerLocation.WaitRoom || PlayerLocation == EPlayerLocation.MissionRoom;
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["RoomId"] = RoomId ?? string.Empty,
                ["PlayingStatus"] = PlayingStatus.ToString(),
                ["PlayerLocation"] = PlayerLocation.ToString()
            };
        }
    }
}
