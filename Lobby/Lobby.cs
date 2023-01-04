




using System.Collections.Generic;

namespace OpenGSServer
{


    public interface ILobby
    {
        void AddChat();

    }
    public class Lobby
    {

        private readonly ChatManager chatManager = new();

        internal ChatManager ChatManager => chatManager;

        private List<PlayerAccount> accounts;

        public int UserCount()
        {
            return 0;
        }

        public void AddPlayer()
        {

        }

        public void AddChat(in string playerName,in string chat)
        {

        }

    }


}

