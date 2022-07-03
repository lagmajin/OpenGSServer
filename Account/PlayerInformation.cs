using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenGSServer
{
    public enum ePlayerPlayingStatus
    {
        Unknown,
        Waiting,
        Mission,
        MATCH,


    }


    public enum ePlayerLocation
    {
        Unknown,



    }

    //public interface IPlayerInformation { }

    public class PlayerInformation
    {

        public string? RoomId { get; private set; } = null;
        private ePlayerPlayingStatus _status = ePlayerPlayingStatus.Unknown;
        public ePlayerLocation PlayerLocation { get; set; } = ePlayerLocation.Unknown;

        public PlayerInformation(ePlayerPlayingStatus status)
        {

        }

        public void SetRoomId(in string? id)
        {
            RoomId = id;
        }


        public bool InLobby()
        {
            return _status == ePlayerPlayingStatus.Unknown;
        }

        public bool InTheRoom()
        {
            return _status == ePlayerPlayingStatus.MATCH;
        }


    }
}
