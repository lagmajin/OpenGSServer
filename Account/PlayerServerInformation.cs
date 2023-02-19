using OpenGSCore;


namespace OpenGSServer
{


    //public interface IPlayerInformation { }

    public class PlayerServerInformation
    {

        public string? RoomId { get; private set; } = null;
        public EPlayerPlayingStatus PlayingStatus { get; set; } = EPlayerPlayingStatus.Unknown;
        public EPlayerLocation PlayerLocation { get; set; } = EPlayerLocation.Unknown;

        public PlayerServerInformation(EPlayerPlayingStatus status=EPlayerPlayingStatus.Unknown)
        {

        }

        public PlayerServerInformation(EPlayerPlayingStatus status, EPlayerLocation location)
        {
            PlayingStatus = status;


            PlayerLocation = location;
        }

        public void SetLocationLobby()
        {

        }


        public bool InLobby()
        {
            return true;
        }

        public bool InTheRoom()
        {
            return true;
        }


    }
}
