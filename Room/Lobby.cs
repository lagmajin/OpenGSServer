




namespace OpenGSServer
{


    public interface ILobby
    {

    }
    public class Lobby
    {
        
        private readonly ChatManager chatManager=new ChatManager();

        internal ChatManager ChatManager => chatManager;

        public  int userCount()
        {
            return 0;
        }

    }

}

